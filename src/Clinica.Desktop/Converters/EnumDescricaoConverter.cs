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
        ModalidadeAtendimento m => m switch
        {
            ModalidadeAtendimento.AcupunturaSimples => "Acupuntura (apenas)",
            ModalidadeAtendimento.AcupunturaComEletro => "Acupuntura + eletroacupuntura",
            ModalidadeAtendimento.BsvComAcupuntura => "BSV + acupuntura",
            ModalidadeAtendimento.BsvApenas => "BSV (apenas)",
            ModalidadeAtendimento.Consulta => "Consulta",
            _ => m.ToString()
        },
        Especialidade e => EspecialidadeInfo.NomeExibicao(e),
        FormatoNumeroGuia g => g switch
        {
            FormatoNumeroGuia.SomenteNumeros => "Somente números",
            FormatoNumeroGuia.Alfanumerico => "Letras e números",
            _ => "Sem validação"
        },
        null => string.Empty,
        // O fallback delega ao rotulador do DOMÍNIO em vez de chamar ToString().
        //
        // `ToString()` é o que põe o identificador do enum na tela — foi assim que o
        // cliente viu "PedidoExame" na suíte, na parcela 41. Aqui o risco era o mesmo:
        // todo enum que este conversor não enumera acima caía no nome cru. `RotulosEnum.De`
        // resolve pelos rótulos já escritos e, para o que não tem rótulo, humaniza
        // ("Pedido exame") — pior que o rótulo à mão, melhor que o identificador, e de
        // propósito visível, porque é a frase estranha que faz alguém vir escrevê-lo.
        // Os casos acima continuam vencendo: eles são os rótulos DESTE app.
        _ => RotulosEnum.De(value)
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
