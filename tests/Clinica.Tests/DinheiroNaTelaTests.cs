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
/// A entrada DINHEIRO do Gerente (out/2026): o resultado por competência ao lado do de caixa
/// e a projeção de 90 dias. As frases e a montagem são puras (<see cref="DinheiroNaTela"/>);
/// as leituras rodam sobre o repositório de verdade, para a consulta nova ser EXECUTADA.
/// </summary>
public sealed class DinheiroNaTelaTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 10, 19);

    private readonly SqliteConnection _conexao = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public DinheiroNaTelaTests()
    {
        _conexao.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _repo = new(_db);
    }

    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }

    private static LancamentoDatado Datado(DateOnly data, TipoLancamento tipo, StatusLancamento status, decimal valor,
        DateOnly? pagamento = null, DateOnly? vencimento = null, decimal? taxa = null, decimal? imposto = null) =>
        new(data, vencimento, pagamento, tipo, status, valor, null) { ValorTaxa = taxa, ValorImposto = imposto };

    private static LancamentoFinanceiro Conta(DateOnly? vencimento, TipoLancamento tipo, decimal valor, string descricao = "Conta",
        FormaPagamento? forma = null, DateOnly? data = null) => new()
    {
        Data = data ?? vencimento ?? Hoje,
        DataVencimento = vencimento,
        Tipo = tipo,
        Valor = valor,
        Descricao = descricao,
        Status = StatusLancamento.Previsto,
        FormaPagamento = forma
    };

    // ==================== Competência (pura) ====================

    [Fact]
    public void Competencia_soma_o_fato_do_mes_recebido_ou_nao_e_deixa_fora_o_que_so_foi_pago_no_mes()
    {
        var lancamentos = new[]
        {
            // Sessão de outubro paga em outubro: entra nos dois regimes.
            Datado(new(2026, 10, 3), TipoLancamento.Entrada, StatusLancamento.Realizado, 300m, pagamento: new(2026, 10, 3), taxa: 9m),
            // Guia de outubro ainda a receber: só a competência vê.
            Datado(new(2026, 10, 8), TipoLancamento.Entrada, StatusLancamento.Previsto, 500m, imposto: 20m),
            // Conta de setembro paga em outubro: é do caixa de outubro, não da competência.
            Datado(new(2026, 9, 28), TipoLancamento.Saida, StatusLancamento.Realizado, 200m, pagamento: new(2026, 10, 2)),
            // Aluguel de outubro a pagar.
            Datado(new(2026, 10, 25), TipoLancamento.Saida, StatusLancamento.Previsto, 1_000m, vencimento: new(2026, 10, 25)),
            // Cancelado nunca entra.
            Datado(new(2026, 10, 5), TipoLancamento.Entrada, StatusLancamento.Cancelado, 999m)
        };

        var c = ResultadoMensalService.ApurarCompetencia(2026, 10, lancamentos, 0, 0);

        c.ReceitaRecebida.Should().Be(300m);
        c.ReceitaAReceber.Should().Be(500m);
        c.Deducoes.Should().Be(29m);
        c.DespesasPagas.Should().Be(0m);
        c.DespesasAPagar.Should().Be(1_000m);
        c.Resultado.Should().Be(800m - 29m - 1_000m);
        c.Prejuizo.Should().BeTrue();
    }

    [Fact]
    public void Competencia_sem_receita_nao_tem_margem_e_producao_nao_conferida_nao_e_zero()
    {
        var c = ResultadoMensalService.ApurarCompetencia(2026, 10, [], null, 2);
        c.Margem.Should().BeNull();
        c.Vazio.Should().BeTrue();
        c.ProducaoSemValor.Should().BeNull();
        DinheiroNaTela.ProducaoSemValor(c).Should().Be("Não foi possível conferir se há produção do mês sem valor lançado.");
        DinheiroNaTela.ResumoCompetencia(c).Should().Be("Nenhum lançamento com competência neste mês.");
    }

    [Fact]
    public void Producao_sem_valor_e_nomeada_e_some_quando_nao_ha()
    {
        var com = new ResultadoPorCompetencia(2026, 10, 0, 0, 0, 0, 0, 3, 1);
        DinheiroNaTela.ProducaoSemValor(com).Should()
            .Be("3 guias baixadas e 1 sessão particular do mês ainda sem valor lançado — ficam fora deste número, que está abaixo do real.");

        var sem = com with { GuiasSemReceita = 0, SessoesParticularesSemReceita = 0 };
        DinheiroNaTela.ProducaoSemValor(sem).Should().BeNull();
    }

    [Fact]
    public void A_diferenca_entre_os_regimes_e_explicada()
    {
        var caixa = new ResultadoMensal(2026, 10, 1_000m, 0m, 400m, [], []);
        var competencia = new ResultadoPorCompetencia(2026, 10, 1_000m, 500m, 0m, 400m, 100m, 0, 0);

        DinheiroNaTela.DiferencaEntreRegimes(caixa, competencia).Should()
            .Be("São R$ 400,00 acima do caixa: o que pertence ao mês e ainda não se moveu (a receber R$ 500,00, a pagar R$ 100,00), mais o que se moveu agora e é de outro mês.");
    }

    [Fact]
    public void A_nota_da_mesa_diz_o_a_receber_e_a_producao_sem_valor()
    {
        MesaDaDirecaoNaTela.NotaDaCompetencia(null).Should().Be("não pôde ser lido agora");
        MesaDaDirecaoNaTela.NotaDaCompetencia(new ResultadoPorCompetencia(2026, 10, 0, 5_430m, 0, 0, 0, 2, 1))
            .Should().Be("inclui R$ 5.430 a receber · 3 atendimentos sem valor lançado");
        MesaDaDirecaoNaTela.NotaDaCompetencia(new ResultadoPorCompetencia(2026, 10, 0, 0, 0, 0, 0, 0, 0))
            .Should().Be("nada do mês a receber");
        MesaDaDirecaoNaTela.NotaDaCompetencia(new ResultadoPorCompetencia(2026, 10, 0, 0, 0, 0, 0, null, 0))
            .Should().Be("nada do mês a receber · produção sem valor não conferida");
    }

    // ==================== Projeção (pura) ====================

    [Fact]
    public void Projecao_separa_atrasado_e_sem_data_da_linha_do_tempo()
    {
        var aluguel = new LancamentoRecorrente { Id = 7, Descricao = "Aluguel", Tipo = TipoLancamento.Saida, Valor = 3_000m };
        var p = DinheiroNaTela.Projetar(Hoje, 90,
            contasEmAberto:
            [
                Conta(Hoje.AddDays(-3), TipoLancamento.Saida, 150m, "Luz"),            // atrasada
                Conta(Hoje.AddDays(2), TipoLancamento.Entrada, 400m, "Cobrança"),      // semana 1
                Conta(Hoje.AddDays(120), TipoLancamento.Saida, 999m, "Fora do horizonte")
            ],
            fixasNaoGeradas: [(aluguel, Hoje.AddDays(10)), (aluguel, Hoje.AddDays(-1))],
            depositos:
            [
                new DepositoEsperado("Stone", Hoje.AddDays(8), 1_000m, 30m, 970m, 4, [1, 2, 3, 4]),
                new DepositoEsperado("Cielo", Hoje.AddDays(-2), 100m, 3m, 97m, 1, [5])
            ],
            previstosSemVencimento:
            [
                Conta(null, TipoLancamento.Entrada, 800m, "Unimed — guia 123", FormaPagamento.Convenio, Hoje.AddDays(-20)),
                Conta(null, TipoLancamento.Saida, 50m, "Anotado sem prazo")
            ]);

        p.Semanas.Should().HaveCount(13);
        p.Semanas[0].Inicio.Should().Be(Hoje);
        p.Semanas[^1].Fim.Should().Be(Hoje.AddDays(89));
        p.Entradas.Should().Be(400m + 970m);
        p.Saidas.Should().Be(3_000m);
        p.Variacao.Should().Be(-1_630m);

        p.Semanas[0].Acumulado.Should().Be(400m);
        p.Semanas[1].Acumulado.Should().Be(400m + 970m - 3_000m);
        p.PiorSemana!.Inicio.Should().Be(Hoje.AddDays(7));

        p.Atrasadas.Should().HaveCount(2);
        p.AtrasadoAPagar.Should().Be(150m);
        p.AtrasadoAReceber.Should().Be(97m);
        p.SemData.Should().HaveCount(2);
        p.SemData.Single(x => x.Tipo == TipoLancamento.Entrada).Origem.Should().Be(OrigemDaParcela.GuiaAReceber);
        p.SemDataAPagar.Should().Be(50m);

        // A fixa de ontem não é inventada como atrasada: o molde não diz que ela venceu sem pagar.
        p.Semanas.SelectMany(s => s.Parcelas).Count(x => x.Origem == OrigemDaParcela.ContaFixaNaoGerada).Should().Be(1);
    }

    [Fact]
    public void Cada_parcela_diz_de_onde_veio()
    {
        var aluguel = new LancamentoRecorrente { Id = 7, Descricao = "Aluguel", Tipo = TipoLancamento.Saida, Valor = 3_000m };
        var p = DinheiroNaTela.Projetar(Hoje, 90,
            [Conta(new(2026, 10, 25), TipoLancamento.Saida, 150m, "Luz")],
            [(aluguel, new DateOnly(2026, 11, 10))],
            [new DepositoEsperado("Stone", new(2026, 10, 27), 1_000m, 30m, 970m, 4, [1])],
            [Conta(null, TipoLancamento.Entrada, 800m, "Unimed — guia 123", FormaPagamento.Convenio, new(2026, 9, 30))]);

        var parcelas = p.Semanas.SelectMany(s => s.Parcelas).ToList();
        parcelas.Single(x => x.Origem == OrigemDaParcela.ContaLancada).Procedencia.Should().Be("Conta lançada · vence 25/10");
        parcelas.Single(x => x.Origem == OrigemDaParcela.ContaFixaNaoGerada).Procedencia.Should()
            .Be("Conta fixa (todo mês) ainda não gerada · vence 10/11 · valor do molde");
        parcelas.Single(x => x.Origem == OrigemDaParcela.Cartao).Procedencia.Should()
            .Be("Cartão · Stone · 4 vendas · bruto R$ 1.000,00, taxa R$ 30,00 · previsto para 27/10");
        p.SemData.Single().Procedencia.Should().Be("Receita de guia a receber · baixada em 30/09 · o convênio não prometeu data");
    }

    [Fact]
    public void Frases_da_projecao()
    {
        var vazia = DinheiroNaTela.Projetar(Hoje, 90, [], [], [], []);
        vazia.Vazia.Should().BeTrue();
        DinheiroNaTela.ResumoDaProjecao(vazia).Should().StartWith("Nada previsto");
        DinheiroNaTela.PiorMomento(vazia).Should().BeNull();
        DinheiroNaTela.ForaDaLinhaDoTempo(vazia).Should().BeNull();

        var p = DinheiroNaTela.Projetar(Hoje, 90, [Conta(Hoje.AddDays(1), TipoLancamento.Saida, 500m)], [], [], []);
        DinheiroNaTela.ResumoDaProjecao(p).Should().Be("Até 16/01 devem entrar R$ 0,00 e sair R$ 500,00: o caixa desce R$ 500,00.");
        DinheiroNaTela.PiorMomento(p).Should().Be("O ponto mais baixo é a semana de 19/10: R$ 500,00 abaixo de hoje, se nada mais entrar.");
        DinheiroNaTela.Periodo(p.Semanas[0]).Should().Be("19/10 a 25/10");
        DinheiroNaTela.Periodo(p.Semanas[^1]).Should().Be("11/01 a 16/01");
        DinheiroNaTela.Periodo(DinheiroNaTela.Projetar(Hoje, 8, [], [], [], []).Semanas[^1]).Should().Be("26/10");
    }

    // ==================== As leituras, executadas ====================

    [Fact]
    public async Task Competencia_do_mes_le_o_banco_e_conta_a_producao_sem_valor()
    {
        var financeiro = new FinanceiroService(_repo);
        await financeiro.LancarAsync(new(2026, 10, 3), TipoLancamento.Entrada, "Sessão", 300m, StatusLancamento.Realizado);
        await financeiro.LancarAsync(new(2026, 10, 8), TipoLancamento.Entrada, "Guia a receber", 500m, StatusLancamento.Previsto);
        await financeiro.LancarAsync(new(2026, 9, 28), TipoLancamento.Saida, "Conta de setembro", 200m, StatusLancamento.Previsto);

        var c = await new ResultadoMensalService(_repo).CompetenciaDoMesAsync(2026, 10);

        c.ReceitaRecebida.Should().Be(300m);
        c.ReceitaAReceber.Should().Be(500m);
        c.Despesas.Should().Be(0m);
        c.GuiasSemReceita.Should().Be(0);
        c.SessoesParticularesSemReceita.Should().Be(0);
    }

    [Fact]
    public async Task Projecao_le_o_banco_sem_contar_duas_vezes_a_fixa_ja_gerada()
    {
        var contas = new ContasService(_repo);
        var luz = await contas.CriarRecorrenteAsync("Luz", TipoLancamento.Saida, 200m,
            PeriodicidadeRecorrencia.Mensal, Hoje.AddDays(5));
        // Gera só a primeira ocorrência: as outras duas do horizonte vêm do molde.
        await contas.GerarAsync(Hoje.AddDays(5));
        await new FinanceiroService(_repo).LancarAsync(Hoje.AddDays(-10), TipoLancamento.Entrada, "Guia sem data", 800m,
            StatusLancamento.Previsto, formaPagamento: FormaPagamento.Convenio);

        var p = await new ProjecaoDeCaixaService(_repo).ProjetarAsync(Hoje);

        var parcelas = p.Semanas.SelectMany(s => s.Parcelas).Where(x => x.Descricao == "Luz").ToList();
        parcelas.Should().HaveCount(3);
        parcelas.Count(x => x.Origem == OrigemDaParcela.ContaLancada).Should().Be(1);
        parcelas.Count(x => x.Origem == OrigemDaParcela.ContaFixaNaoGerada).Should().Be(2);
        p.SemData.Should().ContainSingle(x => x.Origem == OrigemDaParcela.GuiaAReceber && x.Valor == 800m);
        luz.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Previstos_sem_vencimento_traz_so_previsto_sem_data()
    {
        var financeiro = new FinanceiroService(_repo);
        await financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "Sem data", 10m, StatusLancamento.Previsto);
        await financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "Recebido", 20m, StatusLancamento.Realizado);
        await new ContasService(_repo).LancarContaAsync(TipoLancamento.Saida, "Com vencimento", 30m, Hoje.AddDays(3));

        var lista = await _repo.PrevistosSemVencimentoAsync();

        lista.Select(l => l.Descricao).Should().Equal("Sem data");
    }
}
