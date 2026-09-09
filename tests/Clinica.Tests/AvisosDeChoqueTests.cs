using Clinica.Application.Modelos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O VOCABULÁRIO dos avisos do horário (set/2026) — a frase e a gravidade que as duas
/// telas de marcação escrevem.
///
/// Ele existe porque a agenda deixou de RECUSAR choque, e o que sobrou no lugar da
/// barreira foi esta lista. O que a tela afirma mora na Application justamente para caber
/// aqui: enquanto a frase vivia dentro de cada ViewModel (havia DUAS cópias de
/// <c>Descrever</c>, uma em cada tela), nenhum teste a alcançava.
/// </summary>
public class AvisosDeChoqueTests
{
    private static ConflitoAgenda Conflito(RecursoAgenda recurso, string descricao = "x")
        => new(recurso, 1, descricao, DateTime.Today, DateTime.Today.AddMinutes(30));

    /// <summary>
    /// Agenda fechada e fora do expediente são os GRAVES: são os dois avisos que não têm
    /// outro paciente do outro lado — eles não falam de disputa, falam de a clínica não
    /// estar aberta. Os outros três são a rotina da casa.
    /// </summary>
    [Theory]
    [InlineData(RecursoAgenda.Bloqueio, true)]
    [InlineData(RecursoAgenda.Expediente, true)]
    [InlineData(RecursoAgenda.Profissional, false)]
    [InlineData(RecursoAgenda.Sala, false)]
    [InlineData(RecursoAgenda.Paciente, false)]
    public void A_gravidade_separa_agenda_fechada_da_rotina_da_casa(RecursoAgenda recurso, bool grave)
        => AvisosDeChoque.EhGrave(recurso).Should().Be(grave);

    /// <summary>
    /// O GRAVE primeiro. A recepcionista lê a primeira linha e decide; deixar "o
    /// profissional já atende fulano" acima de "a clínica está fechada" enterraria o único
    /// que costuma mudar a decisão.
    /// </summary>
    [Fact]
    public void O_aviso_grave_vem_primeiro()
    {
        var avisos = AvisosDeChoque.Montar(
        [
            Conflito(RecursoAgenda.Profissional, "Ana já atende Maria às 14:00."),
            Conflito(RecursoAgenda.Bloqueio, "Feriado"),
            Conflito(RecursoAgenda.Sala, "A sala 1 já está ocupada.")
        ]);

        avisos.Should().HaveCount(3);
        avisos[0].Should().Be(new AvisoDeChoque("Feriado", true));
        avisos.Skip(1).Should().OnlyContain(a => !a.Grave);
    }

    /// <summary>
    /// Dois horários no mesmo intervalo produzem a MESMA frase (o que muda é o id do
    /// agendamento, que não vai para a tela). Repetida, ela é a linha que faz ninguém ler
    /// a de baixo.
    /// </summary>
    [Fact]
    public void Frase_repetida_aparece_uma_vez_so()
    {
        var avisos = AvisosDeChoque.Montar(
        [
            Conflito(RecursoAgenda.Profissional, "Ana já atende às 14:00."),
            Conflito(RecursoAgenda.Profissional, "Ana já atende às 14:00.")
        ]);

        avisos.Should().ContainSingle();
    }

    /// <summary>
    /// A ordem entre iguais é preservada — <c>OrderByDescending</c> é estável. Sem isso,
    /// dois avisos da mesma gravidade poderiam trocar de lugar entre uma tecla e outra, e
    /// lista que se remexe sozinha é lista que se relê inteira a cada mudança.
    /// </summary>
    [Fact]
    public void Entre_iguais_a_ordem_de_chegada_e_preservada()
    {
        var avisos = AvisosDeChoque.Montar(
        [
            Conflito(RecursoAgenda.Profissional, "primeiro"),
            Conflito(RecursoAgenda.Sala, "segundo"),
            Conflito(RecursoAgenda.Paciente, "terceiro")
        ]);

        avisos.Select(a => a.Texto).Should().Equal("primeiro", "segundo", "terceiro");
    }

    /// <summary>
    /// A frase que DIZ que nada impede. Ela é a metade que impede a tela de se
    /// contradizer: tarja de aviso com o botão "Lançar" aceso ao lado, sem ela, manda a
    /// recepcionista procurar o que fazer para o aviso sumir — e o que ela vai achar é a
    /// caixinha de encaixe, marcando como encaixe o que não é.
    /// </summary>
    [Fact]
    public void O_cabecalho_diz_que_nada_impede()
        => AvisosDeChoque.Cabecalho.Should().Contain("nenhum impede");

    [Fact]
    public void Sem_conflito_nao_ha_aviso()
        => AvisosDeChoque.Montar([]).Should().BeEmpty();
}
