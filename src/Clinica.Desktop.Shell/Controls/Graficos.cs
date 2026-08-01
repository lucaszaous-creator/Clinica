using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Um ponto de uma série: o rótulo do eixo e o valor. Valor NULO é buraco na série —
/// "não medido", que é diferente de zero. A linha se interrompe ali em vez de descer até
/// o chão e inventar uma queda que não houve.
/// </summary>
public sealed record PontoGrafico(string Rotulo, double? Valor);

/// <summary>
/// Gráfico de linha desenhado com as primitivas do WPF e os tokens da marca.
///
/// Sem biblioteca de terceiros de propósito: os quatro apps se auto-atualizam por
/// Velopack, e uma dependência de UI nova é risco desproporcional a uma linha e alguns
/// círculos. O que a suíte precisa aqui é evolução mensal e curva de dor — não é
/// visualização científica.
///
/// Herda de <see cref="FrameworkElement"/> e desenha no <see cref="OnRender"/>: um
/// controle com template exigiria um ControlTemplate por gráfico, e não há o que
/// customizar num traço.
/// </summary>
public sealed class GraficoLinha : FrameworkElement
{
    private const double Margem = 8;
    private const double RaioPonto = 3;

    /// <summary>
    /// Quanto tempo a linha leva para ser traçada, em ms. Muito acima do teto das
    /// microinterações porque não é microinteração: aqui o movimento acompanha a LEITURA
    /// da série da esquerda para a direita, que é a ordem do tempo. Instantâneo, o
    /// gráfico chega pronto e o olho tem de procurar sozinho onde a curva começa.
    /// </summary>
    public const int DuracaoTracoMs = 650;

    public static readonly DependencyProperty PontosProperty =
        DependencyProperty.Register(
            nameof(Pontos), typeof(IEnumerable<PontoGrafico>), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsRender, AoMudarPontos));

    public IEnumerable<PontoGrafico>? Pontos
    {
        get => (IEnumerable<PontoGrafico>?)GetValue(PontosProperty);
        set => SetValue(PontosProperty, value);
    }

    public static readonly DependencyProperty ProgressoProperty =
        DependencyProperty.Register(
            nameof(Progresso), typeof(double), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Quanto da série já foi traçada, de 0 a 1. Existe para o traço poder ser animado;
    /// o padrão é 1 (desenhado inteiro), então quem não pede animação não muda de
    /// comportamento.
    /// </summary>
    public double Progresso
    {
        get => (double)GetValue(ProgressoProperty);
        set => SetValue(ProgressoProperty, value);
    }

    public static readonly DependencyProperty AnimarProperty =
        DependencyProperty.Register(
            nameof(Animar), typeof(bool), typeof(GraficoLinha), new PropertyMetadata(false));

    /// <summary>
    /// Traçar a linha quando a série chega, em vez de mostrá-la pronta.
    ///
    /// A animação mora no controle, e não num gatilho de <c>Loaded</c> na tela, porque a
    /// tela carrega ANTES dos dados: o gatilho rodaria com a série vazia e, quando os
    /// pontos chegassem, já estaria tudo desenhado. Quem sabe a hora é quem recebe a série.
    /// </summary>
    public bool Animar
    {
        get => (bool)GetValue(AnimarProperty);
        set => SetValue(AnimarProperty, value);
    }

    public static readonly DependencyProperty MinimoProperty =
        DependencyProperty.Register(
            nameof(Minimo), typeof(double?), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Piso FIXO do eixo. Null = calculado dos dados (o padrão).
    ///
    /// Serve para escala de significado conhecido, e a EVA é o caso exato: ela vai de 0 a
    /// 10 sempre. Calculada dos dados, uma dor que foi de 8 a 3 ocuparia o gráfico inteiro
    /// e pareceria a mesma coisa que uma dor que foi de 10 a 0 — e é justamente essa
    /// diferença que o paciente está olhando.
    /// </summary>
    public double? Minimo
    {
        get => (double?)GetValue(MinimoProperty);
        set => SetValue(MinimoProperty, value);
    }

    public static readonly DependencyProperty MaximoProperty =
        DependencyProperty.Register(
            nameof(Maximo), typeof(double?), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Teto FIXO do eixo. Null = calculado dos dados. Ver <see cref="Minimo"/>.</summary>
    public double? Maximo
    {
        get => (double?)GetValue(MaximoProperty);
        set => SetValue(MaximoProperty, value);
    }

    private static void AoMudarPontos(DependencyObject alvo, DependencyPropertyChangedEventArgs e)
    {
        if (alvo is not GraficoLinha grafico || !grafico.Animar) return;

        // A preferência de animação do sistema vale aqui como vale na entrada das seções:
        // quem desligou movimento no Windows recebe o gráfico pronto.
        if (!SystemParameters.ClientAreaAnimation)
        {
            grafico.Progresso = 1;
            return;
        }

        var traco = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(DuracaoTracoMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        traco.Completed += (_, _) =>
        {
            // Solta a propriedade: animada para sempre, ela ignoraria em silêncio
            // qualquer valor escrito depois.
            grafico.BeginAnimation(ProgressoProperty, null);
            grafico.Progresso = 1;
        };

        grafico.BeginAnimation(ProgressoProperty, traco);
    }

    public static readonly DependencyProperty CorProperty =
        DependencyProperty.Register(
            nameof(Cor), typeof(Brush), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(
                Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Cor do traço. A tela passa o token: acento para volume, verde/vermelho para tendência.</summary>
    public Brush Cor
    {
        get => (Brush)GetValue(CorProperty);
        set => SetValue(CorProperty, value);
    }

    public static readonly DependencyProperty ComecaDoZeroProperty =
        DependencyProperty.Register(
            nameof(ComecaDoZero), typeof(bool), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Eixo ancorado no zero. É o padrão porque escala que começa no menor valor
    /// transforma variação de 2% num despencar visual — o gráfico mentiria sem uma linha
    /// de código errada. Desligue só quando a leitura for de faixa estreita e conhecida
    /// (a EVA, que vai de 0 a 10).
    /// </summary>
    public bool ComecaDoZero
    {
        get => (bool)GetValue(ComecaDoZeroProperty);
        set => SetValue(ComecaDoZeroProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var pontos = Pontos?.ToList() ?? [];
        var largura = ActualWidth - 2 * Margem;
        var altura = ActualHeight - 2 * Margem;
        if (largura <= 0 || altura <= 0) return;

        var comValor = pontos.Where(p => p.Valor is not null).ToList();
        if (comValor.Count == 0)
        {
            DesenharVazio(dc);
            return;
        }

        // Piso e teto fixos ganham do cálculo: numa escala de significado conhecido (a
        // EVA de 0 a 10) é a escala que dá sentido ao traço, não os dados.
        var maximo = Maximo ?? comValor.Max(p => p.Valor!.Value);
        var minimo = Minimo ?? (ComecaDoZero
            ? Math.Min(0, comValor.Min(p => p.Valor!.Value))
            : comValor.Min(p => p.Valor!.Value));

        // Série achatada (todos os valores iguais) desenharia uma divisão por zero; nesse
        // caso a linha vai no meio da área, que é a leitura honesta de "não variou".
        var faixa = maximo - minimo;
        if (faixa <= 0) faixa = Math.Abs(maximo) > 0 ? Math.Abs(maximo) : 1;

        double X(int i) => pontos.Count == 1
            ? Margem + largura / 2
            : Margem + largura * i / (pontos.Count - 1);

        double Y(double v) => Margem + altura - altura * (v - minimo) / faixa;

        var caneta = new Pen(Cor, 2) { LineJoin = PenLineJoin.Round };
        caneta.Freeze();

        // Até onde a linha já foi traçada, medido em SEGMENTOS. Com Progresso em 1 (o
        // padrão) vale a série inteira, e o desenho é o mesmo de sempre.
        var avanco = Math.Clamp(Progresso, 0, 1) * (pontos.Count - 1);

        // Um segmento por par consecutivo COM valor: assim o buraco da série vira um
        // vão no traço em vez de uma queda inventada até o eixo.
        for (var i = 1; i < pontos.Count; i++)
        {
            if (pontos[i - 1].Valor is not { } anterior || pontos[i].Valor is not { } atual)
                continue;

            // Quanto DESTE segmento já foi traçado: 0 (nem começou), 1 (inteiro) ou a
            // fração, que é o que faz a linha crescer em vez de aparecer por saltos.
            var fatia = Math.Clamp(avanco - (i - 1), 0, 1);
            if (fatia <= 0) continue;

            var inicio = new Point(X(i - 1), Y(anterior));
            var fim = new Point(X(i), Y(atual));
            if (fatia < 1)
                fim = new Point(
                    inicio.X + (fim.X - inicio.X) * fatia,
                    inicio.Y + (fim.Y - inicio.Y) * fatia);

            dc.DrawLine(caneta, inicio, fim);
        }

        for (var i = 0; i < pontos.Count; i++)
        {
            if (pontos[i].Valor is not { } v) continue;
            // O ponto só aparece quando a linha chega nele — senão a bolinha do fim da
            // série ficaria à espera do traço, entregando o final antes do caminho.
            if (i > avanco) continue;
            dc.DrawEllipse(Cor, null, new Point(X(i), Y(v)), RaioPonto, RaioPonto);
        }
    }

    private void DesenharVazio(DrawingContext dc)
    {
        var texto = new FormattedText(
            "sem dados no período",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12,
            new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(texto, new Point(
            Math.Max(0, (ActualWidth - texto.Width) / 2),
            Math.Max(0, (ActualHeight - texto.Height) / 2)));
    }
}
