using System.Globalization;
using System.Windows.Data;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Converters;

/// <summary>
/// Exibe o NOME de uma modalidade a partir do seu código de catálogo (string). Resolve
/// variantes criadas pela clínica; para códigos embutidos, cai no nome padrão da base.
/// </summary>
public sealed class CodigoModalidadeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string codigo ? CatalogoModalidades.Nome(codigo) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Exibe o NOME de uma especialidade a partir do seu código de catálogo (string). Resolve
/// especialidades criadas pela clínica; vazio quando não há especialidade.
/// </summary>
public sealed class CodigoEspecialidadeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string codigo ? CatalogoEspecialidades.Nome(codigo) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
