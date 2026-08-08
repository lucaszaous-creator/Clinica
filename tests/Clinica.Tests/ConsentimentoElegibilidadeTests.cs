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
    private readonly ConsultaService _consultas;
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
        _consultas = new ConsultaService(_repo);
        _elegibilidade = new ElegibilidadeService(_repo, _autorizacoes, _consentimentos, _consultas);
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

    // ==================== O que os OUTROS módulos mandam ao balcão (parcela 27) ====================

    /// <summary>
    /// O serviço construído sem o Financeiro continua respondendo — só não confere
    /// dívida. A conferência financeira é acréscimo, não a razão de existir da classe;
    /// derrubar a elegibilidade inteira por falta dela seria trocar um aviso por um erro.
    /// </summary>
    [Fact]
    public async Task Elegibilidade_SemOFinanceiroMontado_NaoQuebra()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Alertas.Should().NotContain(a =>
            a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito);
    }

    [Fact]
    public async Task Elegibilidade_PacienteComContaVencida_AvisaOBalcao()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var financeiro = new FinanceiroService(_repo);
        var contas = new ContasService(_repo);
        var comFinanceiro = new ElegibilidadeService(
            _repo, _autorizacoes, _consentimentos, _consultas,
            new InadimplenciaService(_repo, financeiro));

        await contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão avulsa", 120m,
            vencimento: Hoje.AddDays(-20), pacienteId: pacienteId);

        var resultado = await comFinanceiro.ConferirAsync(pacienteId, Hoje);

        // Amarelo, nunca vermelho: vermelho aqui significa "a guia vai ser recusada",
        // que é problema do convênio. Dívida é assunto de conversa — e ninguém deixa de
        // ser atendido por dever.
        var aviso = resultado.Alertas.Should()
            .ContainSingle(a => a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito).Subject;
        aviso.Urgencia.Should().Be(NivelUrgencia.Amarelo);
        aviso.Descricao.Should().Contain("20 dia");
    }

    [Fact]
    public async Task Elegibilidade_ContaVencidaOntem_NaoAlarma()
    {
        // A conta que venceu ontem quase sempre está em trânsito (boleto compensando,
        // PIX de domingo). Alerta que dispara para todo mundo é alerta que ninguém lê.
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var financeiro = new FinanceiroService(_repo);
        var contas = new ContasService(_repo);
        var comFinanceiro = new ElegibilidadeService(
            _repo, _autorizacoes, _consentimentos, _consultas,
            new InadimplenciaService(_repo, financeiro));

        await contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão avulsa", 120m,
            vencimento: Hoje.AddDays(-1), pacienteId: pacienteId);

        var resultado = await comFinanceiro.ConferirAsync(pacienteId, Hoje);

        resultado.Alertas.Should().NotContain(a =>
            a.Motivo == ImpedimentoElegibilidade.PacienteEmDebito);
    }

    [Fact]
    public async Task Elegibilidade_GuiaGlosadaEmAberto_AvisaComOPrazo()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var atendimentos = new AtendimentoService(_repo);
        var glosas = new GlosaService(_repo, new ParametrosService(_repo));

        await atendimentos.LancarAsync(
            pacienteId, Hoje.AddDays(-10), ModalidadeAtendimento.AcupunturaComEletro);

        var codigo = (await _repo.CodigosNoPeriodoAsync(Hoje.AddDays(-10), Hoje.AddDays(-10)))
            .First(c => c.Atendimento!.PacienteId == pacienteId);
        codigo.DarBaixa(Hoje.AddDays(-8), "9001", "secretaria", null);
        await _db.SaveChangesAsync();

        // Glosa de ontem: o prazo de recurso ainda corre inteiro.
        await glosas.RegistrarAsync(codigo.Id, Hoje.AddDays(-1), "Falta assinatura do paciente");

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        var aviso = resultado.Alertas.Should()
            .ContainSingle(a => a.Motivo == ImpedimentoElegibilidade.GuiaGlosada).Subject;
        aviso.Descricao.Should().Contain("assinatura");
        aviso.Descricao.Should().Contain("recorrer");
    }

    [Fact]
    public async Task Elegibilidade_GlosaComPrazoAcabando_FicaVermelha()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var atendimentos = new AtendimentoService(_repo);
        var glosas = new GlosaService(_repo, new ParametrosService(_repo));

        await atendimentos.LancarAsync(
            pacienteId, Hoje.AddDays(-40), ModalidadeAtendimento.AcupunturaComEletro);

        var codigo = (await _repo.CodigosNoPeriodoAsync(Hoje.AddDays(-40), Hoje.AddDays(-40)))
            .First(c => c.Atendimento!.PacienteId == pacienteId);
        codigo.DarBaixa(Hoje.AddDays(-38), "9002", "secretaria", null);
        await _db.SaveChangesAsync();

        // Glosada há 27 dias: com o prazo padrão de 30, restam 3 para recorrer.
        await glosas.RegistrarAsync(codigo.Id, Hoje.AddDays(-27), "Sem senha");

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Alertas.Should().Contain(a =>
            a.Motivo == ImpedimentoElegibilidade.GuiaGlosada
            && a.Urgencia == NivelUrgencia.Vermelho);
    }

    [Fact]
    public async Task Elegibilidade_GlosaRecuperada_NaoAvisaMais()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var atendimentos = new AtendimentoService(_repo);
        var glosas = new GlosaService(_repo, new ParametrosService(_repo));

        await atendimentos.LancarAsync(
            pacienteId, Hoje.AddDays(-10), ModalidadeAtendimento.AcupunturaComEletro);

        var codigo = (await _repo.CodigosNoPeriodoAsync(Hoje.AddDays(-10), Hoje.AddDays(-10)))
            .First(c => c.Atendimento!.PacienteId == pacienteId);
        codigo.DarBaixa(Hoje.AddDays(-8), "G-3", "secretaria", null);
        await _db.SaveChangesAsync();

        await glosas.RegistrarAsync(codigo.Id, Hoje.AddDays(-5), "Falta assinatura");
        await glosas.ReapresentarAsync(codigo.Id, Hoje.AddDays(-3));
        await glosas.MarcarRecuperadaAsync(codigo.Id);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        // Glosa resolvida não é notícia. Continuar avisando gastaria a atenção do balcão
        // com o que já está feito — e alerta que se repete sem motivo deixa de ser lido.
        resultado.Alertas.Should().NotContain(a =>
            a.Motivo == ImpedimentoElegibilidade.GuiaGlosada);
    }

    // ===== Consulta renovável =====
    //
    // A consulta cobre laudo, receita e dúvida por 22 ou 30 dias conforme o convênio, e
    // era lida em exatamente dois lugares — a aba Consultas e o painel de pendências —,
    // nenhum deles aberto com o paciente no balcão. Renovar com ele presente é uma
    // assinatura; descobrir na hora de faturar é ligar para quem já foi embora.

    [Fact]
    public async Task Elegibilidade_ConsultaVencida_AvisaEmVermelho()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        // Unimed: 22 dias de validade. Emitida há 40 → vencida.
        await _consultas.RenovarAsync(pacienteId, Hoje.AddDays(-40));

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        var aviso = resultado.Alertas.Should()
            .ContainSingle(a => a.Motivo == ImpedimentoElegibilidade.ConsultaVencida).Subject;
        // Vermelho pela regra deste serviço: o convênio recusa o que for faturado sem
        // consulta vigente.
        aviso.Urgencia.Should().Be(NivelUrgencia.Vermelho);
        aviso.Descricao.Should().Contain("Consulta vencida");
    }

    [Fact]
    public async Task Elegibilidade_ConsultaAVencerDentroDaJanela_AvisaEmAmarelo()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        // 22 dias de validade, emitida há 20 → vence em 2 dias (janela padrão: 5).
        await _consultas.RenovarAsync(pacienteId, Hoje.AddDays(-20));

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        var aviso = resultado.Alertas.Should()
            .ContainSingle(a => a.Motivo == ImpedimentoElegibilidade.ConsultaARenovar).Subject;
        aviso.Urgencia.Should().Be(NivelUrgencia.Amarelo);
        aviso.Descricao.Should().Contain("2 dia(s)");
    }

    [Fact]
    public async Task Elegibilidade_ConsultaComFolga_NaoAvisa()
    {
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);
        await _consultas.RenovarAsync(pacienteId, Hoje); // vence só daqui a 22 dias

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Alertas.Should().NotContain(a =>
            a.Motivo == ImpedimentoElegibilidade.ConsultaVencida
            || a.Motivo == ImpedimentoElegibilidade.ConsultaARenovar);
    }

    [Fact]
    public async Task Elegibilidade_PacienteSemConsultaNenhuma_NaoAvisa()
    {
        // Alerta que dispara para toda a base de convênio no primeiro dia é alerta que
        // ninguém lê. "Renovar" pressupõe que exista uma consulta emitida.
        var pacienteId = await CriarPacienteAsync(Hoje.AddYears(1));
        await ConsentirAsync(pacienteId);

        var resultado = await _elegibilidade.ConferirAsync(pacienteId, Hoje);

        resultado.Alertas.Should().NotContain(a =>
            a.Motivo == ImpedimentoElegibilidade.ConsultaVencida
            || a.Motivo == ImpedimentoElegibilidade.ConsultaARenovar);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
