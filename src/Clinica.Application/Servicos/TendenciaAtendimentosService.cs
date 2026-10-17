using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Por onde a tendência agrupa o movimento.</summary>
public enum DimensaoTendencia
{
    /// <summary>O que foi feito (acupuntura, BSV, consulta, a variante domiciliar…).</summary>
    Modalidade,

    /// <summary>A especialidade atendida — a da consulta, ou a de quem atendeu.</summary>
    Especialidade,

    /// <summary>A operadora (ou "particular") da ficha do paciente.</summary>
    Convenio,

    /// <summary>Quem atendeu.</summary>
    Profissional
}

/// <summary>O que se mede: volume ou dinheiro.</summary>
public enum MetricaTendencia
{
    /// <summary>Sessões REALIZADAS (o mesmo critério dos indicadores: status Realizado).</summary>
    Sessoes,

    /// <summary>Receita REALIZADA ligada a atendimento, pela data do pagamento.</summary>
    Receita
}

/// <summary>A leitura de uma linha: subiu, caiu, ficou, apareceu ou sumiu.</summary>
public enum SituacaoTendencia
{
    EmAlta,
    EmBaixa,
    Estavel,
    Nova,
    Sumiu,

    /// <summary>Movimento pequeno demais para dizer qualquer coisa (abaixo da base mínima).</summary>
    PoucaBase
}

/// <summary>Os recortes da tela — nulo é "todos".</summary>
public sealed record FiltroTendencia(
    DateOnly Inicio, DateOnly Fim,
    DimensaoTendencia Dimensao = DimensaoTendencia.Modalidade,
    MetricaTendencia Metrica = MetricaTendencia.Sessoes,
    int? ProfissionalId = null,
    string? ConvenioCodigo = null,
    string? ModalidadeCodigo = null,
    string? EspecialidadeCodigo = null);

/// <summary>Uma linha do quadro de tendência.</summary>
public sealed record LinhaTendencia(
    string Chave, string Rotulo, decimal Atual, decimal Anterior,
    double? Variacao, SituacaoTendencia Situacao, IReadOnlyList<decimal> SerieMensal,
    double FracaoDoTotal)
{
    public decimal Diferenca => Atual - Anterior;

    /// <summary>"▲ 32%" · "▼ 18%" · "nova" · "sumiu" · "—" — a frase curta da coluna.</summary>
    public string VariacaoTexto => Situacao switch
    {
        SituacaoTendencia.Nova => "nova no período",
        SituacaoTendencia.Sumiu => "sem movimento",
        _ when Variacao is { } v => (v > 0 ? "▲ " : v < 0 ? "▼ " : "") + Math.Abs(v).ToString("0", CultureInfo.InvariantCulture) + "%",
        _ => "—"
    };
}

/// <summary>O quadro inteiro, com o período anterior que serviu de comparação.</summary>
public sealed record PainelTendencia(
    FiltroTendencia Filtro,
    DateOnly InicioAnterior, DateOnly FimAnterior,
    decimal TotalAtual, decimal TotalAnterior,
    IReadOnlyList<LinhaTendencia> Linhas,
    IReadOnlyList<DateOnly> MesesDaSerie)
{
    public double? VariacaoTotal => TendenciaAtendimentos.Variacao(TotalAtual, TotalAnterior);
    public IReadOnlyList<LinhaTendencia> EmAlta => Linhas.Where(l => l.Situacao is SituacaoTendencia.EmAlta or SituacaoTendencia.Nova).ToList();
    public IReadOnlyList<LinhaTendencia> EmBaixa => Linhas.Where(l => l.Situacao is SituacaoTendencia.EmBaixa or SituacaoTendencia.Sumiu).ToList();
}

/// <summary>Um fato a agrupar — uma sessão realizada ou um recebimento.</summary>
public sealed record FatoTendencia(
    DateOnly Dia, decimal Valor, int PacienteId,
    ModalidadeAtendimento Modalidade, string? ModalidadeCodigo,
    string? EspecialidadeCodigo, int? ProfissionalId, string? Profissional,
    string? ConvenioCodigo, Convenio ConvenioFamilia);

/// <summary>
/// AS REGRAS DA TENDÊNCIA — puras, para o <c>dotnet test</c> alcançar (out/2026: a cliente
/// pediu *"filtros para sabermos as categorias e especialidades de atendimento que estamos
/// tendo, as que estão tendo alta, as que estão em baixa"*, como base do sistema de metas).
///
/// <b>"Em alta" e "em baixa" exigem os DOIS lados</b>: variação de pelo menos
/// <see cref="LimiarPercentual"/>% <i>e</i> diferença de pelo menos
/// <see cref="DiferencaMinimaSessoes"/> sessões. Só o percentual faria a especialidade que
/// foi de 1 para 2 sessões aparecer "▲ 100% em alta" — a mesma regra da reincidência de
/// faltas (parcela 29): uma em duas dá 50% e não diz nada. Abaixo da base mínima a linha
/// diz <see cref="SituacaoTendencia.PoucaBase"/>, em vez de afirmar uma tendência.
///
/// <b>A comparação é com o período IMEDIATAMENTE anterior de mesmo tamanho</b> — "outubro
/// contra setembro", "esta semana contra a passada". É a pergunta da direção ("está
/// crescendo?"); a pergunta "chegamos lá?" é a da META, e ela mora ao lado.
/// </summary>
public static class TendenciaAtendimentos
{
    public const double LimiarPercentual = 15;
    public const int DiferencaMinimaSessoes = 3;

    /// <summary>Na receita, a diferença mínima é em reais.</summary>
    public const decimal DiferencaMinimaReceita = 300m;

    public const int MesesDaSerie = 6;

    public static (DateOnly Inicio, DateOnly Fim) PeriodoAnterior(DateOnly inicio, DateOnly fim)
    {
        if (fim < inicio) (inicio, fim) = (fim, inicio);
        // Mês cheio compara com o mês cheio anterior (30 contra 31 dias não importa aqui;
        // importa que "setembro" seja setembro inteiro). Outros recortes: mesmo nº de dias.
        if (inicio.Day == 1 && fim == inicio.AddMonths(1).AddDays(-1))
        {
            var ant = inicio.AddMonths(-1);
            return (ant, ant.AddMonths(1).AddDays(-1));
        }
        var dias = fim.DayNumber - inicio.DayNumber + 1;
        return (inicio.AddDays(-dias), inicio.AddDays(-1));
    }

    /// <summary>Variação em %, ou nula sem base (anterior zero não é 100% nem infinito).</summary>
    public static double? Variacao(decimal atual, decimal anterior)
        => anterior == 0 ? null : Math.Round((double)((atual - anterior) / anterior) * 100.0, 1);

    public static SituacaoTendencia Classificar(decimal atual, decimal anterior, MetricaTendencia metrica)
    {
        var minimo = metrica == MetricaTendencia.Receita ? DiferencaMinimaReceita : DiferencaMinimaSessoes;
        if (Math.Max(atual, anterior) < minimo) return SituacaoTendencia.PoucaBase;
        if (anterior == 0) return SituacaoTendencia.Nova;
        if (atual == 0) return SituacaoTendencia.Sumiu;
        var v = Variacao(atual, anterior)!.Value;
        var dif = Math.Abs(atual - anterior);
        if (dif < minimo) return SituacaoTendencia.Estavel;
        return v >= LimiarPercentual ? SituacaoTendencia.EmAlta
             : v <= -LimiarPercentual ? SituacaoTendencia.EmBaixa
             : SituacaoTendencia.Estavel;
    }

    /// <summary>A chave e o rótulo do fato na dimensão pedida.</summary>
    public static (string Chave, string Rotulo) Agrupar(FatoTendencia f, DimensaoTendencia d) => d switch
    {
        DimensaoTendencia.Modalidade =>
            (f.ModalidadeCodigo ?? f.Modalidade.ToString(), CatalogoModalidades.Nome(f.ModalidadeCodigo, f.Modalidade)),
        DimensaoTendencia.Especialidade => f.EspecialidadeCodigo is { Length: > 0 } e
            ? (e, CatalogoEspecialidades.Nome(e))
            : ("", "Sem especialidade informada"),
        DimensaoTendencia.Convenio =>
            (f.ConvenioCodigo ?? f.ConvenioFamilia.ToString(), CatalogoConvenios.Nome(f.ConvenioCodigo, f.ConvenioFamilia)),
        _ => f.ProfissionalId is { } id
            ? (id.ToString(CultureInfo.InvariantCulture), f.Profissional ?? $"Profissional {id}")
            : ("", "Sem profissional")
    };

    /// <summary>Aplica os recortes. Recorte por especialidade casa a do FATO (a da consulta ou a de quem atendeu).</summary>
    public static bool Passa(FatoTendencia f, FiltroTendencia filtro)
        => (filtro.ProfissionalId is null || f.ProfissionalId == filtro.ProfissionalId)
           && (filtro.ConvenioCodigo is null || string.Equals(f.ConvenioCodigo ?? f.ConvenioFamilia.ToString(), filtro.ConvenioCodigo, StringComparison.OrdinalIgnoreCase))
           && (filtro.ModalidadeCodigo is null || string.Equals(f.ModalidadeCodigo ?? f.Modalidade.ToString(), filtro.ModalidadeCodigo, StringComparison.OrdinalIgnoreCase))
           && (filtro.EspecialidadeCodigo is null || string.Equals(f.EspecialidadeCodigo, filtro.EspecialidadeCodigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Monta o quadro. <paramref name="fatos"/> cobre do início da série (seis meses antes
    /// do fim) ao fim do período — quem chama lê UMA vez e esta função recorta.
    /// </summary>
    public static PainelTendencia Montar(FiltroTendencia filtro, IReadOnlyList<FatoTendencia> fatos)
    {
        var (inicio, fim) = filtro.Fim < filtro.Inicio ? (filtro.Fim, filtro.Inicio) : (filtro.Inicio, filtro.Fim);
        var (antIni, antFim) = PeriodoAnterior(inicio, fim);
        var meses = Enumerable.Range(0, MesesDaSerie)
            .Select(i => new DateOnly(fim.Year, fim.Month, 1).AddMonths(i - MesesDaSerie + 1))
            .ToList();

        var validos = fatos.Where(f => Passa(f, filtro)).ToList();
        var grupos = validos
            .Select(f => (Fato: f, Grupo: Agrupar(f, filtro.Dimensao)))
            .GroupBy(x => x.Grupo.Chave, StringComparer.OrdinalIgnoreCase)
            .ToList();

        decimal Somar(IEnumerable<FatoTendencia> fs, DateOnly a, DateOnly b) => fs.Where(f => f.Dia >= a && f.Dia <= b).Sum(f => f.Valor);

        var totalAtual = Somar(validos, inicio, fim);
        var totalAnterior = Somar(validos, antIni, antFim);

        var linhas = grupos
            .Select(g =>
            {
                var fs = g.Select(x => x.Fato).ToList();
                var atual = Somar(fs, inicio, fim);
                var anterior = Somar(fs, antIni, antFim);
                var serie = meses.Select(m => Somar(fs, m, m.AddMonths(1).AddDays(-1))).ToList();
                return new LinhaTendencia(
                    g.Key, g.First().Grupo.Rotulo, atual, anterior,
                    Variacao(atual, anterior), Classificar(atual, anterior, filtro.Metrica), serie,
                    totalAtual == 0 ? 0 : (double)(atual / totalAtual));
            })
            .Where(l => l.Atual > 0 || l.Anterior > 0)
            // O que mais pesa primeiro; desempate pelo nome, para duas leituras do MESMO
            // período nunca trocarem a ordem das linhas.
            .OrderByDescending(l => l.Atual).ThenByDescending(l => l.Anterior).ThenBy(l => l.Rotulo, StringComparer.Ordinal)
            .ToList();

        return new PainelTendencia(filtro with { Inicio = inicio, Fim = fim }, antIni, antFim,
            totalAtual, totalAnterior, linhas, meses);
    }

    /// <summary>A frase do topo da tela: o que subiu e o que caiu, com nomes.</summary>
    public static string Resumo(PainelTendencia p)
    {
        if (p.Linhas.Count == 0) return "Nenhum movimento no período escolhido.";
        string Nomes(IReadOnlyList<LinhaTendencia> ls) => string.Join(", ", ls.Take(3).Select(l => l.Rotulo)) + (ls.Count > 3 ? $" e mais {ls.Count - 3}" : "");
        var partes = new List<string>();
        if (p.EmAlta.Count > 0) partes.Add($"Em alta: {Nomes(p.EmAlta)}");
        if (p.EmBaixa.Count > 0) partes.Add($"Em baixa: {Nomes(p.EmBaixa)}");
        return partes.Count == 0 ? "Nada mudou o bastante para chamar de alta ou baixa." : string.Join(" · ", partes) + ".";
    }
}

/// <summary>
/// Lê os fatos e monta o quadro de tendência. Só LÊ — como os indicadores.
///
/// <b>As sessões vêm do MESMO critério do <see cref="IndicadoresService"/></b> (agendamento
/// com status Realizado): um segundo critério de "sessão atendida" daria à tela de metas um
/// número e à de tendência outro, para a mesma pergunta. A receita vem da projeção
/// <see cref="IClinicaRepositorio.ReceitaPorAtendimentoAsync"/> (realizado, pela data do
/// pagamento — o regime de caixa que o Resultado do mês já usa).
///
/// <b>A especialidade de uma sessão</b> é a da CONSULTA quando informada, senão a de quem
/// atendeu — acupuntura feita pelo psiquiatra conta na especialidade dele. Sem nenhuma das
/// duas, a linha diz "Sem especialidade informada" em vez de sumir com o movimento.
/// </summary>
public sealed class TendenciaAtendimentosService
{
    private readonly IClinicaRepositorio _repo;

    public TendenciaAtendimentosService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<PainelTendencia> MontarAsync(FiltroTendencia filtro, CancellationToken ct = default)
    {
        var fim = filtro.Fim < filtro.Inicio ? filtro.Inicio : filtro.Fim;
        var inicio = filtro.Fim < filtro.Inicio ? filtro.Fim : filtro.Inicio;
        var (antIni, _) = TendenciaAtendimentos.PeriodoAnterior(inicio, fim);
        var serieIni = new DateOnly(fim.Year, fim.Month, 1).AddMonths(1 - TendenciaAtendimentos.MesesDaSerie);
        var desde = antIni < serieIni ? antIni : serieIni;

        var fatos = filtro.Metrica == MetricaTendencia.Receita
            ? await FatosDeReceitaAsync(desde, fim, ct)
            : await FatosDeSessoesAsync(desde, fim, ct);

        return TendenciaAtendimentos.Montar(filtro, fatos);
    }

    /// <summary>
    /// Os fatos crus de um período — a mesma base que a meta segmentada usa para apurar o
    /// realizado, para a tela de metas e a de tendência darem o MESMO número.
    /// </summary>
    public Task<IReadOnlyList<FatoTendencia>> MontarFatosAsync(
        MetricaTendencia metrica, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => metrica == MetricaTendencia.Receita ? FatosDeReceitaAsync(inicio, fim, ct) : FatosDeSessoesAsync(inicio, fim, ct);

    private async Task<IReadOnlyList<FatoTendencia>> FatosDeSessoesAsync(DateOnly desde, DateOnly fim, CancellationToken ct)
    {
        // Leitura SEQUENCIAL no mesmo repositório (parcela 74): agendamentos, depois as fichas.
        var sessoes = (await _repo.AgendamentosNoPeriodoParaLeituraAsync(
                desde.ToDateTime(TimeOnly.MinValue), fim.ToDateTime(TimeOnly.MaxValue), ct))
            .Where(a => a.Status == StatusAgendamento.Realizado)
            .ToList();
        var fichas = await FichasAsync(sessoes.Select(a => a.PacienteId), ct);

        return sessoes.Select(a =>
        {
            fichas.TryGetValue(a.PacienteId, out var p);
            return new FatoTendencia(
                DateOnly.FromDateTime(a.DataHora), 1m, a.PacienteId,
                a.ModalidadePrevista, a.ModalidadeCodigo,
                a.EspecialidadeConsultaCodigo ?? a.Profissional?.EspecialidadeCodigo,
                a.ProfissionalId, a.Profissional?.Rotulo,
                p?.ConvenioCodigo, p?.ConvenioFamilia ?? default);
        }).ToList();
    }

    private async Task<IReadOnlyList<FatoTendencia>> FatosDeReceitaAsync(DateOnly desde, DateOnly fim, CancellationToken ct)
    {
        var receitas = await _repo.ReceitaPorAtendimentoAsync(desde, fim, ct);
        var fichas = await FichasAsync(receitas.Select(r => r.PacienteId), ct);
        var profissionais = (await _repo.ProfissionaisAsync(ct)).ToDictionary(p => p.Id);

        return receitas.Select(r =>
        {
            fichas.TryGetValue(r.PacienteId, out var p);
            Profissional? quem = r.ProfissionalId is { } id && profissionais.TryGetValue(id, out var pr) ? pr : null;
            return new FatoTendencia(
                r.Dia, r.Valor, r.PacienteId, r.Modalidade, r.ModalidadeCodigo,
                r.EspecialidadeCodigo ?? quem?.EspecialidadeCodigo,
                r.ProfissionalId, quem?.Rotulo,
                p?.ConvenioCodigo, p?.ConvenioFamilia ?? default);
        }).ToList();
    }

    private async Task<Dictionary<int, PacienteParaContato>> FichasAsync(IEnumerable<int> ids, CancellationToken ct)
        => (await _repo.PacientesParaContatoAsync(ids.Distinct().ToList(), ct)).ToDictionary(p => p.Id);
}

