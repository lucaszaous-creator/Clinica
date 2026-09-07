using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A linha que diz o que está FECHADO no dia (set/2026) — o cabeçalho da coluna da visão
/// de semana do balcão.
///
/// O buraco que ela fecha: a coluna do dia não tem dono, então <c>BloqueioDe(quando, null,
/// null)</c> só enxergava o fechamento da clínica inteira, e as férias de um profissional
/// deixavam o dia com menos horários e nenhuma palavra — menos horários se lê como
/// "ninguém marcou", que é a resposta errada mais cara que uma agenda pode dar.
/// </summary>
public class FechamentosDaAgendaTests
{
    private static readonly DateTime Terca = new(2026, 9, 8);

    private static BloqueioAgenda Bloqueio(
        DateTime inicio, DateTime fim, Profissional? profissional = null, Sala? sala = null)
        => new()
        {
            Inicio = inicio,
            Fim = fim,
            Motivo = "Férias",
            ProfissionalId = profissional?.Id,
            Profissional = profissional,
            SalaId = sala?.Id,
            Sala = sala
        };

    private static Profissional Ana => new() { Id = 7, Nome = "Ana Souza" };
    private static Sala Sala2 => new() { Id = 3, Nome = "2" };

    [Fact]
    public void Sem_bloqueio_a_linha_e_vazia()
        => FechamentosDaAgenda.Descrever([], Terca).Should().BeEmpty();

    [Fact]
    public void Ferias_do_profissional_aparecem_na_coluna_de_TODOS()
    {
        // É o defeito que a linha existe para fechar: sem ela, o dia de férias da Ana
        // aparecia só com menos horários.
        var linha = FechamentosDaAgenda.Descrever(
            [Bloqueio(Terca, Terca.AddDays(7), Ana)], Terca);

        linha.Should().Be("Ana Souza");
    }

    [Fact]
    public void Fechamento_da_clinica_diz_que_e_da_clinica()
    {
        var linha = FechamentosDaAgenda.Descrever(
            [Bloqueio(Terca, Terca.AddDays(1))], Terca);

        linha.Should().Be("Clínica fechada");
    }

    [Fact]
    public void Recurso_repetido_e_dito_UMA_vez()
    {
        // Três bloqueios de tarde da mesma pessoa na mesma semana escreveriam o nome dela
        // três vezes, e a linha deixaria de se ler.
        var linha = FechamentosDaAgenda.Descrever(
            [
                Bloqueio(Terca.AddHours(8), Terca.AddHours(12), Ana),
                Bloqueio(Terca.AddHours(14), Terca.AddHours(18), Ana)
            ],
            Terca);

        linha.Should().Be("Ana Souza");
    }

    [Fact]
    public void Com_dono_so_o_que_alcanca_ELE()
    {
        // O fechamento da sala 2 não é notícia para quem atende noutra sala: alerta que
        // dispara para todo mundo é alerta que ninguém lê.
        var linha = FechamentosDaAgenda.Descrever(
            [
                Bloqueio(Terca, Terca.AddDays(1), Ana),
                Bloqueio(Terca, Terca.AddDays(1), sala: Sala2)
            ],
            Terca, donoId: Ana.Id);

        linha.Should().Be("Ana Souza");
    }

    [Fact]
    public void Com_dono_o_fechamento_da_CLINICA_continua_valendo()
    {
        var linha = FechamentosDaAgenda.Descrever(
            [Bloqueio(Terca, Terca.AddDays(1))], Terca, donoId: Ana.Id);

        linha.Should().Be("Clínica fechada");
    }

    [Fact]
    public void Bloqueio_de_OUTRO_dia_nao_entra()
    {
        var linha = FechamentosDaAgenda.Descrever(
            [Bloqueio(Terca.AddDays(2), Terca.AddDays(3), Ana)], Terca);

        linha.Should().BeEmpty();
    }

    [Fact]
    public void Bloqueio_de_MEIO_dia_conta_o_dia_inteiro_no_cabecalho()
    {
        // O cabeçalho responde "há algo fechado neste dia?" — o vão exato continua sendo
        // desenhado pela grade, com o motivo na dica.
        var linha = FechamentosDaAgenda.Descrever(
            [Bloqueio(Terca.AddHours(14), Terca.AddHours(18), Ana)], Terca);

        linha.Should().Be("Ana Souza");
    }

    [Fact]
    public void Dois_recursos_diferentes_saem_na_mesma_linha()
    {
        var linha = FechamentosDaAgenda.Descrever(
            [
                Bloqueio(Terca, Terca.AddDays(1), Ana),
                Bloqueio(Terca, Terca.AddDays(1), sala: Sala2)
            ],
            Terca);

        linha.Should().Be("Ana Souza · Sala 2");
    }
}
