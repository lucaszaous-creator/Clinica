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

    /// <summary>
    /// Chave Pix da clínica — CPF, CNPJ, e-mail, telefone ou aleatória.
    ///
    /// Fica aqui, junto do CNPJ e do endereço, porque é identidade da clínica e não
    /// configuração de operação: quem muda a chave é quem muda a conta bancária.
    /// Acrescentar campo a este objeto é seguro para o faturamento congelado — ele é
    /// gravado como JSON, e a versão dele simplesmente ignora o que não conhece.
    /// </summary>
    public string? ChavePix { get; set; }

    /// <summary>
    /// Cidade da clínica. Obrigatória no código Pix pelo padrão do Banco Central, e é a
    /// única razão de o campo existir — o endereço completo não serve, porque ali cabem
    /// no máximo quinze caracteres.
    /// </summary>
    public string? Cidade { get; set; }

    /// <summary>Registro ANS da operadora (destino do lote).</summary>
    public string? RegistroAnsOperadora { get; set; }

    /// <summary>Código TUSS por tipo de procedimento (configurável pela clínica).</summary>
    public Dictionary<TipoCodigo, string> CodigosTuss { get; set; } = new();

    public string CodigoTuss(TipoCodigo tipo)
        => CodigosTuss.TryGetValue(tipo, out var c) ? c : string.Empty;
}
