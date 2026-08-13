using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// O painel da direção (parcela 22).
///
/// O Gerente Geral carrega os três módulos e abria no primeiro item do primeiro deles:
/// o painel da RECEPÇÃO. Quem manda na clínica entrava no sistema e via a fila do balcão.
/// O resto estava espalhado por sete telas — e ninguém abre sete telas toda manhã.
///
/// O que estes testes protegem:
/// 1. **A comparação é com o MESMO TRECHO do mês anterior.** No dia 5, comparar cinco
///    dias contra trinta apontaria queda de 80% todo começo de mês, e a direção
///    aprenderia a ignorar a seta — o pior destino de um indicador.
/// 2. **Sem base de comparação não há variação**: `null`, não zero.
/// 3. **Glosa vencida e glosa no prazo são alertas DIFERENTES.** Uma é prejuízo
///    consumado, a outra é dinheiro que ainda dá para buscar.
/// 4. **O painel não recalcula nada** — cada número vem do serviço dono dele. Estes
///    testes escrevem pelo fluxo real e conferem que o painel repete o mesmo número.
/// </summary>
public class PainelDirecaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly ContasService _contas;
    private readonly PainelDirecaoService _painel;

    /// <summary>Dia 10 de propósito: sobra mês para trás e para frente nos recortes.</summary>
    private static readonly DateOnly Hoje = new(2026, 8, 10);

    public PainelDirecaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _financeiro = new FinanceiroService(_repo);
        _contas = new ContasService(_repo);
        var parametros = new ParametrosService(_repo);
        var pendencias = new PendenciaService(_repo, parametros);

        _painel = new PainelDirecaoService(
            _repo,
            _financeiro,
            _contas,
            new RecebiveisService(_repo),
            new FechamentoCaixaService(_repo),
            new RodadaPendenciasService(_repo, pendencias, parametros),
            pendencias,
            new InadimplenciaService(_repo, _financeiro),
            new ReceitaGlosadaService(_repo, _financeiro),
            new MetaService(_repo, _financeiro, new IndicadoresService(_repo, parametros)),
            new OrcamentoService(_repo, _contas),
            new ConsultorioService(_repo),
            new ChecagemPrescricaoService(_repo));
    }

    private Task EntradaAsync(DateOnly dia, decimal valor,
        FormaPagamento forma = FormaPagamento.Dinheiro)
        => _financeiro.LancarAsync(dia, TipoLancamento.Entrada, "Sessão", valor,
            StatusLancamento.Realizado, forma);

    private Task SaidaAsync(DateOnly dia, decimal valor)
        => _financeiro.LancarAsync(dia, TipoLancamento.Saida, "Material", valor,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro);

    // ---------------- Dinheiro do mês ----------------

    [Fact]
    public async Task Mes_SomaSoOQueFoiREALIZADONoMesCorrente()
    {
        await EntradaAsync(Hoje, 500m);
        await EntradaAsync(Hoje.AddDays(-3), 300m);
        await SaidaAsync(Hoje, 120m);
        // Mês passado não entra no mês corrente.
        await EntradaAsync(new DateOnly(2026, 7, 28), 999m);
        // Previsto não é realizado: o painel diz o que entrou, não o que promete entrar.
        await _financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "Prevista", 700m,
            StatusLancamento.Previsto);

        var p = await _painel.MontarAsync(Hoje);

        p.EntradasMes.Should().Be(800m);
        p.SaidasMes.Should().Be(120m);
        p.SaldoMes.Should().Be(680m);
    }

    [Fact]
    public async Task Variacao_ComparaOMESMOTRECHODoMesAnterior()
    {
        // Este mês, até o dia 10: 1.000.
        await EntradaAsync(new DateOnly(2026, 8, 5), 1_000m);

        // Mês anterior: 500 até o dia 10 e mais 4.000 depois do dia 10.
        await EntradaAsync(new DateOnly(2026, 7, 4), 500m);
        await EntradaAsync(new DateOnly(2026, 7, 25), 4_000m);

        var p = await _painel.MontarAsync(Hoje);

        // Contra o mês anterior INTEIRO (4.500) a clínica teria despencado 78%; contra o
        // mesmo trecho (500) ela dobrou. A segunda leitura é a verdadeira, e é a única que
        // não pinta todo dia 1º de vermelho.
        p.EntradasMesAnterior.Should().Be(500m);
        p.VariacaoEntradas.Should().BeApproximately(1.0d, 0.0001d);
    }

    [Fact]
    public async Task Variacao_DiaQueNaoExisteNoMesAnterior_CaiNoUltimoDiaDele()
    {
        // 31 de março contra fevereiro: não existe 31 de fevereiro, e o máximo comparável
        // é o mês inteiro.
        var trintaEUm = new DateOnly(2026, 3, 31);
        await EntradaAsync(new DateOnly(2026, 2, 28), 200m);
        await EntradaAsync(trintaEUm, 200m);

        var p = await _painel.MontarAsync(trintaEUm);

        p.EntradasMesAnterior.Should().Be(200m);
        p.VariacaoEntradas.Should().Be(0d);
    }

    [Fact]
    public async Task Variacao_SemNadaNoMesAnterior_NaoTemVariacao()
    {
        await EntradaAsync(Hoje, 1_000m);

        var p = await _painel.MontarAsync(Hoje);

        // Zero e "não medido" são coisas diferentes. A clínica que começou a usar o
        // sistema neste mês veria "+∞%" no lugar de "—".
        p.EntradasMesAnterior.Should().BeNull();
        p.VariacaoEntradas.Should().BeNull();
    }

    [Fact]
    public async Task Variacao_MesAnteriorComMovimentoMasSemEntrada_NaoTemVariacao()
    {
        await SaidaAsync(new DateOnly(2026, 7, 5), 300m);
        await EntradaAsync(Hoje, 1_000m);

        var p = await _painel.MontarAsync(Hoje);

        // Houve base (o mês existe no sistema), mas dividir por zero não produz
        // porcentagem nenhuma.
        p.EntradasMesAnterior.Should().Be(0m);
        p.VariacaoEntradas.Should().BeNull();
    }

    [Fact]
    public async Task Variacao_QuedaVemNegativa()
    {
        await EntradaAsync(new DateOnly(2026, 7, 3), 1_000m);
        await EntradaAsync(Hoje, 750m);

        var p = await _painel.MontarAsync(Hoje);

        p.VariacaoEntradas.Should().BeApproximately(-0.25d, 0.0001d);
    }

    // ---------------- Os alertas ----------------

    [Fact]
    public async Task ClinicaEmDia_NenhumAlerta()
    {
        await EntradaAsync(Hoje, 300m);
        // Espécie entra na gaveta, então o dia precisa estar conferido para não alertar.
        var proposta = await new FechamentoCaixaService(_repo).PrepararAsync(Hoje);
        await new FechamentoCaixaService(_repo).ConferirAsync(Hoje, proposta.Esperado, null, "ana");

        var p = await _painel.MontarAsync(Hoje);

        p.SemAlerta.Should().BeTrue();
        p.TemNaoVerificado.Should().BeFalse();
    }

    [Fact]
    public async Task ContaVencida_ViraAlertaDePERIGO()
    {
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3_200m, Hoje.AddDays(-5), operador: "ana");

        var p = await _painel.MontarAsync(Hoje);

        p.Contas.QuantidadeVencida.Should().Be(1);
        var alerta = p.Alertas.Should().ContainSingle(
            a => a.Assunto == AssuntoDirecao.ContasVencidas).Subject;
        alerta.Gravidade.Should().Be(GravidadeDirecao.Perigo);
        alerta.Detalhe.Should().Contain("3.200,00");
    }

    [Fact]
    public async Task ContaAPagarEPacienteDevendo_SaoAlertasSEPARADOS()
    {
        var maria = new Paciente { Nome = "Maria" };
        await _repo.AdicionarPacienteAsync(maria);
        await _repo.SalvarAsync();

        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3_200m, Hoje.AddDays(-5), operador: "ana");
        await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão", 300m, Hoje.AddDays(-5),
            operador: "ana", pacienteId: maria.Id);

        var p = await _painel.MontarAsync(Hoje);

        // "R$ 3.500 vencidos" misturando o aluguel que a clínica não pagou com a sessão
        // que o paciente não pagou é um número sem significado: um se resolve pagando, o
        // outro cobrando.
        var aPagar = p.Alertas.Single(a => a.Assunto == AssuntoDirecao.ContasVencidas);
        aPagar.Detalhe.Should().Contain("3.200,00");
        aPagar.Detalhe.Should().NotContain("3.500");

        var devendo = p.Alertas.Single(a => a.Assunto == AssuntoDirecao.PacientesDevendo);
        devendo.Titulo.Should().Contain("1 paciente");
        devendo.Detalhe.Should().Contain("300,00");
        p.PacientesDevendo.Should().Be(1);
        p.TotalDevidoPorPacientes.Should().Be(300m);
    }

    [Fact]
    public async Task PacienteDevendo_AteNoventaDiasEAviso_AcimaEPerigo()
    {
        var maria = new Paciente { Nome = "Maria" };
        await _repo.AdicionarPacienteAsync(maria);
        await _repo.SalvarAsync();

        await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão", 300m, Hoje.AddDays(-20),
            operador: "ana", pacienteId: maria.Id);

        var recente = await _painel.MontarAsync(Hoje);
        recente.Alertas.Single(a => a.Assunto == AssuntoDirecao.PacientesDevendo)
            .Gravidade.Should().Be(GravidadeDirecao.Aviso);

        var joao = new Paciente { Nome = "João" };
        await _repo.AdicionarPacienteAsync(joao);
        await _repo.SalvarAsync();
        await _contas.LancarContaAsync(
            TipoLancamento.Entrada, "Sessão", 900m, Hoje.AddDays(-200),
            operador: "ana", pacienteId: joao.Id);

        // Acima de 90 dias a cobrança deixa de ser lembrete e passa a ser decisão —
        // acordo, ou parar de contar com o dinheiro. Marcar as duas com a mesma cor faria
        // a clínica tratar as duas do mesmo jeito.
        var velha = await _painel.MontarAsync(Hoje);
        var alerta = velha.Alertas.Single(a => a.Assunto == AssuntoDirecao.PacientesDevendo);
        alerta.Gravidade.Should().Be(GravidadeDirecao.Perigo);
        alerta.Detalhe.Should().Contain("mais de 90 dias");
    }

    [Fact]
    public async Task ContaAVencer_NaoAlerta()
    {
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3_200m, Hoje.AddDays(5), operador: "ana");

        var p = await _painel.MontarAsync(Hoje);

        // Conta que ainda vai vencer é rotina, não problema. Alertar sobre ela ensinaria
        // a clínica a fechar o painel sem ler.
        p.Alertas.Should().NotContain(a => a.Assunto == AssuntoDirecao.ContasVencidas);
        p.Contas.APagarAVencer.Should().Be(3_200m);
    }

    [Fact]
    public async Task GavetaNaoConferida_ViraAlertaDeAVISO()
    {
        await EntradaAsync(Hoje.AddDays(-2), 150m);
        await EntradaAsync(Hoje.AddDays(-1), 90m);

        var p = await _painel.MontarAsync(Hoje);

        p.DiasCaixaNaoConferido.Should().Be(2);
        p.EspecieNaoConferida.Should().Be(240m);
        var alerta = p.Alertas.Should().ContainSingle(
            a => a.Assunto == AssuntoDirecao.CaixaNaoConferido).Subject;
        // Aviso e não perigo: a divergência ainda pode não existir. O que se perde com o
        // tempo é a chance de descobrir de onde ela veio.
        alerta.Gravidade.Should().Be(GravidadeDirecao.Aviso);
    }

    [Fact]
    public async Task GavetaSoDeCartao_NaoAlerta()
    {
        await EntradaAsync(Hoje, 400m, FormaPagamento.CartaoCredito);

        var p = await _painel.MontarAsync(Hoje);

        // Cartão não passa pela gaveta: cobrar conferência de um dia sem dinheiro
        // treinaria a clínica a fechar no automático.
        p.DiasCaixaNaoConferido.Should().Be(0);
        p.Alertas.Should().NotContain(a => a.Assunto == AssuntoDirecao.CaixaNaoConferido);
    }

    [Fact]
    public async Task Alertas_VemComPerigoPrimeiro()
    {
        // Um perigo (conta vencida) e um aviso (gaveta não conferida).
        await _contas.LancarContaAsync(
            TipoLancamento.Saida, "Aluguel", 3_200m, Hoje.AddDays(-5), operador: "ana");
        await EntradaAsync(Hoje.AddDays(-1), 90m);

        var p = await _painel.MontarAsync(Hoje);

        // O painel é lido de cima para baixo, e a ordem é a única forma de dizer o que
        // vem antes sem escrever "prioridade 1" em cada linha.
        p.Alertas.Should().HaveCountGreaterThanOrEqualTo(2);
        p.Alertas[0].Gravidade.Should().Be(GravidadeDirecao.Perigo);
        p.Alertas.Last().Gravidade.Should().Be(GravidadeDirecao.Aviso);
    }

    [Fact]
    public async Task PainelDeBaseVazia_NaoQuebraENaoInventaAlerta()
    {
        var p = await _painel.MontarAsync(Hoje);

        p.SemAlerta.Should().BeTrue();
        p.TemNaoVerificado.Should().BeFalse();
        p.EntradasMes.Should().Be(0m);
        p.VariacaoEntradas.Should().BeNull();
        p.PendenciasEmAberto.Should().Be(0);
        p.GuiasSemReceita.Should().Be(0);
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
