using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Custo de transação na visão da direção (parcela 17).
///
/// O que estes testes protegem:
/// 1. **A taxa EFETIVA contra a de tabela.** A tabela diz 3,1%; se a clínica parcela mais
///    do que imagina, o efetivo do ano é outro. É a divergência que dá argumento na
///    renegociação, e ninguém a calculava.
/// 2. **Percentual nulo, nunca zero, sem base.** "Não custou nada" e "não passou nada"
///    são leituras diferentes.
/// 3. **Só entrada realizada.** Taxa de recebimento que ainda não aconteceu é desconto de
///    receita que não existe.
/// </summary>
public class CustoTransacaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly CustoTransacaoService _custo;
    private readonly FinanceiroService _financeiro;
    private readonly TaxaService _taxas;

    private static readonly DateOnly Referencia = new(2026, 9, 30);

    public CustoTransacaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _custo = new CustoTransacaoService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _taxas = new TaxaService(_repo);
    }

    private Task<LancamentoFinanceiro> ReceberAsync(
        decimal bruto, decimal taxa, DateOnly dia, string? adquirente = null,
        decimal imposto = 0m, StatusLancamento status = StatusLancamento.Realizado)
        => _financeiro.LancarAsync(
            dia, TipoLancamento.Entrada, "Sessão", bruto, status,
            FormaPagamento.CartaoCredito,
            deducoes: new DeducoesRecebimento(
                bruto > 0 ? taxa / bruto * 100m : null,
                taxa > 0 ? taxa : null,
                null,
                imposto > 0 ? imposto : null,
                null, adquirente),
            adquirente: adquirente);

    // ---------------- Por adquirente ----------------

    [Fact]
    public async Task PorAdquirente_SomaEOrdenaPeloQueMaisDESCONTOU()
    {
        await ReceberAsync(1000m, 31m, Referencia, "Cielo");
        await ReceberAsync(5000m, 195m, Referencia, "Stone");

        var linhas = await _custo.PorAdquirenteAsync(Referencia.AddDays(-30), Referencia);

        // Ordena por dinheiro descontado, não por percentual: a Cielo cobra mais caro em
        // percentual (3,1% contra 3,9%... ) mas quem come o caixa é quem processa volume.
        linhas[0].Adquirente.Should().Be("Stone");
        linhas[0].Taxa.Should().Be(195m);
        linhas[1].Adquirente.Should().Be("Cielo");
    }

    [Fact]
    public async Task PorAdquirente_CalculaATaxaEFETIVA()
    {
        // Duas vendas na mesma adquirente com taxas diferentes (à vista e parcelada):
        // o efetivo do período é a média ponderada, e é ele que se leva à renegociação.
        await ReceberAsync(1000m, 31m, Referencia, "Stone");
        await ReceberAsync(1000m, 49m, Referencia, "Stone");

        var stone = (await _custo.PorAdquirenteAsync(Referencia.AddDays(-30), Referencia))
            .Single(a => a.Adquirente == "Stone");

        stone.TaxaEfetiva.Should().Be(4m);
        stone.Liquido.Should().Be(1920m);
        stone.Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task PorAdquirente_SemMaquininha_ApareceMasNaoTemTaxaEfetiva()
    {
        await _financeiro.LancarAsync(
            Referencia, TipoLancamento.Entrada, "Dinheiro", 500m,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro);

        var linhas = await _custo.PorAdquirenteAsync(Referencia.AddDays(-30), Referencia);

        // Entra porque compõe o faturamento sobre o qual o percentual geral é medido;
        // escondê-lo faria a dedução parecer maior fatia do que é.
        var semMaquina = linhas.Single();
        semMaquina.Adquirente.Should().Be("(sem maquininha)");
        semMaquina.Taxa.Should().Be(0m);
        semMaquina.TaxaEfetiva.Should().Be(0m);
    }

    [Fact]
    public async Task PorAdquirente_FracaoEDoCustoDeMaquininha_NaoDoFaturamento()
    {
        await ReceberAsync(1000m, 30m, Referencia, "Stone");
        await ReceberAsync(1000m, 10m, Referencia, "Cielo");
        await _financeiro.LancarAsync(
            Referencia, TipoLancamento.Entrada, "Dinheiro", 8000m,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro);

        var linhas = await _custo.PorAdquirenteAsync(Referencia.AddDays(-30), Referencia);

        // A pergunta da barra é "qual adquirente come mais". Se a fração fosse do
        // faturamento, "(sem maquininha)" levaria 80% da barra sendo que custa zero.
        linhas.Single(a => a.Adquirente == "Stone").Fracao.Should().BeApproximately(0.75d, 0.0001);
        linhas.Single(a => a.Adquirente == "Cielo").Fracao.Should().BeApproximately(0.25d, 0.0001);
        linhas.Single(a => a.Adquirente == "(sem maquininha)").Fracao.Should().Be(0d);
    }

    [Fact]
    public async Task Previsto_NaoEntra()
    {
        await ReceberAsync(1000m, 31m, Referencia, "Stone", status: StatusLancamento.Previsto);

        // Taxa de recebimento que ainda não aconteceu é desconto de receita que não existe.
        (await _custo.PorAdquirenteAsync(Referencia.AddDays(-30), Referencia)).Should().BeEmpty();
    }

    [Fact]
    public async Task Saida_NaoEntra()
    {
        await _financeiro.LancarAsync(
            Referencia, TipoLancamento.Saida, "Aluguel", 3000m, StatusLancamento.Realizado);

        (await _custo.PorAdquirenteAsync(Referencia.AddDays(-30), Referencia)).Should().BeEmpty();
    }

    // ---------------- A série ----------------

    [Fact]
    public async Task Serie_TemUmMesPorPeriodoDoMaisAntigoAoMaisNovo()
    {
        var serie = await _custo.SerieAsync(Referencia, meses: 6);

        serie.Should().HaveCount(6);
        serie[0].Rotulo.Should().Be("abr/26");
        serie[^1].Rotulo.Should().Be("set/26");
    }

    [Fact]
    public async Task Serie_MesSemFaturamento_TemPercentualNULO()
    {
        await ReceberAsync(1000m, 40m, new DateOnly(2026, 9, 10), "Stone", imposto: 60m);

        var serie = await _custo.SerieAsync(Referencia, meses: 2);

        // "—" e não 0%: não medido e zero são coisas diferentes.
        serie[0].PercentualDeducoes.Should().BeNull();
        serie[^1].PercentualDeducoes.Should().Be(10m);
        serie[^1].Deducoes.Should().Be(100m);
        serie[^1].Liquido.Should().Be(900m);
    }

    [Fact]
    public async Task Serie_UsaODiaDoPAGAMENTO()
    {
        // Competência em agosto, recebido em setembro: o custo de maquininha pertence ao
        // mês da venda que de fato entrou.
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 28), TipoLancamento.Entrada, "Sessão", 1000m,
            StatusLancamento.Realizado, FormaPagamento.CartaoCredito,
            dataPagamento: new DateOnly(2026, 9, 3),
            deducoes: new DeducoesRecebimento(3m, 30m, null, null, null, "Stone"),
            adquirente: "Stone");

        var serie = await _custo.SerieAsync(Referencia, meses: 2);

        serie[0].Bruto.Should().Be(0m);
        serie[^1].Bruto.Should().Be(1000m);
    }

    // ---------------- O resumo ----------------

    [Fact]
    public async Task Resumo_TrazOsPercentuaisEAAdquirenteMaisCara()
    {
        await ReceberAsync(10_000m, 390m, Referencia, "Stone", imposto: 600m);
        await ReceberAsync(2_000m, 62m, Referencia, "Cielo");

        var r = await _custo.ResumoAsync(Referencia.AddDays(-30), Referencia);

        r.Bruto.Should().Be(12_000m);
        r.Taxa.Should().Be(452m);
        r.Imposto.Should().Be(600m);
        r.Deducoes.Should().Be(1_052m);
        r.Liquido.Should().Be(10_948m);
        // "Foram R$ 1.052" é um número sem referência; "8,77% de tudo que entrou" é decisão.
        r.PercentualDeducoes.Should().BeApproximately(8.766m, 0.01m);
        r.MaisCara!.Adquirente.Should().Be("Stone");
    }

    [Fact]
    public async Task Resumo_SemFaturamento_TemPercentuaisNULOS()
    {
        var r = await _custo.ResumoAsync(Referencia.AddDays(-30), Referencia);

        r.PercentualDeducoes.Should().BeNull();
        r.PercentualTaxa.Should().BeNull();
        r.PercentualImposto.Should().BeNull();
        r.MaisCara.Should().BeNull();
    }

    [Fact]
    public async Task Resumo_SoRecebimentoSemTaxa_NaoTemAdquirenteMaisCara()
    {
        await _financeiro.LancarAsync(
            Referencia, TipoLancamento.Entrada, "Pix", 500m,
            StatusLancamento.Realizado, FormaPagamento.Pix);

        var r = await _custo.ResumoAsync(Referencia.AddDays(-30), Referencia);

        // A "mais cara" só existe quando alguma coisa custou. Apontar "(sem maquininha)"
        // como a mais cara seria mentira com cara de dado.
        r.MaisCara.Should().BeNull();
        r.PercentualTaxa.Should().Be(0m);
    }

    // ---------------- Efetiva contra tabela ----------------

    [Fact]
    public async Task FaixaDeTabela_TrazOMenorEOMaiorPercentualVigente()
    {
        await _taxas.SalvarAsync(new TaxaCartao
        {
            Adquirente = "Stone", Modalidade = ModalidadeCartao.Debito,
            Percentual = 1.99m, DiasParaReceber = 1
        });
        await _taxas.SalvarAsync(new TaxaCartao
        {
            Adquirente = "Stone", Modalidade = ModalidadeCartao.CreditoParcelado,
            Percentual = 4.99m, DiasParaReceber = 30
        });

        var faixa = await _custo.FaixaDeTabelaAsync("Stone", Referencia);

        // Uma adquirente tem várias taxas; um número só teria de escolher uma modalidade,
        // e a comparação com o efetivo do período — que mistura todas — seria enganosa.
        faixa!.Value.Menor.Should().Be(1.99m);
        faixa.Value.Maior.Should().Be(4.99m);
    }

    [Fact]
    public async Task FaixaDeTabela_IgnoraOQueNaoEstaVigente()
    {
        await _taxas.SalvarAsync(new TaxaCartao
        {
            Adquirente = "Stone", Modalidade = ModalidadeCartao.CreditoAVista,
            Percentual = 5m, DiasParaReceber = 30,
            VigenteAte = Referencia.AddDays(-1)
        });

        (await _custo.FaixaDeTabelaAsync("Stone", Referencia)).Should().BeNull();
    }

    [Fact]
    public async Task FaixaDeTabela_AdquirenteSemCadastro_ENula()
    {
        (await _custo.FaixaDeTabelaAsync("PagSeguro", Referencia)).Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
