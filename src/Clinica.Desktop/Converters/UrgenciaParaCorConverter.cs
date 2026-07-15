using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Clinica.Application.Modelos;

namespace Clinica.Desktop.Converters;

/// <summary>Converte o nível de urgência no semáforo de cores do dashboard.</summary>
public sealed class UrgenciaParaCorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        NivelUrgencia.Verde => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
        NivelUrgencia.Amarelo => new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)),
        NivelUrgencia.Vermelho => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
        _ => Brushes.Gray
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
