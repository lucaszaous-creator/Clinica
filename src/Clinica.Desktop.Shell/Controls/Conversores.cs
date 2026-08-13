using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Visível quando o TEXTO tem conteúdo (parcela 61).
///
/// Existe porque o <c>BooleanToVisibilityConverter</c> do WPF devolve <c>Collapsed</c>
/// para qualquer valor que não seja <c>bool</c> — ligar uma STRING nele não é erro de
/// compilação nem de binding: o elemento simplesmente nunca aparece. Foi assim que a
/// mensagem da folha de execução, a justificativa da rodela e o nome do executante
/// ficaram invisíveis desde a parcela 42, com as três redes verdes. O faturamento tem o
/// gêmeo dele (<c>TextoParaVisibilidadeConverter</c>) desde sempre; o shell não tinha.
/// </summary>
public sealed class TextoParaVisibilidade : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
