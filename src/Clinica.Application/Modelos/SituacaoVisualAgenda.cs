using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Agrupa a situação informada pelo host apenas para busca e apresentação.</summary>
public static class SituacaoVisualAgenda
{
    public static string Grupo(StatusAgendamento status, EtapaFila etapa) => status switch
    {
        StatusAgendamento.Cancelado => "cancelado",
        StatusAgendamento.Faltou => "faltou",
        StatusAgendamento.Substituido => "substituido",
        _ => etapa switch
        {
            EtapaFila.Finalizado => "atendido",
            EtapaFila.EmAtendimento => "em-atendimento",
            EtapaFila.Chegou or EtapaFila.Chamado => "no-local",
            _ => "pendente"
        }
    };
}
