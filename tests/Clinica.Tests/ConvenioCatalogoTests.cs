using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class ConvenioCatalogoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ParametrosService _parametros;

    public ConvenioCatalogoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _parametros = new ParametrosService(new ClinicaRepositorio(_db));
    }

    [Fact]
    public async Task SemConfiguracao_UsaDefaultsDoCodigo()
    {
        var snap = await _parametros.ObterAsync();

        snap.NomeExibicao(Convenio.Amil).Should().Be(ConvenioInfo.NomeExibicao(Convenio.Amil));
        snap.CategoriaBase(Convenio.UnimedPadrao, possuiApp: true).Should().Be(Categoria.Verde);
        snap.CategoriaBase(Convenio.UnimedPadrao, possuiApp: false).Should().Be(Categoria.Amarela);
        snap.Ativo(Convenio.Petrobras).Should().BeTrue();
        snap.ConveniosAtivos.Should().HaveCount(Enum.GetValues<Convenio>().Length);
    }

    [Fact]
    public async Task NomeCustom_SobrepoeODefault()
    {
        await _parametros.SalvarAsync(new[]
        {
            new ParametroConvenio { Convenio = Convenio.Amil, Nome = "Amil Saúde", Ativo = true, DiasSegundoCodigo = 1 }
        });

        var snap = await _parametros.ObterAsync();
        snap.NomeExibicao(Convenio.Amil).Should().Be("Amil Saúde");
    }

    [Fact]
    public async Task ConvenioInativo_SaiDaListaDeAtivos_MasNaoDosDados()
    {
        await _parametros.SalvarAsync(new[]
        {
            new ParametroConvenio { Convenio = Convenio.Petrobras, Ativo = false, DiasSegundoCodigo = 1 }
        });

        var snap = await _parametros.ObterAsync();
        snap.Ativo(Convenio.Petrobras).Should().BeFalse();
        snap.ConveniosAtivos.Should().NotContain(o => o.Convenio == Convenio.Petrobras);
        snap.ConveniosAtivos.Should().HaveCount(Enum.GetValues<Convenio>().Length - 1);
    }

    [Fact]
    public async Task CategoriaBaseCustom_SobrepoeODefault()
    {
        await _parametros.SalvarAsync(new[]
        {
            new ParametroConvenio
            {
                Convenio = Convenio.Amil,
                Ativo = true,
                DiasSegundoCodigo = 1,
                CategoriaComApp = Categoria.Verde,
                CategoriaSemApp = Categoria.Vermelha
            }
        });

        var snap = await _parametros.ObterAsync();
        snap.CategoriaBase(Convenio.Amil, possuiApp: true).Should().Be(Categoria.Verde);
        snap.CategoriaBase(Convenio.Amil, possuiApp: false).Should().Be(Categoria.Vermelha);
    }

    [Fact]
    public async Task ConveniosAtivos_VemOrdenadosPorNome()
    {
        var nomes = (await _parametros.ObterAsync()).ConveniosAtivos.Select(o => o.Nome).ToList();
        nomes.Should().BeInAscendingOrder();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
