using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Clinica.Desktop.Converters;

/// <summary>
/// Nome do ícone → geometria do dicionário <c>Styles/Componentes/Icones.xaml</c> (set/2026).
/// É o gêmeo do <c>IconePorNome</c> do shell — portado no mesmo commit pela regra do
/// design system (os dois não se referenciam). A sidebar deste app continua com os
/// glifos da fonte; o conversor existe para a próxima tela que precisar dos desenhos
/// não nascer sem ele.
/// </summary>
public sealed class IconePorNomeConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string nome && nome.Length > 0
            ? System.Windows.Application.Current?.TryFindResource($"Icone.{nome}") as Geometry
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
