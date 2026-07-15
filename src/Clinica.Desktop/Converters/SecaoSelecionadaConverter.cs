using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Clinica.Desktop.Converters;

/// <summary>Destaca o item de menu da seção ativa: fundo mais claro quando value == parameter.</summary>
public sealed class SecaoSelecionadaConverter : IValueConverter
{
    private static readonly Brush Selecionado = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Normal = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString() ? Selecionado : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
