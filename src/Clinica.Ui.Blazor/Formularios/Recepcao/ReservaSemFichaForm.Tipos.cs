using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Ui.Blazor.Formularios;

/// <summary>Por onde o <c>ReservaSemFichaForm</c> abre: o detalhe (abas da Agenda) ou direto numa ação (a conversa).</summary>
public enum ModoDaReserva { Detalhe, Remarcar, Desmarcar, Virar }

/// <summary>O que aconteceu com a reserva (quem abriu relê, avisa e, na conversa, deixa a mensagem pronta).</summary>
public enum AcaoNaReserva { Remarcada, Desmarcada, NaoCompareceu, VirouPaciente }

/// <summary>
/// O resultado de uma ação na reserva sem ficha: a reserva como ficou, o horário de ANTES (a mensagem da remarcação diz
/// "passou de … para …") e, quando virou paciente, quem é e o agendamento que nasceu no mesmo horário.
/// </summary>
public sealed record ReservaAlterada(AcaoNaReserva Acao, ReservaDeHorario Reserva, DateTime Antes, PacienteCadastrado? Paciente)
{
    public int? AgendamentoId { get; init; }

    /// <summary>A confirmação passageira de cada ação (a mesma nas três portas).</summary>
    public string Aviso => Acao switch
    {
        AcaoNaReserva.Remarcada => $"Reserva remarcada para {Reserva.DataHora:dd/MM} às {Reserva.DataHora:HH:mm}.",
        AcaoNaReserva.Desmarcada => "Reserva desmarcada. O horário voltou a ficar livre.",
        AcaoNaReserva.NaoCompareceu => "Registrado: a pessoa não compareceu.",
        _ => $"Agora é agendamento de {Paciente?.Nome ?? Reserva.Nome} (ficha {Paciente?.Id}), no mesmo horário."
    };
}
