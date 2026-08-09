using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Uma guia glosada que tem dinheiro contado no caixa. É a linha da tela: de um lado o
/// fato do faturamento (o convênio recusou), do outro o fato do financeiro (a receita
/// está lançada).
/// </summary>
public sealed record ReceitaGlosada(
    int CodigoId,
    int PacienteId,
    string Paciente,
    Convenio Convenio,
    string? NumeroGuiaReal,
    DateOnly DataGlosa,
    string? MotivoGlosa,
    string? MotivoGlosaCodigo,
    StatusGlosa Glosa,
    DateOnly? DataLimiteRecurso,
    int LancamentoId,
    decimal Valor,
    StatusLancamento StatusLancamento,
    DateOnly DataLancamento)
{
    /// <summary>
    /// Código do convênio no catálogo. <see cref="Convenio"/> é a FAMÍLIA de regra, e é
    /// só por ele que a tela chegaria ao nome — o que faria toda operadora cadastrada
    /// pela clínica aparecer como "Personalizado".
    /// </summary>
    public string? ConvenioCodigo { get; init; }

    /// <summary>
    /// A receita ainda é só promessa — é este caso que infla o previsto e o que a tela
    /// resolve com um clique.
    /// </summary>
    public bool AindaPrevisto => StatusLancamento == StatusLancamento.Previsto;

    /// <summary>
    /// O dinheiro ENTROU e depois veio a glosa. Não se mexe: ver
    /// <see cref="ReceitaGlosadaService"/>.
    /// </summary>
    public bool JaRealizado => StatusLancamento == StatusLancamento.Realizado;

    /// <summary>Ainda dá para recorrer — enquanto der, a receita pode voltar.</summary>
    public bool RecursoEmAberto => Glosa is StatusGlosa.Glosada or StatusGlosa.Reapresentada;

    /// <summary>Dias restantes do prazo de recurso; negativo = estourado.</summary>
    public int? DiasParaFimRecurso(DateOnly referencia)
        => RecursoEmAberto && DataLimiteRecurso is { } limite
            ? limite.DayNumber - referencia.DayNumber
            : null;
}

/// <summary>
/// A ponte que faltava entre o FATURAMENTO e o FINANCEIRO: a glosa que derruba receita
/// já contada.
///
/// O buraco que este serviço fecha é dos que não aparecem em tela nenhuma até o mês
/// fechar errado. O <c>GlosaService</c> existe desde cedo e mexe só no
/// <see cref="CodigoFaturamento"/>; o <c>FinanceiroService</c> grava a receita da guia e
/// guarda o vínculo em <c>LancamentoFinanceiro.CodigoFaturamentoId</c>. As duas metades
/// nunca se falaram: o convênio recusava a guia e o dinheiro continuava no fluxo de
/// caixa, no previsto e na rentabilidade por convênio, como se fosse entrar. **Receita
/// fantasma é a pior espécie de número errado, porque tem cara de número exato** — nada
/// na tela avisa que ela não vale.
///
/// Três regras mandam aqui:
///
/// 1. **A glosa não apaga o lançamento.** Lançamento é fato datado, como o consumo de
///    pacote e o fechamento de caixa: some do total, não do histórico. Cancelar com
///    motivo deixa a prova de por que a receita caiu; apagar reescreveria um mês que
///    talvez já esteja conferido.
/// 2. **Só o PREVISTO se cancela.** Receita prevista é promessa, e promessa que o
///    convênio recusou não é promessa. Já o lançamento REALIZADO é dinheiro que entrou
///    na conta: cancelá-lo faria o caixa parar de bater com o extrato e levaria junto a
///    conferência do dia. Se a operadora estornar de fato, isso é uma SAÍDA — outro
///    fato, com data própria —, e quem grava saída é o <see cref="FinanceiroService"/>.
/// 3. **A guia volta sozinha para a conciliação.** Cancelado o lançamento, ela deixa de
///    ter receita ativa e reaparece em <c>GuiasSemLancamentoAsync</c>. É o que faz o
///    caminho de volta funcionar sem código nenhum: recuperada a glosa, a guia está lá
///    esperando ser lançada de novo — com a data e o valor do dia em que isso acontecer,
///    que é a verdade daquele momento.
/// </summary>
public sealed class ReceitaGlosadaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;

    public ReceitaGlosadaService(IClinicaRepositorio repo, FinanceiroService financeiro)
    {
        _repo = repo;
        _financeiro = financeiro;
    }

    /// <summary>
    /// Guias glosadas que ainda têm receita ativa lançada.
    ///
    /// O filtro é pela DATA DA GLOSA, não pela do atendimento: a pergunta é "o que o
    /// convênio recusou neste período", e uma glosa de setembro sobre uma sessão de
    /// junho é problema de setembro — é quando ela chega e quando o caixa erra.
    ///
    /// <paramref name="incluirRecuperadas"/> traz também as glosas já recuperadas, que
    /// não são problema: a receita delas continua valendo. Fora da tela por padrão.
    /// </summary>
    public async Task<IReadOnlyList<ReceitaGlosada>> PendentesAsync(
        DateOnly inicio, DateOnly fim, bool incluirRecuperadas = false,
        CancellationToken ct = default)
    {
        var glosados = await _repo.CodigosGlosadosAsync(
            somenteEmAberto: !incluirRecuperadas, ct);

        var doPeriodo = glosados
            .Where(c => c.DataGlosa is { } data && data >= inicio && data <= fim)
            .Where(c => c.Atendimento?.Paciente is not null)
            .ToList();
        if (doPeriodo.Count == 0) return [];

        var lancamentos = await _repo.LancamentosDeCodigosAsync(
            doPeriodo.Select(c => c.Id).ToList(), ct);

        var porCodigo = lancamentos
            .Where(l => l.CodigoFaturamentoId is not null)
            .GroupBy(l => l.CodigoFaturamentoId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.Id).First());

        return doPeriodo
            .Where(c => porCodigo.ContainsKey(c.Id))
            .Select(c =>
            {
                var lancamento = porCodigo[c.Id];
                return new ReceitaGlosada(
                    c.Id,
                    c.Atendimento!.PacienteId,
                    c.Atendimento.Paciente!.Nome,
                    c.Atendimento.Paciente.Convenio,
                    c.NumeroGuiaReal,
                    c.DataGlosa!.Value,
                    c.MotivoGlosa,
                    c.MotivoGlosaCodigo,
                    c.Glosa,
                    c.DataLimiteRecurso,
                    lancamento.Id,
                    lancamento.Valor,
                    lancamento.Status,
                    lancamento.Data)
                {
                    ConvenioCodigo = c.Atendimento.Paciente.ConvenioCodigo
                };
            })
            // A mais antiga primeiro: o prazo de recurso corre contra ela, e é a que
            // está há mais tempo mentindo no fluxo de caixa.
            .OrderBy(r => r.DataGlosa)
            .ToList();
    }

    /// <summary>Total de receita prevista que a glosa derrubou — o número do painel.</summary>
    public async Task<(int Guias, decimal Valor)> ResumoPrevistoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var pendentes = await PendentesAsync(inicio, fim, ct: ct);
        var previstas = pendentes.Where(r => r.AindaPrevisto).ToList();
        return (previstas.Count, previstas.Sum(r => r.Valor));
    }

    /// <summary>
    /// Cancela a receita prevista de uma guia glosada.
    ///
    /// O motivo é obrigatório e entra no lançamento junto com a referência à guia: quem
    /// abrir o histórico daqui a seis meses precisa saber que aquela receita caiu por
    /// GLOSA, e não por engano de digitação — são coisas diferentes na hora de negociar
    /// com a operadora.
    ///
    /// Recusa o lançamento REALIZADO em vez de cancelá-lo em silêncio: o dinheiro entrou,
    /// e transformar isso em "não entrou" é como o caixa deixa de bater.
    /// </summary>
    public async Task CancelarReceitaAsync(
        int codigoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Escreva por que a receita está caindo. Daqui a seis meses, receita "
                + "cancelada sem motivo é indistinguível de erro de digitação.");

        var lancamentos = await _repo.LancamentosDeCodigosAsync([codigoId], ct);
        var lancamento = lancamentos.OrderByDescending(l => l.Id).FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Esta guia não tem receita lançada — não há o que cancelar.");

        if (lancamento.Status == StatusLancamento.Realizado)
            throw new InvalidOperationException(
                "O dinheiro desta guia já entrou no caixa. Cancelar a entrada faria o "
                + "caixa parar de bater com o extrato (e levaria junto a conferência do "
                + "dia). Se a operadora estornou o valor, lance a DEVOLUÇÃO como saída — "
                + "ela tem data própria, que é a do estorno.");

        var codigo = await _repo.ObterCodigoAsync(codigoId, ct);
        var numero = codigo?.NumeroGuiaReal is { } n and not "" ? $"guia {n}" : $"guia {codigoId}";

        await _financeiro.CancelarAsync(
            lancamento.Id, $"glosa da {numero} — {motivo.Trim()}", operador, ct);
    }
}
