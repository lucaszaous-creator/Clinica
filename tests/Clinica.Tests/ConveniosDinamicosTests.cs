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

    [Fact]
    public async Task Editar_PersisteNomeFamiliaEConfiguracaoDaRegra()
    {
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro { Codigo = "CVEDIT", Nome = "Plano Novo", Familia = Convenio.Personalizado, Ativo = true }
        });

        // Edição: mesmo código, todos os campos alterados (inclusive a config da regra genérica).
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro
            {
                Codigo = "CVEDIT",
                Nome = "Plano Editado",
                Familia = Convenio.Personalizado,
                Ativo = true,
                FazEletro = true,
                TemSegundoCodigo = true,
                FormaSegundoCodigo = FormaObtencao.Ligacao,
                DiasSegundoCodigo = 3,
                ValidadeConsultaDias = 90,
                CategoriaSemApp = Categoria.Vermelha
            }
        });

        var salvo = (await _catalogo.ListarAsync()).Single(c => c.Codigo == "CVEDIT");
        salvo.Nome.Should().Be("Plano Editado");
        salvo.FazEletro.Should().BeTrue();
        salvo.TemSegundoCodigo.Should().BeTrue();
        salvo.FormaSegundoCodigo.Should().Be(FormaObtencao.Ligacao);
        salvo.DiasSegundoCodigo.Should().Be(3);
        salvo.ValidadeConsultaDias.Should().Be(90);
        salvo.CategoriaSemApp.Should().Be(Categoria.Vermelha);

        // O cache (que alimenta a RegraGenerica) reflete a edição imediatamente.
        CatalogoConvenios.Nome("CVEDIT").Should().Be("Plano Editado");
        CatalogoConvenios.Config("CVEDIT")!.DiasSegundoCodigo.Should().Be(3);
        CatalogoConvenios.ValidadeConsultaDias("CVEDIT").Should().Be(90);
    }

    [Fact]
    public async Task Excluir_VarianteSemUso_RemoveDoBancoEDoCache()
    {
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro { Codigo = "CVDEL", Nome = "Plano Descartável", Familia = Convenio.Amil, Ativo = true }
        });

        var (ok, _) = await _catalogo.ExcluirAsync("CVDEL");

        ok.Should().BeTrue();
        (await _catalogo.ListarAsync()).Should().NotContain(c => c.Codigo == "CVDEL");
        CatalogoConvenios.Ativos.Should().NotContain(e => e.Codigo == "CVDEL");
    }

    [Fact]
    public async Task Excluir_ConvenioComPaciente_EhRecusado()
    {
        await _catalogo.SalvarAsync(new[]
        {
            new ConvenioCadastro { Codigo = "CVUSO", Nome = "Plano Em Uso", Familia = Convenio.Amil, Ativo = true }
        });
        _db.Pacientes.Add(new Paciente { Nome = "Maria", Convenio = Convenio.Amil, ConvenioCodigo = "CVUSO" });
        await _db.SaveChangesAsync();

        var (ok, mensagem) = await _catalogo.ExcluirAsync("CVUSO");

        ok.Should().BeFalse();
        mensagem.Should().Contain("Plano Em Uso");
        (await _catalogo.ListarAsync()).Should().Contain(c => c.Codigo == "CVUSO");
    }

    [Fact]
    public async Task Excluir_Embutido_EhRecusado()
    {
        var (ok, mensagem) = await _catalogo.ExcluirAsync(nameof(Convenio.Amil));

        ok.Should().BeFalse();
        mensagem.Should().Contain("embutido");
        (await _catalogo.ListarAsync()).Should().Contain(c => c.Codigo == nameof(Convenio.Amil));
    }

    [Fact]
    public async Task Excluir_ConvenioNuncaSalvo_EhTolerado()
    {
        // Fluxo da tela: item criado em 'Novo convênio' e removido antes de salvar.
        var (ok, _) = await _catalogo.ExcluirAsync("CVNUNCA");
        ok.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
