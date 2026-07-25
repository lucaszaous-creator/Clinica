using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Converters;

/// <summary>
/// Nome do convênio de um paciente: prefere o código do catálogo (que resolve as
/// variantes criadas pela clínica) e cai na família quando o cadastro é anterior
/// aos convênios dinâmicos.
/// </summary>
public sealed class ConvenioPacienteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Paciente p
            ? CatalogoConvenios.Nome(p.ConvenioCodigo ?? p.Convenio.ToString())
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

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
