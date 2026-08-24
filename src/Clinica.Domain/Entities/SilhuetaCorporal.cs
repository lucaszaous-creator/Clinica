using System.Globalization;
using System.Text;

namespace Clinica.Domain.Entities;

/// <summary>Uma peça do desenho da silhueta.</summary>
public abstract record FormaSilhueta;

/// <summary>Cabeça, ombros, pés — o que é redondo no corpo.</summary>
public sealed record ElipseSilhueta(double Cx, double Cy, double Rx, double Ry) : FormaSilhueta;

/// <summary>Tronco, braços, pernas — retângulo de canto arredondado.</summary>
public sealed record RetanguloSilhueta(
    double X, double Y, double Largura, double Altura, double Raio) : FormaSilhueta;

/// <summary>
/// A SILHUETA DO MAPA CORPORAL — UMA definição, lida pela TELA e pelo PDF (parcela 79).
///
/// ⚠️ Ela mora aqui, e não no XAML de onde veio, pela razão que o comentário do próprio
/// <c>MapaCorporalControl</c> já escrevia quando o componente subiu para o shell na parcela
/// 36: <i>"copiar a figura teria criado duas versões do mesmo desenho — e a segunda
/// correção de silhueta já sairia divergente"</i>. Ao pôr o mapa no papel, o desenho ia
/// ganhar a segunda cópia — desta vez atravessando camadas, que é onde ninguém as lê lado
/// a lado. Divergir aqui não estoura nada: produz um papel em que a agulha está num lugar
/// do corpo e a tela mostra outro.
///
/// As coordenadas são ABSOLUTAS dentro de <see cref="Largura"/> × <see cref="Altura"/>, e
/// o ponto do prontuário é NORMALIZADO (0 a 1) sobre elas — é a mesma conta dos dois lados,
/// e é por isso que a figura pode ser desenhada em 220 px na tela e em 150 pt no papel sem
/// espalhar as marcações.
/// </summary>
public static class SilhuetaCorporal
{
    public const double Largura = 220;
    public const double Altura = 460;

    /// <summary>
    /// A linha tracejada da coluna, que é o que diferencia a figura de COSTAS da de frente.
    /// Sem ela as duas são o mesmo desenho, e quem lê o papel não sabe qual é qual — o
    /// rótulo acima resolve para quem procura; a coluna resolve para quem só olha.
    /// </summary>
    public const double ColunaX = 110;
    public const double ColunaTopo = 86;
    public const double ColunaBase = 262;

    /// <summary>
    /// As doze peças, na ordem em que se desenham. São primitivas (elipse e retângulo
    /// arredondado) de propósito: o WPF monta um <c>GeometryGroup</c> e o PDF monta um SVG
    /// a partir da MESMA lista, sem ninguém precisar traduzir dados de caminho à mão — que
    /// é onde um parser de ASN.1 escrito à mão já custou caro a este projeto.
    /// </summary>
    public static IReadOnlyList<FormaSilhueta> Formas { get; } = new FormaSilhueta[]
    {
        new ElipseSilhueta(110, 40, 27, 32),          // cabeça
        new RetanguloSilhueta(99, 66, 22, 18, 0),     // pescoço
        new RetanguloSilhueta(58, 80, 104, 120, 22),  // tórax
        new RetanguloSilhueta(66, 192, 88, 76, 16),   // abdome
        new RetanguloSilhueta(36, 92, 22, 150, 11),   // braço direito
        new RetanguloSilhueta(162, 92, 22, 150, 11),  // braço esquerdo
        new ElipseSilhueta(47, 252, 12, 14),          // mão direita
        new ElipseSilhueta(173, 252, 12, 14),         // mão esquerda
        new RetanguloSilhueta(68, 260, 38, 172, 16),  // perna direita
        new RetanguloSilhueta(114, 260, 38, 172, 16), // perna esquerda
        new ElipseSilhueta(87, 436, 19, 12),          // pé direito
        new ElipseSilhueta(133, 436, 19, 12)          // pé esquerdo
    };

    /// <summary>
    /// A figura em SVG, com os pontos por cima — é o que o PDF desenha.
    /// </summary>
    /// <param name="pontos">
    /// Os pontos DAQUELA face. Vazio desenha a silhueta limpa, que é legítimo: a sessão em
    /// que só se marcou nas costas tem a frente sem marcação, e mostrá-la vazia é dizer
    /// isso — esconder a face faria o leitor supor que ela não foi avaliada.
    /// </param>
    /// <param name="coluna">Desenha a linha da coluna (a face de costas).</param>
    public static string Svg(
        IEnumerable<PontoDesenhado> pontos, bool coluna,
        string preenchimento = "#E8EEF7", string traco = "#94A3B8", string marcador = "#1D4E89")
    {
        var svg = new StringBuilder();
        svg.Append(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {N(Largura)} {N(Altura)}\" "
            + $"width=\"{N(Largura)}\" height=\"{N(Altura)}\">");

        foreach (var forma in Formas)
            svg.Append(forma switch
            {
                ElipseSilhueta e =>
                    $"<ellipse cx=\"{N(e.Cx)}\" cy=\"{N(e.Cy)}\" rx=\"{N(e.Rx)}\" ry=\"{N(e.Ry)}\" "
                    + $"fill=\"{preenchimento}\" stroke=\"{traco}\" stroke-width=\"1\"/>",
                RetanguloSilhueta r =>
                    $"<rect x=\"{N(r.X)}\" y=\"{N(r.Y)}\" width=\"{N(r.Largura)}\" height=\"{N(r.Altura)}\" "
                    + $"rx=\"{N(r.Raio)}\" ry=\"{N(r.Raio)}\" "
                    + $"fill=\"{preenchimento}\" stroke=\"{traco}\" stroke-width=\"1\"/>",
                _ => string.Empty
            });

        if (coluna)
            svg.Append(
                $"<line x1=\"{N(ColunaX)}\" y1=\"{N(ColunaTopo)}\" x2=\"{N(ColunaX)}\" y2=\"{N(ColunaBase)}\" "
                + $"stroke=\"{traco}\" stroke-width=\"1\" stroke-dasharray=\"3 3\"/>");

        // A bolinha NUMERADA, igual à da tela: é o número que liga a marcação à legenda,
        // onde estão a técnica e o nome do ponto. Sem ele o papel mostra onde e não diz o
        // quê — e "onde" sozinho não sustenta o que foi feito na sessão.
        foreach (var p in pontos)
        {
            var cx = p.X * Largura;
            var cy = p.Y * Altura;
            svg.Append(
                $"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"10\" fill=\"{marcador}\" "
                + "stroke=\"#FFFFFF\" stroke-width=\"1.5\"/>");
            svg.Append(
                $"<text x=\"{N(cx)}\" y=\"{N(cy + 3.6)}\" font-family=\"Helvetica\" "
                + "font-size=\"10\" font-weight=\"bold\" fill=\"#FFFFFF\" "
                + $"text-anchor=\"middle\">{p.Numero}</text>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>
    /// ⚠️ Cultura INVARIANTE, e não é preciosismo: em pt-BR o separador decimal é a
    /// vírgula, e <c>cx="110,5"</c> num SVG é atributo inválido — o círculo some, ou a
    /// figura inteira não desenha. É a mesma armadilha do OFX e do <c>ParametrosService</c>:
    /// número que o outro lado precisa ler de volta como número escreve-se invariante.
    /// </summary>
    private static string N(double valor)
        => valor.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>Um ponto já pronto para desenhar: fração da figura e o número da legenda.</summary>
public sealed record PontoDesenhado(double X, double Y, int Numero);
