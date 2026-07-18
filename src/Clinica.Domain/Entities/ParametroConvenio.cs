namespace Clinica.Domain.Entities;

/// <summary>
/// Catálogo/parâmetros de um convênio (dados editáveis sem recompilar).
/// A LÓGICA das regras de faturamento permanece no código (uma <c>IRegraConvenio</c>
/// por convênio); aqui ficam apenas os valores ajustáveis pela clínica: nome exibido,
/// se está ativo, validade da consulta, dias do 2º código e a categoria-base do paciente.
/// </summary>
public class ParametroConvenio
{
    /// <summary>Convênio (chave). Também identifica a regra de faturamento no código.</summary>
    public Convenio Convenio { get; set; }

    /// <summary>Nome exibido nas telas. Null = usa o nome padrão do código (ConvenioInfo).</summary>
    public string? Nome { get; set; }

    /// <summary>Convênio disponível para novos cadastros. Inativo some das listas (o histórico é preservado).</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Dias de validade da consulta (renovação). Null = convênio não usa consulta renovável.</summary>
    public int? ValidadeConsultaDias { get; set; }

    /// <summary>Dias entre o 1º e o 2º código (padrão 1 = 24h).</summary>
    public int DiasSegundoCodigo { get; set; } = 1;

    /// <summary>Categoria-base do paciente quando POSSUI app. Null = usa o padrão do código.</summary>
    public Categoria? CategoriaComApp { get; set; }

    /// <summary>Categoria-base do paciente quando NÃO possui app. Null = usa o padrão do código.</summary>
    public Categoria? CategoriaSemApp { get; set; }
}
