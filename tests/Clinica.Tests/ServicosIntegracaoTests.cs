using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>Exercita AtendimentoService + PendenciaService + FaturamentoService contra um SQLite real (em memória).</summary>
public class ServicosIntegracaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public ServicosIntegracaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<int> CriarPacienteAsync(Convenio convenio, bool possuiApp = false, Sexo sexo = Sexo.Masculino)
    {
        var p = new Paciente { Nome = "Fulano", Convenio = convenio, PossuiApp = possuiApp, Sexo = sexo };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task FluxoCritico_Intercambio_SegundoCodigoViraPendencia_EBaixaResolve()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var atendimentoService = new AtendimentoService(_repo);
        var pendencias = new PendenciaService(_repo);
        var faturamento = new FaturamentoService(_repo);

        var diaAtendimento = new DateOnly(2026, 7, 15);
        var resultado = await atendimentoService.LancarAsync(pacienteId, diaAtendimento, ModalidadeAtendimento.AcupunturaComEletro);

        resultado.Atendimento.Codigos.Should().HaveCount(2);

        // No próprio dia, o 2º código (previsto para +24h) ainda NÃO é pendência.
        (await pendencias.CodigosPendentesAsync(diaAtendimento))
            .Should().ContainSingle(p => p.Ordem == OrdemCodigo.Primeiro);

        // No dia seguinte, o 2º código vira pendência visível no dashboard.
        var noDiaSeguinte = diaAtendimento.AddDays(1);
        var pend = await pendencias.CodigosPendentesAsync(noDiaSeguinte);
        var segundo = pend.Should().ContainSingle(p => p.Ordem == OrdemCodigo.Segundo).Subject;
        segundo.Tipo.Should().Be(TipoCodigo.Eletroacupuntura);
        segundo.FormaObtencao.Should().Be(FormaObtencao.Sistema);

        // Secretária dá baixa → sai das pendências e registra a guia real.
        await faturamento.DarBaixaAsync(segundo.CodigoId, noDiaSeguinte, "GUIA-999", "secretaria", null);

        (await pendencias.CodigosPendentesAsync(noDiaSeguinte))
            .Should().NotContain(p => p.CodigoId == segundo.CodigoId);
    }

    [Fact]
    public async Task ConsultaAvulsa_LancamentoGuardaEspecialidade_ERenovaCicloDoPlano()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.UnimedPadrao); // consulta renova a cada 22 dias
        var consultas = new ConsultaService(_repo);
        var atendimentoService = new AtendimentoService(_repo, consultas: consultas);

        var dia = new DateOnly(2026, 7, 15);
        var resultado = await atendimentoService.LancarAsync(
            pacienteId, dia, ModalidadeAtendimento.Consulta, especialidadeConsulta: Especialidade.Geriatria);

        resultado.Atendimento.EspecialidadeConsulta.Should().Be(Especialidade.Geriatria);
        resultado.Atendimento.Codigos.Should().ContainSingle()
            .Which.Especialidade.Should().Be(Especialidade.Geriatria);
        resultado.Avisos.Should().Contain(a => a.Contains("Renovação registrada"));

        // A consulta avulsa reinicia o ciclo de renovação vigiado na aba Consultas.
        var vigente = (await _repo.ConsultasDoPacienteAsync(pacienteId))
            .Single(c => c.Status == StatusConsulta.Ativa);
        vigente.ValidadeDias.Should().Be(22);
        vigente.DataVencimento.Should().Be(dia.AddDays(22));
    }

    [Fact]
    public async Task ConsultaAvulsa_RelatorioContaConsultasPorEspecialidade()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.Amil);
        var atendimentoService = new AtendimentoService(_repo);
        var dia = new DateOnly(2026, 7, 10);

        await atendimentoService.LancarAsync(pacienteId, dia, ModalidadeAtendimento.Consulta,
            especialidadeConsulta: Especialidade.Geriatria);
        await atendimentoService.LancarAsync(pacienteId, dia.AddDays(1), ModalidadeAtendimento.Consulta,
            especialidadeConsulta: Especialidade.Geriatria);
        await atendimentoService.LancarAsync(pacienteId, dia.AddDays(2), ModalidadeAtendimento.Consulta,
            especialidadeConsulta: Especialidade.Endocrinologia);
        // Sessão comum não entra na contagem de consultas.
        await atendimentoService.LancarAsync(pacienteId, dia.AddDays(3), ModalidadeAtendimento.AcupunturaSimples);

        var relatorio = await new RelatorioService(_repo)
            .GerarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 31));

        relatorio.ConsultasEspecialidades.Should().HaveCount(2);
        relatorio.ConsultasEspecialidades.Single(c => c.Especialidade == "Geriatria").Quantidade.Should().Be(2);
        relatorio.ConsultasEspecialidades.Single(c => c.Especialidade == "Endocrinologia").Quantidade.Should().Be(1);
    }

    [Fact]
    public async Task Petrobras_TresAtendimentosMulher_RotacionamEspecialidades_ViaServico()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.Petrobras, sexo: Sexo.Feminino);
        var service = new AtendimentoService(_repo);

        var esp = new List<Especialidade>();
        for (int semana = 0; semana < 3; semana++)
        {
            var r = await service.LancarAsync(pacienteId, new DateOnly(2026, 7, 1).AddDays(semana * 7),
                ModalidadeAtendimento.AcupunturaSimples);
            esp.Add(r.Atendimento.Codigos.Single(c => c.Especialidade != null).Especialidade!.Value);
        }

        esp.Should().BeEquivalentTo(new[] { Especialidade.Psiquiatria, Especialidade.Geriatria, Especialidade.Ginecologia });
    }

    [Fact]
    public async Task ConsultaAVencer_ApareceNoDashboard()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var consultaService = new ConsultaService(_repo);
        var pendencias = new PendenciaService(_repo);

        // Emite a consulta em 01/07: Unimed Intercâmbio vale 22 dias → vence em 23/07.
        await consultaService.RenovarAsync(pacienteId, new DateOnly(2026, 7, 1));

        // Verificando em 20/07 (dentro da janela de 5 dias), a consulta a vencer aparece.
        var consultas = await pendencias.ConsultasAVencerAsync(new DateOnly(2026, 7, 20));
        consultas.Should().ContainSingle(c => c.PacienteId == pacienteId)
            .Which.DataVencimento.Should().Be(new DateOnly(2026, 7, 23));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
