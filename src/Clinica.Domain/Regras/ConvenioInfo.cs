namespace Clinica.Domain.Regras;

/// <summary>Metadados de cada convênio usados fora do motor de regras (ex.: renovação de consulta).</summary>
public static class ConvenioInfo
{
    /// <summary>Validade da consulta em dias, ou null quando o convênio não usa consulta renovável.</summary>
    public static int? ValidadeConsultaDias(Convenio convenio) => convenio switch
    {
        Convenio.UnimedPadrao => 22,
        Convenio.UnimedIntercambio => 22,
        Convenio.Amil => 30,
        Convenio.Petrobras => null, // acupuntura é faturada como consultas de especialidade; não há consulta renovável
        _ => null
    };

    public static string NomeExibicao(Convenio convenio) => convenio switch
    {
        Convenio.UnimedPadrao => "Unimed Costa do Sol (Padrão)",
        Convenio.UnimedIntercambio => "Unimed Costa do Sol Intercâmbio",
        Convenio.Amil => "Amil",
        Convenio.Petrobras => "Petrobras",
        _ => convenio.ToString()
    };
}
