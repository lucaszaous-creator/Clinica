namespace Clinica.Domain.Regras;

/// <summary>
/// Configuração da regra de faturamento GENÉRICA (família "Personalizado"), definida pela
/// clínica ao cadastrar um convênio novo. Cobre o padrão comum de uma clínica de acupuntura:
/// consulta renovável + acupuntura (± eletro como 2º código) + BSV, com ou sem 2º código.
/// Não cobre fluxos exóticos (ex.: rotação de especialidades da Petrobras).
/// </summary>
public sealed class ConfiguracaoRegraGenerica
{
    /// <summary>Na modalidade acupuntura+eletro, realiza a eletroacupuntura (candidata a 2º código)?</summary>
    public bool FazEletro { get; set; }

    /// <summary>Gera um 2º código (+dias) nas modalidades duplas (acu+eletro, BSV+acu)?</summary>
    public bool TemSegundoCodigo { get; set; }

    /// <summary>Como o 2º código é obtido (pelo sistema, ligando ao paciente/app).</summary>
    public FormaObtencao FormaSegundoCodigo { get; set; } = FormaObtencao.Sistema;

    /// <summary>O 2º código só é possível se o paciente possuir o app (gera QR Code)?</summary>
    public bool SegundoCodigoDependeApp { get; set; }

    /// <summary>Dias entre o 1º e o 2º código (padrão 1 = 24h).</summary>
    public int DiasSegundoCodigo { get; set; } = 1;

    /// <summary>O convênio fatura BSV?</summary>
    public bool FaturaBsv { get; set; } = true;

    /// <summary>Exige inverter as datas no sistema do convênio (BSV)? Apenas gera a instrução.</summary>
    public bool InverteDatasBsv { get; set; }

    /// <summary>Dias de validade da consulta (renovação). Null = sem consulta renovável.</summary>
    public int? ValidadeConsultaDias { get; set; }

    /// <summary>Categoria-base do paciente quando POSSUI app.</summary>
    public Categoria CategoriaComApp { get; set; } = Categoria.Verde;

    /// <summary>Categoria-base do paciente quando NÃO possui app.</summary>
    public Categoria CategoriaSemApp { get; set; } = Categoria.Amarela;
}
