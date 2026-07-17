using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Estado vazio padrão do design system (ícone + título + descrição + ação opcional).
/// Template em Styles/Componentes/Feedback.xaml. Uso típico sobreposto a uma DataGrid
/// com Visibility controlada por Items.Count == 0.
/// </summary>
public class EmptyState : Control
{
    static EmptyState()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EmptyState),
            new FrameworkPropertyMetadata(typeof(EmptyState)));
    }

    public static readonly DependencyProperty GlifoProperty =
        DependencyProperty.Register(nameof(Glifo), typeof(string), typeof(EmptyState),
            new PropertyMetadata(""));

    /// <summary>Glifo Segoe Fluent/MDL2 exibido acima do título.</summary>
    public string Glifo
    {
        get => (string)GetValue(GlifoProperty);
        set => SetValue(GlifoProperty, value);
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EmptyState),
            new PropertyMetadata("Nada por aqui"));

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public static readonly DependencyProperty DescricaoProperty =
        DependencyProperty.Register(nameof(Descricao), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public string Descricao
    {
        get => (string)GetValue(DescricaoProperty);
        set => SetValue(DescricaoProperty, value);
    }

    public static readonly DependencyProperty TextoAcaoProperty =
        DependencyProperty.Register(nameof(TextoAcao), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public string TextoAcao
    {
        get => (string)GetValue(TextoAcaoProperty);
        set => SetValue(TextoAcaoProperty, value);
    }

    public static readonly DependencyProperty AcaoCommandProperty =
        DependencyProperty.Register(nameof(AcaoCommand), typeof(ICommand), typeof(EmptyState),
            new PropertyMetadata(null));

    public ICommand? AcaoCommand
    {
        get => (ICommand?)GetValue(AcaoCommandProperty);
        set => SetValue(AcaoCommandProperty, value);
    }
}
