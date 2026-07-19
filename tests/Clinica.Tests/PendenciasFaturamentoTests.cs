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

/// <summary>Pendências novas do módulo de faturamento: prazo de recurso de glosa e carteirinhas a vencer.</summary>
public class PendenciasFaturamentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PendenciaService _pendencias;

    public PendenciasFaturamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _pendencias = new PendenciaService(_repo);
    }

    private async Task<CodigoFaturamento> CriarGlosadaAsync(DateOnly dataGlosa, int prazoDias)
    {
        var p = new Paciente { Nome = "P", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, dataGlosa.AddDays(-5), ModalidadeAtendimento.AcupunturaSimples);
        var codigo = r.Atendimento.Codigos.First();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, dataGlosa.AddDays(-4), "G-1", "sec", null);
        codigo.RegistrarGlosa(dataGlosa, "motivo", "1201", prazoDias);
        await _db.SaveChangesAsync();
        return codigo;
    }

    [Fact]
    public async Task GlosasARecorrer_SemaforoPorDiasRestantes()
    {
        var hoje = new DateOnly(2026, 7, 19);
        await CriarGlosadaAsync(hoje.AddDays(-28), 30); // restam 2 dias  → vermelho
        await CriarGlosadaAsync(hoje.AddDays(-25), 30); // restam 5 dias  → amarelo
        await CriarGlosadaAsync(hoje.AddDays(-5), 30);  // restam 25 dias → verde

        var lista = await _pendencias.GlosasARecorrerAsync(hoje);

        lista.Should().HaveCount(3);
        lista[0].Urgencia.Should().Be(NivelUrgencia.Vermelho);
        lista[0].DiasParaFimPrazo.Should().Be(2);
        lista[1].Urgencia.Should().Be(NivelUrgencia.Amarelo);
        lista[2].Urgencia.Should().Be(NivelUrgencia.Verde);
        lista[0].MotivoResumo.Should().Contain("Carteirinha"); // descrição do 1201 da tabela ANS

        // Badge: só as com prazo apertado (amarelo/vermelho) contam.
        (await _pendencias.TotalPendenciasAsync(hoje)).Should().Be(2);
    }

    [Fact]
    public async Task GlosaRecuperada_SaiDasPendenciasDeRecurso()
    {
        var hoje = new DateOnly(2026, 7, 19);
        var codigo = await CriarGlosadaAsync(hoje.AddDays(-28), 30);
        codigo.MarcarGlosaRecuperada();
        await _db.SaveChangesAsync();

        (await _pendencias.GlosasARecorrerAsync(hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task CarteirinhasAVencer_VencidaVermelha_VencendoAmarela_ForaDaJanelaNaoAparece()
    {
        var hoje = new DateOnly(2026, 7, 19);
        _db.Pacientes.AddRange(
            new Paciente { Nome = "Vencida", Convenio = Convenio.Amil, ValidadeCarteirinha = hoje.AddDays(-2) },
            new Paciente { Nome = "Vencendo", Convenio = Convenio.Amil, ValidadeCarteirinha = hoje.AddDays(10) },
            new Paciente { Nome = "Longe", Convenio = Convenio.Amil, ValidadeCarteirinha = hoje.AddDays(90) },
            new Paciente { Nome = "SemValidade", Convenio = Convenio.Amil });
        await _db.SaveChangesAsync();

        var lista = await _pendencias.CarteirinhasAVencerAsync(hoje);

        lista.Should().HaveCount(2);
        lista[0].PacienteNome.Should().Be("Vencida");
        lista[0].Urgencia.Should().Be(NivelUrgencia.Vermelho);
        lista[0].DiasParaVencer.Should().Be(-2);
        lista[1].PacienteNome.Should().Be("Vencendo");
        lista[1].Urgencia.Should().Be(NivelUrgencia.Amarelo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
