using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Conferência da gaveta (parcela 14).
///
/// O que estes testes protegem:
/// 1. **Só espécie entra na conta.** Cartão e PIX não passam pela gaveta; incluí-los
///    faria a conferência nunca bater, e conferência que nunca bate treina a clínica a
///    clicar "OK" sem olhar.
/// 2. **Diferença sem justificativa é recusada.** É a única regra deste serviço que
///    impede em vez de avisar — o valor da conferência está inteiro na obrigação de
///    explicar o que não bateu.
/// 3. **O valor do sistema é COPIADO.** Um lançamento digitado depois, com a data de
///    ontem, não pode reescrever a conferência de ontem.
/// </summary>
public class FechamentoCaixaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FechamentoCaixaService _fechamento;
    private readonly FinanceiroService _financeiro;

    private static readonly DateOnly Hoje = new(2026, 8, 20);

    public FechamentoCaixaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _fechamento = new FechamentoCaixaService(_repo);
        _financeiro = new FinanceiroService(_repo);
    }

    private Task EmDinheiroAsync(DateOnly dia, decimal valor, TipoLancamento tipo = TipoLancamento.Entrada)
        => _financeiro.LancarAsync(
            dia, tipo, "Sessão", valor, StatusLancamento.Realizado, FormaPagamento.Dinheiro);

    // ---------------- A proposta ----------------

    [Fact]
    public async Task Proposta_SomaSoOQuePassouPelaGaveta()
    {
        await EmDinheiroAsync(Hoje, 150m);
        await EmDinheiroAsync(Hoje, 80m);
        // Cartão e PIX não passam pela gaveta: o dinheiro deles cai na conta dias depois.
        await _financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "Cartão", 300m,
            StatusLancamento.Realizado, FormaPagamento.CartaoCredito);
        await _financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "Pix", 200m,
            StatusLancamento.Realizado, FormaPagamento.Pix);

        var p = await _fechamento.PrepararAsync(Hoje);

        p.EntradasEspecie.Should().Be(230m);
        p.QuantidadeLancamentos.Should().Be(2);
        p.Esperado.Should().Be(230m);
    }

    [Fact]
    public async Task Proposta_DescontaASaidaEmEspecie()
    {
        await EmDinheiroAsync(Hoje, 500m);
        await EmDinheiroAsync(Hoje, 120m, TipoLancamento.Saida);

        var p = await _fechamento.PrepararAsync(Hoje);

        p.EntradasEspecie.Should().Be(500m);
        p.SaidasEspecie.Should().Be(120m);
        // Sem isso o sistema cobraria da gaveta um dinheiro que saiu dela legitimamente.
        p.Esperado.Should().Be(380m);
    }

    [Fact]
    public async Task Proposta_IgnoraPrevistoEIgnoraOutroDia()
    {
        await _financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "A receber", 400m,
            StatusLancamento.Previsto, FormaPagamento.Dinheiro);
        await EmDinheiroAsync(Hoje.AddDays(-1), 250m);

        var p = await _fechamento.PrepararAsync(Hoje);

        p.SemMovimento.Should().BeTrue();
        p.Esperado.Should().Be(0m);
    }

    [Fact]
    public async Task Proposta_UsaODiaDoPAGAMENTO_NaoODaCompetencia()
    {
        // Competência de ontem, dinheiro recebido hoje: quem passou pela gaveta hoje
        // é este, e é hoje que ele tem de ser contado.
        await _financeiro.LancarAsync(
            Hoje.AddDays(-1), TipoLancamento.Entrada, "Atrasado", 90m,
            StatusLancamento.Realizado, FormaPagamento.Dinheiro, dataPagamento: Hoje);

        (await _fechamento.PrepararAsync(Hoje)).Esperado.Should().Be(90m);
        (await _fechamento.PrepararAsync(Hoje.AddDays(-1))).SemMovimento.Should().BeTrue();
    }

    [Fact]
    public async Task Proposta_NaoGravaNada()
    {
        await EmDinheiroAsync(Hoje, 100m);

        await _fechamento.PrepararAsync(Hoje);
        await _fechamento.PrepararAsync(Hoje);

        (await _repo.FechamentoCaixaDoDiaAsync(Hoje)).Should().BeNull();
    }

    // ---------------- A conferência ----------------

    [Fact]
    public async Task Conferir_QuandoBate_NaoExigeJustificativa()
    {
        await EmDinheiroAsync(Hoje, 230m);

        var f = await _fechamento.ConferirAsync(Hoje, 230m, operador: "maria");

        f.Bateu.Should().BeTrue();
        f.Diferenca.Should().Be(0m);
        f.Justificativa.Should().BeNull();
        f.Situacao.Should().Be("Bateu");
        f.ConferidoPor.Should().Be("maria");
    }

    [Fact]
    public async Task Conferir_ComDiferencaESemJustificativa_ERecusado()
    {
        await EmDinheiroAsync(Hoje, 230m);

        var acao = () => _fechamento.ConferirAsync(Hoje, 200m);

        // A única regra deste serviço que IMPEDE em vez de avisar.
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*diferença*silêncio*");
    }

    [Fact]
    public async Task Conferir_ComFalta_RegistraADiferencaEAExplicacao()
    {
        await EmDinheiroAsync(Hoje, 230m);

        var f = await _fechamento.ConferirAsync(Hoje, 200m, "Troco dado a mais na primeira sessão");

        f.Diferenca.Should().Be(-30m);
        f.Situacao.Should().Be("Falta");
        f.Justificativa.Should().Be("Troco dado a mais na primeira sessão");
    }

    [Fact]
    public async Task Conferir_ComSobra_TambemExigeExplicacao()
    {
        await EmDinheiroAsync(Hoje, 200m);

        var f = await _fechamento.ConferirAsync(Hoje, 250m, "Sessão da tarde não foi lançada");

        // Dinheiro a MAIS na gaveta costuma ser venda não lançada — é problema, não sorte.
        f.Diferenca.Should().Be(50m);
        f.Situacao.Should().Be("Sobra");
    }

    [Fact]
    public async Task Conferir_GuardaAFOTOEUmLancamentoPosteriorNaoAReescreve()
    {
        await EmDinheiroAsync(Hoje, 230m);
        var f = await _fechamento.ConferirAsync(Hoje, 230m);

        // Alguém digita depois um recebimento com a data de hoje.
        await EmDinheiroAsync(Hoje, 500m);

        var gravado = await _repo.FechamentoCaixaDoDiaAsync(Hoje);
        // A conferência continua dizendo o que foi contado e contra o quê. Recalcular
        // faria a diferença aparecer sozinha, sem ninguém para explicá-la.
        gravado!.ValorSistema.Should().Be(230m);
        gravado.Bateu.Should().BeTrue();
        gravado.Id.Should().Be(f.Id);
    }

    [Fact]
    public async Task Conferir_DuasVezes_ERecusado()
    {
        await EmDinheiroAsync(Hoje, 100m);
        await _fechamento.ConferirAsync(Hoje, 100m);

        var acao = () => _fechamento.ConferirAsync(Hoje, 100m);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi conferido*");
    }

    [Fact]
    public async Task Conferir_ValorNegativo_ERecusado()
    {
        var acao = () => _fechamento.ConferirAsync(Hoje, -10m, "seja lá o que for");

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Conferir_DiaSemMovimento_EPermitido()
    {
        // Conferir gaveta vazia e achá-la vazia é um resultado legítimo — e é diferente
        // de nunca ter conferido.
        var f = await _fechamento.ConferirAsync(Hoje, 0m);

        f.Bateu.Should().BeTrue();
        f.QuantidadeLancamentos.Should().Be(0);
    }

    [Fact]
    public async Task Conferir_GravaAuditoria()
    {
        await EmDinheiroAsync(Hoje, 100m);
        await _fechamento.ConferirAsync(Hoje, 90m, "faltou nota de 10", operador: "joao");

        (await _repo.EventosAuditoriaAsync())
            .Should().Contain(e => e.Acao == "CaixaConferido" && e.Operador == "joao");
    }

    // ---------------- Reabertura ----------------

    [Fact]
    public async Task Reabrir_MantemOAnteriorNoHistorico()
    {
        await EmDinheiroAsync(Hoje, 230m);
        await _fechamento.ConferirAsync(Hoje, 200m, "contagem apressada");

        await _fechamento.ReabrirAsync(Hoje, "Recontamos a gaveta", operador: "ana");

        var historico = await _fechamento.HistoricoAsync(Hoje, Hoje);
        // Apagar deixaria o dia parecendo nunca ter sido contado — história diferente
        // da que aconteceu, e justamente a que alguém poderia querer contar.
        historico.Should().ContainSingle();
        historico[0].Reaberto.Should().BeTrue();
        historico[0].MotivoReabertura.Should().Be("Recontamos a gaveta");
        historico[0].ValorContado.Should().Be(200m);
    }

    [Fact]
    public async Task Reabrir_PermiteConferirDeNovo_EOUltimoEOQueVale()
    {
        await EmDinheiroAsync(Hoje, 230m);
        await _fechamento.ConferirAsync(Hoje, 200m, "contagem apressada");
        await _fechamento.ReabrirAsync(Hoje, "recontar");

        await _fechamento.ConferirAsync(Hoje, 230m);

        var vigente = await _repo.FechamentoCaixaDoDiaAsync(Hoje);
        vigente!.ValorContado.Should().Be(230m);
        vigente.Bateu.Should().BeTrue();
        (await _fechamento.HistoricoAsync(Hoje, Hoje)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Reabrir_SemMotivo_ERecusado()
    {
        await EmDinheiroAsync(Hoje, 100m);
        await _fechamento.ConferirAsync(Hoje, 100m);

        var acao = () => _fechamento.ReabrirAsync(Hoje, "   ");

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Reabrir_DiaNuncaConferido_ERecusado()
    {
        var acao = () => _fechamento.ReabrirAsync(Hoje, "qualquer motivo");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Reabrir_DuasVezes_ERecusado()
    {
        await EmDinheiroAsync(Hoje, 100m);
        await _fechamento.ConferirAsync(Hoje, 100m);
        await _fechamento.ReabrirAsync(Hoje, "primeira");

        var acao = () => _fechamento.ReabrirAsync(Hoje, "segunda");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------- Dias não conferidos ----------------

    [Fact]
    public async Task NaoConferidos_MostraODiaComDinheiroQueNinguemContou()
    {
        await EmDinheiroAsync(Hoje.AddDays(-2), 100m);
        await EmDinheiroAsync(Hoje.AddDays(-1), 200m);
        await _fechamento.ConferirAsync(Hoje.AddDays(-1), 200m);

        var pendentes = await _fechamento.NaoConferidosAsync(Hoje.AddDays(-7), Hoje);

        pendentes.Should().ContainSingle();
        pendentes[0].Data.Should().Be(Hoje.AddDays(-2));
        pendentes[0].Esperado.Should().Be(100m);
    }

    [Fact]
    public async Task NaoConferidos_IgnoraDiaSemDinheiroNaGaveta()
    {
        await _financeiro.LancarAsync(Hoje, TipoLancamento.Entrada, "Cartão", 400m,
            StatusLancamento.Realizado, FormaPagamento.CartaoCredito);

        // Cobrar conferência de um dia em que ninguém pagou em dinheiro treinaria a
        // clínica a fechar no automático — o hábito que a conferência existe para evitar.
        (await _fechamento.NaoConferidosAsync(Hoje.AddDays(-7), Hoje)).Should().BeEmpty();
    }

    [Fact]
    public async Task NaoConferidos_DiaReaberto_VoltaAPendencia()
    {
        await EmDinheiroAsync(Hoje, 300m);
        await _fechamento.ConferirAsync(Hoje, 300m);
        (await _fechamento.NaoConferidosAsync(Hoje, Hoje)).Should().BeEmpty();

        await _fechamento.ReabrirAsync(Hoje, "recontar");

        (await _fechamento.NaoConferidosAsync(Hoje, Hoje)).Should().ContainSingle();
    }

    [Fact]
    public async Task NaoConferidos_DesconstaSaidaNoEsperado()
    {
        await EmDinheiroAsync(Hoje, 400m);
        await EmDinheiroAsync(Hoje, 150m, TipoLancamento.Saida);

        var pendentes = await _fechamento.NaoConferidosAsync(Hoje, Hoje);

        pendentes.Should().ContainSingle();
        pendentes[0].Esperado.Should().Be(250m);
        pendentes[0].QuantidadeLancamentos.Should().Be(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
