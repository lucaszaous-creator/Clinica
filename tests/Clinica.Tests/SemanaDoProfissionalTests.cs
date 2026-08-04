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
/// A semana do profissional (parcela 39) — a leitura que o consultório não tinha.
/// </summary>
public class SemanaDoProfissionalTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AgendaService _agenda;
    private readonly ConsultorioService _consultorio;

    // Uma quarta-feira, para provar que a semana volta para a segunda.
    private static readonly DateOnly Quarta = new(2026, 8, 5);

    public SemanaDoProfissionalTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(repo, new AtendimentoService(repo));
        _consultorio = new ConsultorioService(repo);
    }

    private async Task<int> PacienteAsync(string nome = "Paciente")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task AgendarAsync(int pacienteId, DateOnly dia, int hora)
        => _agenda.AgendarAsync(
            pacienteId, dia.ToDateTime(new TimeOnly(hora, 0)),
            ModalidadeAtendimento.AcupunturaSimples, null);

    /// <summary>
    /// A semana começa na SEGUNDA, seja qual for o dia consultado — a clínica pensa a
    /// semana em bloco, e começar no dia escolhido daria uma janela diferente por clique.
    /// </summary>
    [Fact]
    public async Task Semana_ComecaNaSegunda_ETemSeteDias()
    {
        var semana = await _consultorio.DaSemanaAsync(Quarta, null);

        semana.Inicio.DayOfWeek.Should().Be(DayOfWeek.Monday);
        semana.Inicio.Should().Be(new DateOnly(2026, 8, 3));
        semana.Fim.Should().Be(new DateOnly(2026, 8, 9));
        semana.Dias.Should().HaveCount(7);
    }

    /// <summary>
    /// O domingo é o SÉTIMO dia, não o primeiro. Sem esse cuidado, olhar um domingo
    /// devolveria a semana que só começa no dia seguinte.
    /// </summary>
    [Fact]
    public async Task Semana_DoDomingo_VoltaParaASegundaAnterior()
    {
        var domingo = new DateOnly(2026, 8, 9);

        var semana = await _consultorio.DaSemanaAsync(domingo, null);

        semana.Inicio.Should().Be(new DateOnly(2026, 8, 3));
        semana.Fim.Should().Be(domingo);
    }

    /// <summary>Dia sem horário ENTRA na lista, vazio — semana com buraco faria procurar o dia que sumiu.</summary>
    [Fact]
    public async Task Semana_DiaSemHorario_ApareceVazio()
    {
        var pacienteId = await PacienteAsync();
        await AgendarAsync(pacienteId, Quarta, 14);

        var semana = await _consultorio.DaSemanaAsync(Quarta, null);

        semana.Dias.Should().HaveCount(7);
        semana.Dias.Count(d => d.Sessoes.Count == 0).Should().Be(6);
        semana.Dias.Single(d => d.Dia == Quarta).Sessoes.Should().ContainSingle();
    }

    [Fact]
    public async Task Semana_AgrupaCadaSessaoNoSeuDia_EOrdenaPorHorario()
    {
        var pacienteId = await PacienteAsync();
        await AgendarAsync(pacienteId, Quarta, 16);
        await AgendarAsync(pacienteId, Quarta, 9);
        await AgendarAsync(pacienteId, Quarta.AddDays(1), 10);

        var semana = await _consultorio.DaSemanaAsync(Quarta, null);

        semana.Sessoes.Should().Be(3);

        var quarta = semana.Dias.Single(d => d.Dia == Quarta);
        quarta.Sessoes.Select(s => s.DataHora.Hour).Should().ContainInOrder(9, 16);
    }

    /// <summary>
    /// Sessões e pessoas não são a mesma leitura de carga: três sessões do mesmo paciente
    /// é tratamento em série, três de três é primeira consulta atrás de primeira consulta.
    /// </summary>
    [Fact]
    public async Task Semana_ContaPacientesDistintos_NaoSessoes()
    {
        var ana = await PacienteAsync("Ana");
        var bruno = await PacienteAsync("Bruno");

        await AgendarAsync(ana, Quarta, 9);
        await AgendarAsync(ana, Quarta.AddDays(1), 9);
        await AgendarAsync(bruno, Quarta, 10);

        var semana = await _consultorio.DaSemanaAsync(Quarta, null);

        semana.Sessoes.Should().Be(3);
        semana.PacientesDistintos.Should().Be(2);
    }

    /// <summary>Semana vazia não tem "dia mais cheio" — é nulo, nunca um dia com zero sessões.</summary>
    [Fact]
    public async Task Semana_Vazia_NaoTemDiaMaisCheio()
    {
        var semana = await _consultorio.DaSemanaAsync(Quarta, null);

        semana.Sessoes.Should().Be(0);
        semana.DiaMaisCheio.Should().BeNull();
    }

    [Fact]
    public async Task Semana_DiaMaisCheio_EhOQueTemMaisSessoes()
    {
        var pacienteId = await PacienteAsync();
        await AgendarAsync(pacienteId, Quarta, 9);
        await AgendarAsync(pacienteId, Quarta.AddDays(1), 9);
        await AgendarAsync(pacienteId, Quarta.AddDays(1), 10);

        var semana = await _consultorio.DaSemanaAsync(Quarta, null);

        semana.DiaMaisCheio!.Dia.Should().Be(Quarta.AddDays(1));
    }

    /// <summary>Com profissional informado, a semana é DELE — não a da clínica.</summary>
    [Fact]
    public async Task Semana_ComProfissional_FiltraSoOsDele()
    {
        var profissional = new Profissional { Nome = "Dra. Ana" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        var pacienteId = await PacienteAsync();
        var meu = await _agenda.AgendarAsync(
            pacienteId, Quarta.ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaSimples, null);
        meu.ProfissionalId = profissional.Id;

        // Um horário sem profissional, que não pode aparecer na semana de ninguém.
        await AgendarAsync(pacienteId, Quarta, 11);
        await _db.SaveChangesAsync();

        var semana = await _consultorio.DaSemanaAsync(Quarta, profissional.Id);

        semana.Sessoes.Should().Be(1);
        semana.ProfissionalNome.Should().Contain("Ana");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
