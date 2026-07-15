using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>Status usado na consulta central de guias.</summary>
public enum FiltroStatusGuia
{
    Todos,
    Aberto,
    Baixado,
    Glosado
}

/// <summary>Filtros da consulta central de guias.</summary>
public sealed class FiltroConsultaGuias
{
    public string? TermoPaciente { get; set; }   // nome ou CPF
    public string? NumeroGuia { get; set; }
    public DateOnly? Inicio { get; set; }         // data do atendimento
    public DateOnly? Fim { get; set; }
    public FiltroStatusGuia Status { get; set; } = FiltroStatusGuia.Todos;
    public Convenio? Convenio { get; set; }
}
