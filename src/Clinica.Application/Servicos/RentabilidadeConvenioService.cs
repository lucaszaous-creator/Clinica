using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quanto um convênio rendeu no período.
///
/// Os quatro números não são o mesmo dinheiro em estágios: são perguntas diferentes.
/// <paramref name="Guias"/> é o que foi efetivado no convênio; <paramref name="Bruto"/> é
/// o que virou receita lançada; <paramref name="Retido"/> é o que a operadora tirou antes
/// de depositar; e o líquido é o que sobrou. A distância entre a primeira e a segunda é a
/// conciliação que ninguém fez.
/// </summary>
public sealed record RentabilidadeConvenio(
    string Codigo,
    string Convenio,
    int Guias,
    int GuiasComReceita,
    decimal Bruto,
    decimal Retido,
    int Glosadas,
    double? PrazoMedioDias)
{
    /// <summary>O que de fato sobrou depois da retenção da operadora.</summary>
    public decimal Liquido => Bruto - Retido;

    /// <summary>
    /// Quanto rende cada guia efetivada, em líquido. É o número que responde "vale a pena
    /// atender este convênio?" — e o único comparável entre operadoras que pagam valores e
    /// volumes diferentes. Null sem guia com receita: média sobre zero é invenção.
    /// </summary>
    public decimal? LiquidoPorGuia =>
        GuiasComReceita > 0 ? Math.Round(Liquido / GuiasComReceita, 2) : null;

    /// <summary>
    /// Guias efetivadas que ainda não viraram receita lançada. É trabalho já feito e não
    /// cobrado — a fila da conciliação, vista por convênio.
    /// </summary>
    public int SemReceita => Math.Max(Guias - GuiasComReceita, 0);

    /// <summary>
    /// Percentual do bruto que a operadora reteve. Null sem receita — e null não é zero:
    /// "não reteve nada" e "não recebeu nada" são diagnósticos diferentes.
    /// </summary>
    public decimal? PercentualRetido => Bruto > 0m ? Retido / Bruto * 100m : null;

    /// <summary>Fatia do líquido total, de 0 a 1 — o tamanho da barra na tela.</summary>
    public double Fracao { get; init; }
}

/// <summary>O retrato do período, somando todos os convênios.</summary>
public sealed record ResumoRentabilidade(
    int Guias,
    decimal Bruto,
    decimal Retido,
    int Glosadas,
    RentabilidadeConvenio? MaisRentavel,
    RentabilidadeConvenio? MenosRentavel)
{
    public decimal Liquido => Bruto - Retido;

    public decimal? PercentualRetido => Bruto > 0m ? Retido / Bruto * 100m : null;

    /// <summary>Percentual das guias efetivadas que a operadora glosou.</summary>
    public double? TaxaGlosa => Guias > 0 ? Math.Round(Glosadas * 100.0 / Guias, 1) : null;
}

/// <summary>
/// Rentabilidade por convênio (parcela 19).
///
/// A clínica fatura muito convênio e não tinha como responder à pergunta mais básica da
/// direção: **qual operadora vale a pena?**. O faturamento sabe quantas guias saíram e o
/// financeiro sabe quanto entrou, mas os dois números nunca se encontravam por convênio —
/// e é o encontro deles que revela a operadora que paga pouco, paga tarde ou glosa muito.
///
/// Três leituras que só existem aqui:
///
/// 1. **Líquido por guia.** O único número comparável entre operadoras que pagam valores e
///    volumes diferentes. "A Unimed faturou mais" não diz nada se ela precisou de três
///    vezes mais atendimentos para isso.
/// 2. **Prazo médio de pagamento.** Da baixa da guia ao dinheiro no caixa. Uma operadora
///    que paga 10% a mais em 90 dias pode ser pior que outra que paga menos em 30.
/// 3. **Guias sem receita.** Trabalho já feito e não cobrado, visto por convênio — a fila
///    da conciliação com nome de quem está devendo.
///
/// Só lê. Nada aqui escreve no faturamento congelado.
/// </summary>
public sealed class RentabilidadeConvenioService
{
    private readonly IClinicaRepositorio _repo;

    public RentabilidadeConvenioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Rentabilidade de cada convênio no período, do mais rentável para o menos.
    ///
    /// O período é o do ATENDIMENTO da guia, não o do recebimento: a pergunta é "o que
    /// este convênio rendeu do que eu atendi em setembro", e casar pelo recebimento
    /// misturaria guias de meses diferentes no mesmo balanço.
    /// </summary>
    public async Task<IReadOnlyList<RentabilidadeConvenio>> PorConvenioAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var codigos = await _repo.CodigosNoPeriodoAsync(inicio, fim, ct);

        var guias = codigos
            .Where(c => c.Atendimento?.Paciente is not null)
            .ToList();
        if (guias.Count == 0) return [];

        var lancamentos = await _repo.LancamentosDeCodigosAsync(
            guias.Select(c => c.Id).ToList(), ct);

        // Indexa o dinheiro pela guia: um lançamento por guia é o caminho normal, mas a
        // clínica pode ter lançado em duas parcelas, e somar é o que fecha com o caixa.
        var porCodigo = lancamentos
            .GroupBy(l => l.CodigoFaturamentoId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var linhas = guias
            // Agrupa pelo CÓDIGO, não pelo nome exibido: o código é a identidade e o nome
            // é apresentação. Duas operadoras da mesma família que ainda não estejam no
            // catálogo resolvem para o MESMO nome padrão, e agrupar por ele as fundiria
            // num grupo só — apagando justamente a distinção que a retenção da parcela 18
            // usa para reter percentuais diferentes.
            .GroupBy(c => CodigoDoConvenio(c.Atendimento!.Paciente!))
            .Select(g =>
            {
                var doGrupo = g.ToList();
                var comDinheiro = doGrupo
                    .Where(c => porCodigo.ContainsKey(c.Id))
                    .ToList();

                var recebidos = comDinheiro.SelectMany(c => porCodigo[c.Id]).ToList();

                // Prazo: da baixa da guia ao pagamento efetivo. Só entra o que JÁ foi pago
                // — incluir o previsto mediria uma promessa, e é exatamente a operadora
                // que atrasa que teria o melhor número.
                var prazos = comDinheiro
                    .Where(c => c.DataBaixa is not null)
                    .SelectMany(c => porCodigo[c.Id]
                        .Where(l => l.Status == StatusLancamento.Realizado && l.DataPagamento is not null)
                        .Select(l => (double)(l.DataPagamento!.Value.DayNumber - c.DataBaixa!.Value.DayNumber)))
                    .ToList();

                return new RentabilidadeConvenio(
                    g.Key,
                    CatalogoConvenios.Nome(g.Key),
                    doGrupo.Count,
                    comDinheiro.Count,
                    recebidos.Sum(l => l.Valor),
                    recebidos.Sum(l => l.ValorImposto ?? 0m),
                    doGrupo.Count(c => c.Glosa != StatusGlosa.SemGlosa),
                    prazos.Count > 0 ? Math.Round(prazos.Average(), 1) : null);
            })
            .ToList();

        var totalLiquido = linhas.Sum(l => l.Liquido);

        return linhas
            .Select(l => l with
            {
                Fracao = totalLiquido > 0m ? (double)(l.Liquido / totalLiquido) : 0d
            })
            .OrderByDescending(l => l.Liquido)
            .ThenBy(l => l.Convenio)
            .ToList();
    }

    /// <summary>
    /// Retrato do período: totais, a operadora mais rentável e a menos — esta última pelo
    /// LÍQUIDO POR GUIA, não pelo total. A menos rentável não é a que rendeu menos dinheiro
    /// (pode ser só a de menor volume), é a que rende menos por atendimento.
    /// </summary>
    public async Task<ResumoRentabilidade> ResumoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var linhas = await PorConvenioAsync(inicio, fim, ct);

        var comMedia = linhas.Where(l => l.LiquidoPorGuia is not null).ToList();

        return new ResumoRentabilidade(
            linhas.Sum(l => l.Guias),
            linhas.Sum(l => l.Bruto),
            linhas.Sum(l => l.Retido),
            linhas.Sum(l => l.Glosadas),
            comMedia.OrderByDescending(l => l.LiquidoPorGuia).FirstOrDefault(),
            comMedia.Count > 1 ? comMedia.OrderBy(l => l.LiquidoPorGuia).First() : null);
    }

    /// <summary>
    /// A identidade do convênio: o código do catálogo, caindo na família quando o paciente
    /// é antigo e não tem código — como o resto do sistema já faz.
    /// </summary>
    private static string CodigoDoConvenio(Paciente p)
        => p.ConvenioCodigo ?? p.Convenio.ToString();
}
