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
/// Teto de gasto, resultado do mês e acerto de inventário (parcelas 30 e 31).
///
/// O que estes testes protegem:
///
/// 1. **Só categoria de SAÍDA tem teto** — alvo de receita é meta, e meta já existe.
/// 2. **O comprometido conta separado do gasto**: conta prevista que ainda vai vencer não
///    é dinheiro que saiu, mas é dinheiro já decidido, e o teto costuma estourar no que
///    foi contratado.
/// 3. **Taxa e imposto são DEDUÇÃO, não despesa** — eles saem da receita antes de ela
///    existir, e listá-los junto do aluguel faria a clínica achar que pode cortá-los.
/// 4. **Margem sobre zero é nula**, nunca 0%.
/// 5. **Ajuste de inventário soma ou subtrai conforme a direção**, e a contagem que bate
///    não vira movimento.
/// </summary>
public class OrcamentoResultadoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly ContasService _contas;
    private readonly OrcamentoService _orcamentos;
    private readonly ResultadoMensalService _resultado;
    private readonly EstoqueService _estoque;

    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public OrcamentoResultadoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _contas = new ContasService(_repo);
        _orcamentos = new OrcamentoService(_repo, _contas);
        _resultado = new ResultadoMensalService(_repo);
        _estoque = new EstoqueService(_repo);
    }

    private async Task<int> CategoriaAsync(string nome, TipoLancamento tipo)
    {
        var c = new CategoriaFinanceira
        {
            Codigo = nome.ToUpperInvariant().Replace(' ', '_'),
            Nome = nome,
            Tipo = tipo,
            Ativa = true
        };
        _db.CategoriasFinanceiras.Add(c);
        await _db.SaveChangesAsync();
        return c.Id;
    }

    private Task SaidaAsync(DateOnly dia, decimal valor, int categoriaId)
        => _financeiro.LancarAsync(dia, TipoLancamento.Saida, "Compra", valor,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro, categoriaId: categoriaId);

    private Task EntradaAsync(DateOnly dia, decimal valor, int? categoriaId = null)
        => _financeiro.LancarAsync(dia, TipoLancamento.Entrada, "Sessão", valor,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro, categoriaId: categoriaId);

    // ==================== Teto de gasto ====================

    [Fact]
    public async Task Teto_em_categoria_de_entrada_e_recusado()
    {
        var receita = await CategoriaAsync("Consulta particular", TipoLancamento.Entrada);

        var definir = () => _orcamentos.DefinirAsync(2026, 8, receita, 5000m);

        // Alvo de receita é meta, e a direção já define metas na tela própria.
        await definir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SAÍDA*");
    }

    [Fact]
    public async Task Redefinir_o_teto_substitui_em_vez_de_duplicar()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);

        await _orcamentos.DefinirAsync(2026, 8, material, 1000m);
        await _orcamentos.DefinirAsync(2026, 8, material, 1500m, "direcao");

        var tetos = await _orcamentos.DoMesAsync(2026, 8);
        tetos.Should().ContainSingle();
        tetos[0].Teto.Should().Be(1500m);

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "OrcamentoAlterado");
    }

    [Fact]
    public async Task Sem_teto_definido_a_apuracao_devolve_vazio()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await SaidaAsync(Hoje, 800m, material);

        // Vazio, e não "tudo dentro do orçamento": ausência é "não decidimos".
        (await _orcamentos.ApurarMesAsync(2026, 8)).Should().BeEmpty();
    }

    [Fact]
    public async Task Gasto_acima_do_teto_marca_estouro()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await _orcamentos.DefinirAsync(2026, 8, material, 1000m);
        await SaidaAsync(new DateOnly(2026, 8, 3), 700m, material);
        await SaidaAsync(new DateOnly(2026, 8, 7), 500m, material);

        var apurado = (await _orcamentos.ApurarMesAsync(2026, 8))[0];

        apurado.Gasto.Should().Be(1200m);
        apurado.Estourou.Should().BeTrue();
        apurado.Saldo.Should().Be(-200m);
        apurado.PercentualUsado.Should().Be(120.0);
    }

    [Fact]
    public async Task Conta_prevista_conta_como_comprometido_e_nao_como_gasto()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await _orcamentos.DefinirAsync(2026, 8, material, 1000m);
        await SaidaAsync(new DateOnly(2026, 8, 3), 600m, material);

        // Contratado e ainda não pago: não é dinheiro que saiu, mas é dinheiro decidido.
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Pedido ao fornecedor", 500m,
            vencimento: new DateOnly(2026, 8, 25), categoriaId: material);

        var apurado = (await _orcamentos.ApurarMesAsync(2026, 8))[0];

        apurado.Gasto.Should().Be(600m, "previsto não é dinheiro que saiu");
        apurado.Estourou.Should().BeFalse();
        apurado.Comprometido.Should().Be(500m);
        apurado.Projetado.Should().Be(1100m);
        apurado.VaiEstourar.Should().BeTrue("o teto estoura no que já foi contratado");
    }

    [Fact]
    public async Task Faixa_de_atencao_avisa_antes_de_estourar()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await _orcamentos.DefinirAsync(2026, 8, material, 1000m);
        await SaidaAsync(new DateOnly(2026, 8, 3), 850m, material);

        var apurado = (await _orcamentos.ApurarMesAsync(2026, 8))[0];

        apurado.Estourou.Should().BeFalse();
        apurado.EmAtencao.Should().BeTrue();
    }

    // ==================== Resultado do mês ====================

    [Fact]
    public async Task Resultado_desconta_despesa_da_receita()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await EntradaAsync(new DateOnly(2026, 8, 2), 5000m);
        await SaidaAsync(new DateOnly(2026, 8, 5), 2000m, material);

        var r = await _resultado.DoMesAsync(2026, 8);

        r.Receita.Should().Be(5000m);
        r.Despesas.Should().Be(2000m);
        r.Resultado.Should().Be(3000m);
        r.Margem.Should().Be(60.0);
        r.Prejuizo.Should().BeFalse();
    }

    [Fact]
    public async Task Mes_sem_receita_nao_tem_margem()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await SaidaAsync(new DateOnly(2026, 8, 5), 300m, material);

        var r = await _resultado.DoMesAsync(2026, 8);

        // Margem sobre zero não é 0%: é conta que não existe. Mostrá-la como zero faria
        // um mês sem faturamento parecer um mês que faturou e não sobrou nada.
        r.Margem.Should().BeNull();
        r.Prejuizo.Should().BeTrue();
    }

    [Fact]
    public async Task Previsto_nao_entra_no_resultado()
    {
        var material = await CategoriaAsync("Material", TipoLancamento.Saida);
        await EntradaAsync(new DateOnly(2026, 8, 2), 1000m);

        // Promessa no resultado do mês transformaria o DRE numa projeção sem dizer.
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3000m,
            vencimento: new DateOnly(2026, 8, 20), categoriaId: material);

        var r = await _resultado.DoMesAsync(2026, 8);

        r.Despesas.Should().Be(0m);
        r.Resultado.Should().Be(1000m);
    }

    [Fact]
    public async Task Lancamento_sem_categoria_aparece_nomeado()
    {
        await EntradaAsync(new DateOnly(2026, 8, 2), 400m);

        var r = await _resultado.DoMesAsync(2026, 8);

        // Sumir faria as linhas não fecharem com o total, e quem confere desconfia do
        // total primeiro.
        r.Entradas.Should().ContainSingle(l => l.Categoria == "Sem categoria");
        r.Entradas.Sum(l => l.Valor).Should().Be(r.Receita);
    }

    // ==================== Acerto de inventário ====================

    private async Task<int> ItemAsync(string nome = "Agulha")
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque
        {
            Nome = nome, Unidade = "un", EstoqueMinimo = 10m
        });
        return item.Id;
    }

    [Fact]
    public async Task Contagem_a_menos_baixa_o_saldo()
    {
        var itemId = await ItemAsync();
        await _estoque.EntrarAsync(itemId, 40m, 1m, Hoje);

        var movimento = await _estoque.AjustarInventarioAsync(itemId, 37m, "três frascos quebrados");

        movimento.Should().NotBeNull();
        movimento!.Quantidade.Should().Be(3m);
        movimento.AjusteParaCima.Should().BeFalse();

        var saldo = (await _estoque.SaldosAsync()).Single(s => s.ItemId == itemId);
        saldo.Saldo.Should().Be(37m);
    }

    [Fact]
    public async Task Contagem_a_mais_sobe_o_saldo()
    {
        var itemId = await ItemAsync();
        await _estoque.EntrarAsync(itemId, 40m, 1m, Hoje);

        // A contagem que acha A MAIS não é perda nenhuma — e era só como perda que a
        // clínica conseguia registrar a diferença.
        var movimento = await _estoque.AjustarInventarioAsync(itemId, 45m, "entrada de maio não lançada");

        movimento!.AjusteParaCima.Should().BeTrue();
        (await _estoque.SaldosAsync()).Single(s => s.ItemId == itemId).Saldo.Should().Be(45m);
    }

    [Fact]
    public async Task Contagem_que_bate_nao_vira_movimento()
    {
        var itemId = await ItemAsync();
        await _estoque.EntrarAsync(itemId, 40m, 1m, Hoje);

        var movimento = await _estoque.AjustarInventarioAsync(itemId, 40m, "conferido");

        // Ajuste de zero sujaria o extrato com linhas que não mudam nada.
        movimento.Should().BeNull();
        (await _estoque.MovimentosAsync(itemId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Inventario_sem_motivo_e_recusado()
    {
        var itemId = await ItemAsync();
        await _estoque.EntrarAsync(itemId, 40m, 1m, Hoje);

        var ajustar = () => _estoque.AjustarInventarioAsync(itemId, 30m, "  ");

        await ajustar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*por que a contagem não bateu*");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
