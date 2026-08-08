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
    public string? TermoObservacao { get; set; } // texto da observação da pendência
    public DateOnly? Inicio { get; set; }         // data do atendimento
    public DateOnly? Fim { get; set; }
    public FiltroStatusGuia Status { get; set; } = FiltroStatusGuia.Todos;
    public Convenio? Convenio { get; set; }

    /// <summary>
    /// O que foi realizado no atendimento (parcela 45): acupuntura, eletro, BSV, consulta.
    ///
    /// É o enum e não o código do catálogo, pela mesma razão do <see cref="Convenio"/> logo
    /// acima: o filtro é por FAMÍLIA, e a variante que a clínica cadastrou responde junto
    /// da embutida que ela deriva. Filtrar pelo código exigiria escolher entre "Acupuntura"
    /// e "Acupuntura (domiciliar)" para responder quantas acupunturas foram feitas.
    /// </summary>
    public ModalidadeAtendimento? Modalidade { get; set; }

    /// <summary>
    /// Especialidade da guia (parcela 45). Casa com a especialidade gravada no CÓDIGO —
    /// que é o que a consulta avulsa e a rotação da Petrobras preenchem — e cai para a do
    /// atendimento quando o código não a tem. Sem esse caminho de baixo, guia de um
    /// atendimento com especialidade declarada ficaria de fora da própria especialidade.
    /// </summary>
    public Especialidade? Especialidade { get; set; }
}
