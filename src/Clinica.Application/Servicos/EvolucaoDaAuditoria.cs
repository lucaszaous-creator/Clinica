using System.Globalization;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

// ============================================================================ a evolução semana a semana (27/10)
// A direção quer ver se a devolutiva faz efeito: a nota da gerente e a 1ª resposta de cada secretária, semana a semana.
// Tudo o que o gráfico desenha — a escala, as coordenadas, o buraco onde não há dado, a semana corrente parcial, os
// rótulos — e a frase de tendência saem daqui, puros e com teste. A tela só põe no SVG.

/// <summary>A leitura da evolução: os dados, ou a frase da falha (a seção mostra a falha sozinha; o painel continua).</summary>
public sealed record EvolucaoLida(AppQualityTrend? Dados, string? Erro, bool SemPermissao = false);

/// <summary>Um ponto de uma linha. <see cref="Y"/> nulo = semana sem dado (buraco, nunca zero).</summary>
public sealed record PontoDaEvolucao(int Indice, double X, double? Y, double? Valor, bool Parcial);

/// <summary>Um trecho contínuo da linha ("x,y x,y"); <see cref="Parcial"/> = o pedaço que chega à semana corrente (tracejado).</summary>
public sealed record TrechoDaLinha(string Pontos, bool Parcial);

/// <summary>
/// A linha de uma secretária. <see cref="Cor"/>: a vaga da paleta categórica (1 a <see cref="EvolucaoNaTela.CoresDaPaleta"/>,
/// 0 = além da paleta, cinza). <see cref="Marcadores"/>: o ponto da ponta e os pontos ISOLADOS (sem vizinho, não viram
/// linha). <see cref="RotuloY"/>: onde vai o nome na ponta (nulo = sem rótulo direto; a legenda e a tabela dizem).
/// </summary>
public sealed record LinhaDoGrafico(string AgentId, string Nome, int Cor, IReadOnlyList<PontoDaEvolucao> Pontos,
    IReadOnlyList<TrechoDaLinha> Trechos, IReadOnlyList<PontoDaEvolucao> Marcadores, double? RotuloY);

/// <summary>Uma marca do eixo vertical (a grade): o valor, a altura e o texto.</summary>
public sealed record MarcaDoEixo(double Valor, double Y, string Texto);

/// <summary>Uma semana no eixo horizontal: a coluna inteira é o alvo do cursor (a dica com todas as secretárias).</summary>
public sealed record ColunaDaSemana(int Indice, double X, double Inicio, double Largura, string Rotulo, bool MostraRotulo, bool Parcial, string Dica);

/// <summary>Um gráfico pronto para o SVG (coordenadas em unidades do viewBox).</summary>
public sealed record GraficoDaEvolucao(string Titulo, string Descricao, IReadOnlyList<MarcaDoEixo> Marcas, MarcaDoEixo? Referencia,
    IReadOnlyList<ColunaDaSemana> Semanas, IReadOnlyList<LinhaDoGrafico> Linhas, bool TemDado)
{
    public const double Largura = 460, Altura = 210, Esq = 34, Dir = 96, Topo = 18, Base = 178;

    /// <summary>Há nomes na ponta das linhas (≤ 4 secretárias e nenhum rótulo colado no outro).</summary>
    public bool RotulosDiretos => Linhas.Any(l => l.RotuloY is not null);
}

/// <summary>A frase de tendência de uma secretária: a nota e a 1ª resposta, das últimas 4 semanas completas contra as 4 anteriores.</summary>
public sealed record TendenciaDaSecretaria(string AgentId, string Nome, int Cor, string Nota, string TomDaNota, string RotuloDaNota, string Resposta);

/// <summary>Uma linha da tabela de números (a vista sem gráfico): o nome e um texto por semana.</summary>
public sealed record LinhaDaTabela(string Nome, int Cor, IReadOnlyList<string> Valores);

public static class EvolucaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Quantas semanas a tela lê do CRM, sempre: a maior janela que ela mostra (12) — e a tendência precisa de 9 (4 + 4
    /// completas + a corrente). Trocar 8 ↔ 12 recorta o que já foi lido, sem ir ao CRM de novo.
    /// </summary>
    public const int SemanasLidas = 12;

    /// <summary>As janelas que a tela oferece (semanas, contando a corrente); a primeira é o padrão.</summary>
    public static readonly IReadOnlyList<int> Janelas = [8, 12];

    /// <summary>Quantas secretárias têm cor própria (a paleta categórica validada; da quinta em diante, cinza com nome).</summary>
    public const int CoresDaPaleta = 4;

    /// <summary>As semanas comparadas pela tendência, em cada lado.</summary>
    public const int SemanasDaTendencia = 4;

    /// <summary>Menos avaliações que isto num dos lados, a tendência da nota não se afirma.</summary>
    public const int MinimoDeAvaliacoes = 2;

    /// <summary>Menos respostas medidas que isto num dos lados, a tendência da 1ª resposta não se afirma.</summary>
    public const int MinimoDeRespostas = 3;

    /// <summary>Diferença de nota abaixo disto (pontos) é "estável".</summary>
    public const int VariacaoDaNota = 5;

    /// <summary>Distância mínima (unidades do viewBox) entre dois nomes na ponta das linhas; mais perto, sai o rótulo direto.</summary>
    public const double FolgaDosRotulos = 12;

    /// <summary>O prazo da 1ª resposta (minutos úteis): a linha de referência do gráfico.</summary>
    public const int PrazoDaResposta = AuditoriaNaTela.PrazoDaRespostaEmMinutos;

    // ---------------------------------------------------------------- recorte e cores

    /// <summary>As últimas <paramref name="semanas"/> semanas (a corrente inclusa) de cada secretária.</summary>
    public static AppQualityTrend Recorte(AppQualityTrend t, int semanas)
    {
        var n = Math.Clamp(semanas, 1, Math.Max(1, t.Weeks.Length));
        var pular = Math.Max(0, t.Weeks.Length - n);
        return new AppQualityTrend(t.Weeks.Skip(pular).ToArray(),
            t.Agents.Select(a => a with { Weeks = a.Weeks.Skip(Math.Max(0, a.Weeks.Length - n)).ToArray() }).ToArray());
    }

    /// <summary>
    /// A cor SEGUE A PESSOA, nunca a posição: a vaga é dada pela ordem alfabética do NOME (depois o id), a mesma nos dois
    /// gráficos e na frase. Da <see cref="CoresDaPaleta"/>+1ª em diante, 0 (cinza) — nunca uma cor gerada nem repetida.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Cores(IEnumerable<AppQualityTrendAgent> agentes)
        => agentes.OrderBy(a => a.Name, StringComparer.Create(PtBr, true)).ThenBy(a => a.AgentId, StringComparer.Ordinal)
            .Select((a, i) => (a.AgentId, Cor: i < CoresDaPaleta ? i + 1 : 0))
            .GroupBy(x => x.AgentId).ToDictionary(g => g.Key, g => g.First().Cor);

    /// <summary>Quem tem cor vem antes (na ordem da paleta); quem ficou cinza, depois.</summary>
    private static int OrdemDaCor(int cor) => cor == 0 ? int.MaxValue : cor;

    // ---------------------------------------------------------------- os dois gráficos

    /// <summary>"Nota média da gerente (0–100)": escala FIXA de 0 a 100 (a nota tem teto; escala móvel faria 78→82 parecer um salto).</summary>
    public static GraficoDaEvolucao GraficoDaNota(AppQualityTrend t, IReadOnlyDictionary<string, int> cores)
        => Montar(t, cores, "Nota média da gerente (0–100)",
            "A nota média das avaliações da gerente, semana a semana, uma linha por secretária. Os números estão na tabela logo abaixo.",
            w => w.AverageScore is { } n ? (double)n : null, 0, 100, [0, 50, 100], null,
            (w, nome) => $"{nome}: {(w.AverageScore is { } n ? $"{AuditoriaNaTela.NotaFinal(n)} ({AuditoriaNaTela.Avaliacoes(w.Reviewed)})" : "sem avaliação")}");

    /// <summary>"1ª resposta (min úteis)": de 0 a um teto redondo que sempre inclui o prazo de 15 min (a linha de referência).</summary>
    public static GraficoDaEvolucao GraficoDaResposta(AppQualityTrend t, IReadOnlyDictionary<string, int> cores)
    {
        var maior = t.Agents.SelectMany(a => a.Weeks).Select(w => w.FirstReplyMedianMinutes ?? 0).DefaultIfEmpty(0).Max();
        var teto = TetoRedondo(Math.Max(maior, PrazoDaResposta));
        return Montar(t, cores, "1ª resposta (min úteis)",
            "A mediana da primeira resposta em minutos úteis, semana a semana, uma linha por secretária; a linha cinza é o prazo de 15 minutos. Os números estão na tabela logo abaixo.",
            w => w.FirstReplyMedianMinutes, 0, teto, [0, teto / 2, teto], PrazoDaResposta,
            (w, nome) => $"{nome}: {(w.FirstReplyMedianMinutes is { } m ? $"{AuditoriaNaTela.PrimeiraResposta(m)} ({AuditoriaNaTela.NoPrazo(w.RepliedOnTime, w.RepliesMeasured)})" : "sem resposta medida")}");
    }

    /// <summary>O teto do eixo: o primeiro valor "redondo" (15, 20, 30, 45, 60, 90, 120…) que cobre o maior.</summary>
    public static double TetoRedondo(double maior)
    {
        double[] tetos = [10, 15, 20, 30, 45, 60, 90, 120, 180, 240, 360, 480, 720, 960, 1440];
        foreach (var t in tetos) if (maior <= t) return t;
        return Math.Ceiling(maior / 240) * 240;
    }

    private static GraficoDaEvolucao Montar(AppQualityTrend t, IReadOnlyDictionary<string, int> cores, string titulo, string descricao,
        Func<AppQualityWeek, double?> valor, double min, double max, IReadOnlyList<double> marcas, double? referencia,
        Func<AppQualityWeek, string, string> dica)
    {
        const double L = GraficoDaEvolucao.Largura, E = GraficoDaEvolucao.Esq, D = GraficoDaEvolucao.Dir,
            T = GraficoDaEvolucao.Topo, B = GraficoDaEvolucao.Base;
        var n = t.Weeks.Length;
        var passo = n > 1 ? (L - E - D) / (n - 1) : 0;
        double X(int i) => n > 1 ? E + i * passo : E + (L - E - D) / 2;
        double Y(double v) => B - (Math.Clamp(v, min, max) - min) / (max - min) * (B - T);
        var ultima = n - 1;

        var ordem = t.Agents.OrderBy(a => OrdemDaCor(cores.GetValueOrDefault(a.AgentId)))
            .ThenBy(a => a.Name, StringComparer.Create(PtBr, true)).ToList();
        var linhas = ordem.Select(a =>
        {
            var pontos = Enumerable.Range(0, n).Select(i =>
            {
                var v = i < a.Weeks.Length ? valor(a.Weeks[i]) : null;
                return new PontoDaEvolucao(i, X(i), v is { } y ? Y(y) : null, v, i == ultima);
            }).ToList();
            return new LinhaDoGrafico(a.AgentId, a.Name, cores.GetValueOrDefault(a.AgentId), pontos, Trechos(pontos), Marcadores(pontos), null);
        }).ToList();

        // O nome na ponta: só com até 4 linhas e sem dois rótulos colados (empurrar um rótulo o descola da linha dele).
        var pontas = linhas.Select(l => (Linha: l, Y: l.Pontos.LastOrDefault(p => p.Y is not null)?.Y)).Where(x => x.Y is not null).ToList();
        var ys = pontas.Select(p => p.Y!.Value).Order().ToList();
        var folga = ys.Zip(ys.Skip(1), (a, b) => b - a).DefaultIfEmpty(double.MaxValue).Min();
        if (linhas.Count <= CoresDaPaleta && pontas.Count > 0 && folga >= FolgaDosRotulos)
            linhas = linhas.Select(l => pontas.FirstOrDefault(p => p.Linha == l) is { Linha: not null } p ? l with { RotuloY = p.Y } : l).ToList();

        var semanas = Enumerable.Range(0, n).Select(i =>
        {
            var inicio = n > 1 ? X(i) - passo / 2 : E;
            var largura = n > 1 ? passo : L - E - D;
            var data = t.Weeks[i].ToString("dd/MM", PtBr);
            var linhasDaDica = ordem.Select(a => i < a.Weeks.Length ? dica(a.Weeks[i], a.Name) : $"{a.Name}: sem dado");
            var cabeca = i == ultima ? $"Semana de {data} (parcial, ainda em andamento)" : $"Semana de {data}";
            return new ColunaDaSemana(i, X(i), inicio, largura, data, MostraRotulo(i, n), i == ultima,
                string.Join("\n", new[] { cabeca }.Concat(linhasDaDica)));
        }).ToList();

        var marcasDoEixo = marcas.Select(m => new MarcaDoEixo(m, Y(m), m.ToString("0.#", PtBr))).ToList();
        MarcaDoEixo? linhaDeReferencia = referencia is { } r ? new MarcaDoEixo(r, Y(r), $"prazo {r.ToString("0", PtBr)} min") : null;
        return new GraficoDaEvolucao(titulo, descricao, marcasDoEixo, linhaDeReferencia, semanas, linhas,
            linhas.Any(l => l.Pontos.Any(p => p.Y is not null)));
    }

    /// <summary>
    /// Os trechos CONTÍNUOS: semana sem dado separa trechos (buraco, nunca uma descida ao zero). O pedaço que liga a última
    /// semana completa à corrente sai à parte, como <see cref="TrechoDaLinha.Parcial"/> (tracejado).
    /// </summary>
    public static IReadOnlyList<TrechoDaLinha> Trechos(IReadOnlyList<PontoDaEvolucao> pontos)
    {
        var trechos = new List<TrechoDaLinha>();
        var atual = new List<PontoDaEvolucao>();
        void Fechar()
        {
            if (atual.Count >= 2) trechos.Add(new TrechoDaLinha(string.Join(" ", atual.Select(Par)), false));
            atual.Clear();
        }
        foreach (var p in pontos)
        {
            if (p.Parcial) break;
            if (p.Y is null) Fechar(); else atual.Add(p);
        }
        Fechar();
        if (pontos.Count >= 2 && pontos[^1] is { Parcial: true, Y: not null } fim && pontos[^2].Y is not null)
            trechos.Add(new TrechoDaLinha($"{Par(pontos[^2])} {Par(fim)}", true));
        return trechos;
    }

    /// <summary>O ponto da ponta (o último com dado) e os pontos sem vizinho com dado — esses não viram linha e sumiriam sem o marcador.</summary>
    public static IReadOnlyList<PontoDaEvolucao> Marcadores(IReadOnlyList<PontoDaEvolucao> pontos)
    {
        var com = pontos.Where(p => p.Y is not null).ToList();
        if (com.Count == 0) return [];
        bool Tem(int i) => i >= 0 && i < pontos.Count && pontos[i].Y is not null;
        var marcas = com.Where(p => !Tem(p.Indice - 1) && !Tem(p.Indice + 1)).ToList();
        if (!marcas.Contains(com[^1])) marcas.Add(com[^1]);
        return marcas.OrderBy(p => p.Indice).ToList();
    }

    /// <summary>Até 8 semanas, todos os rótulos; com mais, um sim um não — contando da corrente para trás, que sempre aparece.</summary>
    public static bool MostraRotulo(int i, int total) => total <= 8 || (total - 1 - i) % 2 == 0;

    private static string Par(PontoDaEvolucao p) => $"{N(p.X)},{N(p.Y!.Value)}";

    /// <summary>Número no SVG: invariante ("110.5"; "110,5" é atributo inválido).</summary>
    public static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- a tendência

    /// <summary>
    /// A frase de cada secretária: as últimas <see cref="SemanasDaTendencia"/> semanas COMPLETAS contra as 4 anteriores — a
    /// corrente, parcial, fica de fora. A nota de cada lado é a média ponderada pelo número de avaliações; a 1ª resposta, a
    /// média das medianas ponderada pelas respostas medidas (o tempo típico, como a Mesa da direção).
    /// </summary>
    public static IReadOnlyList<TendenciaDaSecretaria> Tendencias(AppQualityTrend t, IReadOnlyDictionary<string, int> cores)
        => t.Agents.OrderBy(a => OrdemDaCor(cores.GetValueOrDefault(a.AgentId)))
            .ThenBy(a => a.Name, StringComparer.Create(PtBr, true))
            .Select(a => Tendencia(a, cores.GetValueOrDefault(a.AgentId))).ToList();

    public static TendenciaDaSecretaria Tendencia(AppQualityTrendAgent a, int cor = 0)
    {
        var completas = a.Weeks.Length > 0 ? a.Weeks[..^1] : [];
        var recentes = completas.TakeLast(SemanasDaTendencia).ToArray();
        var anteriores = completas.SkipLast(recentes.Length).TakeLast(SemanasDaTendencia).ToArray();
        var (nota, tom, rotulo) = FraseDaNota(recentes, anteriores);
        return new TendenciaDaSecretaria(a.AgentId, a.Name, cor, nota, tom, rotulo, FraseDaResposta(recentes, anteriores));
    }

    private static (decimal? Media, int Quantas) NotaDe(IEnumerable<AppQualityWeek> semanas)
    {
        var com = semanas.Where(w => w.AverageScore is not null && w.Reviewed > 0).ToList();
        var quantas = com.Sum(w => w.Reviewed);
        return quantas == 0 ? (null, 0) : (com.Sum(w => w.AverageScore!.Value * w.Reviewed) / quantas, quantas);
    }

    private static (double? Tipica, int Medidas) RespostaDe(IEnumerable<AppQualityWeek> semanas)
    {
        var com = semanas.Where(w => w.FirstReplyMedianMinutes is not null && w.RepliesMeasured > 0).ToList();
        var medidas = com.Sum(w => w.RepliesMeasured);
        return medidas == 0 ? (null, 0) : (com.Sum(w => w.FirstReplyMedianMinutes!.Value * w.RepliesMeasured) / medidas, medidas);
    }

    private static (string Frase, string Tom, string Rotulo) FraseDaNota(AppQualityWeek[] recentes, AppQualityWeek[] anteriores)
    {
        var (agora, nAgora) = NotaDe(recentes);
        var (antes, nAntes) = NotaDe(anteriores);
        if (agora is null || antes is null || nAgora < MinimoDeAvaliacoes || nAntes < MinimoDeAvaliacoes)
            return ("Nota: sem avaliações suficientes para comparar.", "neutro", "sem base");
        var a = (int)Math.Round(agora.Value, MidpointRounding.AwayFromZero);
        var b = (int)Math.Round(antes.Value, MidpointRounding.AwayFromZero);
        var diferenca = a - b;
        if (Math.Abs(diferenca) < VariacaoDaNota)
            return ($"Nota estável ({a} nas últimas {SemanasDaTendencia} semanas).", "neutro", "estável");
        var pontos = Math.Abs(diferenca) == 1 ? "1 ponto" : $"{Math.Abs(diferenca)} pontos";
        return diferenca > 0
            ? ($"Nota subiu {pontos} nas últimas {SemanasDaTendencia} semanas (de {b} para {a}).", "sucesso", "subiu")
            : ($"Nota caiu {pontos} nas últimas {SemanasDaTendencia} semanas (de {b} para {a}).", "perigo", "caiu");
    }

    private static string FraseDaResposta(AppQualityWeek[] recentes, AppQualityWeek[] anteriores)
    {
        var (agora, nAgora) = RespostaDe(recentes);
        var (antes, nAntes) = RespostaDe(anteriores);
        if (agora is null || antes is null || nAgora < MinimoDeRespostas || nAntes < MinimoDeRespostas)
            return "1ª resposta: poucas respostas medidas para comparar.";
        var a = AuditoriaNaTela.PrimeiraResposta(agora);
        var b = AuditoriaNaTela.PrimeiraResposta(antes);
        var diferenca = agora.Value - antes.Value;
        // 20% e pelo menos 1 minuto: de 4 para 5 min é ruído; de 6 para 12 é outra secretária.
        if (Math.Abs(diferenca) < 1 || Math.Abs(diferenca) < 0.2 * antes.Value)
            return $"1ª resposta estável (cerca de {a}).";
        return diferenca > 0 ? $"1ª resposta piorou: de {b} para {a}." : $"1ª resposta melhorou: de {b} para {a}.";
    }

    // ---------------------------------------------------------------- a tabela (a vista sem gráfico)

    /// <summary>Os rótulos das colunas da tabela: "dd/MM", e a corrente com "(parcial)".</summary>
    public static IReadOnlyList<string> CabecalhoDaTabela(AppQualityTrend t)
        => t.Weeks.Select((d, i) => d.ToString("dd/MM", PtBr) + (i == t.Weeks.Length - 1 ? " (parcial)" : "")).ToList();

    public static IReadOnlyList<LinhaDaTabela> TabelaDaNota(AppQualityTrend t, IReadOnlyDictionary<string, int> cores)
        => Tabela(t, cores, w => w.AverageScore is { } n ? $"{AuditoriaNaTela.NotaFinal(n)} ({w.Reviewed})" : "—");

    public static IReadOnlyList<LinhaDaTabela> TabelaDaResposta(AppQualityTrend t, IReadOnlyDictionary<string, int> cores)
        => Tabela(t, cores, w => w.FirstReplyMedianMinutes is { } m ? AuditoriaNaTela.PrimeiraResposta(m) : "—");

    private static IReadOnlyList<LinhaDaTabela> Tabela(AppQualityTrend t, IReadOnlyDictionary<string, int> cores, Func<AppQualityWeek, string> texto)
        => t.Agents.OrderBy(a => OrdemDaCor(cores.GetValueOrDefault(a.AgentId)))
            .ThenBy(a => a.Name, StringComparer.Create(PtBr, true))
            .Select(a => new LinhaDaTabela(a.Name, cores.GetValueOrDefault(a.AgentId), a.Weeks.Select(texto).ToList())).ToList();
}
