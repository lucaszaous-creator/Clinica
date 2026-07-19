using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Attached properties do design system:
/// - Placeholder: texto-fantasma em TextBox (consumido pelo template em Styles/Componentes/Campos.xaml);
/// - Icone: glifo (Segoe Fluent/MDL2) exibido por templates que suportam ícone;
/// - EstaCarregando: estado de loading em botões (spinner + desabilita o clique);
/// - SomenteNumeros: bloqueia entrada não numérica (digitação e colagem) em TextBox.
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

    public static readonly DependencyProperty SomenteNumerosProperty =
        DependencyProperty.RegisterAttached("SomenteNumeros", typeof(bool), typeof(Ajudantes),
            new PropertyMetadata(false, OnSomenteNumerosChanged));

    public static bool GetSomenteNumeros(DependencyObject obj) => (bool)obj.GetValue(SomenteNumerosProperty);
    public static void SetSomenteNumeros(DependencyObject obj, bool value) => obj.SetValue(SomenteNumerosProperty, value);

    private static void OnSomenteNumerosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox caixa) return;
        if ((bool)e.NewValue)
        {
            caixa.PreviewTextInput += BloquearNaoNumerico;
            DataObject.AddPastingHandler(caixa, BloquearColagemNaoNumerica);
            InputMethod.SetIsInputMethodEnabled(caixa, false);
        }
        else
        {
            caixa.PreviewTextInput -= BloquearNaoNumerico;
            DataObject.RemovePastingHandler(caixa, BloquearColagemNaoNumerica);
        }
    }

    private static void BloquearNaoNumerico(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);

    private static void BloquearColagemNaoNumerica(object sender, DataObjectPastingEventArgs e)
    {
        var texto = e.DataObject.GetData(DataFormats.Text) as string;
        if (texto is null || !texto.All(char.IsDigit))
            e.CancelCommand();
    }
}
