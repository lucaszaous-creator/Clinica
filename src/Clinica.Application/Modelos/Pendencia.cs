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
    string? Descricao);

/// <summary>Uma consulta a renovar (cobre laudos, receitas e dúvidas — 22 dias Unimed / 30 dias Amil).</summary>
public sealed record PendenciaConsulta(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    DateOnly DataVencimento,
    int DiasParaVencer,
    NivelUrgencia Urgencia);
