using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests.PonteCrm;

// Out/2026: o paciente pede pelo WhatsApp para cancelar, a recepção liga e tenta reverter.
// Quando ele MANTÉM o horário, o pedido sai da agenda e do sino, e a trilha guarda o que houve.
public sealed class PacienteManteveOHorarioTests : IDisposable
{
    readonly SqliteConnection connection;
    readonly ClinicaDbContext db;
    readonly ClinicAgendaBridge bridge;
    readonly CampanhaService campanha;

    public PacienteManteveOHorarioTests()
    {
        connection = new("Data Source=:memory:"); connection.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(db);
        bridge = new(db, new AgendaService(repo, new AtendimentoService(repo)));
        campanha = new CampanhaService(repo);
    }

    async Task<Agendamento> Seed()
    {
        var p = new Paciente { Nome = "Pessoa fictícia", Telefone = "(11) 99999-0001" };
        db.Pacientes.Add(p);
        var a = new Agendamento { Paciente = p, DataHora = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime.AddHours(23) };
        db.Agendamentos.Add(a); await db.SaveChangesAsync(); return a;
    }

    ClinicDecision Pedido(Agendamento a, string decisao) =>
        new(Guid.NewGuid().ToString("N"), a.Id, ClinicAgendaBridge.Revision(a), ClinicAgendaBridge.Phone(a.Paciente!.Telefone), decisao, "incoming-test");

    SituacaoConfirmacao Situacao()
    {
        var c = db.Contatos.AsNoTracking().Single();
        return RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("reschedule")]
    public async Task Paciente_que_manteve_o_horario_passa_a_confirmado_com_trilha(string decisao)
    {
        var a = await Seed();
        await bridge.Decide(Pedido(a, decisao), default);
        Assert.NotEqual(SituacaoConfirmacao.Confirmou, Situacao());

        await campanha.ManterHorarioAsync(a.Id, "Ana Recepção");

        Assert.Equal(SituacaoConfirmacao.Confirmou, Situacao());
        Assert.Equal(StatusAgendamento.Agendado, (await db.Agendamentos.AsNoTracking().SingleAsync()).Status);
        var trilha = await db.Auditoria.AsNoTracking().SingleAsync(e => e.Acao == "PedidoDeCancelamentoRevertido");
        Assert.Equal("Ana Recepção", trilha.Operador);
        Assert.Equal(a.PacienteId, trilha.PacienteId);
        // Confirmação dada pela recepção não é notícia do sino.
        Assert.False(AvisosDeConfirmacao.RespondeuOPaciente(db.Contatos.AsNoTracking().Single().Comentario));
    }

    [Fact]
    public async Task Sem_pedido_em_aberto_recusa_dizendo_por_que()
    {
        var a = await Seed();
        await bridge.Decide(Pedido(a, "confirm"), default);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => campanha.ManterHorarioAsync(a.Id, "Ana"));
        Assert.Contains("pedido de cancelamento", ex.Message);
        Assert.Empty(await db.Auditoria.AsNoTracking().Where(e => e.Acao == "PedidoDeCancelamentoRevertido").ToListAsync());
    }

    [Fact]
    public async Task Horario_sem_resposta_do_paciente_recusa()
    {
        var a = await Seed();
        await Assert.ThrowsAsync<InvalidOperationException>(() => campanha.ManterHorarioAsync(a.Id, "Ana"));
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
