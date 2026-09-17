using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>Leitura da agenda: evolução, conclusão e guias são fatos independentes.</summary>
public sealed record SessaoNaFichaPaciente(
    int AgendamentoId, DateTime DataHora, string? Profissional,
    ModalidadeAtendimento Modalidade, string? ModalidadeCodigo, string? EspecialidadeCodigo,
    StatusAgendamento Status, DateTime? Inicio, DateTime? Fim,
    int? AtendimentoId, string? NumeroAtendimento, DateTime? RealizadoEm,
    DateTime? EstornadoEm, int? EvolucaoId, int Guias)
{
    public string DataTexto => DataHora.ToString("dd/MM/yyyy HH:mm");
    public string ModalidadeTexto => CatalogoModalidades.NomeComEspecialidade(
        ModalidadeCodigo, Modalidade, EspecialidadeCodigo);
    public string ProfissionalTexto => string.IsNullOrWhiteSpace(Profissional) ? "Profissional não informado" : Profissional;
    public string EvolucaoTexto => EvolucaoId is null ? "Sem evolução vinculada" : "Evolução salva";
    public string Situacao => EstornadoEm is not null ? "Atendimento estornado" : Status switch
    {
        StatusAgendamento.Cancelado => "Cancelada",
        StatusAgendamento.Faltou => "Falta registrada",
        StatusAgendamento.Substituido => "Substituída por outro atendimento",
        StatusAgendamento.Realizado when Fim is not null && RealizadoEm is not null => "Sessão concluída",
        StatusAgendamento.Realizado when AtendimentoId is null => "Conclusão a conferir: sem atendimento",
        _ when Fim is not null => "Conclusão pendente",
        _ when Inicio is not null => "Em atendimento",
        StatusAgendamento.Realizado => "Presença confirmada · registro em aberto",
        _ => "Agendada"
    };
    public string GuiasTexto => EstornadoEm is not null ? "Atendimento estornado" : AtendimentoId is null
        ? "Sem atendimento vinculado" : Guias == 0 ? "Nenhuma guia gerada" : $"{Guias} guia(s) gerada(s)";
    public string Protocolo => string.IsNullOrWhiteSpace(NumeroAtendimento)
        ? AtendimentoId is { } id ? $"Atendimento {id}" : "—" : NumeroAtendimento;
}
