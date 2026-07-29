using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Retenção na fonte por convênio (parcela 18).
///
/// A operadora que paga serviço de PJ retém IRRF, CSLL/PIS/COFINS e às vezes o ISS antes
/// de depositar: a guia vale R$ 1.000 e caem R$ 943,50. Até aqui o sistema gravava os
/// R$ 1.000 e não sabia da diferença — o mesmo defeito que a parcela 9 corrigiu para a
/// maquininha, intocado justamente no convênio.
///
/// A regra que estes testes protegem acima de todas: **a retenção do convênio SUBSTITUI
/// os tributos gerais naquele recebimento**. Os dois representam o mesmo imposto, e
/// somá-los contaria duas vezes — o erro que mais aparece em planilha de clínica de
/// convênio.
/// </summary>
public class RetencaoConvenioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly TributoService _tributos;
    private readonly TaxaService _taxas;
    private readonly FinanceiroService _financeiro;
    private readonly AtendimentoService _atendimentos;

    private static readonly DateOnly Hoje = new(2026, 10, 15);

    public RetencaoConvenioTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        var parametros = new ParametrosService(_repo);
        _tributos = new TributoService(_repo, parametros);
        _taxas = new TaxaService(_repo, parametros, _tributos);
        _financeiro = new FinanceiroService(_repo);
        _atendimentos = new AtendimentoService(_repo);
    }

    private Task<Tributo> CriarAsync(
        string sigla, decimal percentual, string? convenio = null, decimal basePercentual = 100m)
        => _tributos.SalvarAsync(new Tributo
        {
            Sigla = sigla,
            Nome = sigla,
            Percentual = percentual,
            BasePercentual = basePercentual,
            ConvenioCodigo = convenio
        });

    // ---------------- A resolução ----------------

    [Fact]
    public async Task Convenio_ComRetencaoCadastrada_UsaARetencao()
    {
        await CriarAsync("ISS", 3m);                            // tributo geral da clínica
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");
        await CriarAsync("CSLL/PIS/COFINS", 4.65m, convenio: "Unimed");

        var r = await _tributos.ApurarAsync(1000m, Hoje, "Unimed");

        // 6,15% da operadora — o ISS de 3% NÃO se soma. Os dois são o mesmo imposto, e
        // somá-los daria 9,15%, que é o erro clássico da planilha de convênio.
        r.Aliquota.Should().Be(6.15m);
        r.Valor.Should().Be(61.50m);
        r.RetidoNaFonte.Should().BeTrue();
        r.Detalhe.Should().Contain("retido na fonte");
    }

    [Fact]
    public async Task Convenio_SemRetencaoCadastrada_CaiNosTributosGerais()
    {
        await CriarAsync("ISS", 3m);
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");

        var r = await _tributos.ApurarAsync(1000m, Hoje, "Amil");

        // Mudar isso faria a receita de convênio parar de sofrer imposto da noite para o
        // dia, em toda base que já existe.
        r.Valor.Should().Be(30m);
        r.RetidoNaFonte.Should().BeFalse();
    }

    [Fact]
    public async Task ParticularEmDinheiro_NaoSofreARetencaoDoConvenio()
    {
        await CriarAsync("ISS", 3m);
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");

        var r = await _tributos.ApurarAsync(1000m, Hoje);

        // A retenção da Unimed não vaza para o recebimento que não é dela.
        r.Valor.Should().Be(30m);
        r.Detalhe.Should().Be("ISS 3%");
    }

    [Fact]
    public async Task SoRetencaoCadastrada_ParticularNaoSofreImpostoNenhum()
    {
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");

        // Nenhum tributo geral: o particular fica sem retenção, que é a verdade — a
        // clínica não cadastrou o que ela mesma recolhe.
        (await _tributos.ApurarAsync(1000m, Hoje)).Houve.Should().BeFalse();
    }

    [Fact]
    public async Task Retencao_TemVigenciaComoQualquerTributo()
    {
        await _tributos.SalvarAsync(new Tributo
        {
            Sigla = "IRRF", Nome = "IRRF", Percentual = 1.5m, BasePercentual = 100m,
            ConvenioCodigo = "Unimed", VigenteAte = Hoje.AddDays(-1)
        });

        (await _tributos.ApurarAsync(1000m, Hoje, "Unimed")).Houve.Should().BeFalse();
    }

    [Fact]
    public async Task Retencao_CasaOCodigoIgnorandoMaiusculas()
    {
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");

        (await _tributos.ApurarAsync(1000m, Hoje, "UNIMED")).Valor.Should().Be(15m);
    }

    [Fact]
    public async Task DoisConvenios_RetemPercentuaisDiferentes()
    {
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");
        await CriarAsync("ISS-RETIDO", 5m, convenio: "Petrobras");

        (await _tributos.ApurarAsync(1000m, Hoje, "Unimed")).Valor.Should().Be(15m);
        (await _tributos.ApurarAsync(1000m, Hoje, "Petrobras")).Valor.Should().Be(50m);
    }

    [Fact]
    public async Task RetencaoComBasePresumida_UsaAEfetiva()
    {
        // Algumas operadoras retêm sobre base reduzida.
        await CriarAsync("IRRF", 15m, convenio: "Unimed", basePercentual: 32m);

        (await _tributos.ApurarAsync(10_000m, Hoje, "Unimed")).Valor.Should().Be(480m);
    }

    [Fact]
    public async Task Catalogo_DistingueRetencaoDeTributoProprio()
    {
        var iss = await CriarAsync("ISS", 3m);
        var irrf = await CriarAsync("IRRF", 1.5m, convenio: "Unimed");

        iss.RetidoNaFonte.Should().BeFalse();
        irrf.RetidoNaFonte.Should().BeTrue();
        irrf.ConvenioCodigo.Should().Be("Unimed");
    }

    // ---------------- O caminho completo, da guia ao caixa ----------------

    private async Task<GuiaSemLancamento> GuiaBaixadaAsync(string convenioCodigo)
    {
        var paciente = new Paciente
        {
            Nome = "Maria Souza",
            Convenio = Convenio.UnimedPadrao,
            ConvenioCodigo = convenioCodigo
        };
        await _repo.AdicionarPacienteAsync(paciente);
        await _repo.SalvarAsync();

        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, Hoje, ModalidadeAtendimento.AcupunturaSimples);
        foreach (var c in resultado.Atendimento.Codigos)
        {
            c.Status = StatusCodigo.Baixado;
            c.DataBaixa = Hoje;
        }
        await _repo.SalvarAsync();

        return (await _financeiro.GuiasSemLancamentoAsync(Hoje, Hoje)).First();
    }

    [Fact]
    public async Task Guia_LevaOCodigoDoConvenioParaResolverARetencao()
    {
        var guia = await GuiaBaixadaAsync("Unimed");

        guia.ConvenioCodigo.Should().Be("Unimed");
    }

    [Fact]
    public async Task ReceitaDaGuia_GravaOBrutoEARetencaoAoLado()
    {
        await CriarAsync("IRRF", 1.5m, convenio: "Unimed");
        await CriarAsync("CSLL/PIS/COFINS", 4.65m, convenio: "Unimed");
        var guia = await GuiaBaixadaAsync("Unimed");

        var deducoes = await _taxas.CalcularAsync(
            1000m, guia.DataBaixa, FormaPagamento.Convenio,
            reterImposto: true, convenioCodigo: guia.ConvenioCodigo);
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(guia, 1000m, deducoes: deducoes);

        // O BRUTO continua sendo o valor do lançamento — como em todo o resto do módulo.
        lancamento.Valor.Should().Be(1000m);
        lancamento.ValorImposto.Should().Be(61.50m);
        lancamento.ValorLiquido.Should().Be(938.50m);
        lancamento.DetalheImposto.Should().Contain("retido na fonte");
        lancamento.ConvenioCodigo.Should().Be("Unimed");
    }

    [Fact]
    public async Task ReceitaDaGuia_SemRetencaoCadastrada_NaoInventaDesconto()
    {
        var guia = await GuiaBaixadaAsync("Unimed");

        var deducoes = await _taxas.CalcularAsync(
            1000m, guia.DataBaixa, FormaPagamento.Convenio,
            reterImposto: true, convenioCodigo: guia.ConvenioCodigo);
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(guia, 1000m, deducoes: deducoes);

        lancamento.ValorImposto.Should().BeNull();
        lancamento.ValorLiquido.Should().Be(1000m);
    }

    [Fact]
    public async Task PacienteSemCodigoNoCatalogo_CaiNaFamilia()
    {
        var paciente = new Paciente
        {
            Nome = "João",
            Convenio = Convenio.Amil,
            ConvenioCodigo = null
        };
        await _repo.AdicionarPacienteAsync(paciente);
        await _repo.SalvarAsync();

        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, Hoje, ModalidadeAtendimento.AcupunturaSimples);
        foreach (var c in resultado.Atendimento.Codigos)
        {
            c.Status = StatusCodigo.Baixado;
            c.DataBaixa = Hoje;
        }
        await _repo.SalvarAsync();

        var guia = (await _financeiro.GuiasSemLancamentoAsync(Hoje, Hoje)).First();

        // É o que o resto do sistema já faz para resolver nome e regra do convênio.
        guia.ConvenioCodigo.Should().Be("Amil");
    }

    [Fact]
    public async Task ReceitaDaGuia_NaoPodeTerRetencaoMaiorQueOBruto()
    {
        var guia = await GuiaBaixadaAsync("Unimed");

        var acao = () => _financeiro.LancarReceitaDaGuiaAsync(
            guia, 100m,
            deducoes: new DeducoesRecebimento(null, null, 200m, 200m, null, null));

        // Deixaria a clínica recebendo menos que zero por um atendimento.
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
