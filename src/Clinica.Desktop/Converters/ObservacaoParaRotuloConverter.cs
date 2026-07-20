using System.Globalization;
using System.Windows.Data;

namespace Clinica.Desktop.Converters;

/// <summary>
/// Rótulo do botão de observação da pendência: "Anotar" quando ainda não há nota,
/// "Ver/editar" quando já existe uma — o responsável percebe de relance quais guias
/// já têm explicação registrada.
/// </summary>
public sealed class ObservacaoParaRotuloConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Ver/editar" : "Anotar";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
