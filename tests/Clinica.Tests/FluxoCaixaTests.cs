using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Fluxo de caixa e resultado por categoria (parcela 13).
///
/// O que estes testes protegem:
/// 1. Realizado e previsto NÃO se somam num número só. É a honestidade que custa a
///    linha bonita: o mês fechado é medida, o que vai vencer é expectativa.
/// 2. Cada lançamento entra no mês CERTO — realizado pelo pagamento, previsto pelo
///    vencimento, e a competência como último recurso.
/// 3. A fração da categoria é do total DO MESMO TIPO. Comparar despesa com o total
///    geral daria barras que não somam 100% e não querem dizer nada.
/// </summary>
public class FluxoCaixaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FluxoCaixaService _fluxo;
    private readonly FinanceiroService _financeiro;
    private readonly ContasService _contas;

    private static readonly DateOnly Referencia = new(2026, 8, 31);

    public FluxoCaixaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _fluxo = new FluxoCaixaService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _contas = new ContasService(_repo);
    }

    // ---------------- A série ----------------

    [Fact]
    public async Task Projecao_TemUmMesPorPeriodoDoMaisAntigoAoMaisNovo()
    {
        var serie = await _fluxo.ProjecaoAsync(Referencia, meses: 6);

        serie.Should().HaveCount(6);
        serie[0].Rotulo.Should().Be("mar/26");
        serie[^1].Rotulo.Should().Be("ago/26");
    }

    [Fact]
    public async Task MesSemMovimento_EntraZerado_NaoSomeDaSerie()
    {
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 5), TipoLancamento.Entrada, "Sessão", 150m);

        var serie = await _fluxo.ProjecaoAsync(Referencia, meses: 3);

        // Buraco na linha diria "não medido", e aqui seria falso: o mês foi medido e
        // não teve nada.
        serie.Should().HaveCount(3);
        serie[0].Vazio.Should().BeTrue();
        serie[^1].Vazio.Should().BeFalse();
    }

    [Fact]
    public async Task RealizadoEPrevisto_NaoSeSomamNumNumeroSo()
    {
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 5), TipoLancamento.Entrada, "Recebido", 1000m,
            StatusLancamento.Realizado);
        await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "A receber", 400m, new DateOnly(2026, 8, 20));
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 300m, new DateOnly(2026, 8, 10));

        var agosto = (await _fluxo.ProjecaoAsync(Referencia, meses: 1)).Single();

        agosto.EntradasRealizadas.Should().Be(1000m);
        agosto.EntradasPrevistas.Should().Be(400m);
        agosto.SaidasPrevistas.Should().Be(300m);
        agosto.Realizado.Should().Be(1000m);
        agosto.Previsto.Should().Be(100m);
        agosto.Total.Should().Be(1100m);
    }

    [Fact]
    public async Task Realizado_EntraNoMesDoPAGAMENTO_NaoNoDaCompetencia()
    {
        // Competência em julho, pagamento em agosto: o dinheiro se moveu em agosto, e é
        // no fluxo de agosto que ele tem de aparecer.
        await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 28), TipoLancamento.Entrada, "Guia de julho", 900m,
            StatusLancamento.Realizado, dataPagamento: new DateOnly(2026, 8, 12));

        var serie = await _fluxo.ProjecaoAsync(Referencia, meses: 2);

        serie[0].Rotulo.Should().Be("jul/26");
        serie[0].EntradasRealizadas.Should().Be(0m);
        serie[1].EntradasRealizadas.Should().Be(900m);
    }

    [Fact]
    public async Task Previsto_EntraNoMesDoVENCIMENTO()
    {
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Contador", 800m,
            vencimento: new DateOnly(2026, 8, 15), competencia: new DateOnly(2026, 7, 31));

        var serie = await _fluxo.ProjecaoAsync(Referencia, meses: 2);

        serie[0].SaidasPrevistas.Should().Be(0m);
        serie[1].SaidasPrevistas.Should().Be(800m);
    }

    [Fact]
    public async Task PrevistoSemVencimento_CaiNaCompetencia_NaoSomeDoFluxo()
    {
        // É o previsto que já existia antes da parcela 12: sem vencimento, mas ainda
        // assim dinheiro esperado. Deixá-lo de fora esconderia receita do mês.
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 9), TipoLancamento.Entrada, "Guia do convênio", 320m,
            StatusLancamento.Previsto);

        (await _fluxo.ProjecaoAsync(Referencia, meses: 1))
            .Single().EntradasPrevistas.Should().Be(320m);
    }

    [Fact]
    public async Task Cancelado_NaoEntraNoFluxo()
    {
        var l = await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 3), TipoLancamento.Saida, "Compra desfeita", 700m);
        await _financeiro.CancelarAsync(l.Id, "Fornecedor não entregou");

        (await _fluxo.ProjecaoAsync(Referencia, meses: 1)).Single().Vazio.Should().BeTrue();
    }

    [Fact]
    public async Task Acumulado_ESomaCorrenteDaSerie()
    {
        await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 5), TipoLancamento.Entrada, "Julho", 500m);
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 5), TipoLancamento.Entrada, "Agosto", 300m);
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 6), TipoLancamento.Saida, "Despesa", 100m);

        var acumulado = FluxoCaixaService.Acumular(await _fluxo.ProjecaoAsync(Referencia, meses: 2));

        acumulado[0].Acumulado.Should().Be(500m);
        // É VARIAÇÃO no período, não saldo em conta: a clínica nunca cadastrou saldo
        // inicial, e chamar isto de saldo daria um número que não bate com o extrato.
        acumulado[1].Acumulado.Should().Be(700m);
    }

    [Fact]
    public async Task Projecao_ComMesesInvalido_DevolveUmMes()
    {
        (await _fluxo.ProjecaoAsync(Referencia, meses: 0)).Should().HaveCount(1);
    }

    // ---------------- Por categoria ----------------

    [Fact]
    public async Task PorCategoria_SomaEOrdenaDoMaiorParaOMenor()
    {
        var consulta = await _financeiro.CriarCategoriaAsync(
            "CONSULTA", "Consultas", TipoLancamento.Entrada);
        var pacote = await _financeiro.CriarCategoriaAsync(
            "PACOTE", "Pacotes", TipoLancamento.Entrada);

        await _financeiro.LancarAsync(new DateOnly(2026, 8, 1), TipoLancamento.Entrada,
            "a", 100m, categoriaId: consulta.Id);
        await _financeiro.LancarAsync(new DateOnly(2026, 8, 2), TipoLancamento.Entrada,
            "b", 150m, categoriaId: consulta.Id);
        await _financeiro.LancarAsync(new DateOnly(2026, 8, 3), TipoLancamento.Entrada,
            "c", 900m, categoriaId: pacote.Id);

        var linhas = await _fluxo.PorCategoriaAsync(new DateOnly(2026, 8, 1), Referencia);

        linhas.Should().HaveCount(2);
        linhas[0].Categoria.Should().Be("Pacotes");
        linhas[0].Total.Should().Be(900m);
        linhas[1].Categoria.Should().Be("Consultas");
        linhas[1].Total.Should().Be(250m);
        linhas[1].Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task PorCategoria_FracaoEDoTotalDoMESMOTipo()
    {
        var receita = await _financeiro.CriarCategoriaAsync(
            "CONSULTA", "Consultas", TipoLancamento.Entrada);
        var despesa = await _financeiro.CriarCategoriaAsync(
            "ALUGUEL", "Aluguel", TipoLancamento.Saida);

        await _financeiro.LancarAsync(new DateOnly(2026, 8, 1), TipoLancamento.Entrada,
            "receita", 1000m, categoriaId: receita.Id);
        await _financeiro.LancarAsync(new DateOnly(2026, 8, 2), TipoLancamento.Saida,
            "aluguel", 400m, categoriaId: despesa.Id);

        var linhas = await _fluxo.PorCategoriaAsync(new DateOnly(2026, 8, 1), Referencia);

        // Categoria única do seu tipo = 100% daquele tipo. Se a fração fosse do total
        // geral, a receita daria 71% e o aluguel 29%, e as barras não diriam nada.
        linhas.Should().OnlyContain(l => Math.Abs(l.Fracao - 1d) < 0.0001);
    }

    [Fact]
    public async Task PorCategoria_SemCategoria_ApareceEmVezDeSumir()
    {
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 4), TipoLancamento.Saida, "Compra avulsa", 200m);

        var linhas = await _fluxo.PorCategoriaAsync(new DateOnly(2026, 8, 1), Referencia);

        // Dinheiro não classificado é justamente o que a direção precisa ver para mandar
        // classificar. Escondê-lo faria as fatias somarem menos que o total, sem explicação.
        linhas.Should().ContainSingle().Which.Categoria.Should().Be("Sem categoria");
    }

    [Fact]
    public async Task PorCategoria_IgnoraPrevistoPorPadrao()
    {
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Vai vencer", 500m, new DateOnly(2026, 8, 20));

        (await _fluxo.PorCategoriaAsync(new DateOnly(2026, 8, 1), Referencia))
            .Should().BeEmpty();
        (await _fluxo.PorCategoriaAsync(new DateOnly(2026, 8, 1), Referencia, incluirPrevisto: true))
            .Should().ContainSingle();
    }

    // ---------------- Resumo ----------------

    [Fact]
    public async Task Resumo_ApontaAMaiorEntradaEAMaiorSaida()
    {
        var consulta = await _financeiro.CriarCategoriaAsync(
            "CONSULTA", "Consultas", TipoLancamento.Entrada);
        var aluguel = await _financeiro.CriarCategoriaAsync(
            "ALUGUEL", "Aluguel", TipoLancamento.Saida);
        var luz = await _financeiro.CriarCategoriaAsync("LUZ", "Luz", TipoLancamento.Saida);

        await _financeiro.LancarAsync(new DateOnly(2026, 8, 1), TipoLancamento.Entrada,
            "a", 5000m, categoriaId: consulta.Id);
        await _financeiro.LancarAsync(new DateOnly(2026, 8, 2), TipoLancamento.Saida,
            "b", 3000m, categoriaId: aluguel.Id);
        await _financeiro.LancarAsync(new DateOnly(2026, 8, 3), TipoLancamento.Saida,
            "c", 400m, categoriaId: luz.Id);

        var r = await _fluxo.ResumoAsync(new DateOnly(2026, 8, 1), Referencia);

        r.Entradas.Should().Be(5000m);
        r.Saidas.Should().Be(3400m);
        r.Resultado.Should().Be(1600m);
        r.MaiorEntrada!.Categoria.Should().Be("Consultas");
        r.MaiorSaida!.Categoria.Should().Be("Aluguel");
        r.Margem.Should().BeApproximately(0.32d, 0.0001d);
    }

    [Fact]
    public async Task Resumo_SemReceita_TemMargemNULA_NaoZero()
    {
        await _financeiro.LancarAsync(
            new DateOnly(2026, 8, 2), TipoLancamento.Saida, "Só despesa", 800m);

        var r = await _fluxo.ResumoAsync(new DateOnly(2026, 8, 1), Referencia);

        // "Não sobrou nada" e "não teve receita para sobrar" são diagnósticos diferentes.
        // A tela mostra "—" no segundo caso, como já faz com a ocupação não medida.
        r.Margem.Should().BeNull();
        r.MaiorEntrada.Should().BeNull();
        r.MaiorSaida!.Total.Should().Be(800m);
    }

    [Fact]
    public async Task Resumo_PeriodoVazio_NaoQuebra()
    {
        var r = await _fluxo.ResumoAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        r.Entradas.Should().Be(0m);
        r.Saidas.Should().Be(0m);
        r.Margem.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
