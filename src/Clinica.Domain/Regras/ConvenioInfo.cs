namespace Clinica.Domain.Regras;

/// <summary>Metadados de cada convênio usados fora do motor de regras (ex.: renovação de consulta).</summary>
public static class ConvenioInfo
{
    /// <summary>
    /// O que DISPUTA o prazo entre duas consultas pelo convênio (trava de consulta e aviso de glosa na
    /// baixa, nov/2026). É dado do convênio, como o prazo: a Saúde Petrobras glosa a segunda consulta da
    /// MESMA ESPECIALIDADE dentro de 30 dias, com qualquer médico (pedido do dono, 18/11/2026); os demais
    /// continuam com o critério que já usavam — mesma especialidade com o MESMO médico. Convênio novo
    /// entra aqui como uma linha.
    /// </summary>
    public static JanelaDaTravaDeConsulta JanelaDaTravaDeConsulta(Convenio convenio) => convenio switch
    {
        Convenio.Petrobras => Regras.JanelaDaTravaDeConsulta.PorEspecialidade,
        _ => Regras.JanelaDaTravaDeConsulta.PorEspecialidadeEProfissional
    };

    /// <summary>Validade da consulta em dias, ou null quando o convênio não usa consulta renovável.</summary>
    public static int? ValidadeConsultaDias(Convenio convenio) => convenio switch
    {
        Convenio.UnimedPadrao => 22,
        Convenio.UnimedIntercambio => 22,
        Convenio.Amil => 30,
        Convenio.Petrobras => 30,
        _ => null
    };

    /// <summary>Nome padrão (embutido no código). O nome EXIBIDO pode ser sobreposto pelo catálogo.</summary>
    public static string NomeExibicaoPadrao(Convenio convenio) => convenio switch
    {
        Convenio.UnimedPadrao => "Unimed Costa do Sol (Padrão)",
        Convenio.UnimedIntercambio => "Unimed Costa do Sol Intercâmbio",
        Convenio.Amil => "Amil",
        Convenio.Petrobras => "Petrobras",
        _ => convenio.ToString()
    };

    /// <summary>Nome exibido do convênio (reflete rename salvo no catálogo, com fallback no padrão).</summary>
    public static string NomeExibicao(Convenio convenio) => CatalogoConvenios.Nome(convenio);
}
