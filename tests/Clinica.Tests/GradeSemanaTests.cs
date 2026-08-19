using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A grade da "Minha semana" do Consultório (parcela 69) — a tela era uma PILHA de
/// cartões por dia, o desenho que a parcela 58 condenou no balcão com a frase que decide:
/// "numa pilha, 'quando cabe?' só se responde lendo cartão por cartão; numa grade, o
/// vazio TEM tamanho". O montador mora na Application porque a camada de tela do WPF não
/// compila nos testes — tudo o que decide o desenho tem de ser testável aqui.
/// </summary>
public class GradeSemanaTests
{
    private static readonly DateOnly Segunda = new(2026, 8, 10); // segunda-feira

    private static SessaoDoDia Sessao(
        DateOnly dia, int hora, int minuto = 0, int duracao = 30,
        StatusAgendamento status = StatusAgendamento.Agendado, int id = 1)
        => new(id, dia.ToDateTime(new TimeOnly(hora, minuto)), 9, "Maria", "Acupuntura",
               null, status, EtapaFila.Aguardando, false, null, null)
        { DuracaoMinutos = duracao };

    private static SemanaDoProfissional Semana(params SessaoDoDia[] sessoes)
    {
        var dias = Enumerable.Range(0, 7)
            .Select(i => Segunda.AddDays(i))
            .Select(dia => new DiaDoProfissional(
                dia, 5, "Dra. Paula",
                sessoes.Where(s => DateOnly.FromDateTime(s.DataHora) == dia).ToList()))
            .ToList();
        return new SemanaDoProfissional(Segunda, 5, "Dra. Paula", dias);
    }

    private static DateTime Agora => Segunda.ToDateTime(new TimeOnly(12, 0));

    [Fact]
    public void Toda_faixa_tem_sete_celulas_e_o_rotulo_so_aparece_na_hora_cheia()
    {
        var grade = GradeSemana.Montar(Semana(), [], Agora);

        grade.Faixas.Should().NotBeEmpty();
        grade.Faixas.Should().OnlyContain(f => f.Celulas.Count == 7,
            "dia sem horário ENTRA na grade, vazio — semana de cinco colunas faria o olho "
            + "procurar a quarta que sumiu");
        grade.Faixas.First().Hora.Should().Be(Agendamento.AberturaPadraoGrade);
        grade.Faixas.Last().Hora.Should().Be(Agendamento.FechamentoPadraoGrade);
        grade.Faixas.Where(f => f.Hora.Minute == 0).Should().OnlyContain(f => f.HoraCheia);
        grade.Faixas.Where(f => f.Hora.Minute != 0).Should().OnlyContain(f => f.Rotulo == "");
    }

    [Fact]
    public void A_janela_estica_pela_sessao_fora_do_padrao_em_vez_de_deixa_la_de_fora()
    {
        var grade = GradeSemana.Montar(
            Semana(Sessao(Segunda, 6, 30)), [], Agora);

        grade.Faixas.First().Hora.Should().Be(new TimeOnly(6, 30),
            "uma sessão às 6h30 puxa a grade para cima em vez de sumir dela");
    }

    [Fact]
    public void Sessao_de_uma_hora_cobre_a_faixa_seguinte_como_continuacao()
    {
        var grade = GradeSemana.Montar(
            Semana(Sessao(Segunda, 9, duracao: 60)), [], Agora);

        CelulaDe(grade, Segunda, new TimeOnly(9, 0)).Sessoes.Should().ContainSingle();
        CelulaDe(grade, Segunda, new TimeOnly(9, 30)).Continuacao.Should().BeTrue(
            "sem a continuação, a meia hora de dentro da sessão pareceria vaga");
        CelulaDe(grade, Segunda, new TimeOnly(10, 0)).Livre.Should().BeTrue();
    }

    [Fact]
    public void Cancelada_aparece_marcada_na_faixa_dela_e_nao_cobre_a_seguinte()
    {
        var grade = GradeSemana.Montar(
            Semana(Sessao(Segunda, 9, duracao: 60, status: StatusAgendamento.Cancelado)),
            [], Agora);

        // A linha fica (regra da folha do dia)…
        CelulaDe(grade, Segunda, new TimeOnly(9, 0)).Sessoes.Should().ContainSingle()
            .Which.ForaDoDia.Should().BeTrue();
        // …e o vão que ela cobriria volta a ser vão: o horário vagou de verdade.
        CelulaDe(grade, Segunda, new TimeOnly(9, 30)).Continuacao.Should().BeFalse();
    }

    [Fact]
    public void Bloqueio_do_profissional_escreve_o_motivo_na_celula()
    {
        var ferias = new BloqueioAgenda
        {
            ProfissionalId = 5,
            Inicio = Segunda.ToDateTime(TimeOnly.MinValue),
            Fim = Segunda.AddDays(1).ToDateTime(TimeOnly.MinValue),
            Motivo = "Férias da Dra. Paula"
        };

        var grade = GradeSemana.Montar(Semana(), [ferias], Agora);

        CelulaDe(grade, Segunda, new TimeOnly(9, 0)).Bloqueio
            .Should().Be("Férias da Dra. Paula",
                "a semana do consultório nunca mostrou férias — o vão fechado era idêntico "
                + "ao vão livre, e o retorno era combinado para um dia em que ninguém atende");
        CelulaDe(grade, Segunda.AddDays(1), new TimeOnly(9, 0)).Bloqueio.Should().BeNull();
    }

    [Fact]
    public void Bloqueio_de_outro_profissional_nao_fecha_a_minha_grade()
    {
        var feriasDoColega = new BloqueioAgenda
        {
            ProfissionalId = 99,
            Inicio = Segunda.ToDateTime(TimeOnly.MinValue),
            Fim = Segunda.AddDays(7).ToDateTime(TimeOnly.MinValue),
            Motivo = "Férias do colega"
        };

        var grade = GradeSemana.Montar(Semana(), [feriasDoColega], Agora);

        grade.Faixas.SelectMany(f => f.Celulas).Should().OnlyContain(c => c.Bloqueio == null);
    }

    [Fact]
    public void Bloqueio_da_clinica_fecha_para_todo_mundo()
    {
        var feriado = new BloqueioAgenda
        {
            Inicio = Segunda.AddDays(2).ToDateTime(TimeOnly.MinValue),
            Fim = Segunda.AddDays(3).ToDateTime(TimeOnly.MinValue),
            Motivo = "Feriado"
        };

        var grade = GradeSemana.Montar(Semana(), [feriado], Agora);

        CelulaDe(grade, Segunda.AddDays(2), new TimeOnly(9, 0)).Bloqueio.Should().Be("Feriado");
    }

    private static CelulaSemana CelulaDe(GradeSemana grade, DateOnly dia, TimeOnly hora)
        => grade.Faixas.Single(f => f.Hora == hora).Celulas.Single(c => c.Dia == dia);
}
