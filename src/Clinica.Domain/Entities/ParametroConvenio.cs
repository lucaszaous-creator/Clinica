namespace Clinica.Domain.Entities;

/// <summary>
/// Parâmetros editáveis das regras de um convênio (números ajustáveis sem recompilar).
/// A lógica das regras permanece no código; apenas estes valores vêm da configuração.
/// </summary>
public class ParametroConvenio
{
    /// <summary>Convênio (chave).</summary>
    public Convenio Convenio { get; set; }

    /// <summary>Dias de validade da consulta (renovação). Null = convênio não usa consulta renovável.</summary>
    public int? ValidadeConsultaDias { get; set; }

    /// <summary>Dias entre o 1º e o 2º código (padrão 1 = 24h).</summary>
    public int DiasSegundoCodigo { get; set; } = 1;
}
