using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A chamada do profissional (parcela 38) — o recado que atravessa do consultório ao balcão.
///
/// Não há sincronização entre os dois módulos: eles leem a mesma linha do banco. Por isso
/// o que estes testes fixam é o ESTADO da linha e a etapa derivada dela — se a etapa
/// estiver certa, os dois quadros mostram a mesma coisa por construção, e se ela estiver
/// errada os dois mostram a mesma coisa errada.
/// </summary>
public class ChamadaDoProfissionalTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AgendaService _agenda;

    public ChamadaDoProfissionalTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(repo, new AtendimentoService(repo));
    }

    private async Task<Agendamento> AgendarAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        return await _agenda.AgendarAsync(
            p.Id, DateTime.Today.AddHours(14), ModalidadeAtendimento.AcupunturaComEletro, null);
    }

    [Fact]
    public async Task Chamar_MoveOCartaoParaAColunaChamado()
    {
        var ag = await AgendarAsync();
        await _agenda.RegistrarChegadaAsync(ag.Id);

        await _agenda.ChamarAsync(ag.Id);

        var depois = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        depois.ChamadoEm.Should().NotBeNull();
        depois.Etapa.Should().Be(EtapaFila.Chamado);
    }

    /// <summary>
    /// Chamar quem não fez check-in mandaria a recepção anunciar um nome para uma sala de
    /// espera onde a pessoa não está — e a fila perderia a informação que a torna útil.
    /// </summary>
    [Fact]
    public async Task Chamar_SemCheckIn_Recusa()
    {
        var ag = await AgendarAsync();

        var acao = () => _agenda.ChamarAsync(ag.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*check-in*");
    }

    /// <summary>
    /// Insistir não pode zerar o cronômetro: quem cobra o balcão precisa ver há QUANTO
    /// tempo chamou, e é justamente o caso demorado que o segundo clique esconderia.
    /// </summary>
    [Fact]
    public async Task Chamar_DuasVezes_NaoReiniciaOCronometro()
    {
        var ag = await AgendarAsync();
        await _agenda.RegistrarChegadaAsync(ag.Id);

        var primeira = DateTime.Today.AddHours(14).AddMinutes(5);
        await _agenda.ChamarAsync(ag.Id, primeira);
        await _agenda.ChamarAsync(ag.Id, primeira.AddMinutes(7));

        var depois = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        depois.ChamadoEm.Should().Be(primeira);
        depois.ChamadoHaMinutos(primeira.AddMinutes(7)).Should().Be(7);
    }

    [Fact]
    public async Task DesfazerChamada_DevolveOCartaoParaARecepcao()
    {
        var ag = await AgendarAsync();
        await _agenda.RegistrarChegadaAsync(ag.Id);
        await _agenda.ChamarAsync(ag.Id);

        await _agenda.DesfazerChamadaAsync(ag.Id);

        var depois = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        depois.ChamadoEm.Should().BeNull();
        depois.Etapa.Should().Be(EtapaFila.Chegou);
        depois.ChegadaEm.Should().NotBeNull("desfazer a chamada não desfaz o check-in");
    }

    [Fact]
    public async Task DesfazerChamada_ComOAtendimentoJaIniciado_Recusa()
    {
        var ag = await AgendarAsync();
        await _agenda.RegistrarChegadaAsync(ag.Id);
        await _agenda.IniciarAtendimentoAsync(ag.Id);

        var acao = () => _agenda.DesfazerChamadaAsync(ag.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// O balcão pode levar o paciente direto para a sala (o profissional avisou pela
    /// porta). Uma linha do tempo com entrada e sem chamada não existe, então a chamada
    /// é carimbada junto — senão o histórico diria que a pessoa entrou sem ser chamada.
    /// </summary>
    [Fact]
    public async Task IniciarAtendimento_SemChamadaAnterior_CarimbaAChamadaJunto()
    {
        var ag = await AgendarAsync();
        var agora = DateTime.Today.AddHours(14).AddMinutes(3);

        await _agenda.IniciarAtendimentoAsync(ag.Id, agora);

        var depois = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        depois.ChegadaEm.Should().Be(agora);
        depois.ChamadoEm.Should().Be(agora);
        depois.InicioAtendimentoEm.Should().Be(agora);
        depois.Etapa.Should().Be(EtapaFila.EmAtendimento);
    }

    /// <summary>Já chamado, entrar não pode reescrever a hora em que foi chamado.</summary>
    [Fact]
    public async Task IniciarAtendimento_DepoisDaChamada_PreservaAHoraDaChamada()
    {
        var ag = await AgendarAsync();
        var chegada = DateTime.Today.AddHours(13).AddMinutes(50);
        var chamada = DateTime.Today.AddHours(14);

        await _agenda.RegistrarChegadaAsync(ag.Id, chegada);
        await _agenda.ChamarAsync(ag.Id, chamada);
        await _agenda.IniciarAtendimentoAsync(ag.Id, chamada.AddMinutes(2));

        var depois = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        depois.ChamadoEm.Should().Be(chamada);
    }

    /// <summary>
    /// O relógio da chamada para quando a pessoa entra: continuar contando faria o balcão
    /// ver "chamado há 40 min" de alguém que está na sala desde o minuto 4.
    /// </summary>
    [Fact]
    public async Task ChamadoHaMinutos_ParaDeCorrerQuandoOPacienteEntra()
    {
        var ag = await AgendarAsync();
        var chamada = DateTime.Today.AddHours(14);

        await _agenda.RegistrarChegadaAsync(ag.Id, chamada.AddMinutes(-10));
        await _agenda.ChamarAsync(ag.Id, chamada);

        var chamado = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        chamado.ChamadoHaMinutos(chamada.AddMinutes(6)).Should().Be(6);

        await _agenda.IniciarAtendimentoAsync(ag.Id, chamada.AddMinutes(6));

        var dentro = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        dentro.ChamadoHaMinutos(chamada.AddMinutes(40)).Should().BeNull();
    }

    /// <summary>
    /// A espera também para na CHAMADA, não na entrada: o que se mede é quanto tempo o
    /// paciente ficou sem notícia. Contar até ele levantar da cadeira somaria à espera o
    /// tempo de atravessar a sala, e faria a clínica parecer pior do que é.
    /// </summary>
    [Fact]
    public async Task EsperaMinutos_ParaNaChamada()
    {
        var ag = await AgendarAsync();
        var chegada = DateTime.Today.AddHours(14);

        await _agenda.RegistrarChegadaAsync(ag.Id, chegada);
        await _agenda.ChamarAsync(ag.Id, chegada.AddMinutes(12));

        var depois = await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id);
        depois.EsperaMinutos(chegada.AddMinutes(30)).Should().Be(12);
    }

    /// <summary>
    /// Voltar etapa desce UMA coluna por clique, agora passando pela chamada. Pular a
    /// coluna nova mandaria quem clicou errado de volta ao check-in de uma vez.
    /// </summary>
    [Fact]
    public async Task VoltarEtapa_DesceUmaColunaPorVez()
    {
        var ag = await AgendarAsync();
        await _agenda.RegistrarChegadaAsync(ag.Id);
        await _agenda.IniciarAtendimentoAsync(ag.Id);

        (await _agenda.VoltarEtapaAsync(ag.Id)).Etapa.Should().Be(EtapaFila.Chamado);
        (await _agenda.VoltarEtapaAsync(ag.Id)).Etapa.Should().Be(EtapaFila.Chegou);
        (await _agenda.VoltarEtapaAsync(ag.Id)).Etapa.Should().Be(EtapaFila.Aguardando);

        var acao = () => _agenda.VoltarEtapaAsync(ag.Id);
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// O CIRCUITO: o que o consultório grava é o que o balcão lê. Não há mensagem entre
    /// os módulos — a prova de que o recado chega é a etapa da MESMA linha.
    /// </summary>
    [Fact]
    public async Task ConsultorioChama_ERecepcaoVeNaListaDoDia()
    {
        var ag = await AgendarAsync();
        await _agenda.RegistrarChegadaAsync(ag.Id);

        // Lado do consultório.
        await _agenda.ChamarAsync(ag.Id);

        // Lado do balcão: a MESMA consulta que a fila usa.
        var doDia = await _agenda.DoDiaAsync(DateOnly.FromDateTime(DateTime.Today));

        doDia.Should().ContainSingle()
            .Which.Etapa.Should().Be(EtapaFila.Chamado);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
