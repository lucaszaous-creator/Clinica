using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

// Fase 2 (docs/crm-na-recepcao.md): a mensagem que a equipe mandou pelo CRM entra no histórico de
// cada acompanhamento ABERTO do paciente, com quem mandou — sem mexer em etapa, data nem versão.
public sealed class PonteClinicContactTests : IDisposable
{
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicContactBridge bridge;

    public PonteClinicContactTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        bridge = new(db);
    }

    async Task<(Paciente P, AcompanhamentoPaciente Recall, AcompanhamentoPaciente Encerrado)> Casos()
    {
        var p = new Paciente { Nome = "Pessoa fictícia", Telefone = "22999422253" };
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var recall = new AcompanhamentoPaciente { Paciente = p, Tipo = TipoAcompanhamento.Recall, Modalidade = ModalidadeAtendimento.BsvApenas, ReferenciaEm = DateTime.Today.AddDays(-40), ProximoContato = hoje };
        var encerrado = new AcompanhamentoPaciente { Paciente = p, Tipo = TipoAcompanhamento.NovoBsv, Modalidade = ModalidadeAtendimento.BsvApenas, ReferenciaEm = DateTime.Today.AddDays(-90), ProximoContato = hoje, EncerradoEm = DateTime.Now, MotivoEncerramento = "Mudou de cidade" };
        db.AddRange(recall, encerrado);
        await db.SaveChangesAsync();
        return (p, recall, encerrado);
    }

    static ClinicContactNotice Aviso(int paciente, string canal = "whatsapp")
        => new(Guid.NewGuid().ToString("N"), paciente, DateTimeOffset.UtcNow, canal, "Ana do balcão");

    [Fact] public async Task Mensagem_da_equipe_entra_so_no_caso_aberto_e_uma_vez()
    {
        var (p, recall, _) = await Casos();
        var versao = recall.Versao;
        var aviso = Aviso(p.Id);
        Assert.Equal("recorded", (await bridge.Contact(aviso, default)).Status);
        await bridge.Contact(aviso, default);
        var c = Assert.Single(await db.ContatosAcompanhamento.AsNoTracking().ToListAsync());
        Assert.Equal(recall.Id, c.AcompanhamentoPacienteId);
        Assert.Equal((CanalContato.WhatsApp, "Ana do balcão · CRM · WhatsApp"), (c.Canal, c.Operador));
        var depois = await db.Acompanhamentos.AsNoTracking().SingleAsync(a => a.Id == recall.Id);
        Assert.Equal((versao, EtapaAcompanhamento.AContatar), (depois.Versao, depois.Etapa));
    }

    [Fact] public async Task Pelo_SMS_fica_como_SMS()
    {
        var (p, _, _) = await Casos();
        await bridge.Contact(Aviso(p.Id, "sms"), default);
        Assert.Equal(CanalContato.Sms, (await db.ContatosAcompanhamento.SingleAsync()).Canal);
    }

    [Fact] public async Task Sem_caso_aberto_nao_e_erro()
    {
        var p = new Paciente { Nome = "Sem caso", Telefone = "22999990000" };
        db.Pacientes.Add(p); await db.SaveChangesAsync();
        Assert.Equal("skipped", (await bridge.Contact(Aviso(p.Id), default)).Status);
        Assert.Empty(await db.ContatosAcompanhamento.ToListAsync());
    }

    [Fact] public async Task Registro_invalido_e_recusado()
    {
        var (p, _, _) = await Casos();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Contact(Aviso(p.Id) with { CommandId = "x" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Contact(Aviso(p.Id, "email"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Contact(Aviso(p.Id) with { SentAt = DateTimeOffset.UtcNow.AddHours(1) }, default));
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
