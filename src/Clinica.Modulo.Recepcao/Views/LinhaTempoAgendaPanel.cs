using System.Windows;
using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>Posiciona a projeção visual pela hora e duração sem alterar agendamentos.</summary>
public sealed class LinhaTempoAgendaPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var largura = double.IsInfinity(availableSize.Width)
            ? (ActualWidth > 0 ? ActualWidth : 160) : availableSize.Width;
        var altura = 0d;
        foreach (UIElement filho in InternalChildren)
        {
            if (filho is not FrameworkElement { DataContext: BlocoAgendaVisual bloco }) continue;
            filho.Measure(new Size(Math.Max(0, largura / bloco.Raias - 4), bloco.Altura));
            altura = Math.Max(altura, bloco.Topo + bloco.Altura);
        }
        // A largura medida não pode prender a coluna ao tamanho da janela anterior.
        // O painel pai distribui o espaço; o conteúdo é medido outra vez ao redimensionar.
        return new Size(double.IsInfinity(availableSize.Width) ? 160 : largura, altura);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement filho in InternalChildren)
        {
            if (filho is not FrameworkElement { DataContext: BlocoAgendaVisual bloco }) continue;
            var largura = finalSize.Width / bloco.Raias;
            filho.Arrange(new Rect(largura * bloco.Raia + 2, bloco.Topo,
                Math.Max(0, largura - 4), bloco.Altura));
        }
        return finalSize;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged) InvalidateMeasure();
    }
}
