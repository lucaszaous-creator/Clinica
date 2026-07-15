using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>Testes das correções: estorno de baixa, baixa unitária (regressão) e busca de pacientes.</summary>
public class CorrecoesIntegracaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public CorrecoesIntegracaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Fulano", string? doc = null,
        Convenio convenio = Convenio.UnimedIntercambio)
    {
        var p = new Paciente { Nome = nome, Documento = doc, Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task EstornarBaixa_ReabreAPendencia()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimentos = new AtendimentoService(_repo);
        var faturamento = new FaturamentoService(_repo);

        var dia = new DateOnly(2026, 7, 10);
        var r = await atendimentos.LancarAsync(pacienteId, dia, ModalidadeAtendimento.AcupunturaComEletro);
        var codigo = r.Atendimento.Codigos.First();

        await faturamento.DarBaixaAsync(codigo.Id, dia, "G-1", "sec", null);
        (await _db.Codigos.FindAsync(codigo.Id))!.Baixado.Should().BeTrue();

        await faturamento.EstornarBaixaAsync(codigo.Id, "baixa por engano", "sec");

        var estornado = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        estornado.Baixado.Should().BeFalse();
        estornado.Status.Should().Be(StatusCodigo.Aberto);
        estornado.NumeroGuiaReal.Should().BeNull();
        estornado.ObservacaoBaixa.Should().Contain("Estornado");
    }

    [Fact]
    public async Task DarBaixa_AfetaApenasUmCodigo_NaoTodos()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimentos = new AtendimentoService(_repo);
        var faturamento = new FaturamentoService(_repo);

        // Acu+Eletro gera 2 códigos abertos (acupuntura hoje, eletro +24h).
        var r = await atendimentos.LancarAsync(pacienteId, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        r.Atendimento.Codigos.Should().HaveCount(2);
        var alvo = r.Atendimento.Codigos.First();
        var outro = r.Atendimento.Codigos.Last();

        await faturamento.DarBaixaAsync(alvo.Id, new DateOnly(2026, 7, 11), "G-1", "sec", null);

        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == alvo.Id)).Baixado.Should().BeTrue();
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == outro.Id)).Baixado
            .Should().BeFalse("dar baixa em uma guia não pode baixar as demais");
    }

    [Fact]
    public async Task BuscarPacientes_PorNomeEPorCpf()
    {
        await CriarPacienteAsync("Maria Silva", "52998224725");
        await CriarPacienteAsync("João Souza", "11144477735");

        (await _repo.BuscarPacientesAsync("maria")).Should().ContainSingle(p => p.Nome == "Maria Silva");
        (await _repo.BuscarPacientesAsync("SILVA")).Should().ContainSingle(p => p.Nome == "Maria Silva");
        // por CPF com pontuação (normaliza para dígitos)
        (await _repo.BuscarPacientesAsync("529.982")).Should().ContainSingle(p => p.Nome == "Maria Silva");
        // termo vazio devolve todos
        (await _repo.BuscarPacientesAsync("")).Should().HaveCount(2);
    }

    [Fact]
    public async Task PacienteService_RejeitaCpfInvalido()
    {
        var service = new PacienteService(_repo);
        var acao = () => service.SalvarNovoAsync(new Paciente { Nome = "X", Documento = "123", Convenio = Convenio.Amil });
        await acao.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
