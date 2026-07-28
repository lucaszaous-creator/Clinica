using System.Globalization;
using System.Windows;
using System.Windows.Media;

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

    public static readonly DependencyProperty PontosProperty =
        DependencyProperty.Register(
            nameof(Pontos), typeof(IEnumerable<PontoGrafico>), typeof(GraficoLinha),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<PontoGrafico>? Pontos
    {
        get => (IEnumerable<PontoGrafico>?)GetValue(PontosProperty);
        set => SetValue(PontosProperty, value);
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

        var maximo = comValor.Max(p => p.Valor!.Value);
        var minimo = ComecaDoZero ? Math.Min(0, comValor.Min(p => p.Valor!.Value))
                                  : comValor.Min(p => p.Valor!.Value);

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

        // Um segmento por par consecutivo COM valor: assim o buraco da série vira um
        // vão no traço em vez de uma queda inventada até o eixo.
        for (var i = 1; i < pontos.Count; i++)
        {
            if (pontos[i - 1].Valor is not { } anterior || pontos[i].Valor is not { } atual)
                continue;
            dc.DrawLine(caneta, new Point(X(i - 1), Y(anterior)), new Point(X(i), Y(atual)));
        }

        for (var i = 0; i < pontos.Count; i++)
        {
            if (pontos[i].Valor is not { } v) continue;
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
