using Clinica.Application.Modelos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

// "Quem é o paciente desta conversa?" — o telefone não é identidade; a ponte devolve as fichas
// candidatas com o que permite conferir e o que vem pela frente (out/2026).
public sealed class PonteClinicPatientTests : IDisposable
{
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicPatientBridge bridge;

    public PonteClinicPatientTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        bridge = new(db);
    }

    Paciente Paciente(string nome, string? fone, string? doc = null)
    {
        var p = new Paciente { Nome = nome, Telefone = fone, Documento = doc, DataNascimento = new DateOnly(1980, 5, 2) };
        db.Pacientes.Add(p); db.SaveChanges(); return p;
    }

    [Fact] public async Task Mesmo_celular_traz_as_duas_fichas_com_o_que_permite_conferir()
    {
        var mae = Paciente("Maria Mãe", "(22) 99942-2253", "123.456.789-01");
        var filho = Paciente("João Filho", "22999422253");
        Paciente("Outra pessoa", "22999990000");
        var cards = await bridge.Lookup(new("5522999422253"), default);
        Assert.Equal(2, cards.Length);
        Assert.All(cards, c => Assert.True(c.PhoneMatch));
        var m = Assert.Single(cards, c => c.Id == mae.Id);
        Assert.Equal("901", m.DocumentEnd);
        Assert.Equal(new DateOnly(1980, 5, 2), m.BirthDate);
        Assert.Contains(cards, c => c.Id == filho.Id);
    }

    [Fact] public async Task Paciente_que_escreve_de_outro_numero_e_achado_pelo_nome_ou_documento()
    {
        var p = Paciente("Ana Beatriz Souza", "21988887777", "98765432100");
        Assert.Contains(await bridge.Lookup(new("5522911112222", "beatriz"), default), c => c.Id == p.Id && !c.PhoneMatch);
        Assert.Contains(await bridge.Lookup(new("", "876543"), default), c => c.Id == p.Id);
        Assert.Empty(await bridge.Lookup(new("", "an"), default)); // menos de 3 letras não busca o cadastro inteiro
    }

    [Fact] public async Task Proximo_horario_vem_com_a_resposta_ao_lembrete()
    {
        var p = Paciente("Confirmou", "22999422253");
        var ag = new Agendamento { PacienteId = p.Id, DataHora = DateTime.Now.AddDays(1), ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura };
        db.Agendamentos.Add(ag); db.SaveChanges();
        db.Contatos.Add(new ContatoCampanha { PacienteId = p.Id, Tipo = TipoContato.ConfirmacaoSessao, AgendamentoId = ag.Id, Origem = "AGD:" + ag.Id,
            Status = StatusContato.Respondido, EnviadoEm = DateTime.Now.AddHours(-3), RespondidoEm = DateTime.Now.AddHours(-1), Comentario = RodadaDeConfirmacao.RespostaNaoVaiPeloLink });
        db.SaveChanges();
        var v = Assert.Single(Assert.Single(await bridge.Lookup(new("22999422253"), default)).Upcoming);
        Assert.Equal(ag.Id, v.AppointmentId); Assert.Equal("not-coming", v.Answer);
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
