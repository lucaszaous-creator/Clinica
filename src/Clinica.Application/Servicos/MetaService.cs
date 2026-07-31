using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Uma meta com o que aconteceu de verdade ao lado dela.
///
/// <see cref="Realizado"/> nulo significa **não medido**, nunca zero: ocupação sem
/// nenhuma agenda aberta no mês não é 0% de ocupação, é ausência de base — a mesma regra
/// que vale nos indicadores desde a parcela 5.
/// </summary>
public sealed record MetaApurada(
    IndicadorMeta Indicador,
    int Ano,
    int Mes,
    decimal Alvo,
    decimal? Realizado,
    decimal? Projecao,
    int? ProfissionalId,
    string? Profissional)
{
    /// <summary>Quanto da meta já foi cumprido, de 0 em diante. Nulo sem base.</summary>
    public double? PercentualAtingido => Realizado is { } r && Alvo > 0m
        ? Math.Round((double)(r / Alvo) * 100.0, 1)
        : null;

    /// <summary>Quanto falta para o alvo. Negativo = já passou dele.</summary>
    public decimal? Falta => Realizado is { } r ? Alvo - r : null;

    public bool Atingida => Realizado is { } r && r >= Alvo;

    /// <summary>
    /// O mês vai fechar abaixo do alvo se continuar neste ritmo. É a leitura que muda a
    /// conduta no dia 12 — "estamos 40% abaixo" no dia 31 já não muda nada.
    /// </summary>
    public bool EmRisco => !Atingida && Projecao is { } p && p < Alvo;

    /// <summary>Quanto a projeção fica abaixo do alvo, em % — o tamanho do problema.</summary>
    public double? DesvioProjetado => Projecao is { } p && Alvo > 0m
        ? Math.Round((double)((p - Alvo) / Alvo) * 100.0, 1)
        : null;

    public string Rotulo => MetaMensal.Rotular(Indicador);
    public string Unidade => MetaMensal.Unidade(Indicador);
}

/// <summary>
/// Metas da direção (parcela 28): o alvo, o realizado e a projeção do mês.
///
/// O painel da direção comparava o mês com o **mês anterior**. Isso responde "melhorou?"
/// e nunca responde "chegamos onde a gente disse que ia chegar?" — que é a única pergunta
/// capaz de fazer alguém mudar de conduta no meio do mês. Sem alvo, um mês 10% melhor que
/// um mês ruim aparece como vitória.
///
/// Três regras:
///
/// 1. **O realizado vem do serviço DONO do indicador**, nunca recalculado aqui. Faturamento
///    é o `FinanceiroService`, sessões e ocupação são o `IndicadoresService`. Recontar
///    daria um segundo número para a mesma pergunta, e a direção veria a meta batida numa
///    tela e não batida na outra — foi por isso que o painel da parcela 22 já nasceu
///    delegando tudo.
/// 2. **A projeção só existe depois que o mês tem lastro.** Projetar no dia 2 é
///    multiplicar ruído por quinze: um único dia forte diria que a clínica vai fechar o
///    dobro. Antes de <see cref="DiasMinimosParaProjetar"/> a projeção é nula e a tela
///    mostra "—".
/// 3. **Mês passado não se projeta.** Para um mês fechado a projeção é o próprio
///    realizado; inventar tendência sobre o que já acabou seria enfeite.
/// </summary>
public sealed class MetaService
{
    /// <summary>
    /// Dias corridos do mês antes dos quais não se projeta nada. Cinco porque é onde a
    /// média do dia começa a valer alguma coisa numa clínica que não abre todo dia.
    /// </summary>
    public const int DiasMinimosParaProjetar = 5;

    private readonly IClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly IndicadoresService _indicadores;

    public MetaService(
        IClinicaRepositorio repo, FinanceiroService financeiro, IndicadoresService indicadores)
    {
        _repo = repo;
        _financeiro = financeiro;
        _indicadores = indicadores;
    }

    // ==================== Cadastro ====================

    public Task<IReadOnlyList<MetaMensal>> DoAnoAsync(int ano, CancellationToken ct = default)
        => _repo.MetasDoAnoAsync(ano, ct);

    /// <summary>
    /// Define (ou corrige) a meta de um mês. Uma linha por mês, indicador e dono — se já
    /// existe, o valor é substituído: corrigir o alvo de um mês em curso é decisão
    /// legítima da direção, e criar uma segunda linha daria dois alvos para a mesma
    /// pergunta.
    /// </summary>
    public async Task<MetaMensal> DefinirAsync(
        int ano, int mes, IndicadorMeta indicador, decimal valor,
        int? profissionalId = null, string? observacoes = null,
        string? operador = null, CancellationToken ct = default)
    {
        if (mes is < 1 or > 12)
            throw new InvalidOperationException("Mês precisa estar entre 1 e 12.");

        if (valor < 0m)
            throw new InvalidOperationException("Meta negativa não significa nada — use zero.");

        if (indicador == IndicadorMeta.Ocupacao && valor > 100m)
            throw new InvalidOperationException(
                "Ocupação é percentual: o máximo é 100. Agenda mais cheia que a jornada "
                + "significa encaixe, e encaixe não é meta.");

        var existente = await _repo.ObterMetaAsync(ano, mes, indicador, profissionalId, ct);

        if (existente is not null)
        {
            var antes = existente.Valor;
            existente.Valor = valor;
            existente.Observacoes = observacoes;

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "MetaAlterada",
                Detalhe = $"{MetaMensal.Rotular(indicador)} {mes:00}/{ano}: {antes} → {valor}"
            }, ct);
            await _repo.SalvarAsync(ct);
            return existente;
        }

        var meta = new MetaMensal
        {
            Ano = ano,
            Mes = mes,
            Indicador = indicador,
            Valor = valor,
            ProfissionalId = profissionalId,
            Observacoes = observacoes,
            CriadoPor = operador
        };

        await _repo.AdicionarMetaAsync(meta, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "MetaDefinida",
            Detalhe = $"{MetaMensal.Rotular(indicador)} {mes:00}/{ano}: {valor}"
        }, ct);
        await _repo.SalvarAsync(ct);

        return meta;
    }

    /// <summary>
    /// Apaga a meta. Diferente de zerar: **meta ausente é "não decidimos"**, meta zero é
    /// "decidimos que é zero". Guardar uma meta zerada faria todo mês não planejado
    /// aparecer como alvo batido.
    /// </summary>
    public async Task ExcluirAsync(int metaId, string? operador = null, CancellationToken ct = default)
    {
        var meta = await _repo.ObterMetaPorIdAsync(metaId, ct)
            ?? throw new InvalidOperationException("Meta não encontrada.");

        await _repo.RemoverMetaAsync(metaId, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "MetaExcluida",
            Detalhe = $"{MetaMensal.Rotular(meta.Indicador)} {meta.Mes:00}/{meta.Ano}"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Apuração ====================

    /// <summary>
    /// As metas do mês com o realizado e a projeção. Devolve vazio quando a direção não
    /// definiu nada — a tela diz "nenhuma meta definida", que é diferente de "não bateu".
    /// </summary>
    public async Task<IReadOnlyList<MetaApurada>> ApurarMesAsync(
        DateOnly referencia, CancellationToken ct = default)
    {
        var metas = (await _repo.MetasDoAnoAsync(referencia.Year, ct))
            .Where(m => m.Mes == referencia.Month)
            .ToList();
        if (metas.Count == 0) return [];

        var inicio = new DateOnly(referencia.Year, referencia.Month, 1);
        var fimDoMes = inicio.AddMonths(1).AddDays(-1);

        // O recorte do realizado vai até HOJE quando o mês está em curso, e até o fim
        // quando já passou: somar dias que ainda não aconteceram daria realizado menor
        // que o real em nenhum caso, mas mediria ocupação sobre agenda que nem existe.
        var ate = referencia < fimDoMes ? referencia : fimDoMes;
        var mesEmCurso = referencia >= inicio && referencia < fimDoMes;

        decimal? faturamento = null;
        decimal? sessoes = null;
        decimal? ocupacao = null;

        if (metas.Any(m => m.Indicador == IndicadorMeta.Faturamento))
        {
            var resumo = await _financeiro.ResumoAsync(inicio, ate, ct);
            faturamento = resumo.EntradasRealizadas;
        }

        if (metas.Any(m => m.Indicador is IndicadorMeta.Sessoes or IndicadorMeta.Ocupacao))
        {
            var painel = await _indicadores.GerarAsync(inicio, ate, ct);
            sessoes = painel.Agenda.Atendidos;
            // Nulo quando não houve agenda: 0% de ocupação e "não medido" são coisas
            // diferentes, e a segunda não pode virar meta perdida.
            ocupacao = painel.Agenda.OcupacaoPercentual is { } o ? (decimal)o : null;
        }

        var diasCorridos = ate.Day;
        var diasDoMes = fimDoMes.Day;

        return metas
            .Select(m =>
            {
                var realizado = m.Indicador switch
                {
                    IndicadorMeta.Faturamento => faturamento,
                    IndicadorMeta.Sessoes => sessoes,
                    IndicadorMeta.Ocupacao => ocupacao,
                    _ => null
                };

                return new MetaApurada(
                    m.Indicador, m.Ano, m.Mes, m.Valor, realizado,
                    Projetar(m.Indicador, realizado, diasCorridos, diasDoMes, mesEmCurso),
                    m.ProfissionalId, m.Profissional?.Rotulo);
            })
            .OrderBy(m => m.Indicador)
            .ToList();
    }

    /// <summary>
    /// Onde o mês fecha se o ritmo se mantiver.
    ///
    /// **Ocupação não se projeta por regra de três**: ela já é uma média (minutos ocupados
    /// sobre minutos disponíveis), e multiplicá-la pelos dias que faltam daria 300%. Para
    /// ela, a melhor projeção é a própria média até aqui — que é o que ela significa.
    /// </summary>
    private static decimal? Projetar(
        IndicadorMeta indicador, decimal? realizado,
        int diasCorridos, int diasDoMes, bool mesEmCurso)
    {
        if (realizado is not { } r) return null;

        // Mês fechado (ou futuro): o que aconteceu é o que aconteceu.
        if (!mesEmCurso) return r;

        if (indicador == IndicadorMeta.Ocupacao) return r;

        if (diasCorridos < DiasMinimosParaProjetar) return null;

        return Math.Round(r / diasCorridos * diasDoMes, 2);
    }
}
