using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Regime tributário (parcela 15).
///
/// O que estes testes protegem:
/// 1. **A base de cálculo.** No Presumido o IRPJ de 15% incide sobre 32% da receita e a
///    efetiva é 4,8%. Errar isso multiplica o imposto por três.
/// 2. **A vigência.** Reajuste do ISS não pode reescrever o mês já declarado.
/// 3. **O fallback da alíquota única.** A base do cliente já tem o valor da parcela 9
///    gravado; trocar o mecanismo zerando a retenção faria a clínica emitir um mês
///    inteiro sem imposto sem perceber.
/// 4. **O detalhe copiado.** "Quanto saiu" sem "de quê" não serve ao contador.
/// </summary>
public class TributoServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly TributoService _tributos;

    private static readonly DateOnly Hoje = new(2026, 9, 15);

    public TributoServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _tributos = new TributoService(_repo, _parametros);
    }

    private Task<Tributo> CriarAsync(
        string sigla, decimal percentual, decimal basePercentual = 100m,
        DateOnly? de = null, DateOnly? ate = null, bool ativo = true)
        => _tributos.SalvarAsync(new Tributo
        {
            Sigla = sigla,
            Nome = sigla,
            Percentual = percentual,
            BasePercentual = basePercentual,
            VigenteDe = de,
            VigenteAte = ate,
            Ativo = ativo
        });

    // ---------------- A alíquota efetiva ----------------

    [Fact]
    public void BaseCheia_AliquotaEfetivaEAPropriaAliquota()
    {
        var iss = new Tributo { Sigla = "ISS", Percentual = 3m, BasePercentual = 100m };

        iss.AliquotaEfetiva.Should().Be(3m);
        // A base só aparece escrita quando não é 100%: dizer "sobre 100%" em toda linha
        // viraria ruído.
        iss.Descricao.Should().Be("ISS 3%");
    }

    [Fact]
    public void BasePresumida_AliquotaEfetivaEOProduto()
    {
        // O caso que motivou o campo: a clínica conhece "IRPJ 15%", não "IRPJ 4,8%".
        var irpj = new Tributo { Sigla = "IRPJ", Percentual = 15m, BasePercentual = 32m };

        irpj.AliquotaEfetiva.Should().Be(4.8m);
        irpj.Descricao.Should().Contain("15%").And.Contain("32%").And.Contain("4,8");
    }

    [Fact]
    public async Task Base_ForaDeZeroACem_ERecusada()
    {
        var acao = () => CriarAsync("ISS", 3m, basePercentual: 0m);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Aliquota_ForaDeZeroACem_ERecusada()
    {
        var acao = () => CriarAsync("ISS", 101m);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Sigla_EGravadaEmMaiuscula()
    {
        var t = await CriarAsync("iss", 3m);

        t.Sigla.Should().Be("ISS");
    }

    // ---------------- Vigência ----------------

    [Fact]
    public async Task Vigentes_TrazemSoOQueValeNoDia()
    {
        await CriarAsync("ISS", 3m, ate: Hoje.AddDays(-1));   // encerrado ontem
        await CriarAsync("ISS", 4m, de: Hoje);                 // o reajuste
        await CriarAsync("PIS", 0.65m);                        // sem prazo

        var vigentes = await _tributos.VigentesEmAsync(Hoje);

        vigentes.Should().HaveCount(2);
        vigentes.Should().ContainSingle(t => t.Sigla == "ISS").Which.Percentual.Should().Be(4m);
    }

    [Fact]
    public async Task Reajuste_NaoReescreveOMesJaDeclarado()
    {
        await CriarAsync("ISS", 3m, ate: new DateOnly(2026, 8, 31));
        await CriarAsync("ISS", 4m, de: new DateOnly(2026, 9, 1));

        var agosto = await _tributos.ApurarAsync(1000m, new DateOnly(2026, 8, 20));
        var setembro = await _tributos.ApurarAsync(1000m, new DateOnly(2026, 9, 20));

        agosto.Valor.Should().Be(30m);
        setembro.Valor.Should().Be(40m);
    }

    [Fact]
    public async Task Inativo_NaoEntraNaConta()
    {
        await CriarAsync("ISS", 3m);
        await CriarAsync("PIS", 0.65m, ativo: false);

        (await _tributos.VigentesEmAsync(Hoje)).Should().ContainSingle();
    }

    // ---------------- A apuração ----------------

    [Fact]
    public async Task Apuracao_SomaOsTributosEEscreveDeQueSaoOsNumeros()
    {
        await CriarAsync("ISS", 3m);
        await CriarAsync("PIS", 0.65m);
        await CriarAsync("COFINS", 3m);

        var r = await _tributos.ApurarAsync(1000m, Hoje);

        r.Aliquota.Should().Be(6.65m);
        r.Valor.Should().Be(66.50m);
        // "Quanto saiu" sem "de quê" não serve ao contador, que precisa separar as guias.
        r.Detalhe.Should().Be("COFINS 3% + ISS 3% + PIS 0,65%");
        r.Houve.Should().BeTrue();
    }

    [Fact]
    public async Task Apuracao_ArredondaCadaTributoSeparadamente()
    {
        // 0,65% de 33,33 = 0,2166... e 3% de 33,33 = 0,9999. Arredondar só no fim daria
        // um total que não bate com a soma das linhas do detalhe — e é o detalhe que a
        // clínica leva para o contador.
        await CriarAsync("PIS", 0.65m);
        await CriarAsync("ISS", 3m);

        var r = await _tributos.ApurarAsync(33.33m, Hoje);

        r.Valor.Should().Be(0.22m + 1.00m);
    }

    [Fact]
    public async Task Apuracao_NoPresumido_UsaAEfetiva()
    {
        await CriarAsync("IRPJ", 15m, basePercentual: 32m);

        var r = await _tributos.ApurarAsync(10_000m, Hoje);

        // 4,8%, não 15%: errar isso multiplicaria o imposto por três.
        r.Valor.Should().Be(480m);
    }

    [Fact]
    public async Task Apuracao_SemTributoESemAliquotaAntiga_NaoInventaImposto()
    {
        var r = await _tributos.ApurarAsync(1000m, Hoje);

        r.Should().Be(ImpostoApurado.Nenhum);
        r.Houve.Should().BeFalse();
    }

    [Fact]
    public async Task Apuracao_ValorNaoPositivo_NaoRetemNada()
    {
        await CriarAsync("ISS", 3m);

        (await _tributos.ApurarAsync(0m, Hoje)).Houve.Should().BeFalse();
    }

    // ---------------- Compatibilidade com a parcela 9 ----------------

    [Fact]
    public async Task SemTributoCadastrado_AAliquotaUnicaAntigaContinuaValendo()
    {
        await _parametros.SalvarAliquotaImpostoAsync(2.5m);

        var r = await _tributos.ApurarAsync(1000m, Hoje);

        // A base do cliente já tem esse valor gravado. Trocar o mecanismo zerando a
        // retenção faria a clínica emitir um mês inteiro sem imposto sem perceber.
        r.Valor.Should().Be(25m);
        r.Detalhe.Should().Contain("Alíquota única");
    }

    [Fact]
    public async Task OPrimeiroTributoCadastrado_TomaOLugarDaAliquotaUnica()
    {
        await _parametros.SalvarAliquotaImpostoAsync(2.5m);
        await CriarAsync("ISS", 3m);

        var r = await _tributos.ApurarAsync(1000m, Hoje);

        r.Valor.Should().Be(30m);
        r.Detalhe.Should().Be("ISS 3%");
    }

    [Fact]
    public async Task AliquotaTotal_CaiNaAntigaQuandoNaoHaTributo()
    {
        await _parametros.SalvarAliquotaImpostoAsync(6m);
        (await _tributos.AliquotaTotalAsync(Hoje)).Should().Be(6m);

        await CriarAsync("ISS", 3m);
        await CriarAsync("PIS", 0.65m);
        (await _tributos.AliquotaTotalAsync(Hoje)).Should().Be(3.65m);
    }

    // ---------------- Integração com o lançamento ----------------

    [Fact]
    public async Task Lancamento_GuardaODetalheCopiado()
    {
        await CriarAsync("ISS", 3m);
        await CriarAsync("PIS", 0.65m);

        var taxas = new TaxaService(_repo, _parametros, _tributos);
        var financeiro = new FinanceiroService(_repo);

        var deducoes = await taxas.CalcularAsync(
            200m, Hoje, FormaPagamento.Pix, reterImposto: true);
        var lancamento = await financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão", 200m,
            StatusLancamento.Realizado, FormaPagamento.Pix, deducoes: deducoes);

        lancamento.ValorImposto.Should().Be(7.30m);
        lancamento.DetalheImposto.Should().Be("ISS 3% + PIS 0,65%");
        // O bruto continua sendo o valor do lançamento; o líquido é calculado.
        lancamento.Valor.Should().Be(200m);
        lancamento.ValorLiquido.Should().Be(192.70m);
    }

    [Fact]
    public async Task Lancamento_SemReterImposto_NaoGravaDetalhe()
    {
        await CriarAsync("ISS", 3m);

        var taxas = new TaxaService(_repo, _parametros, _tributos);
        var deducoes = await taxas.CalcularAsync(200m, Hoje, FormaPagamento.Dinheiro);

        deducoes.ValorImposto.Should().BeNull();
        deducoes.DetalheImposto.Should().BeNull();
    }

    [Fact]
    public async Task Editar_MantemOMesmoRegistroEGravaAuditoria()
    {
        var t = await CriarAsync("ISS", 3m);

        t.Percentual = 4m;
        var salvo = await _tributos.SalvarAsync(t, operador: "ana");

        salvo.Id.Should().Be(t.Id);
        (await _tributos.CatalogoAsync()).Should().ContainSingle();
        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "TributoAtualizado" && e.Operador == "ana");
    }

    [Fact]
    public async Task Vigencia_QueTerminaAntesDeComecar_ERecusada()
    {
        var acao = () => CriarAsync("ISS", 3m, de: Hoje, ate: Hoje.AddDays(-1));

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------- A apuração do mês (parcela 28) ----------------
    //
    // A medição de cobertura mostrou `ApuracaoMensalAsync` com ZERO execução: ela foi
    // escrita, aprovada no CI e nunca rodou. É a tela que responde "de QUÊ foi o imposto"
    // — a pergunta do contador — e o número dela vai para a guia.

    private async Task ReceberAsync(
        decimal valor, DateOnly quando, decimal? imposto = null, string? convenio = null)
    {
        _db.Lancamentos.Add(new LancamentoFinanceiro
        {
            Tipo = TipoLancamento.Entrada,
            Status = StatusLancamento.Realizado,
            Data = quando,
            DataPagamento = quando,
            Valor = valor,
            ValorImposto = imposto,
            ConvenioCodigo = convenio,
            Descricao = "Sessão"
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// O caso do Presumido: cinco tributos, cada um com a sua alíquota, sobre a mesma
    /// receita. A soma das linhas é o total — o ponto inteiro da tela.
    /// </summary>
    [Fact]
    public async Task Apuracao_abre_o_imposto_por_tributo()
    {
        await CriarAsync("ISS", 3m);
        await CriarAsync("PIS", 0.65m);
        await CriarAsync("COFINS", 3m);

        await ReceberAsync(1000m, new DateOnly(2026, 9, 10));

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.Vazia.Should().BeFalse();
        a.ReceitaBruta.Should().Be(1000m);
        a.Linhas.Should().HaveCount(3);
        a.Linhas.Sum(l => l.Valor).Should().Be(a.TotalApurado,
            "a soma do detalhe é o total — é o detalhe que vai ao contador");
        a.TotalApurado.Should().Be(66.50m);
    }

    /// <summary>
    /// Base presumida: a clínica cadastra "IRPJ 15% sobre 32%" e o que sai é 4,8%. Sem o
    /// campo de base, digitar 15% triplicaria o imposto do mês.
    /// </summary>
    [Fact]
    public async Task Apuracao_usa_a_aliquota_efetiva_e_nao_a_de_tabela()
    {
        await CriarAsync("IRPJ", 15m, basePercentual: 32m);
        await ReceberAsync(1000m, new DateOnly(2026, 9, 10));

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.TotalApurado.Should().Be(48m, "15% sobre 32% da receita, não 15% dela");
    }

    /// <summary>
    /// Recebimento FORA do mês não entra. Parece óbvio e é o erro mais fácil de cometer
    /// num corte de datas — e um mês que puxa o vizinho manda para a guia um imposto que
    /// já foi recolhido.
    /// </summary>
    [Fact]
    public async Task Apuracao_ignora_recebimento_de_outro_mes()
    {
        await CriarAsync("ISS", 3m);

        await ReceberAsync(1000m, new DateOnly(2026, 9, 30));
        await ReceberAsync(5000m, new DateOnly(2026, 10, 1));
        await ReceberAsync(7000m, new DateOnly(2026, 8, 31));

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.ReceitaBruta.Should().Be(1000m);
    }

    /// <summary>
    /// A retenção do convênio SUBSTITUI os tributos gerais naquele recebimento — os dois
    /// são o mesmo imposto, e somá-los conta duas vezes (o erro clássico da planilha de
    /// convênio). Aqui: mil reais do convênio, mil particulares.
    /// </summary>
    [Fact]
    public async Task Retencao_do_convenio_nao_soma_com_o_tributo_geral()
    {
        await CriarAsync("ISS", 3m);

        var retido = new Tributo
        {
            Sigla = "IRRF",
            Nome = "IRRF retido",
            Percentual = 1.5m,
            BasePercentual = 100m,
            ConvenioCodigo = "UNIMED",
            Ativo = true
        };
        await _tributos.SalvarAsync(retido);

        await ReceberAsync(1000m, new DateOnly(2026, 9, 10), convenio: "UNIMED");
        await ReceberAsync(1000m, new DateOnly(2026, 9, 11));

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.Linhas.Should().Contain(l => l.Sigla == "IRRF" && l.RetidoNaFonte);
        a.Linhas.Single(l => l.Sigla == "IRRF").Base.Should().Be(1000m,
            "a retenção incide só sobre o que veio daquele convênio");
        a.Linhas.Single(l => l.Sigla == "ISS").Base.Should().Be(1000m,
            "o particular não sofre a retenção do convênio, e o do convênio não sofre o ISS geral");

        a.ARecolher.Should().Be(30m, "retido já saiu; a guia leva só o que a clínica recolhe");
        a.Retido.Should().Be(15m);
    }

    /// <summary>
    /// Sem tributo cadastrado a apuração é VAZIA, não zero: "não configuramos" e
    /// "configuramos e deu zero" são respostas diferentes, e a tela mostra cada uma de
    /// um jeito.
    /// </summary>
    [Fact]
    public async Task Sem_tributo_cadastrado_a_apuracao_e_vazia()
    {
        await ReceberAsync(1000m, new DateOnly(2026, 9, 10));

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.Vazia.Should().BeTrue();
        a.ReceitaBruta.Should().Be(1000m, "a receita existe mesmo sem imposto configurado");
    }

    /// <summary>
    /// A divergência entre o apurado AGORA e o gravado NA ÉPOCA aparece em vez de ser
    /// escondida: ela significa que uma alíquota mudou depois de o dinheiro entrar, e é a
    /// clínica que decide qual número vai para a guia.
    /// </summary>
    [Fact]
    public async Task Divergencia_com_o_gravado_na_epoca_fica_visivel()
    {
        await CriarAsync("ISS", 5m);

        // Gravado quando a alíquota ainda era 3%.
        await ReceberAsync(1000m, new DateOnly(2026, 9, 10), imposto: 30m);

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.TotalGravado.Should().Be(30m);
        a.TotalApurado.Should().Be(50m);
        a.Diverge.Should().BeTrue();
    }

    /// <summary>Mês sem nenhum recebimento não estoura e não inventa receita.</summary>
    [Fact]
    public async Task Mes_sem_recebimento_apura_zero()
    {
        await CriarAsync("ISS", 3m);

        var a = await _tributos.ApuracaoMensalAsync(2026, 9);

        a.ReceitaBruta.Should().Be(0m);
        a.Vazia.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
