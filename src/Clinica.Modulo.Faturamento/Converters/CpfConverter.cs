using System.Globalization;
using System.Windows.Data;
using Clinica.Domain;

namespace Clinica.Desktop.Converters;

/// <summary>Exibe o documento do paciente formatado como CPF (000.000.000-00).</summary>
public sealed class CpfConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? Cpf.Formatar(s) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
