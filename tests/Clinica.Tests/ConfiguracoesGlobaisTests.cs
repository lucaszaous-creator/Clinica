using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class ConfiguracoesGlobaisTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ParametrosService _parametros;

    public ConfiguracoesGlobaisTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _parametros = new ParametrosService(new ClinicaRepositorio(_db));
    }

    [Fact]
    public async Task JanelaAlerta_PadraoCinco_SalvaERecupera()
    {
        (await _parametros.ObterJanelaAlertaConsultaAsync()).Should().Be(5);

        await _parametros.SalvarJanelaAlertaConsultaAsync(10);
        (await _parametros.ObterJanelaAlertaConsultaAsync()).Should().Be(10);

        await _parametros.SalvarJanelaAlertaConsultaAsync(-3); // negativo vira 0
        (await _parametros.ObterJanelaAlertaConsultaAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Prestador_SemConfiguracao_VoltaVazio()
    {
        (await _parametros.PrestadorConfiguradoAsync()).Should().BeFalse();

        var d = await _parametros.ObterPrestadorAsync();
        d.RazaoSocial.Should().BeNull();
        d.CodigosTuss.Should().BeEmpty();
    }

    [Fact]
    public async Task Prestador_RoundTrip_PreservaTudo()
    {
        var original = new DadosPrestador
        {
            RazaoSocial = "Clínica Exemplo LTDA",
            NomeFantasia = "Clínica Exemplo",
            Cnpj = "12.345.678/0001-90",
            Cnes = "1234567",
            Endereco = "Rua A, 100 — Centro, Macaé/RJ",
            Telefone = "(22) 99999-9999",
            Email = "contato@exemplo.com",
            CodigoNaOperadora = "PRE-1",
            RegistroAnsOperadora = "654321",
            CodigosTuss = new()
            {
                [TipoCodigo.Acupuntura] = "31602029",
                [TipoCodigo.Consulta] = "10101012"
            }
        };

        await _parametros.SalvarPrestadorAsync(original);

        (await _parametros.PrestadorConfiguradoAsync()).Should().BeTrue();
        var lido = await _parametros.ObterPrestadorAsync();
        lido.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task Prestador_Salvar_SobrescreveOAnterior()
    {
        await _parametros.SalvarPrestadorAsync(new DadosPrestador { RazaoSocial = "Antiga" });
        await _parametros.SalvarPrestadorAsync(new DadosPrestador { RazaoSocial = "Nova" });

        (await _parametros.ObterPrestadorAsync()).RazaoSocial.Should().Be("Nova");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
