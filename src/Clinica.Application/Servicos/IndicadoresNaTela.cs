using System.Globalization;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de INDICADORES do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu do WPF para
/// o Blazor). Era o <c>IndicadoresViewModel</c>: as frases, as variações contra o mês anterior,
/// as barras de ocupação e o CSV. Nada aqui lê o banco — a tela lê com os MESMOS serviços
/// (<see cref="IndicadoresService"/> e <see cref="CampanhaService.NpsAsync"/>).
///
/// Toda métrica que pode não ter base de cálculo sai "—", nunca zero: 0% de ocupação e "não
/// deu para medir" são coisas diferentes, e confundi-las faria a direção decidir em cima de
/// um número que não existe.
/// </summary>
public static class IndicadoresNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string Travessao = "—";

    public static PainelDeIndicadores Montar(PainelGerencial painel, ResumoNps nps)
    {
        var a = painel.Agenda;
        var serie = painel.Serie;

        var nosShow = serie.Select(m => new PontoDaSerie(m.Rotulo, m.Fechados == 0 ? null : m.TaxaNoShow)).ToList();

        return new PainelDeIndicadores(
            Intervalo: ResumoFaturamentoNaTela.Rotular((painel.Inicio, painel.Fim)),
            Agendados: a.Agendados,
            Atendidos: a.Atendidos,
            Faltas: a.Faltas,
            Encaixes: a.Encaixes,
            PacientesDistintos: a.PacientesDistintos,
            NoShow: a.Fechados == 0 ? Travessao : Pct(a.TaxaNoShow),
            Ocupacao: a.OcupacaoPercentual is { } o ? Pct(o) : Travessao,
            HorasOcupadas: $"{Um(a.MinutosOcupados / 60.0)} h",
            Jornada: $"Ocupação medida sobre {Um(painel.JornadaDiariaMinutos / 60.0)} h por dia com agenda aberta.",
            Nps: nps.Medido ? nps.Pontuacao.ToString(PtBr) : Travessao,
            NpsDetalhe: DetalheDoNps(nps),
            VariacaoAtendidos: VariacaoDeAtendidos(serie),
            VariacaoNoShow: VariacaoDeNoShow(serie),
            Produtividade: painel.Produtividade.Select(LinhaDe).ToList(),
            Barras: Barras(painel.Produtividade),
            SerieAtendidos: serie.Select(m => new PontoDaSerie(m.Rotulo, m.Atendidos)).ToList(),
            SerieNoShow: nosShow,
            // Um ponto só não é evolução.
            TemSerie: serie.Count > 1,
            Meses: serie.Select(m => new MesDosIndicadores(
                m.Rotulo, m.Atendidos,
                m.Fechados == 0 ? Travessao : Pct(m.TaxaNoShow),
                m.OcupacaoPercentual is { } om ? Pct(om) : Travessao)).ToList(),
            MensagemVazio: painel.Produtividade.Count == 0 ? "Nenhum horário na agenda neste período." : null);
    }

    /// <summary>
    /// "12 resposta(s): …" quando medido; senão diz POR QUE não há número — pesquisa enviada
    /// sem resposta e pesquisa nenhuma são situações diferentes.
    /// </summary>
    public static string DetalheDoNps(ResumoNps nps)
        => nps.Medido
            ? $"{nps.Respondidos} resposta(s): {nps.Promotores} promotor(es), "
              + $"{nps.Neutros} neutro(s), {nps.Detratores} detrator(es)"
            : nps.Enviados > 0
                ? $"{nps.Enviados} enviada(s), nenhuma resposta ainda"
                : "nenhuma pesquisa no período";

    /// <summary>
    /// Último mês da série contra o anterior. Null sem base de comparação (série curta ou mês
    /// anterior sem atendimento): "0% vs mês anterior" sem mês anterior é invenção.
    /// Mais atendimento é BOM.
    /// </summary>
    public static VariacaoDeIndicador? VariacaoDeAtendidos(IReadOnlyList<MesGerencial> serie)
    {
        if (serie.Count < 2) return null;
        var atual = serie[^1];
        var anterior = serie[^2];
        if (anterior.Atendidos <= 0) return null;

        var delta = (atual.Atendidos - anterior.Atendidos) * 100.0 / anterior.Atendidos;
        return new VariacaoDeIndicador($"{Seta(delta)} {Um(Math.Abs(delta))}% vs {anterior.Rotulo}", delta >= 0);
    }

    /// <summary>
    /// No-show já é percentual: a variação é em PONTOS, não em % de % ("subiu 50%" sobre 4%
    /// confunde; "subiu 2 pontos" não). Aqui subir é RUIM. Precisa dos dois meses com horário
    /// fechado.
    /// </summary>
    public static VariacaoDeIndicador? VariacaoDeNoShow(IReadOnlyList<MesGerencial> serie)
    {
        if (serie.Count < 2) return null;
        var atual = serie[^1];
        var anterior = serie[^2];
        if (anterior.Fechados <= 0 || atual.Fechados <= 0) return null;

        var pontos = atual.TaxaNoShow - anterior.TaxaNoShow;
        return new VariacaoDeIndicador($"{Seta(pontos)} {Um(Math.Abs(pontos))} ponto(s) vs {anterior.Rotulo}", pontos <= 0);
    }

    /// <summary>
    /// Ocupação por profissional em barras, da maior para a menor. A barra cheia é a MAIOR
    /// ocupação do período, não 100%: numa clínica que nunca passa de 60% todas ficariam
    /// curtas e iguais, e a comparação — que é a pergunta — sumiria. Profissional sem base de
    /// ocupação fica fora (não há barra para "não medido").
    /// </summary>
    public static IReadOnlyList<BarraDeOcupacao> Barras(IReadOnlyList<ProdutividadeProfissional> produtividade)
    {
        var comOcupacao = produtividade
            .Where(p => p.OcupacaoPercentual is not null)
            .OrderByDescending(p => p.OcupacaoPercentual)
            .ToList();
        var maior = comOcupacao.Count == 0 ? 0 : comOcupacao.Max(p => p.OcupacaoPercentual!.Value);

        return comOcupacao.Select(p =>
        {
            var valor = p.OcupacaoPercentual!.Value;
            return new BarraDeOcupacao(p.Nome, $"{Pct(valor)} · {p.Atendidos} atendido(s)", maior <= 0 ? 0 : valor / maior);
        }).ToList();
    }

    /// <summary>
    /// O CSV da tela, com os MESMOS cabeçalhos do WPF (a planilha da direção não muda de
    /// colunas porque a tela mudou de tecnologia). Métrica sem base sai "—", igual à tela:
    /// zero entraria na média da planilha.
    /// </summary>
    public static (IReadOnlyList<string> Cabecalho, IReadOnlyList<IReadOnlyList<string>> Linhas) Csv(PainelGerencial painel)
    {
        var linhas = new List<IReadOnlyList<string>>();
        foreach (var m in painel.Serie)
            linhas.Add([
                "Mes", m.Rotulo,
                m.Atendidos.ToString(PtBr), m.Faltas.ToString(PtBr), m.Cancelados.ToString(PtBr),
                m.Fechados == 0 ? Travessao : Um(m.TaxaNoShow),
                m.OcupacaoPercentual is { } o ? Um(o) : Travessao,
                // A série mensal não tem completude por profissional.
                Travessao]);
        foreach (var p in painel.Produtividade)
            linhas.Add([
                "Profissional", p.Nome,
                p.Atendidos.ToString(PtBr), p.Faltas.ToString(PtBr), p.Cancelados.ToString(PtBr),
                p.Fechados == 0 ? Travessao : Um(p.TaxaNoShow),
                p.OcupacaoPercentual is { } o ? Um(o) : Travessao,
                p.CompletudeProntuario is { } c ? Um(c) : Travessao]);

        return (["Tipo", "Nome", "Atendidos", "Faltas", "Cancelados", "No-show %", "Ocupacao %", "Prontuario %"], linhas);
    }

    private static LinhaDeProdutividade LinhaDe(ProdutividadeProfissional p) => new(
        p.Nome, p.Atendidos, p.Faltas,
        // Sem horário fechado não há taxa: "0%" diria que ninguém faltou.
        p.Fechados == 0 ? Travessao : Pct(p.TaxaNoShow),
        $"{Um(p.HorasOcupadas)} h",
        p.Evolucoes,
        // "—" quando não houve sessão atendida: 0% acusaria de negligência quem tirou férias.
        p.CompletudeProntuario is { } c ? Pct(c) : Travessao,
        p.MelhoraMediaEva is { } e ? $"{Um(e)} {(e == 1 ? "ponto" : "pontos")}" : Travessao);

    private static string Seta(double delta) => delta > 0 ? "▲" : delta < 0 ? "▼" : "→";
    private static string Um(double v) => v.ToString("0.#", PtBr);
    private static string Pct(double v) => $"{Um(v)}%";
}

/// <summary>Ponto de uma série de gráfico de linha; <c>Valor</c> nulo é BURACO ("não medido"), não zero.</summary>
public sealed record PontoDaSerie(string Rotulo, double? Valor);

/// <summary>Variação contra o mês anterior: o texto com a seta e se a mudança é boa para a clínica.</summary>
public sealed record VariacaoDeIndicador(string Texto, bool Boa);

/// <summary>Uma barra de ocupação: a fração (0..1) já normalizada pela maior do período.</summary>
public sealed record BarraDeOcupacao(string Rotulo, string ValorRotulo, double Fracao);

public sealed record LinhaDeProdutividade(
    string Nome, int Atendidos, int Faltas, string NoShow, string Horas, int Evolucoes,
    string Prontuario, string QuedaDaDor);

public sealed record MesDosIndicadores(string Rotulo, int Atendidos, string NoShow, string Ocupacao);

public sealed record PainelDeIndicadores(
    string Intervalo,
    int Agendados, int Atendidos, int Faltas, int Encaixes, int PacientesDistintos,
    string NoShow, string Ocupacao, string HorasOcupadas, string Jornada,
    string Nps, string NpsDetalhe,
    VariacaoDeIndicador? VariacaoAtendidos, VariacaoDeIndicador? VariacaoNoShow,
    IReadOnlyList<LinhaDeProdutividade> Produtividade,
    IReadOnlyList<BarraDeOcupacao> Barras,
    IReadOnlyList<PontoDaSerie> SerieAtendidos,
    IReadOnlyList<PontoDaSerie> SerieNoShow,
    bool TemSerie,
    IReadOnlyList<MesDosIndicadores> Meses,
    string? MensagemVazio);
