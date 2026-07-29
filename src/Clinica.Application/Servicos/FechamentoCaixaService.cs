using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A proposta de fechamento de um dia: o que o sistema apurou, antes de alguém contar.
///
/// Não grava nada. É o mesmo desenho do fechamento da sessão: o sistema faz o trabalho
/// (soma o que entrou e o que saiu em espécie), e o número que vale continua sendo o que
/// a pessoa contou.
/// </summary>
public sealed record PropostaFechamentoCaixa(
    DateOnly Data,
    decimal EntradasEspecie,
    decimal SaidasEspecie,
    int QuantidadeLancamentos,
    FechamentoCaixa? JaConferido)
{
    /// <summary>O que a gaveta deveria ter.</summary>
    public decimal Esperado => EntradasEspecie - SaidasEspecie;

    /// <summary>
    /// O dia não teve movimento em espécie. Dia sem dinheiro na gaveta não precisa de
    /// conferência, e cobrá-la treinaria a clínica a fechar no automático — o que é
    /// exatamente o hábito que a conferência existe para evitar.
    /// </summary>
    public bool SemMovimento => QuantidadeLancamentos == 0;
}

/// <summary>
/// Um dia com dinheiro na gaveta que ninguém conferiu.
/// </summary>
public sealed record DiaNaoConferido(DateOnly Data, decimal Esperado, int QuantidadeLancamentos);

/// <summary>
/// Fechamento de caixa do dia (parcela 14): a conferência da gaveta.
///
/// O módulo sabia tudo sobre o dinheiro registrado e nada sobre o dinheiro FÍSICO. A
/// diferença entre os dois é onde some dinheiro numa clínica — a venda que ninguém
/// lançou, o troco que saiu sem registro, a nota que ficou no bolso —, e ela só aparece
/// quando alguém conta a gaveta e o sistema cobra a explicação.
///
/// Três regras sustentam isso:
///
/// 1. **Só espécie.** Cartão e PIX não passam pela gaveta. Incluí-los faria a conferência
///    nunca bater, e uma conferência que nunca fecha não é controle, é ruído — a clínica
///    aprenderia a clicar "OK" sem olhar.
/// 2. **O valor do sistema é copiado no fechamento.** Um lançamento digitado amanhã com a
///    data de ontem não pode reescrever a conferência de ontem, levando junto a
///    justificativa que alguém escreveu.
/// 3. **Divergência exige justificativa.** Aceitar em silêncio é o mesmo que não conferir.
/// </summary>
public sealed class FechamentoCaixaService
{
    private readonly IClinicaRepositorio _repo;

    public FechamentoCaixaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// O que o sistema apurou no dia, sem gravar nada. Traz junto o fechamento que já
    /// existe, quando existe — a tela precisa distinguir "ainda não conferido" de
    /// "conferido e bateu", que são estados diferentes com a mesma aparência de calmaria.
    /// </summary>
    public async Task<PropostaFechamentoCaixa> PrepararAsync(
        DateOnly dia, CancellationToken ct = default)
    {
        var lancamentos = await _repo.LancamentosEmEspecieDoDiaAsync(dia, ct);

        decimal Somar(TipoLancamento tipo) =>
            lancamentos.Where(l => l.Tipo == tipo).Sum(l => l.Valor);

        var jaConferido = await _repo.FechamentoCaixaDoDiaAsync(dia, ct);

        return new PropostaFechamentoCaixa(
            dia,
            Somar(TipoLancamento.Entrada),
            Somar(TipoLancamento.Saida),
            lancamentos.Count,
            jaConferido is { Reaberto: false } ? jaConferido : null);
    }

    /// <summary>
    /// Grava a conferência do dia.
    ///
    /// A justificativa é obrigatória quando o contado difere do esperado — e essa é a
    /// única regra deste serviço que IMPEDE em vez de avisar. O valor de uma conferência
    /// está inteiro na obrigação de explicar o que não bateu; sem ela, o registro vira um
    /// carimbo que diz apenas que alguém clicou.
    /// </summary>
    public async Task<FechamentoCaixa> ConferirAsync(
        DateOnly dia,
        decimal valorContado,
        string? justificativa = null,
        string? observacoes = null,
        string? operador = null,
        CancellationToken ct = default)
    {
        if (valorContado < 0m)
            throw new ArgumentException(
                "O valor contado não pode ser negativo — gaveta não deve dinheiro.",
                nameof(valorContado));

        var existente = await _repo.FechamentoCaixaDoDiaAsync(dia, ct);
        if (existente is { Reaberto: false })
            throw new InvalidOperationException(
                $"O caixa de {dia:dd/MM/yyyy} já foi conferido. Reabra antes de conferir de novo.");

        var proposta = await PrepararAsync(dia, ct);
        var diferenca = valorContado - proposta.Esperado;

        if (diferenca != 0m && string.IsNullOrWhiteSpace(justificativa))
            throw new InvalidOperationException(
                $"O contado difere do esperado em {diferenca:C}. Escreva o que aconteceu — "
                + "diferença aceita em silêncio é o mesmo que caixa não conferido.");

        // Reabertura: o registro anterior fica no histórico, marcado, e este nasce por
        // cima. Sobrescrever aquele apagaria a contagem que de fato foi feita antes.
        var fechamento = new FechamentoCaixa
        {
            Data = dia,
            ValorSistema = proposta.EntradasEspecie,
            SaidasEspecie = proposta.SaidasEspecie,
            ValorContado = valorContado,
            QuantidadeLancamentos = proposta.QuantidadeLancamentos,
            Justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim(),
            Observacoes = observacoes,
            ConferidoPor = operador
        };

        await _repo.AdicionarFechamentoCaixaAsync(fechamento, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = "CaixaConferido",
            Detalhe = $"{dia:dd/MM/yyyy}: esperado {proposta.Esperado:C}, contado {valorContado:C}"
                      + (diferenca == 0m ? " — bateu" : $" — diferença de {diferenca:C}"),
            Operador = operador ?? Environment.UserName
        }, ct);
        await _repo.SalvarAsync(ct);

        return fechamento;
    }

    /// <summary>
    /// Reabre o dia para recontagem. O fechamento anterior NÃO é apagado: fica marcado
    /// com o motivo, e o dia volta a aparecer como não conferido. Apagá-lo deixaria o dia
    /// parecendo nunca ter sido contado, que é uma história diferente da que aconteceu —
    /// e é justamente a história que alguém poderia querer contar.
    /// </summary>
    public async Task ReabrirAsync(
        DateOnly dia, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Informe o motivo da reabertura.", nameof(motivo));

        var fechamento = await _repo.FechamentoCaixaDoDiaAsync(dia, ct)
            ?? throw new InvalidOperationException($"O caixa de {dia:dd/MM/yyyy} não foi conferido ainda.");

        if (fechamento.Reaberto)
            throw new InvalidOperationException("Este fechamento já está reaberto.");

        fechamento.Reaberto = true;
        fechamento.MotivoReabertura = motivo.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = "CaixaReaberto",
            Detalhe = $"{dia:dd/MM/yyyy}: {motivo.Trim()}",
            Operador = operador ?? Environment.UserName
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Fechamentos do período, do mais recente para o mais antigo.</summary>
    public Task<IReadOnlyList<FechamentoCaixa>> HistoricoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.FechamentosCaixaNoPeriodoAsync(inicio, fim, ct);

    /// <summary>
    /// Dias com dinheiro na gaveta que ninguém conferiu — o que esta tela existe para
    /// mostrar.
    ///
    /// Dia SEM movimento em espécie não entra: cobrar conferência de um dia em que
    /// ninguém pagou em dinheiro treinaria a clínica a fechar no automático, que é
    /// exatamente o hábito que a conferência existe para evitar.
    /// </summary>
    public async Task<IReadOnlyList<DiaNaoConferido>> NaoConferidosAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var lancamentos = await _repo.LancamentosEmEspecieNoPeriodoAsync(inicio, fim, ct);
        if (lancamentos.Count == 0) return [];

        var conferidos = (await _repo.FechamentosCaixaNoPeriodoAsync(inicio, fim, ct))
            .Where(f => !f.Reaberto)
            .Select(f => f.Data)
            .ToHashSet();

        return lancamentos
            .GroupBy(l => l.Dia)
            .Where(g => !conferidos.Contains(g.Key))
            .Select(g => new DiaNaoConferido(
                g.Key,
                g.Where(l => l.Tipo == TipoLancamento.Entrada).Sum(l => l.Valor)
                    - g.Where(l => l.Tipo == TipoLancamento.Saida).Sum(l => l.Valor),
                g.Count()))
            .OrderByDescending(d => d.Data)
            .ToList();
    }
}
