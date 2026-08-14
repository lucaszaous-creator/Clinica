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
/// Visível quando a CONTAGEM é maior que zero (parcela 68).
///
/// Irmão do de cima, e pelo mesmo motivo: <c>BooleanToVisibilityConverter</c> devolve
/// <c>Collapsed</c> para qualquer valor que não seja <c>bool</c>, e
/// <c>{Binding Colecao.Count, Converter={StaticResource BoolParaVisibilidade}}</c> parece
/// certíssimo — o XAML é bem-formado, a propriedade existe, o binding resolve e nada
/// lança. O elemento só nunca aparece.
///
/// Foi assim que o bloco "no sistema e NÃO no extrato" da conciliação bancária ficou
/// invisível desde que nasceu: justamente a metade que ninguém pede, a que denuncia
/// pagamento dado como recebido no sistema e sem correspondência no banco.
///
/// A parcela 61 achou este defeito sobre STRING e criou o conversor de texto; ninguém
/// procurou o mesmo erro sobre INT. <b>Ao ligar `Visibility` a alguma coisa, confira o
/// TIPO dela</b> — só o `bool` serve ao conversor do WPF.
/// </summary>
public sealed class ContagemParaVisibilidade : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
