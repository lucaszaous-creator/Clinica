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
/// CRM do paciente (parcela 8): de onde ele veio, e o histórico do que a clínica já
/// falou com ele.
///
/// A regra que carrega este arquivo: **null é "ninguém perguntou"**, e isso não pode
/// virar "Outro". A direção decide onde investir em cima deste número — um padrão
/// preenchido sozinho daria à carteira inteira já cadastrada uma origem inventada, e o
/// relatório do primeiro mês seria mentira com aparência de dado.
/// </summary>
public class CrmPacienteTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PacienteService _pacientes;
    private readonly CampanhaService _campanhas;

    private static readonly DateOnly Hoje = new(2026, 8, 20);

    public CrmPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _pacientes = new PacienteService(_repo);
        _campanhas = new CampanhaService(_repo);
    }

    private async Task<Paciente> CriarAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<ContatoCampanha> ContatoAsync(
        int pacienteId, TipoContato tipo, DateOnly referencia, string origem)
    {
        var c = new ContatoCampanha
        {
            PacienteId = pacienteId,
            Tipo = tipo,
            Referencia = referencia,
            Origem = origem
        };
        _db.Contatos.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    // ==================== Origem ====================

    [Fact]
    public async Task PacienteNovo_NasceSemOrigem()
    {
        var p = await CriarAsync();

        // Não é "Outro", não é "Indicação": é null — ninguém perguntou ainda.
        (await _repo.ObterPacienteAsync(p.Id))!.Origem.Should().BeNull();
    }

    [Fact]
    public async Task Origem_EIndicacao_SaoGravadasEVoltamJuntas()
    {
        var p = await CriarAsync();
        p.Origem = OrigemPaciente.Indicacao;
        p.IndicadoPor = "Dra. Helena (ortopedia)";
        await _pacientes.AtualizarAsync(p);

        var lido = await _repo.ObterPacienteAsync(p.Id);
        lido!.Origem.Should().Be(OrigemPaciente.Indicacao);
        lido.IndicadoPor.Should().Be("Dra. Helena (ortopedia)");
    }

    [Fact]
    public async Task Origem_NaoQuebraOQueJaEstavaCadastrado()
    {
        // A migration é aditiva e as colunas são anuláveis: o paciente cadastrado antes
        // da parcela continua carregando e salvando como sempre.
        var p = await CriarAsync("Paciente antigo");
        p.Telefone = "21999998888";
        await _pacientes.AtualizarAsync(p);

        var lido = await _repo.ObterPacienteAsync(p.Id);
        lido!.Telefone.Should().Be("21999998888");
        lido.Origem.Should().BeNull();
        lido.IndicadoPor.Should().BeNull();
    }

    // ==================== Histórico de contatos ====================

    [Fact]
    public async Task HistoricoDoPaciente_VemDoMaisNovoParaOMaisVelho()
    {
        var p = await CriarAsync();
        await ContatoAsync(p.Id, TipoContato.Recall, Hoje.AddDays(-60), "REC:1:2026-06");
        await ContatoAsync(p.Id, TipoContato.Nps, Hoje.AddDays(-2), "ATD:10");
        await ContatoAsync(p.Id, TipoContato.ConfirmacaoSessao, Hoje.AddDays(-30), "AGD:7");

        var historico = await _campanhas.DoPacienteAsync(p.Id);

        // Fila e histórico se leem em ordens opostas: a tela de campanhas trabalha o
        // pendente primeiro; a ficha quer a última conversa no topo.
        historico.Select(c => c.Tipo).Should().ContainInOrder(
            TipoContato.Nps, TipoContato.ConfirmacaoSessao, TipoContato.Recall);
    }

    [Fact]
    public async Task HistoricoDoPaciente_NaoTrazContatoDeOutroPaciente()
    {
        var maria = await CriarAsync("Maria");
        var joao = await CriarAsync("João");
        await ContatoAsync(maria.Id, TipoContato.Nps, Hoje, "ATD:1");
        await ContatoAsync(joao.Id, TipoContato.Nps, Hoje, "ATD:2");

        var historico = await _campanhas.DoPacienteAsync(maria.Id);

        historico.Should().ContainSingle().Which.PacienteId.Should().Be(maria.Id);
    }

    [Fact]
    public async Task HistoricoDoPaciente_RespeitaOLimite()
    {
        var p = await CriarAsync();
        for (var i = 1; i <= 8; i++)
            await ContatoAsync(p.Id, TipoContato.Recall, Hoje.AddDays(-i), $"REC:{p.Id}:{i}");

        var historico = await _campanhas.DoPacienteAsync(p.Id, limite: 3);

        // O corte vai no SQL: abrir a ficha não pode arrastar toda a conversa desde a
        // instalação pela rede — o banco é remoto.
        historico.Should().HaveCount(3);
        historico[0].Referencia.Should().Be(Hoje.AddDays(-1));
    }

    [Fact]
    public async Task HistoricoDoPaciente_SemContato_VemVazio()
        => (await _campanhas.DoPacienteAsync((await CriarAsync()).Id)).Should().BeEmpty();

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
