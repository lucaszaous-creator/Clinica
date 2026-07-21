using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Semáforo de urgência exibido no dashboard.</summary>
public enum NivelUrgencia
{
    Verde,    // dentro do prazo
    Amarelo,  // vence hoje / muito em breve
    Vermelho  // atrasado
}

/// <summary>Uma guia/código pendente de baixa (a causa da perda de faturas).</summary>
public sealed record PendenciaCodigo(
    int CodigoId,
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    TipoCodigo Tipo,
    OrdemCodigo Ordem,
    DateOnly DataPrevista,
    FormaObtencao FormaObtencao,
    int DiasEmAtraso,
    NivelUrgencia Urgencia,
    string? Descricao,
    /// <summary>Telefone do paciente (para abrir o WhatsApp direto da pendência). Nulo = sem contato no cadastro.</summary>
    string? PacienteTelefone = null,
    /// <summary>Anotação do responsável sobre por que a guia ainda não foi baixada (nula = sem anotação).</summary>
    string? ObservacaoPendencia = null,
    /// <summary>Quando a observação foi anotada/atualizada.</summary>
    DateTime? ObservacaoPendenciaEm = null)
{
    /// <summary>True quando há uma observação registrada (para destacar a linha na tela).</summary>
    public bool TemObservacao => !string.IsNullOrWhiteSpace(ObservacaoPendencia);
}

/// <summary>Uma consulta a renovar (cobre laudos, receitas e dúvidas — 22 dias Unimed / 30 dias Amil).</summary>
public sealed record PendenciaConsulta(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    DateOnly DataVencimento,
    int DiasParaVencer,
    NivelUrgencia Urgencia);

/// <summary>Uma glosa em aberto com prazo de recurso correndo (perder o prazo = perder a guia de vez).</summary>
public sealed record PendenciaRecursoGlosa(
    int CodigoId,
    string PacienteNome,
    Convenio Convenio,
    TipoCodigo Tipo,
    string? NumeroGuia,
    DateOnly? DataGlosa,
    string MotivoResumo,
    DateOnly DataLimiteRecurso,
    int DiasParaFimPrazo,
    NivelUrgencia Urgencia);

/// <summary>Carteirinha vencida ou a vencer (carteirinha vencida = guia recusada na origem).</summary>
public sealed record PendenciaCarteirinha(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    string? Carteirinha,
    DateOnly Validade,
    int DiasParaVencer,
    NivelUrgencia Urgencia);
