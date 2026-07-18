using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class ConveniosDinamicosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ConvenioCatalogoService _catalogo;

    public ConveniosDinamicosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _catalogo = new ConvenioCatalogoService(new ClinicaRepositorio(_db));
    }

    [Fact]
    public async Task Listar_GaranteOsQuatroEmbutidos()
    {
        var lista = await _catalogo.ListarAsync();

        lista.Select(c => c.Codigo).Should().Contain(new[]
        {
            nameof(Convenio.UnimedPadrao), nameof(Convenio.UnimedIntercambio),
            nameof(Convenio.Amil), nameof(Convenio.Petrobras)
        });
        lista.Should().OnlyContain(c => c.Ativo);
    }

    [Fact]
    public async Task Cache_ServeNomeEFamiliaDeUmaVariante()
    {
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro { Codigo = "CV12345678", Nome = "Unimed Empresa X", Familia = Convenio.UnimedPadrao, Ativo = true }
        });

        // SalvarAsync recarrega o cache em memória.
        CatalogoConvenios.Nome("CV12345678").Should().Be("Unimed Empresa X");
        CatalogoConvenios.Familia("CV12345678").Should().Be(Convenio.UnimedPadrao);
        CatalogoConvenios.Ativos.Should().Contain(e => e.Codigo == "CV12345678");
    }

    [Fact]
    public void Cache_FallbackQuandoCodigoDesconhecido()
    {
        CatalogoConvenios.Atualizar(Array.Empty<EntradaConvenio>());

        // Código = nome de uma família embutida → resolve pela família.
        CatalogoConvenios.Familia("Amil").Should().Be(Convenio.Amil);
        CatalogoConvenios.Nome("Amil").Should().Be(ConvenioInfo.NomeExibicaoPadrao(Convenio.Amil));
    }

    [Fact]
    public async Task VarianteInativa_SaiDosAtivos()
    {
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro { Codigo = "CVINATIVO", Nome = "Plano Antigo", Familia = Convenio.Amil, Ativo = false }
        });

        CatalogoConvenios.Ativos.Should().NotContain(e => e.Codigo == "CVINATIVO");
        (await _catalogo.ListarAsync()).Should().Contain(c => c.Codigo == "CVINATIVO"); // continua nos dados
    }

    [Fact]
    public async Task Variante_UsaARegraDaFamilia()
    {
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro { Codigo = "CVAMILX", Nome = "Amil Regional", Familia = Convenio.Amil, Ativo = true }
        });

        // A regra de faturamento é selecionada pela família (Amil), não pelo código.
        var familia = CatalogoConvenios.Familia("CVAMILX");
        new RegistroRegras().Para(familia).Convenio.Should().Be(Convenio.Amil);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
