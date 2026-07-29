using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Recebíveis de cartão (parcela 16).
///
/// O que estes testes protegem:
/// 1. **A conciliação é por DEPÓSITO, não por venda.** A adquirente deposita o lote do
///    dia; conferir venda a venda contra um extrato que traz um valor só nunca fecharia.
/// 2. **O líquido do depósito é bruto menos TAXA, sem imposto.** O imposto é recolhido
///    depois, por guia; descontá-lo aqui faria o número não bater com o extrato — que é
///    o único uso desta tela.
/// 3. **A data real é informada, não assumida como hoje.** A clínica confere o extrato na
///    segunda e o depósito caiu na sexta; gravar "hoje" transformaria todo depósito num
///    atraso de três dias, errando justamente a métrica que a tela mede.
/// </summary>
public class RecebiveisServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RecebiveisService _recebiveis;
    private readonly FinanceiroService _financeiro;
    private readonly TaxaService _taxas;

    private static readonly DateOnly Hoje = new(2026, 9, 20);

    public RecebiveisServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _recebiveis = new RecebiveisService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _taxas = new TaxaService(_repo);
    }

    private Task<TaxaCartao> CriarTaxaAsync(
        string adquirente, decimal percentual, int dias, ModalidadeCartao modalidade)
        => _taxas.SalvarAsync(new TaxaCartao
        {
            Adquirente = adquirente,
            Modalidade = modalidade,
            Percentual = percentual,
            DiasParaReceber = dias
        });

    /// <summary>Uma venda no crédito, com a taxa já aplicada — como o balcão a lança.</summary>
    private async Task<LancamentoFinanceiro> VenderNoCreditoAsync(
        decimal valor, DateOnly dia, string adquirente = "Stone")
    {
        var deducoes = await _taxas.CalcularAsync(
            valor, dia, FormaPagamento.CartaoCredito, adquirente);
        return await _financeiro.LancarAsync(
            dia, TipoLancamento.Entrada, "Sessão", valor,
            StatusLancamento.Realizado, FormaPagamento.CartaoCredito,
            deducoes: deducoes, adquirente: adquirente);
    }

    // ---------------- O que há para receber ----------------

    [Fact]
    public async Task Deposito_AgrupaAsVendasDoMesmoDiaEAdquirente()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(100m, Hoje);
        await VenderNoCreditoAsync(200m, Hoje);

        var depositos = await _recebiveis.EsperadosAsync(Hoje.AddDays(60));

        // A adquirente não deposita venda por venda: deposita o lote do dia.
        depositos.Should().ContainSingle();
        depositos[0].Adquirente.Should().Be("Stone");
        depositos[0].Previsao.Should().Be(Hoje.AddDays(30));
        depositos[0].Bruto.Should().Be(300m);
        depositos[0].Taxa.Should().Be(9m);
        depositos[0].Liquido.Should().Be(291m);
        depositos[0].Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task Deposito_SeparaAdquirentesDiferentes()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await CriarTaxaAsync("Cielo", 4m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(100m, Hoje, "Stone");
        await VenderNoCreditoAsync(100m, Hoje, "Cielo");

        var depositos = await _recebiveis.EsperadosAsync(Hoje.AddDays(60));

        depositos.Should().HaveCount(2);
        depositos.Should().Contain(d => d.Adquirente == "Cielo" && d.Liquido == 96m);
        depositos.Should().Contain(d => d.Adquirente == "Stone" && d.Liquido == 97m);
    }

    [Fact]
    public async Task Deposito_OLiquidoNaoDescontaImposto()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);

        // Venda com imposto retido: o extrato da adquirente NÃO conhece esse imposto,
        // que é recolhido depois por guia. Descontá-lo aqui faria o número não bater.
        var deducoes = new DeducoesRecebimento(
            3m, 3m, 6m, 6m, Hoje.AddDays(30), "Stone");
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão", 100m,
            StatusLancamento.Realizado, FormaPagamento.CartaoCredito,
            deducoes: deducoes, adquirente: "Stone");

        var deposito = (await _recebiveis.EsperadosAsync(Hoje.AddDays(60))).Single();

        deposito.Liquido.Should().Be(97m);
    }

    [Fact]
    public async Task Deposito_SemAdquirenteInformado_ContinuaVisivel()
    {
        var deducoes = new DeducoesRecebimento(null, null, null, null, Hoje.AddDays(30), null);
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão", 150m,
            StatusLancamento.Realizado, FormaPagamento.CartaoCredito, deducoes: deducoes);

        var depositos = await _recebiveis.EsperadosAsync(Hoje.AddDays(60));

        // O dinheiro continua sendo esperado — só não se sabe de quem. Sumir com ele
        // esconderia valor a receber.
        depositos.Should().ContainSingle().Which.Adquirente.Should().Be("(não informado)");
    }

    [Fact]
    public async Task DinheiroEPix_NaoEntram()
    {
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Dinheiro", 100m,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro);
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Pix", 100m,
            StatusLancamento.Realizado, FormaPagamento.Pix);

        // Entram inteiros e na hora: não há o que esperar, e listá-los encheria a tela
        // de linhas sem ação.
        (await _recebiveis.EsperadosAsync(Hoje.AddDays(60))).Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelado_NaoEEsperado()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        var venda = await VenderNoCreditoAsync(100m, Hoje);

        await _financeiro.CancelarAsync(venda.Id, "estorno ao paciente");

        (await _recebiveis.EsperadosAsync(Hoje.AddDays(60))).Should().BeEmpty();
    }

    // ---------------- O atraso ----------------

    [Fact]
    public async Task Atrasado_EODepositoQuePassouDaDataENaoCaiu()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(500m, Hoje.AddDays(-40));  // previsto para 10 dias atrás
        await VenderNoCreditoAsync(300m, Hoje.AddDays(-10));  // previsto para daqui a 20

        var atrasados = await _recebiveis.AtrasadosAsync(Hoje);

        atrasados.Should().ContainSingle();
        atrasados[0].Previsao.Should().Be(Hoje.AddDays(-10));
        atrasados[0].Liquido.Should().Be(485m);
    }

    [Fact]
    public async Task Atrasado_ApareceMesmoForaDoPeriodoConsultado()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(500m, Hoje.AddDays(-60));

        // Esconder o problema porque ele é antigo é esconder justamente o que importa.
        var esperados = await _recebiveis.EsperadosAsync(Hoje);

        esperados.Should().ContainSingle();
        esperados[0].Atrasado(Hoje).Should().BeTrue();
    }

    [Fact]
    public async Task VenceHoje_NaoEstaAtrasado()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(100m, Hoje.AddDays(-30));

        (await _recebiveis.AtrasadosAsync(Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task Resumo_SeparaOAtrasadoDoQueAindaVaiCair()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(1000m, Hoje.AddDays(-40)); // atrasado
        await VenderNoCreditoAsync(2000m, Hoje.AddDays(-5));  // cai daqui a 25

        var r = await _recebiveis.ResumoAsync(Hoje, Hoje.AddDays(60));

        r.LiquidoAtrasado.Should().Be(970m);
        r.LiquidoAVencer.Should().Be(1940m);
        r.QuantidadeAtrasada.Should().Be(1);
        r.TemAtraso.Should().BeTrue();
        r.ProximoDeposito.Should().Be(Hoje.AddDays(25));
        r.Total.Should().Be(2910m);
    }

    [Fact]
    public async Task Resumo_SemRecebivel_NaoTemProximoDeposito()
    {
        var r = await _recebiveis.ResumoAsync(Hoje, Hoje.AddDays(60));

        r.Total.Should().Be(0m);
        r.ProximoDeposito.Should().BeNull();
        r.TemAtraso.Should().BeFalse();
    }

    // ---------------- A confirmação ----------------

    [Fact]
    public async Task Confirmar_TiraODepositoDaEspera()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        await VenderNoCreditoAsync(100m, Hoje);
        await VenderNoCreditoAsync(200m, Hoje);

        var deposito = (await _recebiveis.EsperadosAsync(Hoje.AddDays(60))).Single();
        var n = await _recebiveis.ConfirmarAsync(
            deposito.LancamentoIds, Hoje.AddDays(31), "ana");

        n.Should().Be(2);
        (await _recebiveis.EsperadosAsync(Hoje.AddDays(60))).Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar_GuardaADataREALEPreservaAPrevista()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        var venda = await VenderNoCreditoAsync(100m, Hoje);

        // Caiu três dias depois do previsto — a divergência É o registro do atraso.
        await _recebiveis.ConfirmarAsync([venda.Id], Hoje.AddDays(33));

        var gravado = await _repo.ObterLancamentoAsync(venda.Id);
        gravado!.RecebimentoConfirmadoEm.Should().Be(Hoje.AddDays(33));
        // Sobrescrever a previsão apagaria a prova de que houve atraso.
        gravado.PrevisaoRecebimento.Should().Be(Hoje.AddDays(30));
        gravado.RecebivelEmAberto.Should().BeFalse();
    }

    [Fact]
    public async Task Confirmar_DuasVezes_NaoReescreveAPrimeiraData()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        var venda = await VenderNoCreditoAsync(100m, Hoje);

        await _recebiveis.ConfirmarAsync([venda.Id], Hoje.AddDays(31));
        var segunda = await _recebiveis.ConfirmarAsync([venda.Id], Hoje.AddDays(40));

        segunda.Should().Be(0);
        (await _repo.ObterLancamentoAsync(venda.Id))!
            .RecebimentoConfirmadoEm.Should().Be(Hoje.AddDays(31));
    }

    [Fact]
    public async Task Confirmar_SemIds_NaoFazNada()
    {
        (await _recebiveis.ConfirmarAsync([], Hoje)).Should().Be(0);
    }

    [Fact]
    public async Task Confirmar_GravaAuditoria()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        var venda = await VenderNoCreditoAsync(100m, Hoje);

        await _recebiveis.ConfirmarAsync([venda.Id], Hoje.AddDays(30), "joao");

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "RecebimentoConfirmado" && e.Operador == "joao");
    }

    [Fact]
    public async Task Desfazer_DevolveORecebivelAEspera()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        var venda = await VenderNoCreditoAsync(100m, Hoje);
        await _recebiveis.ConfirmarAsync([venda.Id], Hoje.AddDays(31));

        var n = await _recebiveis.DesfazerConfirmacaoAsync([venda.Id], "ana");

        n.Should().Be(1);
        (await _recebiveis.EsperadosAsync(Hoje.AddDays(60))).Should().ContainSingle();
        // Desfazer não apaga o lançamento: o dinheiro continua lançado no caixa.
        (await _repo.ObterLancamentoAsync(venda.Id))!.Valor.Should().Be(100m);
    }

    [Fact]
    public async Task Desfazer_OQueNuncaFoiConfirmado_NaoFazNada()
    {
        await CriarTaxaAsync("Stone", 3m, dias: 30, ModalidadeCartao.CreditoAVista);
        var venda = await VenderNoCreditoAsync(100m, Hoje);

        (await _recebiveis.DesfazerConfirmacaoAsync([venda.Id])).Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
