using System.Globalization;
using System.Windows.Data;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Converters;

/// <summary>Resume a situação de um código para exibição na ficha do paciente.</summary>
public sealed class StatusCodigoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CodigoFaturamento c)
            return string.Empty;

        if (c.Baixado)
            return $"Baixado em {c.DataBaixa:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(c.NumeroGuiaReal) ? "" : $" (guia {c.NumeroGuiaReal})");

        return c.Status switch
        {
            Clinica.Domain.StatusCodigo.NaoAplicavel => "Não aplicável",
            _ => $"Aberto — faturar em {c.DataPrevistaFaturamento:dd/MM/yyyy}"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
