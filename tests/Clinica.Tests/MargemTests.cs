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
/// A margem por sessão (out/2026, etapa "Produção e margem" do Gerente): receita realizada
/// menos taxa, imposto, insumo e repasse. Sessão sem receita fica fora e é contada à parte.
/// </summary>
public sealed class MargemTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly RepasseService _repasses;
    private readonly MargemService _margem;

    private static readonly DateOnly Inicio = new(2026, 8, 1);
    private static readonly DateOnly Fim = new(2026, 8, 31);

    public MargemTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _repasses = new RepasseService(_repo);
        _margem = new MargemService(_repo, new EstoqueService(_repo), _repasses);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private async Task<(int Paciente, int Profissional)> BaseAsync()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        var pr = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 1234" };
        _db.AddRange(p, pr);
        await _db.SaveChangesAsync();
        return (p.Id, pr.Id);
    }

    private async Task<int> SessaoAsync(int paciente, int profissional, DateOnly dia, ModalidadeAtendimento modalidade = ModalidadeAtendimento.AcupunturaSimples)
    {
        var atendimento = new Atendimento { PacienteId = paciente, Data = dia, Modalidade = modalidade };
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente, ProfissionalId = profissional, AtendimentoId = atendimento.Id,
            DataHora = dia.ToDateTime(new TimeOnly(14, 0)), Status = StatusAgendamento.Realizado,
            ModalidadePrevista = modalidade
        });
        await _db.SaveChangesAsync();
        return atendimento.Id;
    }

    [Fact]
    public async Task Margem_desconta_repasse_e_deixa_a_sessao_sem_receita_de_fora()
    {
        var (paciente, profissional) = await BaseAsync();
        var paga = await SessaoAsync(paciente, profissional, Inicio.AddDays(3));
        await SessaoAsync(paciente, profissional, Inicio.AddDays(4)); // guia ainda não paga
        await _financeiro.LancarAsync(Inicio.AddDays(3), TipoLancamento.Entrada, "sessão", 200m,
            StatusLancamento.Realizado, atendimentoId: paga);
        await _repasses.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissional, Base = BaseRepasse.PercentualDaReceita, Percentual = 40m, VigenteDe = Inicio, Ativa = true
        }, "gerente");

        var m = await _margem.CalcularAsync(Inicio, Fim);

        m.SessoesSemReceita.Should().Be(1);
        m.Total.Sessoes.Should().Be(2);
        m.Total.SessoesComReceita.Should().Be(1);
        m.Total.Receita.Should().Be(200m);
        m.Total.Repasse.Should().Be(80m);
        m.Total.Margem.Should().Be(120m);
        m.Total.MargemPorSessao.Should().Be(120m);
        m.PorPagador.Single().Nome.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Periodo_sem_sessao_nao_inventa_margem()
    {
        var m = await _margem.CalcularAsync(Inicio, Fim);
        m.Total.Sessoes.Should().Be(0);
        m.Total.MargemPorSessao.Should().BeNull();
        m.Total.FracaoDaReceita.Should().BeNull();
    }

    [Fact]
    public void O_rateio_do_repasse_segue_a_receita_e_a_soma_bate()
    {
        MargemNaTela.RatearRepasse(100m, [200m, 100m, 0m]).Should().Equal(66.67m, 33.33m, 0m);
        MargemNaTela.RatearRepasse(100m, [0m, 0m, 0m]).Should().Equal(33.33m, 33.33m, 33.34m);
        MargemNaTela.RatearRepasse(0m, []).Should().BeEmpty();
    }

    [Fact]
    public void Os_recortes_somam_o_total_e_ordenam_pela_margem()
    {
        SessaoParaMargem S(string mod, string prof, decimal receita, bool tem = true) =>
            new(1, prof, mod, "Unimed", receita, 10m, 5m, false, 20m, tem);
        var m = MargemNaTela.Montar(Inicio, Fim,
        [
            S("Acupuntura", "Dra. Ana", 200m), S("Acupuntura", "Dr. Paulo", 150m),
            S("BSV", "Dra. Ana", 400m), S("BSV", "Dra. Ana", 0m, tem: false)
        ]);

        m.Total.Margem.Should().Be(750m - 3 * 35m);
        m.PorModalidade.Select(l => l.Nome).Should().Equal("BSV", "Acupuntura");
        m.PorModalidade.Sum(l => l.Margem).Should().Be(m.Total.Margem);
        m.PorProfissional.Single(l => l.Nome == "Dra. Ana").Sessoes.Should().Be(3);
        m.SessoesSemReceita.Should().Be(1);
    }

    [Fact]
    public void As_frases_dizem_o_que_ficou_fora_e_calam_quando_nada_ficou()
    {
        SessaoParaMargem S(bool tem, bool incompleto = false) =>
            new(1, "Dra. Ana", "BSV", "Unimed", tem ? 300m : 0m, 0m, 10m, incompleto, 0m, tem);

        var limpo = MargemNaTela.Montar(Inicio, Fim, [S(true), S(true)]);
        MargemNaTela.ForaDaMargem(limpo).Should().BeNull();
        MargemNaTela.ResumoDoPeriodo(limpo).Should().Be("2 sessões com receita entre 01/08 e 31/08.");

        var sujo = MargemNaTela.Montar(Inicio, Fim, [S(true, incompleto: true), S(false)]);
        MargemNaTela.ResumoDoPeriodo(sujo).Should().Be("1 sessão com receita de 2 realizadas entre 01/08 e 31/08.");
        MargemNaTela.ForaDaMargem(sujo).Should().Contain("1 sessão realizada ainda não tem receita").And.Contain("insumo sem custo");

        MargemNaTela.Percentual(null).Should().Be("—");
        MargemNaTela.Percentual(0.38).Should().Be("38%");
    }
}
