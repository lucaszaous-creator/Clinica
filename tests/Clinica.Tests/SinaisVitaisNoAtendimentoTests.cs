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
/// OS SINAIS VITAIS DA ENFERMAGEM NA TELA DE QUEM ATENDE (parcela 76).
///
/// A clínica disse que <b>todo paciente passa pela enfermagem</b>: a PA, a FC e a
/// temperatura são colhidas minutos antes da consulta. Até aqui, quem prescreve escrevia a
/// sessão sem elas na frente — ou saía da tela para procurá-las. É o defeito recorrente do
/// projeto na variante "o leitor existe, mas não onde a decisão acontece".
///
/// O que estes testes fixam não é a leitura: é <b>qual registro vale</b>. Um número
/// desdito — cancelado ou já retificado — apresentado como aferição do dia é pior do que
/// número nenhum, porque muda conduta e não tem como ser distinguido do certo.
/// </summary>
public class SinaisVitaisNoAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ConsultorioService _consultorio;

    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);

    public SinaisVitaisNoAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _consultorio = new ConsultorioService(new ClinicaRepositorio(_db));
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Marisa Silva", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<EvolucaoEnfermagem> AferirAsync(
        int pacienteId, DateOnly data, TimeOnly hora, int sistolica,
        int? retifica = null, bool cancelada = false)
    {
        var e = new EvolucaoEnfermagem
        {
            PacienteId = pacienteId,
            Data = data,
            Hora = hora,
            Texto = "Aferição de rotina.",
            PressaoSistolica = sistolica,
            PressaoDiastolica = 80,
            FrequenciaCardiaca = 72,
            AutorNome = "Joana Técnica",
            AutorConselho = "COREN-SP 999999",
            RegistradoEm = data.ToDateTime(hora),
            RetificaEvolucaoId = retifica,
            MotivoRetificacao = retifica is null ? null : "digitei a PA errada",
            CanceladaEm = cancelada ? DateTime.Now : null,
            MotivoCancelamento = cancelada ? "lançada no paciente errado" : null
        };
        _db.EvolucoesEnfermagem.Add(e);
        await _db.SaveChangesAsync();
        return e;
    }

    [Fact]
    public async Task A_afericao_do_dia_chega_com_a_hora_e_quem_afericou()
    {
        var pacienteId = await PacienteAsync();
        await AferirAsync(pacienteId, Hoje, new TimeOnly(9, 12), 120);

        var vitais = await _consultorio.SinaisVitaisDaSessaoAsync(pacienteId, Hoje);

        vitais.Should().NotBeNull();
        vitais!.Resumo.Should().Contain("PA 120x80").And.Contain("FC 72");
        vitais.Hora.Should().Be(new TimeOnly(9, 12));
        vitais.Procedencia.Should().Contain("09:12")
            .And.Contain("Joana Técnica")
            .And.Contain("COREN-SP 999999",
                "quem afere responde pela aferição, e o conselho é o que a identifica");
    }

    /// <summary>
    /// A técnica afere na chegada e de novo depois da medicação. Quem vai prescrever
    /// precisa do estado MAIS RECENTE — a primeira já está na curva de PA da tela de
    /// Medidas, com a hora ao lado.
    /// </summary>
    [Fact]
    public async Task A_mais_tardia_do_dia_e_a_que_vale()
    {
        var pacienteId = await PacienteAsync();
        await AferirAsync(pacienteId, Hoje, new TimeOnly(9, 0), 120);
        await AferirAsync(pacienteId, Hoje, new TimeOnly(11, 30), 150);

        var vitais = await _consultorio.SinaisVitaisDaSessaoAsync(pacienteId, Hoje);

        vitais!.Resumo.Should().Contain("PA 150x80");
        vitais.Hora.Should().Be(new TimeOnly(11, 30));
    }

    /// <summary>
    /// ⚠️ O teste que carrega a suíte. Registro CANCELADO é registro desdito, e um número
    /// desdito apresentado como a aferição do dia muda conduta sem que ninguém possa
    /// distingui-lo do certo.
    /// </summary>
    [Fact]
    public async Task Afericao_CANCELADA_nao_vale()
    {
        var pacienteId = await PacienteAsync();
        await AferirAsync(pacienteId, Hoje, new TimeOnly(9, 0), 190, cancelada: true);

        (await _consultorio.SinaisVitaisDaSessaoAsync(pacienteId, Hoje)).Should().BeNull();
    }

    /// <summary>
    /// Retificar não apaga: a anterior FICA na folha, marcada. Mas quem prescreve tem de
    /// ver a corrigida — é para isso que a retificação existe.
    /// </summary>
    [Fact]
    public async Task Afericao_RETIFICADA_perde_para_a_que_a_corrigiu()
    {
        var pacienteId = await PacienteAsync();
        var errada = await AferirAsync(pacienteId, Hoje, new TimeOnly(9, 0), 190);
        await AferirAsync(pacienteId, Hoje, new TimeOnly(9, 0), 130, retifica: errada.Id);

        var vitais = await _consultorio.SinaisVitaisDaSessaoAsync(pacienteId, Hoje);

        vitais!.Resumo.Should().Contain("PA 130x80")
            .And.NotContain("190", "o número retificado não pode voltar pela porta da tela");
    }

    /// <summary>
    /// ⚠️ O dia é o da SESSÃO, nunca hoje: a dívida de prontuário e a Minha semana abrem
    /// horários de dias passados. A aferição de hoje ao lado da sessão de terça diria que
    /// aquela PA foi medida na consulta que está sendo escrita.
    /// </summary>
    [Fact]
    public async Task Afericao_de_OUTRO_dia_nao_entra_na_sessao()
    {
        var pacienteId = await PacienteAsync();
        await AferirAsync(pacienteId, Hoje, new TimeOnly(9, 0), 120);

        var deTerca = await _consultorio.SinaisVitaisDaSessaoAsync(
            pacienteId, Hoje.AddDays(-2));

        deTerca.Should().BeNull(
            "aferição antiga tem casa: a curva de PA da tela de Medidas, com a procedência");
    }

    /// <summary>
    /// Evolução de enfermagem SEM sinais vitais (um curativo, uma orientação) não é
    /// aferição — e devolver o registro dela faria a tela escrever uma linha em branco
    /// onde deveria dizer que ninguém mediu.
    /// </summary>
    [Fact]
    public async Task Registro_sem_sinais_vitais_nao_conta_como_afericao()
    {
        var pacienteId = await PacienteAsync();
        _db.EvolucoesEnfermagem.Add(new EvolucaoEnfermagem
        {
            PacienteId = pacienteId,
            Data = Hoje,
            Hora = new TimeOnly(10, 0),
            Texto = "Curativo trocado, ferida limpa.",
            AutorNome = "Joana Técnica",
            RegistradoEm = DateTime.Now
        });
        await _db.SaveChangesAsync();

        (await _consultorio.SinaisVitaisDaSessaoAsync(pacienteId, Hoje)).Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
