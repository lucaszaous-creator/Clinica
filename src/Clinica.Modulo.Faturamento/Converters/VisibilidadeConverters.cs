using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Clinica.Desktop.Converters;

/// <summary>Visível quando o valor NÃO é nulo (ex.: badge "Em edição").</summary>
public sealed class NuloParaVisibilidadeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visível quando o texto tem conteúdo (esconde separadores de campos vazios).</summary>
public sealed class TextoParaVisibilidadeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visível quando o número é diferente de zero (badges de contagem).</summary>
public sealed class ZeroParaVisibilidadeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n != 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Nega um booleano (ex.: travar um campo enquanto um modo está ativo).</summary>
public sealed class BoolInvertidoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>Visível quando o booleano é FALSO — para alternar dois blocos com um só estado.</summary>
public sealed class BoolInvertidoParaVisibilidadeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
