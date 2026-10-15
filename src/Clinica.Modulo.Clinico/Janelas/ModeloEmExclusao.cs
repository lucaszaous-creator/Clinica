using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Decide qual das duas linhas de um modelo salvo aparece: a normal (usar / excluir) ou a
/// CONFIRMAÇÃO da exclusão, que abre no próprio modelo (out/2026).
///
/// <c>values[0]</c> é o id do modelo em confirmação (<c>ModeloEmExclusao</c>, nulo quando
/// nenhum) e <c>values[1]</c> é o id da linha. Com <c>ConverterParameter="Confirmacao"</c>
/// devolve visível só na linha confirmada; sem ele, o contrário.
/// </summary>
public sealed class ModeloEmExclusao : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var confirmando = values.Length == 2 && values[0] is int emExclusao
                          && values[1] is int id && emExclusao == id;
        var querConfirmacao = parameter as string == "Confirmacao";
        return confirmando == querConfirmacao ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
