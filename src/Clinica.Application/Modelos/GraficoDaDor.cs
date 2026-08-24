using System.Globalization;
using System.Text;

namespace Clinica.Application.Modelos;

/// <summary>Uma sessão na curva: a data e a dor no fim dela.</summary>
public sealed record MedidaDaDor(DateOnly Data, int Antes, int Depois);

/// <summary>
/// A CURVA DA DOR do relatório de evolução (parcela 79).
///
/// O corpo do relatório já AFIRMA a variação em uma frase ("de 8/10 para 2/10, com alívio
/// médio de 3,4 pontos"). A curva não repete a frase: ela mostra o que a frase não cabe —
/// se a queda foi contínua, se houve recaída no meio, se o alívio de cada sessão se sustenta
/// até a seguinte. É o que o convênio olha para autorizar a continuidade, e é o que o
/// paciente entende sem ninguém explicar.
///
/// ⚠️ Mora aqui, e não na ViewModel nem dentro do PDF, pela lição da grade da semana
/// (parcela 69): <b>o que decide um desenho precisa morar onde o `dotnet test` alcança</b>.
/// A camada de tela do WPF não compila no projeto de teste, e o texto dentro de um PDF
/// gerado pelo QuestPDF não se lê de volta (a fonte vai em subconjunto, como IDs de glifo).
///
/// Sem biblioteca de gráfico, como o resto do projeto: são duas polilinhas e uma régua.
/// </summary>
public static class GraficoDaDor
{
    public const double Largura = 520;
    public const double Altura = 150;

    /// <summary>
    /// Duas medidas, e não uma: com UMA sessão não há curva nenhuma — é linha de base, e
    /// desenhar um ponto solto num eixo faria o papel prometer uma evolução que ele não
    /// tem. É a mesma regra da escala aplicada uma vez só ("aplicação única, sem evolução
    /// a relatar").
    /// </summary>
    private const int MinimoDeSessoes = 2;

    public static bool VaiDesenhar(IReadOnlyList<MedidaDaDor> medidas)
        => medidas.Count >= MinimoDeSessoes;

    /// <summary>
    /// A curva em SVG. Duas linhas: a dor ANTES de cada sessão e a dor DEPOIS.
    ///
    /// As duas juntas são o que responde a pergunta do tratamento — a de baixo mostra que a
    /// sessão alivia, e a distância entre elas mostra se o alívio DURA até a próxima. Só a
    /// de depois diria que o paciente melhorou; só a de antes esconderia o efeito da sessão.
    /// </summary>
    public static string Svg(
        IReadOnlyList<MedidaDaDor> medidas,
        string corAntes = "#B45309", string corDepois = "#1D4E89", string grade = "#E2E8F0")
    {
        const double margemEsq = 26, margemDir = 10, margemTopo = 10, margemBase = 22;
        var largura = Largura - margemEsq - margemDir;
        var altura = Altura - margemTopo - margemBase;

        // ⚠️ O eixo vai de 0 a 10 SEMPRE, porque é a escala publicada da EVA — nunca do
        // menor ao maior valor medido. Escala que se ajusta aos dados transforma uma queda
        // de 8 para 7 num despencar visual, e é a regra que o projeto já aplica aos
        // gráficos da suíte desde que eles existem.
        double Y(int dor) => margemTopo + altura - (dor / 10.0 * altura);
        double X(int i) => medidas.Count == 1
            ? margemEsq + largura / 2
            : margemEsq + i * (largura / (medidas.Count - 1));

        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {N(Largura)} {N(Altura)}\" "
                   + $"width=\"{N(Largura)}\" height=\"{N(Altura)}\">");

        // Régua de 0, 5 e 10: três linhas bastam para situar. Onze linhas seriam uma grade
        // que compete com a curva pela atenção de quem lê.
        foreach (var marca in new[] { 0, 5, 10 })
        {
            svg.Append($"<line x1=\"{N(margemEsq)}\" y1=\"{N(Y(marca))}\" "
                       + $"x2=\"{N(Largura - margemDir)}\" y2=\"{N(Y(marca))}\" "
                       + $"stroke=\"{grade}\" stroke-width=\"1\"/>");
            svg.Append($"<text x=\"{N(margemEsq - 6)}\" y=\"{N(Y(marca) + 3)}\" text-anchor=\"end\" "
                       + $"font-family=\"Helvetica\" font-size=\"8\" fill=\"#64748B\">{marca}</text>");
        }

        svg.Append(Linha(medidas.Select((m, i) => (X(i), Y(m.Antes))), corAntes, tracejada: true));
        svg.Append(Linha(medidas.Select((m, i) => (X(i), Y(m.Depois))), corDepois, tracejada: false));

        for (var i = 0; i < medidas.Count; i++)
        {
            svg.Append($"<circle cx=\"{N(X(i))}\" cy=\"{N(Y(medidas[i].Antes))}\" r=\"2.5\" fill=\"{corAntes}\"/>");
            svg.Append($"<circle cx=\"{N(X(i))}\" cy=\"{N(Y(medidas[i].Depois))}\" r=\"3\" fill=\"{corDepois}\"/>");
        }

        // A data só da PRIMEIRA e da ÚLTIMA. Numa série de quarenta sessões, quarenta datas
        // de 8 pt viram uma tarja preta — e o que se pergunta olhando a curva é "de quando
        // até quando", que duas respondem.
        svg.Append($"<text x=\"{N(X(0))}\" y=\"{N(Altura - 6)}\" text-anchor=\"start\" "
                   + $"font-family=\"Helvetica\" font-size=\"8\" fill=\"#64748B\">"
                   + $"{medidas[0].Data:dd/MM/yy}</text>");
        if (medidas.Count > 1)
            svg.Append($"<text x=\"{N(X(medidas.Count - 1))}\" y=\"{N(Altura - 6)}\" text-anchor=\"end\" "
                       + $"font-family=\"Helvetica\" font-size=\"8\" fill=\"#64748B\">"
                       + $"{medidas[^1].Data:dd/MM/yy}</text>");

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string Linha(IEnumerable<(double X, double Y)> pontos, string cor, bool tracejada)
        => $"<polyline points=\"{string.Join(" ", pontos.Select(p => $"{N(p.X)},{N(p.Y)}"))}\" "
           + $"fill=\"none\" stroke=\"{cor}\" stroke-width=\"1.8\" stroke-linejoin=\"round\""
           + (tracejada ? " stroke-dasharray=\"4 3\"" : string.Empty) + "/>";

    /// <summary>Invariante: vírgula decimal num atributo de SVG é atributo inválido.</summary>
    private static string N(double valor)
        => valor.ToString("0.##", CultureInfo.InvariantCulture);
}
