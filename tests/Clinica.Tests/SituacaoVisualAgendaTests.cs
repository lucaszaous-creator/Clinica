using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

public sealed class SituacaoVisualAgendaTests
{
    [Theory]
    [InlineData(StatusAgendamento.Cancelado, "cancelado")]
    [InlineData(StatusAgendamento.Faltou, "faltou")]
    [InlineData(StatusAgendamento.Substituido, "substituido")]
    public void Saida_da_agenda_prevalece_sobre_carimbos_anteriores(StatusAgendamento status, string grupo)
    {
        foreach (var etapa in Enum.GetValues<EtapaFila>())
            SituacaoVisualAgenda.Grupo(status, etapa).Should().Be(grupo);
    }

    [Fact]
    public void Chegada_inicio_e_conclusao_mudam_cor_sem_mudar_estado_gravado_no_navegador()
    {
        var horario = new Agendamento { Status = StatusAgendamento.Agendado };
        SituacaoVisualAgenda.Grupo(horario.Status, horario.Etapa).Should().Be("pendente");
        horario.ChegadaEm = new DateTime(2026, 10, 9, 8, 0, 0);
        SituacaoVisualAgenda.Grupo(horario.Status, horario.Etapa).Should().Be("no-local");
        horario.InicioAtendimentoEm = horario.ChegadaEm.Value.AddMinutes(5);
        SituacaoVisualAgenda.Grupo(horario.Status, horario.Etapa).Should().Be("em-atendimento");
        horario.Status = StatusAgendamento.Realizado;
        SituacaoVisualAgenda.Grupo(horario.Status, horario.Etapa).Should().Be("atendido");
        horario.Status.Should().Be(StatusAgendamento.Realizado);
    }
}
