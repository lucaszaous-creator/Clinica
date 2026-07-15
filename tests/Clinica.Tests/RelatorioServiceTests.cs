using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class RelatorioServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public RelatorioServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<int> CriarPacienteAsync(Convenio convenio)
    {
        var p = new Paciente { Nome = "P", Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task TaxaBaixa_ReflitaCodigosBaixadosVsGerados()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var atendimentos = new AtendimentoService(_repo);
        var faturamento = new FaturamentoService(_repo);
        var relatorios = new RelatorioService(_repo);

        var dia = new DateOnly(2026, 7, 10);
        // Acu+Eletro gera 2 códigos; damos baixa em 1 → taxa 50%.
        var r = await atendimentos.LancarAsync(pacienteId, dia, ModalidadeAtendimento.AcupunturaComEletro);
        var primeiro = r.Atendimento.Codigos.First();
        await faturamento.DarBaixaAsync(primeiro.Id, dia, "G-1", "sec", null);

        var rel = await relatorios.GerarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 20));

        rel.Resumo.TotalCodigos.Should().Be(2);
        rel.Resumo.Baixados.Should().Be(1);
        rel.Resumo.Pendentes.Should().Be(1);
        rel.Resumo.TaxaBaixa.Should().Be(50.0);
    }

    [Fact]
    public async Task PorConvenio_SeparaOsConvenios()
    {
        var unimed = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var amil = await CriarPacienteAsync(Convenio.Amil);
        var atendimentos = new AtendimentoService(_repo);
        var relatorios = new RelatorioService(_repo);
        var dia = new DateOnly(2026, 7, 10);

        await atendimentos.LancarAsync(unimed, dia, ModalidadeAtendimento.AcupunturaComEletro); // 2 códigos
        await atendimentos.LancarAsync(amil, dia, ModalidadeAtendimento.AcupunturaSimples);     // 1 código

        var rel = await relatorios.GerarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), dia);

        rel.PorConvenio.Should().Contain(c => c.Convenio == Convenio.UnimedIntercambio && c.TotalCodigos == 2);
        rel.PorConvenio.Should().Contain(c => c.Convenio == Convenio.Amil && c.TotalCodigos == 1);
    }

    [Fact]
    public async Task Envelhecimento_ClassificaPendenciasPorFaixaDeAtraso()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var atendimentos = new AtendimentoService(_repo);
        var relatorios = new RelatorioService(_repo);

        // Gera 2 códigos: acupuntura (10/07) e eletro 2º (11/07). Nenhum baixado.
        // Referência 25/07 → atraso de 15 e 14 dias → ambos na faixa 8–30.
        await atendimentos.LancarAsync(pacienteId, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);

        var rel = await relatorios.GerarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 25));

        rel.Envelhecimento.Single(f => f.Faixa == "8–30 dias").Quantidade.Should().Be(2);
        rel.Envelhecimento.Single(f => f.Faixa == "0–7 dias").Quantidade.Should().Be(0);
        rel.Envelhecimento.Single(f => f.Faixa == "+30 dias").Quantidade.Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
