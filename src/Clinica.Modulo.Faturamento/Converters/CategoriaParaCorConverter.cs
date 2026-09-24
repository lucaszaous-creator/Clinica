using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Clinica.Domain;

namespace Clinica.Desktop.Converters;

/// <summary>Categoria do paciente → cor do semáforo (mesmos tons de Brush.Semaforo.*).</summary>
public sealed class CategoriaParaCorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Categoria.Verde => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
        Categoria.Amarela => new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)),
        Categoria.Vermelha => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
        _ => Brushes.Gray
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
