using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Contas a pagar e a receber (parcela 12).
///
/// O que estes testes protegem, em ordem de importância:
/// 1. O vencimento existe e separa "a vencer" de "vencido" — sem isso o módulo não
///    responde à pergunta de segunda-feira.
/// 2. A geração recorrente é IDEMPOTENTE. É a regra que, se quebrar, cobra o aluguel
///    duas vezes — e ninguém percebe até o fim do mês.
/// 3. A série sai sempre do primeiro vencimento, nunca da ocorrência anterior: o
///    aluguel do dia 31 não pode virar aluguel do dia 28 para sempre por causa de
///    fevereiro.
/// </summary>
public class ContasServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ContasService _contas;
    private readonly FinanceiroService _financeiro;

    private static readonly DateOnly Hoje = new(2026, 8, 15);

    public ContasServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _contas = new ContasService(_repo);
        _financeiro = new FinanceiroService(_repo);
    }

    // ---------------- Contas avulsas ----------------

    [Fact]
    public async Task Conta_NasceEmAbertoComVencimento()
    {
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel de agosto", 3200m, new DateOnly(2026, 8, 10));

        conta.Status.Should().Be(StatusLancamento.Previsto);
        conta.DataVencimento.Should().Be(new DateOnly(2026, 8, 10));
        // A competência padrão é o próprio vencimento: a clínica não faz regime de
        // competência separado, e pedir duas datas tornaria a tarefa mais comum a mais chata.
        conta.Data.Should().Be(new DateOnly(2026, 8, 10));
        conta.DataPagamento.Should().BeNull();
    }

    [Fact]
    public async Task Competencia_PodeSerDiferenteDoVencimento()
    {
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Luz de julho", 480m,
            vencimento: new DateOnly(2026, 8, 5), competencia: new DateOnly(2026, 7, 31));

        conta.Data.Should().Be(new DateOnly(2026, 7, 31));
        conta.DataVencimento.Should().Be(new DateOnly(2026, 8, 5));
    }

    [Fact]
    public async Task Valor_ZeroOuNegativo_ERecusado()
    {
        var acao = () => _contas.LancarContaAsync(
            TipoLancamento.Saida, "Nada", 0m, Hoje);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Vencida_EAQueJaPassouEContinuaEmAberto()
    {
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Atrasada", 100m, Hoje.AddDays(-3));
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Vence hoje", 200m, Hoje);
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Vence depois", 300m, Hoje.AddDays(5));

        var vencidas = await _contas.VencidasAsync(Hoje);

        // "Vence hoje" NÃO está vencida: o dia do vencimento ainda é dia de pagar.
        vencidas.Should().ContainSingle().Which.Descricao.Should().Be("Atrasada");
    }

    [Fact]
    public async Task ContaPaga_SaiDaListaDeAberto()
    {
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Contador", 700m, Hoje.AddDays(-2));

        (await _contas.VencidasAsync(Hoje)).Should().HaveCount(1);

        await _financeiro.RealizarAsync(conta.Id, Hoje);

        // Pago não é atrasado — e o que saiu do aberto some das duas listas.
        (await _contas.VencidasAsync(Hoje)).Should().BeEmpty();
        (await _contas.EmAbertoAsync(Hoje.AddDays(60))).Should().BeEmpty();
    }

    [Fact]
    public async Task ContaCancelada_NaoEDevida()
    {
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Serviço não prestado", 500m, Hoje.AddDays(-10));

        await _financeiro.CancelarAsync(conta.Id, "Serviço não foi prestado");

        (await _contas.VencidasAsync(Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task EmAberto_TrazVencidaJuntoComAVencer()
    {
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Atrasada", 100m, Hoje.AddDays(-20));
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Semana que vem", 200m, Hoje.AddDays(4));

        var abertas = await _contas.EmAbertoAsync(Hoje.AddDays(7));

        // A vencida entra mesmo estando "fora do período": esconder o atraso porque ele
        // é antigo seria esconder justamente o que mais importa. E vem primeiro.
        abertas.Should().HaveCount(2);
        abertas[0].Descricao.Should().Be("Atrasada");
    }

    [Fact]
    public async Task EmAberto_FiltraPorTipo()
    {
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Aluguel", 3200m, Hoje.AddDays(2));
        await _contas.LancarContaAsync(TipoLancamento.Entrada, "Convênio", 1500m, Hoje.AddDays(3));

        (await _contas.EmAbertoAsync(Hoje.AddDays(30), TipoLancamento.Saida))
            .Should().ContainSingle().Which.Descricao.Should().Be("Aluguel");
        (await _contas.EmAbertoAsync(Hoje.AddDays(30), TipoLancamento.Entrada))
            .Should().ContainSingle().Which.Descricao.Should().Be("Convênio");
    }

    [Fact]
    public async Task LancamentoSemVencimento_NaoEConta()
    {
        // Venda no balcão paga na hora: não vence, já aconteceu.
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Sessão avulsa", 150m, StatusLancamento.Realizado);
        // Previsto sem vencimento também fica de fora — é o que existia antes da
        // parcela 12, e continua não respondendo "quando".
        await _financeiro.LancarAsync(
            Hoje, TipoLancamento.Entrada, "Guia do convênio", 90m, StatusLancamento.Previsto);

        (await _contas.EmAbertoAsync(Hoje.AddDays(365))).Should().BeEmpty();
    }

    [Fact]
    public async Task Resumo_SeparaVencidoDeAVencerNosDoisLados()
    {
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Aluguel atrasado", 3000m, Hoje.AddDays(-5));
        await _contas.LancarContaAsync(TipoLancamento.Saida, "Luz", 400m, Hoje.AddDays(3));
        await _contas.LancarContaAsync(TipoLancamento.Entrada, "Convênio atrasado", 2000m, Hoje.AddDays(-10));
        await _contas.LancarContaAsync(TipoLancamento.Entrada, "Particular", 500m, Hoje.AddDays(6));

        var r = await _contas.ResumoAsync(Hoje, Hoje.AddDays(30));

        r.APagarVencido.Should().Be(3000m);
        r.APagarAVencer.Should().Be(400m);
        r.AReceberVencido.Should().Be(2000m);
        r.AReceberAVencer.Should().Be(500m);
        r.QuantidadeVencida.Should().Be(2);
        r.TotalVencido.Should().Be(5000m);
        r.SaldoPrevisto.Should().Be(2500m - 3400m);
        r.TemVencido.Should().BeTrue();
    }

    [Fact]
    public async Task Reagendar_MudaOVencimentoDaContaEmAberto()
    {
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Fornecedor", 900m, Hoje.AddDays(2));

        await _contas.ReagendarAsync(conta.Id, Hoje.AddDays(20));

        var atualizada = await _repo.ObterLancamentoAsync(conta.Id);
        atualizada!.DataVencimento.Should().Be(Hoje.AddDays(20));
    }

    [Fact]
    public async Task Reagendar_ContaJaPaga_ERecusado()
    {
        var conta = await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Já paga", 100m, Hoje.AddDays(-4));
        await _financeiro.RealizarAsync(conta.Id, Hoje.AddDays(-1));

        var acao = () => _contas.ReagendarAsync(conta.Id, Hoje.AddDays(10));

        // A data em que uma conta venceu é fato: reescrevê-la apagaria o atraso que houve.
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------- Recorrência: a série ----------------

    [Fact]
    public void SerieMensal_SaiSempreDoPrimeiroVencimento()
    {
        var r = new LancamentoRecorrente
        {
            Periodicidade = PeriodicidadeRecorrencia.Mensal,
            PrimeiroVencimento = new DateOnly(2026, 1, 31)
        };

        var datas = r.VencimentosAte(new DateOnly(2026, 4, 30));

        // Fevereiro encurta para 28, mas MARÇO VOLTA PARA 31. Se a série encadeasse a
        // partir da ocorrência anterior, o aluguel do dia 31 ficaria preso no dia 28
        // para o resto da vida, e ninguém entenderia por quê.
        datas.Should().Equal(
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 30));
    }

    [Fact]
    public void Serie_ParaNoUltimoVencimento()
    {
        var r = new LancamentoRecorrente
        {
            Periodicidade = PeriodicidadeRecorrencia.Mensal,
            PrimeiroVencimento = new DateOnly(2026, 1, 10),
            UltimoVencimento = new DateOnly(2026, 3, 10)
        };

        r.VencimentosAte(new DateOnly(2026, 12, 31)).Should().HaveCount(3);
        // Contrato com prazo se encerra sozinho, sem depender de alguém lembrar de desativar.
        r.ProximoDepoisDe(new DateOnly(2026, 3, 10)).Should().BeNull();
    }

    [Theory]
    [InlineData(PeriodicidadeRecorrencia.Semanal, 5)]
    [InlineData(PeriodicidadeRecorrencia.Quinzenal, 3)]
    [InlineData(PeriodicidadeRecorrencia.Mensal, 2)]
    [InlineData(PeriodicidadeRecorrencia.Trimestral, 1)]
    [InlineData(PeriodicidadeRecorrencia.Anual, 1)]
    public void Periodicidade_ContaAsOcorrenciasDoPasso(PeriodicidadeRecorrencia p, int esperado)
    {
        var r = new LancamentoRecorrente
        {
            Periodicidade = p,
            PrimeiroVencimento = new DateOnly(2026, 1, 1)
        };

        r.VencimentosAte(new DateOnly(2026, 2, 1)).Should().HaveCount(esperado);
    }

    [Fact]
    public void Serie_QueAindaNaoComecou_EVazia()
    {
        var r = new LancamentoRecorrente
        {
            Periodicidade = PeriodicidadeRecorrencia.Mensal,
            PrimeiroVencimento = new DateOnly(2027, 1, 1)
        };

        r.VencimentosAte(new DateOnly(2026, 12, 31)).Should().BeEmpty();
    }

    // ---------------- Recorrência: a geração ----------------

    [Fact]
    public async Task Geracao_CriaUmaContaPrevistaPorOcorrencia()
    {
        await _contas.CriarRecorrenteAsync(
            "Aluguel", TipoLancamento.Saida, 3200m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 6, 10));

        var r = await _contas.GerarAsync(new DateOnly(2026, 8, 31));

        r.Geradas.Should().Be(3); // junho, julho, agosto
        r.JaExistiam.Should().Be(0);
        r.FezAlgo.Should().BeTrue();

        var abertas = await _contas.EmAbertoAsync(new DateOnly(2026, 8, 31));
        abertas.Should().HaveCount(3);
        // Nasce PREVISTA, nunca realizada: o sistema sabe que o aluguel vence, não que
        // ele foi pago — dar baixa sozinho encheria o caixa de saída que talvez não saiu.
        abertas.Should().OnlyContain(l => l.Status == StatusLancamento.Previsto);
        abertas.Should().OnlyContain(l => l.DataPagamento == null);
    }

    [Fact]
    public async Task Geracao_RodadaDuasVezes_NaoDuplica()
    {
        await _contas.CriarRecorrenteAsync(
            "Aluguel", TipoLancamento.Saida, 3200m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 6, 10));

        var primeira = await _contas.GerarAsync(new DateOnly(2026, 8, 31));
        var segunda = await _contas.GerarAsync(new DateOnly(2026, 8, 31));

        primeira.Geradas.Should().Be(3);
        // A regra que, se quebrar, cobra o aluguel duas vezes.
        segunda.Geradas.Should().Be(0);
        segunda.JaExistiam.Should().Be(3);
        segunda.FezAlgo.Should().BeFalse();

        (await _contas.EmAbertoAsync(new DateOnly(2026, 8, 31))).Should().HaveCount(3);
    }

    [Fact]
    public async Task Geracao_AvancaSoOMesNovo()
    {
        await _contas.CriarRecorrenteAsync(
            "Internet", TipoLancamento.Saida, 250m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 6, 20));

        await _contas.GerarAsync(new DateOnly(2026, 7, 31));
        var setembro = await _contas.GerarAsync(new DateOnly(2026, 9, 30));

        setembro.Geradas.Should().Be(2);   // agosto e setembro
        setembro.JaExistiam.Should().Be(2); // junho e julho
    }

    [Fact]
    public async Task Geracao_IgnoraRecorrenteDesativada()
    {
        var r = await _contas.CriarRecorrenteAsync(
            "Software", TipoLancamento.Saida, 199m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 6, 5));

        await _contas.AtualizarRecorrenteAsync(
            r.Id, "Software", 199m, ultimoVencimento: null, ativa: false);

        (await _contas.GerarAsync(new DateOnly(2026, 12, 31))).Geradas.Should().Be(0);
    }

    [Fact]
    public async Task Desativar_NaoApagaOQueJaFoiGerado()
    {
        var r = await _contas.CriarRecorrenteAsync(
            "Software", TipoLancamento.Saida, 199m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 6, 5));
        await _contas.GerarAsync(new DateOnly(2026, 8, 31));

        await _contas.AtualizarRecorrenteAsync(
            r.Id, "Software", 199m, ultimoVencimento: null, ativa: false);

        // Contas de meses passados não somem porque o contrato acabou.
        (await _contas.EmAbertoAsync(new DateOnly(2026, 8, 31))).Should().HaveCount(3);
    }

    [Fact]
    public async Task MudarOValorDoMolde_NaoReescreveOQueJaNasceu()
    {
        var r = await _contas.CriarRecorrenteAsync(
            "Aluguel", TipoLancamento.Saida, 3000m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 7, 10));
        await _contas.GerarAsync(new DateOnly(2026, 8, 31));

        await _contas.AtualizarRecorrenteAsync(
            r.Id, "Aluguel", 3500m, ultimoVencimento: null, ativa: true);
        await _contas.GerarAsync(new DateOnly(2026, 9, 30));

        var abertas = await _contas.EmAbertoAsync(new DateOnly(2026, 9, 30));
        // Julho e agosto ficam com o valor antigo — já podem ter sido pagos e conferidos
        // no extrato. Só setembro nasce com o reajuste. Mesma regra da vigência do
        // repasse e do preço do pacote.
        abertas.Where(l => l.DataVencimento < new DateOnly(2026, 9, 1))
            .Should().OnlyContain(l => l.Valor == 3000m);
        abertas.Single(l => l.DataVencimento == new DateOnly(2026, 9, 10))
            .Valor.Should().Be(3500m);
    }

    [Fact]
    public async Task Geracao_CopiaCategoriaEFormaDePagamento()
    {
        var categoria = await _financeiro.CriarCategoriaAsync(
            "ALUGUEL", "Aluguel", TipoLancamento.Saida);

        await _contas.CriarRecorrenteAsync(
            "Aluguel", TipoLancamento.Saida, 3200m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 8, 10),
            categoriaId: categoria.Id, formaPagamento: FormaPagamento.Transferencia);

        await _contas.GerarAsync(new DateOnly(2026, 8, 31));

        var conta = (await _contas.EmAbertoAsync(new DateOnly(2026, 8, 31))).Single();
        conta.CategoriaFinanceiraId.Should().Be(categoria.Id);
        conta.FormaPagamento.Should().Be(FormaPagamento.Transferencia);
    }

    [Fact]
    public async Task Geracao_ContaGeradaPodeSerPagaSemAfetarAsOutras()
    {
        await _contas.CriarRecorrenteAsync(
            "Luz", TipoLancamento.Saida, 480m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 6, 8));
        await _contas.GerarAsync(new DateOnly(2026, 8, 31));

        var junho = (await _contas.EmAbertoAsync(new DateOnly(2026, 8, 31)))
            .First(l => l.DataVencimento == new DateOnly(2026, 6, 8));
        await _financeiro.RealizarAsync(junho.Id, new DateOnly(2026, 6, 7));

        // O lançamento tem vida própria: pagar junho não mexe em julho nem em agosto,
        // e a rodada seguinte não recria junho.
        (await _contas.EmAbertoAsync(new DateOnly(2026, 8, 31))).Should().HaveCount(2);
        (await _contas.GerarAsync(new DateOnly(2026, 8, 31))).Geradas.Should().Be(0);
    }

    [Fact]
    public async Task Recorrente_ComUltimoVencimentoAntesDoPrimeiro_ERecusada()
    {
        var acao = () => _contas.CriarRecorrenteAsync(
            "Impossível", TipoLancamento.Saida, 100m,
            PeriodicidadeRecorrencia.Mensal,
            primeiroVencimento: new DateOnly(2026, 8, 1),
            ultimoVencimento: new DateOnly(2026, 7, 1));

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Geracao_SemRecorrentes_NaoFazNada()
    {
        var r = await _contas.GerarAsync(new DateOnly(2026, 12, 31));

        r.Geradas.Should().Be(0);
        r.JaExistiam.Should().Be(0);
        r.Avisos.Should().BeEmpty();
    }

    [Fact]
    public async Task Recorrentes_SomenteAtivas_FiltraODesativado()
    {
        var a = await _contas.CriarRecorrenteAsync(
            "Ativa", TipoLancamento.Saida, 100m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 8, 1));
        await _contas.CriarRecorrenteAsync(
            "Também ativa", TipoLancamento.Saida, 200m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 8, 1));
        await _contas.AtualizarRecorrenteAsync(a.Id, "Ativa", 100m, null, ativa: false);

        (await _contas.RecorrentesAsync()).Should().HaveCount(2);
        (await _contas.RecorrentesAsync(somenteAtivas: true))
            .Should().ContainSingle().Which.Descricao.Should().Be("Também ativa");
    }

    [Fact]
    public async Task Geracao_GravaAuditoria()
    {
        await _contas.CriarRecorrenteAsync(
            "Aluguel", TipoLancamento.Saida, 3200m,
            PeriodicidadeRecorrencia.Mensal, new DateOnly(2026, 8, 10));
        await _contas.GerarAsync(new DateOnly(2026, 8, 31), operador: "maria");

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().Contain(e => e.Acao == "RecorrenteCriada");
        eventos.Should().Contain(e => e.Acao == "ContasRecorrentesGeradas" && e.Operador == "maria");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
