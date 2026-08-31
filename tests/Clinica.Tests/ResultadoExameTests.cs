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
/// Resultados de exame estruturados (ago/2026). O que estes testes fixam:
///
/// 1. <b>A consulta EXECUTA</b> — método de repositório nasce com um teste que o roda,
///    porque a tradução do LINQ acontece em runtime (a lição da parcela 74: a derivada
///    `Cancelado` num Where derrubaria a tela no primeiro clique).
/// 2. <b>Registro clínico não se apaga</b>: cancelar exige motivo, a linha fica (a
///    exportação e a guarda a veem), e a lista vigente não.
/// 3. <b>A auditoria sai no MESMO SaveChanges do ato</b> — ação sem linha é ação sem
///    trilha (regra 7 do compromisso).
/// </summary>
public class ResultadoExameTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ResultadoExameService _servico;

    public ResultadoExameTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new ResultadoExameService(_repo);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Helena",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Registra_e_a_consulta_do_paciente_EXECUTA_e_devolve()
    {
        var pacienteId = await PacienteAsync();

        await _servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 10),
            Nome = "Hemoglobina glicada",
            Valor = "6,1",
            Unidade = "%",
            Referencia = "4,0 a 5,6"
        }, "dra.ana");

        var lista = await _servico.DoPacienteAsync(pacienteId);
        lista.Should().HaveCount(1);
        lista[0].ValorComUnidade.Should().Be("6,1 %");
        lista[0].CriadoPor.Should().Be("dra.ana");

        // A trilha saiu no MESMO commit do ato.
        (await _db.Auditoria.AnyAsync(
            e => e.Acao == "ResultadoExameRegistrado" && e.PacienteId == pacienteId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Valor_e_TEXTO_por_desenho_e_nao_reagente_entra()
    {
        var pacienteId = await PacienteAsync();

        // Recusar o que não é número seria a regra apertada demais que o projeto rejeita
        // desde o formato do número da guia: metade dos laudos reais não é número.
        await _servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 10),
            Nome = "Anti-HBs",
            Valor = "não reagente"
        }, "dra.ana");

        (await _servico.DoPacienteAsync(pacienteId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Sem_nome_sem_valor_ou_com_data_futura_e_recusado()
    {
        var pacienteId = await PacienteAsync();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        await FluentActions.Awaiting(() => _servico.RegistrarAsync(new ResultadoExame
            { PacienteId = pacienteId, Data = hoje, Valor = "6,1" }, "a"))
            .Should().ThrowAsync<InvalidOperationException>();

        await FluentActions.Awaiting(() => _servico.RegistrarAsync(new ResultadoExame
            { PacienteId = pacienteId, Data = hoje, Nome = "Glicada" }, "a"))
            .Should().ThrowAsync<InvalidOperationException>();

        // Resultado é registro do que JÁ foi medido — data futura é dedo no teclado.
        await FluentActions.Awaiting(() => _servico.RegistrarAsync(new ResultadoExame
            { PacienteId = pacienteId, Data = hoje.AddDays(1), Nome = "Glicada", Valor = "6" }, "a"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancelar_exige_motivo_marca_a_linha_e_ela_sai_da_lista_vigente()
    {
        var pacienteId = await PacienteAsync();
        var registrado = await _servico.RegistrarAsync(new ResultadoExame
        {
            PacienteId = pacienteId,
            Data = new DateOnly(2026, 8, 10),
            Nome = "Glicada",
            Valor = "6,1"
        }, "dra.ana");

        await FluentActions.Awaiting(() => _servico.CancelarAsync(registrado.Id, "  ", "op"))
            .Should().ThrowAsync<InvalidOperationException>("cancelar sem motivo é apagar devagar");

        await _servico.CancelarAsync(registrado.Id, "valor digitado errado", "op");

        // Some da lista VIGENTE…
        (await _servico.DoPacienteAsync(pacienteId)).Should().BeEmpty();

        // …e FICA no prontuário sob guarda, marcada, com o motivo e a trilha.
        var guardadas = await _repo.ResultadosExameDoPacienteAsync(
            pacienteId, incluirCancelados: true);
        guardadas.Should().HaveCount(1);
        guardadas[0].Cancelado.Should().BeTrue();
        guardadas[0].MotivoCancelamento.Should().Be("valor digitado errado");
        (await _db.Auditoria.AnyAsync(e => e.Acao == "ResultadoExameCancelado"))
            .Should().BeTrue();

        // Cancelar de novo é recusado: o registro já está desdito.
        await FluentActions.Awaiting(() => _servico.CancelarAsync(registrado.Id, "de novo", "op"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
