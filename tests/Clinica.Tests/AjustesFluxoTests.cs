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

/// <summary>
/// Ajustes de fluxo pedidos pela clínica:
/// (1) a categoria do paciente muda conforme o convênio + app;
/// (5) o "Novo atendimento" aparece na agenda do dia marcado.
/// </summary>
public class AjustesFluxoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public AjustesFluxoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    // ---------- (1) Categoria conforme plano + app ----------

    [Theory]
    [InlineData(Convenio.Petrobras, false, Categoria.Vermelha)]
    [InlineData(Convenio.Petrobras, true, Categoria.Vermelha)]
    [InlineData(Convenio.Amil, false, Categoria.Amarela)]
    [InlineData(Convenio.Amil, true, Categoria.Amarela)]
    [InlineData(Convenio.UnimedIntercambio, false, Categoria.Verde)]
    [InlineData(Convenio.UnimedPadrao, true, Categoria.Verde)]
    [InlineData(Convenio.UnimedPadrao, false, Categoria.Amarela)]
    public void CategoriaBase_SegueConvenioEApp(Convenio convenio, bool possuiApp, Categoria esperada)
        => CategoriaConvenio.Base(convenio, possuiApp).Should().Be(esperada);

    [Fact]
    public async Task SalvarNovoPaciente_DefineCategoriaPeloConvenioEApp()
    {
        var service = new PacienteService(_repo);

        var padraoComApp = await service.SalvarNovoAsync(
            new Paciente { Nome = "Com App", Convenio = Convenio.UnimedPadrao, PossuiApp = true });
        padraoComApp.Categoria.Should().Be(Categoria.Verde);

        var padraoSemApp = await service.SalvarNovoAsync(
            new Paciente { Nome = "Sem App", Convenio = Convenio.UnimedPadrao, PossuiApp = false });
        padraoSemApp.Categoria.Should().Be(Categoria.Amarela);

        var petro = await service.SalvarNovoAsync(
            new Paciente { Nome = "Petro", Convenio = Convenio.Petrobras });
        petro.Categoria.Should().Be(Categoria.Vermelha);
    }

    [Fact]
    public async Task AtualizarPaciente_RecalculaCategoria_ExcetoQuandoManual()
    {
        var service = new PacienteService(_repo);
        var p = await service.SalvarNovoAsync(
            new Paciente { Nome = "Muda Plano", Convenio = Convenio.UnimedPadrao, PossuiApp = false });
        p.Categoria.Should().Be(Categoria.Amarela);

        // Passou a ter app → categoria recalculada para Verde.
        p.PossuiApp = true;
        await service.AtualizarAsync(p);
        p.Categoria.Should().Be(Categoria.Verde);

        // Override manual é preservado.
        p.Categoria = Categoria.Amarela;
        await service.AtualizarAsync(p, categoriaManual: true);
        p.Categoria.Should().Be(Categoria.Amarela);
    }

    // ---------- (6) Ordem escolhida na modalidade dupla ----------

    [Fact]
    public void Intercambio_AcuComEletro_PreferindoEletroPrimeiro_InverteOrdem()
    {
        var p = new Paciente { Nome = "Dupla", Convenio = Convenio.UnimedIntercambio };
        var a = new Atendimento { Data = new DateOnly(2026, 7, 15), Modalidade = ModalidadeAtendimento.AcupunturaComEletro };
        var ctx = new ContextoFaturamento { PrimeiroCodigoPreferido = TipoCodigo.Eletroacupuntura };

        var r = new RegraUnimedIntercambio().Gerar(p, a, ctx);

        var primeiro = r.Codigos.Single(c => c.Ordem == OrdemCodigo.Primeiro);
        var segundo = r.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo);
        primeiro.Tipo.Should().Be(TipoCodigo.Eletroacupuntura);
        primeiro.DataPrevistaFaturamento.Should().Be(a.Data);
        segundo.Tipo.Should().Be(TipoCodigo.Acupuntura);
        segundo.DataPrevistaFaturamento.Should().Be(a.Data.AddDays(1));
    }

    [Fact]
    public void Intercambio_AcuComEletro_SemPreferencia_MantemAcupunturaPrimeiro()
    {
        var p = new Paciente { Nome = "Dupla", Convenio = Convenio.UnimedIntercambio };
        var a = new Atendimento { Data = new DateOnly(2026, 7, 15), Modalidade = ModalidadeAtendimento.AcupunturaComEletro };

        var r = new RegraUnimedIntercambio().Gerar(p, a, ContextoFaturamento.Vazio);

        r.Codigos.Single(c => c.Ordem == OrdemCodigo.Primeiro).Tipo.Should().Be(TipoCodigo.Acupuntura);
        r.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo).Tipo.Should().Be(TipoCodigo.Eletroacupuntura);
    }

    [Fact]
    public void PadraoComApp_BsvComAcupuntura_PreferindoAcupunturaPrimeiro_InverteOrdem()
    {
        var p = new Paciente { Nome = "Dupla", Convenio = Convenio.UnimedPadrao, PossuiApp = true };
        var a = new Atendimento { Data = new DateOnly(2026, 7, 15), Modalidade = ModalidadeAtendimento.BsvComAcupuntura };
        var ctx = new ContextoFaturamento { PrimeiroCodigoPreferido = TipoCodigo.Acupuntura };

        var r = new RegraUnimedPadrao().Gerar(p, a, ctx);

        r.Codigos.Single(c => c.Ordem == OrdemCodigo.Primeiro).Tipo.Should().Be(TipoCodigo.Acupuntura);
        r.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo).Tipo.Should().Be(TipoCodigo.Bsv);
    }

    [Fact]
    public async Task ModalidadePreferida_PersisteNoCadastro()
    {
        var service = new PacienteService(_repo);
        var p = await service.SalvarNovoAsync(new Paciente
        {
            Nome = "Habitual",
            Convenio = Convenio.Amil,
            ModalidadePreferida = ModalidadeAtendimento.BsvComAcupuntura
        });

        var lido = await _db.Pacientes.AsNoTracking().FirstAsync(x => x.Id == p.Id);
        lido.ModalidadePreferida.Should().Be(ModalidadeAtendimento.BsvComAcupuntura);
    }

    // ---------- (5) Novo atendimento reflete na agenda ----------

    [Fact]
    public async Task LancarComRegistrarNaAgenda_CriaAgendamentoRealizadoNoDia()
    {
        var pacienteService = new PacienteService(_repo);
        var paciente = await pacienteService.SalvarNovoAsync(
            new Paciente { Nome = "Agenda", Convenio = Convenio.UnimedIntercambio });

        var atendimentos = new AtendimentoService(_repo);
        var dia = new DateOnly(2026, 7, 20);

        var resultado = await atendimentos.LancarAsync(
            paciente.Id, dia, ModalidadeAtendimento.AcupunturaComEletro, registrarNaAgenda: true);

        var todos = await _db.Agendamentos.AsNoTracking().ToListAsync();
        var naAgenda = todos.Where(a => DateOnly.FromDateTime(a.DataHora) == dia).ToList();

        naAgenda.Should().ContainSingle();
        naAgenda[0].Status.Should().Be(StatusAgendamento.Realizado);
        naAgenda[0].AtendimentoId.Should().Be(resultado.Atendimento.Id);
        naAgenda[0].PacienteId.Should().Be(paciente.Id);
    }

    [Fact]
    public async Task Lancar_SemRegistrarNaAgenda_NaoCriaAgendamento()
    {
        var pacienteService = new PacienteService(_repo);
        var paciente = await pacienteService.SalvarNovoAsync(
            new Paciente { Nome = "Sem Agenda", Convenio = Convenio.UnimedIntercambio });

        var atendimentos = new AtendimentoService(_repo);
        await atendimentos.LancarAsync(paciente.Id, new DateOnly(2026, 7, 20), ModalidadeAtendimento.AcupunturaSimples);

        (await _db.Agendamentos.AsNoTracking().CountAsync()).Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
