namespace Clinica.Application.Modelos;

/// <summary>Assinaturas ainda não colhidas, separadas pelo profissional que deve agir.</summary>
public sealed record PendenciasAssinaturasInfusao(int Enfermagem, int Medico, int Devolvidas = 0);
