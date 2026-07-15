using System.Globalization;
using System.Windows.Data;
using Clinica.Domain;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Converters;

/// <summary>Exibe enums de forma amigável (nomes de convênio, forma de obtenção etc.).</summary>
public sealed class EnumDescricaoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Convenio c => ConvenioInfo.NomeExibicao(c),
        FormaObtencao f => f switch
        {
            FormaObtencao.NaoAplica => "—",
            FormaObtencao.App => "Pelo app (QR Code)",
            FormaObtencao.Sistema => "Pelo sistema",
            FormaObtencao.Ligacao => "Ligar para o paciente",
            _ => f.ToString()
        },
        TipoCodigo t => t switch
        {
            TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
            TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
            _ => t.ToString()
        },
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
