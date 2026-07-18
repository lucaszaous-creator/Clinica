namespace Clinica.Domain.Entities;

/// <summary>
/// Configuração global da clínica (chave/valor no banco) — vale para TODAS as máquinas,
/// ao contrário das preferências locais. Ex.: janela de alerta de consultas.
/// </summary>
public class ConfiguracaoGlobal
{
    /// <summary>Chave da configuração (PK). Ex.: "JanelaAlertaConsultaDias".</summary>
    public string Chave { get; set; } = string.Empty;

    public string Valor { get; set; } = string.Empty;
}
