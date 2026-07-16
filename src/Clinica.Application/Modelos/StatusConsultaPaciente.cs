using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Situação da consulta (renovável) de um paciente, exibida na aba de Consultas.</summary>
public sealed record StatusConsultaPaciente(
    int PacienteId,
    string PacienteNome,
    Convenio Convenio,
    bool UsaConsulta,            // convênio possui consulta renovável (ex.: Petrobras não usa)
    DateOnly? UltimaEmissao,
    DateOnly? Vencimento,
    int? DiasParaVencer,         // negativo = já venceu
    bool Vencida,
    bool PrecisaRenovar,         // vencida, sem consulta ainda, ou a vencer dentro da janela de alerta
    NivelUrgencia Urgencia);
