using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>Consultas renováveis: registro/renovação e alarme de expiração (item 7).</summary>
public class ConsultaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConsultaService _consultas;

    public ConsultaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consultas = new ConsultaService(_repo); // sem Parâmetros → usa validades padrão do ConvenioInfo
    }

    private async Task<int> CriarPacienteAsync(Convenio convenio, string? convenioCodigo = null)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = convenio, ConvenioCodigo = convenioCodigo, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Registra no catálogo um convênio personalizado SEM consulta renovável.</summary>
    private static void RegistrarPersonalizadoSemConsulta(string codigo)
        => CatalogoConvenios.Atualizar(new[]
        {
            new EntradaConvenio(codigo, "Convênio Sem Consulta", Convenio.Personalizado, Ativo: true,
                new ConfiguracaoRegraGenerica { ValidadeConsultaDias = null })
        });

    [Fact]
    public async Task Renovar_CriaConsultaAtivaComVencimentoPelaValidade()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio); // validade 22 dias
        var emissao = new DateOnly(2026, 7, 1);

        var c = await _consultas.RenovarAsync(id, emissao);

        c.Status.Should().Be(StatusConsulta.Ativa);
        c.ValidadeDias.Should().Be(22);
        c.DataVencimento.Should().Be(emissao.AddDays(22));
    }

    [Fact]
    public async Task Renovar_DuasVezes_MarcaAnteriorComoRenovada()
    {
        var id = await CriarPacienteAsync(Convenio.Amil);
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 1));
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 20));

        var todas = await _repo.ConsultasDoPacienteAsync(id);
        todas.Should().HaveCount(2);
        todas.Count(c => c.Status == StatusConsulta.Ativa).Should().Be(1);
        todas.Count(c => c.Status == StatusConsulta.Renovada).Should().Be(1);
    }

    [Fact]
    public async Task Renovar_Petrobras_Usa30Dias()
    {
        var id = await CriarPacienteAsync(Convenio.Petrobras); // consulta renova a cada 30 dias
        var emissao = new DateOnly(2026, 7, 1);

        var c = await _consultas.RenovarAsync(id, emissao);

        c.ValidadeDias.Should().Be(30);
        c.DataVencimento.Should().Be(emissao.AddDays(30));
    }

    [Fact]
    public async Task Renovar_ConvenioSemConsulta_Lanca()
    {
        // Personalizado sem validade configurada no catálogo → não usa consulta renovável.
        RegistrarPersonalizadoSemConsulta("CVSEMCONS");
        var id = await CriarPacienteAsync(Convenio.Personalizado, "CVSEMCONS");
        var acao = () => _consultas.RenovarAsync(id, new DateOnly(2026, 7, 1));
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Listar_ConsultaVencida_EntraEmAlertaVermelho()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio); // 22 dias
        await _consultas.RenovarAsync(id, new DateOnly(2026, 6, 1));    // vence 23/06

        var lista = await _consultas.ListarAsync(new DateOnly(2026, 7, 16));

        var item = lista.Single(s => s.PacienteId == id);
        item.UsaConsulta.Should().BeTrue();
        item.Vencida.Should().BeTrue();
        item.PrecisaRenovar.Should().BeTrue();
        item.Urgencia.Should().Be(NivelUrgencia.Vermelho);
    }

    [Fact]
    public async Task Listar_ConvenioSemConsulta_NaoAlerta()
    {
        RegistrarPersonalizadoSemConsulta("CVSEMCONS");
        var id = await CriarPacienteAsync(Convenio.Personalizado, "CVSEMCONS");

        var item = (await _consultas.ListarAsync(new DateOnly(2026, 7, 16))).Single(s => s.PacienteId == id);

        item.UsaConsulta.Should().BeFalse();
        item.PrecisaRenovar.Should().BeFalse();
    }

    [Fact]
    public async Task Listar_ConsultaDentroDoPrazo_NaoAlerta()
    {
        var id = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        await _consultas.RenovarAsync(id, new DateOnly(2026, 7, 15)); // vence 06/08

        var item = (await _consultas.ListarAsync(new DateOnly(2026, 7, 16))).Single(s => s.PacienteId == id);

        item.Vencida.Should().BeFalse();
        item.PrecisaRenovar.Should().BeFalse();
        item.Urgencia.Should().Be(NivelUrgencia.Verde);
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar(Array.Empty<EntradaConvenio>()); // não vaza o catálogo para outros testes
        _db.Dispose();
        _conn.Dispose();
    }
}
