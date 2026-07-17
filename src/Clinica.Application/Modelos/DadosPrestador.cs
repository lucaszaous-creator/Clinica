using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Dados do prestador (clínica) usados no lote TISS e impressos na capa de faturamento.</summary>
public sealed class DadosPrestador
{
    public string? CodigoNaOperadora { get; set; }
    public string? Cnpj { get; set; }
    public string? RazaoSocial { get; set; }

    // Identificação exibida em documentos (capa de faturamento)
    public string? NomeFantasia { get; set; }
    public string? Cnes { get; set; }
    public string? Endereco { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }

    /// <summary>Registro ANS da operadora (destino do lote).</summary>
    public string? RegistroAnsOperadora { get; set; }

    /// <summary>Código TUSS por tipo de procedimento (configurável pela clínica).</summary>
    public Dictionary<TipoCodigo, string> CodigosTuss { get; set; } = new();

    public string CodigoTuss(TipoCodigo tipo)
        => CodigosTuss.TryGetValue(tipo, out var c) ? c : string.Empty;
}
