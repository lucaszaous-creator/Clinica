using Clinica.Application.Modelos;
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
/// Bloqueio de agenda: férias, feriado, folga, sala em manutenção.
///
/// O que estes testes protegem:
///
/// 1. **A agenda RECUSA marcação dentro do bloqueio** — era isso que faltava; até aqui
///    o que segurava era a memória de quem está no balcão.
/// 2. **O encaixe continua furando**, como em todo o resto da agenda: quem assume
///    atender no feriado assume por escrito.
/// 3. **Bloquear NÃO desmarca ninguém.** Fechar o Natal depois de alguém ter marcado no
///    dia 25 devolve a lista de quem está marcado, para a recepção remarcar — a sessão
///    não pode sumir do sistema sem ninguém avisar o paciente.
/// 4. **Sem profissional e sem sala, o bloqueio é da clínica** e alcança todo mundo;
///    com profissional, alcança só a agenda dele.
/// </summary>
public class BloqueioAgendaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly BloqueioAgendaService _bloqueios;

    private static readonly DateTime Manha = new(2026, 8, 10, 9, 0, 0);

    public BloqueioAgendaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _bloqueios = new BloqueioAgendaService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync(string nome = "Dra. Ana")
    {
        var p = new Profissional { Nome = nome, RegistroConselho = "CRM-SP 1234" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Agenda_recusa_marcacao_dentro_do_bloqueio()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Férias", profissionalId: profissionalId);

        var marcar = () => _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: profissionalId);

        await marcar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Férias*");
    }

    [Fact]
    public async Task Encaixe_fura_o_bloqueio()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Feriado", profissionalId: profissionalId);

        // Quem assume atender no feriado assume por escrito — e fica registrado.
        var agendamento = await _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: profissionalId, encaixe: true);

        agendamento.Encaixe.Should().BeTrue();
    }

    [Fact]
    public async Task Bloqueio_da_clinica_alcanca_quem_nao_informa_profissional()
    {
        var pacienteId = await CriarPacienteAsync();

        // Sem profissional e sem sala: feriado da clínica inteira.
        await _bloqueios.CriarAsync(Manha.Date, Manha.Date.AddDays(1), "Natal");

        var marcar = () => _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null);

        await marcar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Natal*");
    }

    [Fact]
    public async Task Bloqueio_de_um_profissional_nao_fecha_a_agenda_do_outro()
    {
        var pacienteId = await CriarPacienteAsync();
        var deFerias = await CriarProfissionalAsync("Dra. Ana");
        var trabalhando = await CriarProfissionalAsync("Dr. Bruno");

        await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Férias", profissionalId: deFerias);

        var agendamento = await _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: trabalhando);

        agendamento.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Fora_do_intervalo_nao_bloqueia()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        // Fecha só a manhã; a tarde continua aberta.
        await _bloqueios.CriarAsync(
            Manha.Date.AddHours(8), Manha.Date.AddHours(12), "Congresso",
            profissionalId: profissionalId);

        var agendamento = await _agenda.AgendarAsync(
            pacienteId, Manha.Date.AddHours(14), ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: profissionalId);

        agendamento.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Bloquear_nao_desmarca_quem_ja_estava_marcado()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var jaMarcado = await _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: profissionalId);

        var resultado = await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Férias", profissionalId: profissionalId);

        // A sessão continua na agenda: quem desmarca avisa o paciente.
        resultado.TemConflito.Should().BeTrue();
        resultado.JaMarcados.Should().ContainSingle();
        resultado.JaMarcados[0].Id.Should().Be(jaMarcado.Id);

        (await _agenda.DoDiaAsync(DateOnly.FromDateTime(Manha))).Should().ContainSingle();
    }

    [Fact]
    public async Task Bloqueio_sem_motivo_e_recusado()
    {
        var criar = () => _bloqueios.CriarAsync(Manha.Date, Manha.Date.AddDays(1), "   ");

        await criar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*por que*");
    }

    [Fact]
    public async Task Fim_antes_do_inicio_e_recusado()
    {
        var criar = () => _bloqueios.CriarAsync(Manha.AddHours(2), Manha, "Férias");

        await criar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depois do início*");
    }

    [Fact]
    public async Task Conflito_de_bloqueio_aparece_na_conferencia_da_tela()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Feriado", profissionalId: profissionalId);

        var conflitos = await _agenda.ConflitosAsync(
            Manha, duracaoMinutos: 30, profissionalId: profissionalId, pacienteId: pacienteId);

        conflitos.Should().ContainSingle(c => c.Recurso == RecursoAgenda.Bloqueio);
    }

    [Fact]
    public async Task Reabrir_apaga_o_bloqueio_e_a_agenda_volta_a_aceitar()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var criado = await _bloqueios.CriarAsync(
            Manha.Date, Manha.Date.AddDays(1), "Férias", profissionalId: profissionalId);

        await _bloqueios.ExcluirAsync(criado.Bloqueio.Id, "ana");

        var agendamento = await _agenda.AgendarAsync(
            pacienteId, Manha, ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: profissionalId);

        agendamento.Id.Should().BeGreaterThan(0);
        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "AgendaReaberta" && e.Operador == "ana");
    }

    [Fact]
    public async Task Lista_ignora_o_que_ja_passou_por_padrao()
    {
        await _bloqueios.CriarAsync(
            DateTime.Today.AddDays(-30), DateTime.Today.AddDays(-29), "Férias do ano passado");
        await _bloqueios.CriarAsync(
            DateTime.Today.AddDays(30), DateTime.Today.AddDays(31), "Congresso");

        // A tela responde "o que está fechado daqui para a frente"; o passado é histórico.
        (await _bloqueios.ListarAsync()).Should().ContainSingle()
            .Which.Motivo.Should().Be("Congresso");

        (await _bloqueios.ListarAsync(incluirPassado: true)).Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
