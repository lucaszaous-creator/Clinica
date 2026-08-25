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

/// <summary>
/// Visível quando o valor NÃO é nulo (parcela 74).
///
/// É o terceiro da família, e nasce da mesma armadilha: o
/// <c>BooleanToVisibilityConverter</c> do WPF devolve <c>Collapsed</c> para qualquer coisa
/// que não seja <c>bool</c>, e a foto do paciente é um <c>BitmapImage</c>. Ligá-la
/// naquele conversor não é erro de compilação nem de binding — o retrato simplesmente
/// nunca apareceria, com as três redes verdes.
///
/// ⚠️ Ele NÃO substitui o <see cref="TextoParaVisibilidade"/>: string vazia não é nula, e
/// usar este numa mensagem faria a caixa de alerta ficar aberta e vazia depois de a
/// mensagem ser limpa. A pergunta que decide é "o que significa 'não tem'?" — para texto é
/// o branco, para objeto é o nulo.
/// </summary>
public sealed class ObjetoParaVisibilidade : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
