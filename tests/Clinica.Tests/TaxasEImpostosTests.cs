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
/// Taxa da maquininha e imposto retido (parcela 9).
///
/// O buraco que a parcela fecha: o caixa só conhecia o BRUTO. A clínica passava R$ 150 no
/// cartão, a tela dizia R$ 150, e o extrato da adquirente trazia R$ 145,20 trinta dias
/// depois — a diferença virava um caixa que não bate no fim do mês.
///
/// As três regras que carregam o arquivo:
/// 1. o valor do lançamento continua sendo o BRUTO, e o líquido é CALCULADO;
/// 2. a taxa é COPIADA da vigência do dia — renegociar não reescreve o passado;
/// 3. sem taxa cadastrada não se inventa desconto.
/// </summary>
public class TaxasEImpostosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly TaxaService _taxas;
    private readonly ParametrosService _parametros;
    private readonly FinanceiroService _financeiro;

    private static readonly DateOnly Hoje = new(2026, 8, 20);

    public TaxasEImpostosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _taxas = new TaxaService(_repo, _parametros);
        _financeiro = new FinanceiroService(_repo);
    }

    private Task<TaxaCartao> CriarTaxaAsync(
        decimal percentual, ModalidadeCartao modalidade = ModalidadeCartao.CreditoAVista,
        string adquirente = "Stone", string? bandeira = null, int dias = 30,
        DateOnly? de = null, DateOnly? ate = null, int? parcelasDe = null, int? parcelasAte = null)
        => _taxas.SalvarAsync(new TaxaCartao
        {
            Adquirente = adquirente,
            Bandeira = bandeira,
            Modalidade = modalidade,
            Percentual = percentual,
            DiasParaReceber = dias,
            VigenteDe = de,
            VigenteAte = ate,
            ParcelasDe = parcelasDe,
            ParcelasAte = parcelasAte
        });

    // ==================== Cálculo ====================

    [Fact]
    public async Task Credito_DescontaATaxaEProjetaORecebimento()
    {
        await CriarTaxaAsync(percentual: 3.2m, dias: 30);

        var d = await _taxas.CalcularAsync(150m, Hoje, FormaPagamento.CartaoCredito);

        d.TaxaPercentual.Should().Be(3.2m);
        d.ValorTaxa.Should().Be(4.80m);
        // O paciente pagou hoje; a clínica recebe em 30 dias. É o que sustenta o "a receber".
        d.PrevisaoRecebimento.Should().Be(Hoje.AddDays(30));
        d.Procedencia.Should().Contain("Stone");
    }

    [Fact]
    public async Task PixEDinheiro_NaoTemTaxaDeAdquirente()
    {
        await CriarTaxaAsync(percentual: 3.2m);

        (await _taxas.CalcularAsync(150m, Hoje, FormaPagamento.Pix)).ValorTaxa.Should().BeNull();
        (await _taxas.CalcularAsync(150m, Hoje, FormaPagamento.Dinheiro)).ValorTaxa.Should().BeNull();
    }

    [Fact]
    public async Task SemTaxaCadastrada_NaoInventaDesconto()
    {
        // Chutar uma taxa "de mercado" daria um líquido errado com aparência de exato.
        var d = await _taxas.CalcularAsync(150m, Hoje, FormaPagamento.CartaoCredito);

        d.ValorTaxa.Should().BeNull();
        d.PrevisaoRecebimento.Should().BeNull();
        d.Total.Should().Be(0m);
    }

    [Fact]
    public async Task DebitoECredito_UsamTaxasDiferentes()
    {
        await CriarTaxaAsync(percentual: 1.5m, modalidade: ModalidadeCartao.Debito, dias: 1);
        await CriarTaxaAsync(percentual: 3.5m, modalidade: ModalidadeCartao.CreditoAVista, dias: 30);

        (await _taxas.CalcularAsync(200m, Hoje, FormaPagamento.CartaoDebito)).ValorTaxa.Should().Be(3.00m);
        (await _taxas.CalcularAsync(200m, Hoje, FormaPagamento.CartaoCredito)).ValorTaxa.Should().Be(7.00m);
    }

    [Fact]
    public async Task Arredondamento_VaiAoCentavoMeioParaCima()
    {
        await CriarTaxaAsync(percentual: 3.33m);

        // 99,90 × 3,33% = 3,32667 → 3,33. Truncar deixaria centavo sobrando todo mês.
        (await _taxas.CalcularAsync(99.90m, Hoje, FormaPagamento.CartaoCredito))
            .ValorTaxa.Should().Be(3.33m);
    }

    // ==================== Vigência e especificidade ====================

    [Fact]
    public async Task Vigencia_RenegociarNaoReescreveOPassado()
    {
        await CriarTaxaAsync(percentual: 4m, ate: Hoje.AddDays(-1));
        await CriarTaxaAsync(percentual: 2.5m, de: Hoje);

        var antes = await _taxas.CalcularAsync(100m, Hoje.AddDays(-10), FormaPagamento.CartaoCredito);
        var agora = await _taxas.CalcularAsync(100m, Hoje, FormaPagamento.CartaoCredito);

        antes.ValorTaxa.Should().Be(4.00m);
        agora.ValorTaxa.Should().Be(2.50m);
    }

    [Fact]
    public async Task ARegraDaBandeira_VenceARegraGeralDoAdquirente()
    {
        await CriarTaxaAsync(percentual: 3.5m);                       // todas as bandeiras
        await CriarTaxaAsync(percentual: 2.9m, bandeira: "Visa");     // exceção

        var visa = await _taxas.CalcularAsync(
            100m, Hoje, FormaPagamento.CartaoCredito, bandeira: "Visa");
        var elo = await _taxas.CalcularAsync(
            100m, Hoje, FormaPagamento.CartaoCredito, bandeira: "Elo");

        // Sem isso, a clínica cadastraria a exceção e continuaria vendo o número geral.
        visa.ValorTaxa.Should().Be(2.90m);
        elo.ValorTaxa.Should().Be(3.50m);
    }

    [Fact]
    public async Task Parcelado_UsaAFaixaDeParcelas()
    {
        await CriarTaxaAsync(
            percentual: 4.5m, modalidade: ModalidadeCartao.CreditoParcelado,
            parcelasDe: 2, parcelasAte: 6);
        await CriarTaxaAsync(
            percentual: 6.9m, modalidade: ModalidadeCartao.CreditoParcelado,
            parcelasDe: 7, parcelasAte: 12);

        var tres = await _taxas.CalcularAsync(
            1000m, Hoje, FormaPagamento.CartaoCredito, parcelas: 3);
        var dez = await _taxas.CalcularAsync(
            1000m, Hoje, FormaPagamento.CartaoCredito, parcelas: 10);

        tres.ValorTaxa.Should().Be(45.00m);
        dez.ValorTaxa.Should().Be(69.00m);
    }

    [Fact]
    public async Task TaxaInativa_NaoEAplicada()
    {
        var taxa = await CriarTaxaAsync(percentual: 3m);
        taxa.Ativa = false;
        await _taxas.SalvarAsync(taxa);

        (await _taxas.CalcularAsync(100m, Hoje, FormaPagamento.CartaoCredito))
            .ValorTaxa.Should().BeNull();
    }

    // ==================== Imposto ====================

    [Fact]
    public async Task Imposto_SoIncideQuandoConfiguradoEPedido()
    {
        await _parametros.SalvarAliquotaImpostoAsync(5m);

        var comRetencao = await _taxas.CalcularAsync(
            200m, Hoje, FormaPagamento.Pix, reterImposto: true);
        var semRetencao = await _taxas.CalcularAsync(
            200m, Hoje, FormaPagamento.Pix, reterImposto: false);

        comRetencao.ValorImposto.Should().Be(10.00m);
        semRetencao.ValorImposto.Should().BeNull();
    }

    [Fact]
    public async Task Imposto_PadraoEZero_ENaoDescontaNada()
    {
        // Cada clínica tem o seu regime. Chutar uma alíquota erraria a base inteira.
        (await _parametros.ObterAliquotaImpostoAsync()).Should().Be(0m);

        (await _taxas.CalcularAsync(200m, Hoje, FormaPagamento.Pix, reterImposto: true))
            .ValorImposto.Should().BeNull();
    }

    [Fact]
    public async Task Aliquota_ELidaComPontoDecimal_IndependenteDaCultura()
    {
        await _parametros.SalvarAliquotaImpostoAsync(2.5m);

        // "2,5" lido como 25 multiplicaria o imposto por dez na máquina com vírgula.
        (await _parametros.ObterAliquotaImpostoAsync()).Should().Be(2.5m);
    }

    // ==================== O lançamento ====================

    [Fact]
    public async Task Lancamento_GuardaOBrutoECalculaOLiquido()
    {
        await CriarTaxaAsync(percentual: 3m, dias: 30);
        await _parametros.SalvarAliquotaImpostoAsync(2m);

        var d = await _taxas.CalcularAsync(
            100m, Hoje, FormaPagamento.CartaoCredito, reterImposto: true);

        var lancamento = await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão", 100m,
            formaPagamento: FormaPagamento.CartaoCredito, deducoes: d,
            adquirente: "Stone", modalidadeCartao: ModalidadeCartao.CreditoAVista);

        // O bruto é o que o paciente pagou — é ele que fica no Valor.
        lancamento.Valor.Should().Be(100m);
        lancamento.ValorTaxa.Should().Be(3.00m);
        lancamento.ValorImposto.Should().Be(2.00m);
        // O líquido é calculado, nunca gravado: uma verdade só sobre o mesmo dinheiro.
        lancamento.ValorLiquido.Should().Be(95.00m);
        lancamento.PrevisaoRecebimento.Should().Be(Hoje.AddDays(30));
        lancamento.TaxaPercentual.Should().Be(3m);
    }

    [Fact]
    public async Task Lancamento_RecusaDeducaoMaiorQueOBruto()
    {
        var absurda = new DeducoesRecebimento(90m, 90m, 30m, 30m, null, "teste");

        var lancar = () => _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão", 100m, deducoes: absurda);

        // Recebimento líquido negativo não existe: a clínica não paga para atender.
        await lancar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResumoDoCaixa_SeparaOBrutoDoLiquido()
    {
        await CriarTaxaAsync(percentual: 4m);
        var d = await _taxas.CalcularAsync(500m, Hoje, FormaPagamento.CartaoCredito);

        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Cartão", 500m,
            formaPagamento: FormaPagamento.CartaoCredito, deducoes: d);
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Pix", 300m, formaPagamento: FormaPagamento.Pix);

        var resumo = await _financeiro.ResumoAsync(Hoje, Hoje);

        resumo.EntradasRealizadas.Should().Be(800m);
        resumo.TaxasDescontadas.Should().Be(20m);
        // 800 entrou pelo balcão; 780 é o que a clínica de fato recebe.
        resumo.EntradasLiquidas.Should().Be(780m);
        resumo.TemDeducao.Should().BeTrue();
    }

    [Fact]
    public async Task ResumoDoCaixa_NaoContaDeducaoDeEntradaAindaPrevista()
    {
        await CriarTaxaAsync(percentual: 4m);
        var d = await _taxas.CalcularAsync(500m, Hoje, FormaPagamento.CartaoCredito);

        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "A receber", 500m,
            status: StatusLancamento.Previsto,
            formaPagamento: FormaPagamento.CartaoCredito, deducoes: d);

        var resumo = await _financeiro.ResumoAsync(Hoje, Hoje);

        // Descontar taxa de dinheiro que ainda não entrou inflaria o "descontado" do mês
        // com o que a adquirente nem processou.
        resumo.TaxasDescontadas.Should().Be(0m);
        resumo.TemDeducao.Should().BeFalse();
    }

    // ==================== Catálogo ====================

    [Fact]
    public async Task Catalogo_RecusaPercentualForaDaFaixa()
    {
        var salvar = () => CriarTaxaAsync(percentual: 130m);
        await salvar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Catalogo_RecusaFaixaDeParcelasInvertida()
    {
        var salvar = () => CriarTaxaAsync(
            percentual: 3m, modalidade: ModalidadeCartao.CreditoParcelado,
            parcelasDe: 10, parcelasAte: 2);
        await salvar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Catalogo_RecusaVigenciaInvertida()
    {
        var salvar = () => CriarTaxaAsync(
            percentual: 3m, de: Hoje.AddDays(10), ate: Hoje);
        await salvar.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
