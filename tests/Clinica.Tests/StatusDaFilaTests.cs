using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O vocabulário da coluna STATUS das duas listas do dia (Meu dia e Agenda do dia) — uma
/// definição, lida pelo médico e pelo balcão sobre o MESMO horário.
/// </summary>
public class StatusDaFilaTests
{
    private static readonly DateTime Chegada = new(2026, 9, 10, 14, 40, 0);

    [Theory]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.Aguardando, "Marcado")]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.Chegou, "No local")]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.Chamado, "Chamado")]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.EmAtendimento, "Em atendimento")]
    [InlineData(StatusAgendamento.Realizado, EtapaFila.Finalizado, "Concluído")]
    [InlineData(StatusAgendamento.Cancelado, EtapaFila.ForaDaFila, "Cancelado")]
    [InlineData(StatusAgendamento.Faltou, EtapaFila.ForaDaFila, "Faltou")]
    public void A_palavra_e_uma_por_etapa_e_o_status_gravado_vence(StatusAgendamento status, EtapaFila etapa, string esperado)
        => StatusDaFila.Palavra(status, etapa).Should().Be(esperado);

    /// <summary>
    /// O cartão da GRADE cala enquanto nada aconteceu, e fala assim que acontece. É a
    /// mesma palavra da lista — o que muda é só o silêncio do "Marcado", que na grade
    /// sairia em quarenta cartões de um dia que ainda não começou.
    /// </summary>
    [Theory]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.Aguardando, "")]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.Chegou, "No local")]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.Chamado, "Chamado")]
    [InlineData(StatusAgendamento.Agendado, EtapaFila.EmAtendimento, "Em atendimento")]
    [InlineData(StatusAgendamento.Realizado, EtapaFila.Finalizado, "Concluído")]
    [InlineData(StatusAgendamento.Cancelado, EtapaFila.ForaDaFila, "Cancelado")]
    [InlineData(StatusAgendamento.Faltou, EtapaFila.ForaDaFila, "Faltou")]
    public void Na_grade_so_fala_quem_ja_tem_fato(StatusAgendamento status, EtapaFila etapa, string esperado)
        => StatusDaFila.SituacaoNaGrade(status, etapa).Should().Be(esperado);

    [Fact]
    public void Na_grade_a_palavra_e_a_MESMA_da_lista_quando_ha_fato()
    {
        // Duas telas sobre o mesmo horário, uma palavra: o que a grade escreve, quando
        // escreve, é exatamente o que a lista escreve — senão o balcão leria dois fatos.
        foreach (var etapa in new[]
                 {
                     EtapaFila.Chegou, EtapaFila.Chamado,
                     EtapaFila.EmAtendimento, EtapaFila.Finalizado
                 })
        {
            var status = etapa == EtapaFila.Finalizado
                ? StatusAgendamento.Realizado
                : StatusAgendamento.Agendado;

            StatusDaFila.SituacaoNaGrade(status, etapa)
                .Should().Be(StatusDaFila.Palavra(status, etapa));
        }
    }

    [Fact]
    public void Cancelado_e_falta_ficam_fora_da_fila_e_sem_detalhe()
    {
        StatusDaFila.ForaDaFila(StatusAgendamento.Cancelado).Should().BeTrue();
        StatusDaFila.ForaDaFila(StatusAgendamento.Faltou).Should().BeTrue();
        StatusDaFila.ForaDaFila(StatusAgendamento.Agendado).Should().BeFalse();
        StatusDaFila.ForaDaFila(StatusAgendamento.Realizado).Should().BeFalse();

        // Mesmo com carimbos gravados, o cancelado não mostra hora de fato: ele saiu.
        StatusDaFila.Detalhe(StatusAgendamento.Cancelado, EtapaFila.ForaDaFila, Chegada, 12, null, null, null)
            .Should().BeEmpty();
    }

    [Fact]
    public void O_detalhe_e_a_hora_do_fato_de_cada_etapa()
    {
        StatusDaFila.Detalhe(StatusAgendamento.Agendado, EtapaFila.Aguardando, null, null, null, null, null)
            .Should().BeEmpty("quem não chegou não tem fato");

        StatusDaFila.Detalhe(StatusAgendamento.Agendado, EtapaFila.Chegou, Chegada, 12, null, null, null)
            .Should().Be("chegou às 14:40 · espera 12 min");
        StatusDaFila.Detalhe(StatusAgendamento.Agendado, EtapaFila.Chegou, null, 12, null, null, null)
            .Should().Be("espera 12 min", "sem a hora gravada, a espera sozinha");

        StatusDaFila.Detalhe(StatusAgendamento.Agendado, EtapaFila.Chamado, Chegada, 15, 0, null, null)
            .Should().Be("chamado agora");
        StatusDaFila.Detalhe(StatusAgendamento.Agendado, EtapaFila.Chamado, Chegada, 15, 4, null, null)
            .Should().Be("chamado há 4 min");

        StatusDaFila.Detalhe(StatusAgendamento.Agendado, EtapaFila.EmAtendimento, Chegada, null, null,
                Chegada.AddMinutes(12), null)
            .Should().Be("desde 14:52");

        StatusDaFila.Detalhe(StatusAgendamento.Realizado, EtapaFila.Finalizado, Chegada, null, null,
                Chegada.AddMinutes(12), Chegada.AddMinutes(40))
            .Should().Be("às 15:20");
    }
}
