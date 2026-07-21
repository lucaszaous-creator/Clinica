using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class ModalidadesEspecialidadesTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ModalidadeCatalogoService _modalidades;
    private readonly EspecialidadeCatalogoService _especialidades;

    public ModalidadesEspecialidadesTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _modalidades = new ModalidadeCatalogoService(_repo);
        _especialidades = new EspecialidadeCatalogoService(_repo);
    }

    // ---- Modalidades ----

    [Fact]
    public async Task Modalidades_ListarGaranteAsEmbutidas()
    {
        var lista = await _modalidades.ListarAsync();

        lista.Select(m => m.Codigo).Should().Contain(Enum.GetNames<ModalidadeAtendimento>());
        lista.Should().OnlyContain(m => m.Ativo);
    }

    [Fact]
    public async Task Modalidade_VarianteServeNomeEBasePeloCache()
    {
        await _modalidades.SalvarAsync(new[]
        {
            new ModalidadeCadastro { Codigo = "MD1", Nome = "Auriculoterapia", Base = ModalidadeAtendimento.AcupunturaSimples, Ativo = true }
        });

        CatalogoModalidades.Nome("MD1").Should().Be("Auriculoterapia");
        CatalogoModalidades.Base("MD1").Should().Be(ModalidadeAtendimento.AcupunturaSimples);
        CatalogoModalidades.Ativas.Should().Contain(e => e.Codigo == "MD1");
    }

    [Fact]
    public async Task Modalidade_EdicaoPersisteNomeEBase()
    {
        await _modalidades.SalvarAsync(new[]
        {
            new ModalidadeCadastro { Codigo = "MD2", Nome = "Antiga", Base = ModalidadeAtendimento.AcupunturaSimples, Ativo = true }
        });
        await _modalidades.SalvarAsync(new[]
        {
            new ModalidadeCadastro { Codigo = "MD2", Nome = "Nova", Base = ModalidadeAtendimento.BsvApenas, Ativo = false }
        });

        var salva = (await _modalidades.ListarAsync()).Single(m => m.Codigo == "MD2");
        salva.Nome.Should().Be("Nova");
        salva.Base.Should().Be(ModalidadeAtendimento.BsvApenas);
        salva.Ativo.Should().BeFalse();
        CatalogoModalidades.Nome("MD2").Should().Be("Nova");
    }

    [Fact]
    public async Task Modalidade_ExcluirVarianteSemUso_Remove()
    {
        await _modalidades.SalvarAsync(new[]
        {
            new ModalidadeCadastro { Codigo = "MDDEL", Nome = "Descartável", Base = ModalidadeAtendimento.AcupunturaSimples, Ativo = true }
        });

        var (ok, _) = await _modalidades.ExcluirAsync("MDDEL");

        ok.Should().BeTrue();
        (await _modalidades.ListarAsync()).Should().NotContain(m => m.Codigo == "MDDEL");
    }

    [Fact]
    public async Task Modalidade_ExcluirEmUso_EhRecusado()
    {
        await _modalidades.SalvarAsync(new[]
        {
            new ModalidadeCadastro { Codigo = "MDUSO", Nome = "Em Uso", Base = ModalidadeAtendimento.AcupunturaSimples, Ativo = true }
        });
        _db.Pacientes.Add(new Paciente { Nome = "Ana", Convenio = Convenio.Amil, ModalidadePreferidaCodigo = "MDUSO" });
        await _db.SaveChangesAsync();

        var (ok, mensagem) = await _modalidades.ExcluirAsync("MDUSO");

        ok.Should().BeFalse();
        mensagem.Should().Contain("Em Uso");
        (await _modalidades.ListarAsync()).Should().Contain(m => m.Codigo == "MDUSO");
    }

    [Fact]
    public async Task Modalidade_ExcluirEmbutida_EhRecusado()
    {
        var (ok, mensagem) = await _modalidades.ExcluirAsync(nameof(ModalidadeAtendimento.Consulta));

        ok.Should().BeFalse();
        mensagem.Should().Contain("embutida");
    }

    [Fact]
    public async Task Modalidade_ExcluirNuncaSalva_EhTolerado()
    {
        var (ok, _) = await _modalidades.ExcluirAsync("MDNUNCA");
        ok.Should().BeTrue();
    }

    // ---- Especialidades ----

    [Fact]
    public async Task Especialidades_ListarGaranteAsEmbutidas()
    {
        var lista = await _especialidades.ListarAsync();
        lista.Select(e => e.Codigo).Should().Contain(Enum.GetNames<Especialidade>());
    }

    [Fact]
    public async Task Especialidade_AdicionadaServeNomePeloCache()
    {
        await _especialidades.SalvarAsync(new[]
        {
            new EspecialidadeCadastro { Codigo = "ES1", Nome = "Reumatologia", Ativo = true }
        });

        CatalogoEspecialidades.Nome("ES1").Should().Be("Reumatologia");
        CatalogoEspecialidades.Ativas.Should().Contain(e => e.Codigo == "ES1");
    }

    [Fact]
    public async Task Especialidade_ExcluirEmbutida_EhRecusado()
    {
        var (ok, mensagem) = await _especialidades.ExcluirAsync(nameof(Especialidade.Ginecologia));

        ok.Should().BeFalse();
        mensagem.Should().Contain("embutida");
        (await _especialidades.ListarAsync()).Should().Contain(e => e.Codigo == nameof(Especialidade.Ginecologia));
    }

    [Fact]
    public async Task Especialidade_ExcluirEmUso_EhRecusado()
    {
        await _especialidades.SalvarAsync(new[]
        {
            new EspecialidadeCadastro { Codigo = "ESUSO", Nome = "Em Uso", Ativo = true }
        });
        var p = new Paciente { Nome = "Bia", Convenio = Convenio.Amil };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = p.Id, Data = new DateOnly(2026, 7, 10),
            Modalidade = ModalidadeAtendimento.Consulta, EspecialidadeConsultaCodigo = "ESUSO"
        });
        await _db.SaveChangesAsync();

        var (ok, _) = await _especialidades.ExcluirAsync("ESUSO");

        ok.Should().BeFalse();
        (await _especialidades.ListarAsync()).Should().Contain(e => e.Codigo == "ESUSO");
    }

    // ---- Integração com o lançamento ----

    [Fact]
    public async Task Lancar_ComModalidadeVariante_ResolvePelaBaseEGravaOCodigo()
    {
        await _modalidades.RecarregarCacheAsync();
        await _modalidades.SalvarAsync(new[]
        {
            new ModalidadeCadastro { Codigo = "MDAURI", Nome = "Auriculoterapia", Base = ModalidadeAtendimento.AcupunturaComEletro, Ativo = true }
        });

        var p = new Paciente { Nome = "Carla", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaSimples,
            modalidadeCodigo: "MDAURI");

        // A base (AcupunturaComEletro) rege a geração de códigos, não o enum passado por posição.
        r.Atendimento.ModalidadeCodigo.Should().Be("MDAURI");
        r.Atendimento.Modalidade.Should().Be(ModalidadeAtendimento.AcupunturaComEletro);
        r.Atendimento.Codigos.Should().Contain(c => c.Ordem == OrdemCodigo.Segundo); // modalidade dupla gera 2º código
    }

    [Fact]
    public async Task Lancar_ConsultaComEspecialidadeCustom_GravaCodigoDaEspecialidade()
    {
        await _especialidades.SalvarAsync(new[]
        {
            new EspecialidadeCadastro { Codigo = "ESREU", Nome = "Reumatologia", Ativo = true }
        });

        var p = new Paciente { Nome = "Dora", Convenio = Convenio.Amil, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.Consulta,
            especialidadeConsultaCodigo: "ESREU");

        var codigo = r.Atendimento.Codigos.Single();
        codigo.Tipo.Should().Be(TipoCodigo.Consulta);
        codigo.EspecialidadeCodigo.Should().Be("ESREU");
        codigo.Descricao.Should().Contain("Reumatologia");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
