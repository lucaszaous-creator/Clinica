using System.Windows;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Attached properties do design system:
/// - Placeholder: texto-fantasma em TextBox (consumido pelo template em Styles/Componentes/Campos.xaml);
/// - Icone: glifo (Segoe Fluent/MDL2) exibido por templates que suportam ícone;
/// - EstaCarregando: estado de loading em botões (spinner + desabilita o clique).
/// </summary>
public static class Ajudantes
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(Ajudantes),
            new PropertyMetadata(string.Empty));

    public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);

    public static readonly DependencyProperty IconeProperty =
        DependencyProperty.RegisterAttached("Icone", typeof(string), typeof(Ajudantes),
            new PropertyMetadata(string.Empty));

    public static string GetIcone(DependencyObject obj) => (string)obj.GetValue(IconeProperty);
    public static void SetIcone(DependencyObject obj, string value) => obj.SetValue(IconeProperty, value);

    public static readonly DependencyProperty EstaCarregandoProperty =
        DependencyProperty.RegisterAttached("EstaCarregando", typeof(bool), typeof(Ajudantes),
            new PropertyMetadata(false));

    public static bool GetEstaCarregando(DependencyObject obj) => (bool)obj.GetValue(EstaCarregandoProperty);
    public static void SetEstaCarregando(DependencyObject obj, bool value) => obj.SetValue(EstaCarregandoProperty, value);
}
