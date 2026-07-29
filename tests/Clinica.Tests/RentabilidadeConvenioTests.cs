using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Rentabilidade por convênio (parcela 19).
///
/// A clínica fatura muito convênio e não tinha como responder à pergunta mais básica da
/// direção: **qual operadora vale a pena?**. O faturamento sabe quantas guias saíram e o
/// financeiro sabe quanto entrou, mas os dois números nunca se encontravam por convênio.
///
/// O que estes testes protegem:
/// 1. **Líquido por guia** — o único número comparável entre operadoras que pagam valores
///    e volumes diferentes.
/// 2. **Prazo médio só do que JÁ foi pago.** Incluir o previsto mediria uma promessa, e a
///    operadora que atrasa teria o melhor número.
/// 3. **Guias sem receita** — trabalho feito e não cobrado, visto por convênio.
/// </summary>
public class RentabilidadeConvenioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RentabilidadeConvenioService _rentabilidade;
    private readonly FinanceiroService _financeiro;
    private readonly AtendimentoService _atendimentos;

    private static readonly DateOnly Dia = new(2026, 9, 10);
    private static readonly DateOnly Inicio = new(2026, 9, 1);
    private static readonly DateOnly Fim = new(2026, 9, 30);

    public RentabilidadeConvenioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _rentabilidade = new RentabilidadeConvenioService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _atendimentos = new AtendimentoService(_repo);
    }

    /// <summary>Uma guia efetivada de um convênio, pronta para virar receita.</summary>
    private async Task<GuiaSemLancamento> GuiaAsync(
        Convenio familia, string codigo, DateOnly? dia = null)
    {
        var d = dia ?? Dia;
        var paciente = new Paciente
        {
            Nome = $"Paciente {codigo} {Guid.NewGuid():N}",
            Convenio = familia,
            ConvenioCodigo = codigo
        };
        await _repo.AdicionarPacienteAsync(paciente);
        await _repo.SalvarAsync();

        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, d, ModalidadeAtendimento.AcupunturaSimples);

        // Só o primeiro código: o teste mede convênio, não a regra de 2º código.
        var codigoGuia = resultado.Atendimento.Codigos[0];
        codigoGuia.Status = StatusCodigo.Baixado;
        codigoGuia.DataBaixa = d;
        foreach (var c in resultado.Atendimento.Codigos.Skip(1))
            c.Status = StatusCodigo.NaoAplicavel;
        await _repo.SalvarAsync();

        return (await _financeiro.GuiasSemLancamentoAsync(d, d))
            .First(g => g.CodigoId == codigoGuia.Id);
    }

    /// <summary>
    /// O fluxo real: a receita da guia nasce PREVISTA (a operadora ainda não pagou) e só
    /// vira realizada quando o dinheiro cai, com a data em que caiu. É essa data que o
    /// prazo médio mede — e é por isso que ela não pode ser a da baixa da guia.
    /// </summary>
    private async Task<LancamentoFinanceiro> ReceberAsync(
        GuiaSemLancamento guia, decimal valor, decimal retido = 0m,
        DateOnly? pagamento = null)
    {
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(
            guia, valor,
            deducoes: retido > 0m
                ? new DeducoesRecebimento(null, null, null, retido, null, null)
                : null);

        if (pagamento is { } dia) await _financeiro.RealizarAsync(lancamento.Id, dia);
        return lancamento;
    }

    // ---------------- O agrupamento ----------------

    [Fact]
    public async Task Convenio_SomaGuiasEDinheiro()
    {
        var g1 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var g2 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(g1, 100m, retido: 6.15m);
        await ReceberAsync(g2, 200m, retido: 12.30m);

        var linhas = await _rentabilidade.PorConvenioAsync(Inicio, Fim);

        linhas.Should().ContainSingle();
        linhas[0].Codigo.Should().Be("Unimed");
        linhas[0].Guias.Should().Be(2);
        linhas[0].GuiasComReceita.Should().Be(2);
        linhas[0].Bruto.Should().Be(300m);
        linhas[0].Retido.Should().Be(18.45m);
        linhas[0].Liquido.Should().Be(281.55m);
    }

    [Fact]
    public async Task DoisConvenios_ApareceSeparados_MaisRentavelPrimeiro()
    {
        var unimed = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var amil = await GuiaAsync(Convenio.Amil, "Amil");
        await ReceberAsync(unimed, 100m);
        await ReceberAsync(amil, 400m);

        var linhas = await _rentabilidade.PorConvenioAsync(Inicio, Fim);

        linhas.Should().HaveCount(2);
        linhas[0].Codigo.Should().Be("Amil");
        linhas[1].Codigo.Should().Be("Unimed");
    }

    [Fact]
    public async Task MesmaFamilia_CodigosDiferentes_NaoSeMisturam()
    {
        // Duas operadoras podem compartilhar a mesma família de regra — e é justamente a
        // distinção que a retenção da parcela 18 usa.
        var a = await GuiaAsync(Convenio.UnimedPadrao, "UnimedRegional");
        var b = await GuiaAsync(Convenio.UnimedPadrao, "UnimedNacional");
        await ReceberAsync(a, 100m);
        await ReceberAsync(b, 300m);

        (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Should().HaveCount(2);
    }

    // ---------------- Líquido por guia ----------------

    [Fact]
    public async Task LiquidoPorGuia_EOQueTornaOsConveniosComparaveis()
    {
        // A Unimed fatura mais no total, mas precisa de três atendimentos para isso.
        var u1 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var u2 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var u3 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(u1, 100m);
        await ReceberAsync(u2, 100m);
        await ReceberAsync(u3, 100m);

        var amil = await GuiaAsync(Convenio.Amil, "Amil");
        await ReceberAsync(amil, 250m);

        var linhas = await _rentabilidade.PorConvenioAsync(Inicio, Fim);

        linhas.Single(l => l.Codigo == "Unimed").Liquido.Should().Be(300m);
        linhas.Single(l => l.Codigo == "Unimed").LiquidoPorGuia.Should().Be(100m);
        // "A Unimed faturou mais" não diz nada: por atendimento, a Amil rende 2,5x.
        linhas.Single(l => l.Codigo == "Amil").LiquidoPorGuia.Should().Be(250m);
    }

    [Fact]
    public async Task LiquidoPorGuia_SemReceita_ENULO()
    {
        await GuiaAsync(Convenio.UnimedPadrao, "Unimed");

        var linha = (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single();

        // Média sobre zero é invenção; a tela mostra "—".
        linha.LiquidoPorGuia.Should().BeNull();
        linha.PercentualRetido.Should().BeNull();
    }

    [Fact]
    public async Task SemReceita_ContaOTrabalhoFeitoENaoCobrado()
    {
        var comDinheiro = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(comDinheiro, 100m);

        var linha = (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single();

        // É a fila da conciliação com nome de quem está devendo.
        linha.Guias.Should().Be(3);
        linha.GuiasComReceita.Should().Be(1);
        linha.SemReceita.Should().Be(2);
    }

    // ---------------- Prazo de pagamento ----------------

    [Fact]
    public async Task PrazoMedio_ContaDaBaixaAtePagamento()
    {
        var g1 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var g2 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(g1, 100m, pagamento: Dia.AddDays(30));
        await ReceberAsync(g2, 100m, pagamento: Dia.AddDays(50));

        var linha = (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single();

        linha.PrazoMedioDias.Should().Be(40);
    }

    [Fact]
    public async Task PrazoMedio_IgnoraOQueAindaNaoFoiPago()
    {
        var pago = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var previsto = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(pago, 100m, pagamento: Dia.AddDays(30));
        await ReceberAsync(previsto, 100m);

        var linha = (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single();

        // Incluir o previsto mediria uma promessa — e a operadora que atrasa teria o
        // melhor número, que é o oposto do que a métrica existe para mostrar.
        linha.PrazoMedioDias.Should().Be(30);
    }

    [Fact]
    public async Task PrazoMedio_SemNadaPago_ENULO()
    {
        var g = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(g, 100m);

        (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single()
            .PrazoMedioDias.Should().BeNull();
    }

    // ---------------- Glosa ----------------

    [Fact]
    public async Task Glosada_EContadaPorConvenio()
    {
        var g = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await GuiaAsync(Convenio.UnimedPadrao, "Unimed");

        var codigo = await _repo.ObterCodigoAsync(g.CodigoId);
        codigo!.Glosa = StatusGlosa.Glosada;
        await _repo.SalvarAsync();

        var linha = (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single();
        linha.Glosadas.Should().Be(1);

        var resumo = await _rentabilidade.ResumoAsync(Inicio, Fim);
        resumo.TaxaGlosa.Should().Be(50.0);
    }

    // ---------------- O resumo ----------------

    [Fact]
    public async Task Resumo_ApontaAMaisEAMenosRentavelPORGUIA()
    {
        var u1 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var u2 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var u3 = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(u1, 100m);
        await ReceberAsync(u2, 100m);
        await ReceberAsync(u3, 100m);

        var amil = await GuiaAsync(Convenio.Amil, "Amil");
        await ReceberAsync(amil, 250m);

        var r = await _rentabilidade.ResumoAsync(Inicio, Fim);

        r.Bruto.Should().Be(550m);
        r.Guias.Should().Be(4);
        // A menos rentável NÃO é a que rendeu menos dinheiro no total (a Amil, com R$ 250):
        // é a que rende menos POR ATENDIMENTO.
        r.MaisRentavel!.Codigo.Should().Be("Amil");
        r.MenosRentavel!.Codigo.Should().Be("Unimed");
    }

    [Fact]
    public async Task Resumo_UmConvenioSo_NaoTemMenosRentavel()
    {
        var g = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        await ReceberAsync(g, 100m);

        var r = await _rentabilidade.ResumoAsync(Inicio, Fim);

        // Comparar uma operadora com ela mesma não diz nada.
        r.MaisRentavel!.Codigo.Should().Be("Unimed");
        r.MenosRentavel.Should().BeNull();
    }

    [Fact]
    public async Task Resumo_PeriodoVazio_NaoQuebra()
    {
        var r = await _rentabilidade.ResumoAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        r.Guias.Should().Be(0);
        r.Bruto.Should().Be(0m);
        r.PercentualRetido.Should().BeNull();
        r.TaxaGlosa.Should().BeNull();
        r.MaisRentavel.Should().BeNull();
    }

    [Fact]
    public async Task LancamentoCancelado_NaoConta()
    {
        var g = await GuiaAsync(Convenio.UnimedPadrao, "Unimed");
        var l = await ReceberAsync(g, 100m);
        await _financeiro.CancelarAsync(l.Id, "lançado em duplicidade");

        var linha = (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Single();

        linha.Bruto.Should().Be(0m);
        linha.GuiasComReceita.Should().Be(0);
        linha.SemReceita.Should().Be(1);
    }

    [Fact]
    public async Task Periodo_EODOATENDIMENTO_NaoODoRecebimento()
    {
        // Guia atendida em agosto, recebida em setembro: ela pertence ao balanço de
        // agosto — casar pelo recebimento misturaria meses no mesmo número.
        var g = await GuiaAsync(Convenio.UnimedPadrao, "Unimed", dia: new DateOnly(2026, 8, 20));
        await ReceberAsync(g, 100m, pagamento: new DateOnly(2026, 9, 25));

        (await _rentabilidade.PorConvenioAsync(Inicio, Fim)).Should().BeEmpty();
        (await _rentabilidade.PorConvenioAsync(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))).Should().ContainSingle();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
