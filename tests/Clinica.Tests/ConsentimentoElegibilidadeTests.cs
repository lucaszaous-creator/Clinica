using Clinica.Application.Modelos;
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
/// Consentimento LGPD e elegibilidade (parcela 2).
///
/// Duas ideias sustentam esta suíte:
/// 1. consentimento é FATO DATADO, não interruptor — revogar não apaga o registro, que
///    continua provando o consentimento do período já tratado;
/// 2. elegibilidade INFORMA, nunca impede: carteirinha vencida e cota estourada viram
///    glosa depois do serviço prestado, e a recepção precisa saber disso no balcão.
/// </summary>
public class ConsentimentoElegibilidadeTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ConsentimentoService _consentimentos;
    private readonly AutorizacaoService _autorizacoes;
    private readonly ElegibilidadeService _elegibilidade;

    private static readonly DateOnly Hoje = new(2026, 8, 3);

    public ConsentimentoElegibilidadeTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consentimentos = new ConsentimentoService(_repo);
        _autorizacoes = new AutorizacaoService(_repo);
        _elegibilidade = new ElegibilidadeService(_repo, _autorizacoes, _consentimentos);
    }

    private async Task<int> CriarPacienteAsync(DateOnly? validadeCarteirinha = null)
    {
        var p = new Paciente
        {
            Nome = "Paciente",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Carteirinha = "123456",
            ValidadeCarteirinha = validadeCarteirinha
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task CriarAutorizacaoAsync(int pacienteId, string numero, int quantidade, int jaUsadas)
        => _autorizacoes.SalvarAsync(new AutorizacaoSessoes
        {
            PacienteId = pacienteId,
            Convenio = Convenio.UnimedIntercambio,
            Numero = numero,
            DataEmissao = Hoje.AddDays(-30),
            DataValidade = Hoje.AddDays(30),
            QuantidadeAutorizada = quantidade,
            QuantidadeUtilizadaManual = jaUsadas
        }, "secretaria");

    private Task ConsentirAsync(int pacienteId)
        => _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.TratamentoDeDados, true, operador: "secretaria");

    // ===== Consentimento =====

    [Fact]
    public async Task Registrar_ConcedeEFicaVigente()
    {
        var pacienteId = await CriarPacienteAsync();

        await ConsentirAsync(pacienteId);

        (await _consentimentos.VigenteAsync(pacienteId, FinalidadeConsentimento.TratamentoDeDados))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Registrar_RecusaFicaGravadaEnaoVigente()
    {
        var pacienteId = await CriarPacienteAsync();

        await _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.UsoDeImagem, false, operador: "secretaria");

        var historico = await _consentimentos.HistoricoAsync(pacienteId);
        historico.Should().ContainSingle("recusa também é fato a provar");
        historico[0].Concedido.Should().BeFalse();

        (await _consentimentos.VigenteAsync(pacienteId, FinalidadeConsentimento.UsoDeImagem))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Situacao_TrazSoOregistroMaisRecenteDeCadaFinalidade()
    {
        var pacienteId = await CriarPacienteAsync();
        await _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.ComunicacaoEMarketing, false);
        await _consentimentos.RegistrarAsync(
            pacienteId, FinalidadeConsentimento.ComunicacaoEMarketing, true);

        var situacao = await _consentimentos.SituacaoAsync(pacienteId);

        situacao[FinalidadeConsentimento.ComunicacaoEMarketing].Concedido.Should().BeTrue();
        (await _consentimentos.HistoricoAsync(pacienteId))
            .Should().HaveCount(2, "o histórico inteiro fica para auditoria");
    }

    [Fact]
    public async Task Revogar_NaoApagaORegistroAnterior()
    {
        var pacienteId = await CriarPacienteAsync();
        await ConsentirAsync(pacienteId);
        var registro = (await _consentimentos.HistoricoAsync(pacienteId))[0];

        await _consentimentos.RevogarAsync(registro.Id, "secretaria", "pedido do paciente");

        var historico = await _consentimentos.HistoricoAsync(pacienteId);
        historico.Should().ContainSingle("revogar não apaga: a linha prova o período tratado");
        historico[0].Concedido.Should().BeTrue();
        historico[0].RevogadoEm.Should().NotBeNull();
        historico[0].Vigente.Should().BeFalse();
        historico[0].Observacoes.Should().Contain("pedido do paciente");
    }

    [Fact]
    public async Task Revogar_DuasVezes_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        await ConsentirAsync(pacienteId);
        var registro = (await _consentimentos.HistoricoAsync(pacienteId))[0];
        await _consentimentos.RevogarAsync(registro.Id);

        var acao = () => _consentimentos.RevogarAsync(registro.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Revogar_UmaRecusa_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        await _consentimentos.RegistrarAsync(pacienteId, FinalidadeConsentimento.UsoDeImagem, false);
        var registro = (await _consentimentos.HistoricoAsync(pacienteId))[0];

        var acao = () => _consentimentos.RevogarAsync(registro.Id);

        await acao.Should().ThrowAsync<InvalidOperationException>("não há o que revogar numa recusa");
    }

    [Fact]
    public async Task Registrar_DeixaRastroNaAuditoria()
    {
        var pacienteId = await CriarPacienteAsync();

        await ConsentirAsync(pacienteId);

        var evento = await _db.Auditoria.AsNoTracking().SingleAsync();
        evento.Acao.Should().Be("ConsentimentoConcedido");
        evento.PacienteId.Should().Be(pacienteId);
    }

    // ===== Elegibilidade =====

    [Fact]
    public async Task Elegibilidade_CarteirinhaVencida_EhImpedimentoVermelho()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddDays(-1));
        await ConsentirAsync(pacienteId);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.TemImpedimento.Should().BeTrue();
        resultado.Alertas.Should().Contain(a =>
            a.Motivo == ImpedimentoElegibilidade.CarteirinhaVencida
            && a.Urgencia == NivelUrgencia.Vermelho);
    }

    [Fact]
    public async Task Elegibilidade_CarteirinhaAVencer_EhSoAviso()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddDays(10));
        await ConsentirAsync(pacienteId);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.TemImpedimento.Should().BeFalse();
        resultado.Alertas.Should().Contain(a =>
            a.Motivo == ImpedimentoElegibilidade.CarteirinhaAVencer);
    }

    [Fact]
    public async Task Elegibilidade_SemConsentimentoLgpd_Avisa()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Alertas.Should().Contain(a =>
            a.Motivo == ImpedimentoElegibilidade.SemConsentimentoLgpd);
    }

    [Fact]
    public async Task Elegibilidade_SemSenhaNemHistorico_NaoInventaAlerta()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Liberado.Should().BeTrue(
            "numa clínica que não controla senha, avisar sempre seria um alerta que "
            + "dispara para todo mundo — e alerta que sempre aparece ninguém lê");
    }

    [Fact]
    public async Task Elegibilidade_CotaEsgotada_EhImpedimentoVermelho()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        await CriarAutorizacaoAsync(pacienteId, "SENHA-1", quantidade: 2, jaUsadas: 2);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.TemImpedimento.Should().BeTrue();
        resultado.Alertas.Should().Contain(a =>
            a.Motivo == ImpedimentoElegibilidade.CotaEsgotada);
    }

    [Fact]
    public async Task Elegibilidade_UltimaSessaoAutorizada_Avisa()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        await CriarAutorizacaoAsync(pacienteId, "SENHA-2", quantidade: 3, jaUsadas: 2);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.TemImpedimento.Should().BeFalse();
        resultado.Alertas.Should().Contain(a =>
            a.Motivo == ImpedimentoElegibilidade.CotaQuaseNoFim);
    }

    [Fact]
    public async Task Elegibilidade_TudoEmOrdem_Libera()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        await CriarAutorizacaoAsync(pacienteId, "SENHA-3", quantidade: 10, jaUsadas: 0);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Liberado.Should().BeTrue();
        resultado.Alertas.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
