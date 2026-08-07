using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Caixa e, principalmente, a conversa com o faturamento: guia efetivada no convênio
/// que ainda não virou dinheiro precisa aparecer na conciliação — e sumir dela assim
/// que o lançamento é criado.
/// </summary>
public class FinanceiroServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly AtendimentoService _atendimentos;

    public FinanceiroServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _atendimentos = new AtendimentoService(_repo);
    }

    private async Task<Paciente> CriarPacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Maria Souza",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    // ---------- Caixa ----------

    [Fact]
    public async Task Lancamento_ComValorZeroOuNegativo_EhRejeitado()
    {
        var acao = () => _financeiro.LancarAsync(
            new DateOnly(2026, 7, 1), TipoLancamento.Entrada, "Consulta particular", 0);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LancamentoRealizado_RecebeDataDePagamentoAutomatica()
    {
        var data = new DateOnly(2026, 7, 10);

        var lancamento = await _financeiro.LancarAsync(
            data, TipoLancamento.Entrada, "Consulta particular", 150m);

        lancamento.Status.Should().Be(StatusLancamento.Realizado);
        lancamento.DataPagamento.Should().Be(data);
    }

    [Fact]
    public async Task LancamentoPrevisto_NaoRecebeDataDePagamento()
    {
        var lancamento = await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 10), TipoLancamento.Saida, "Aluguel", 2000m,
            status: StatusLancamento.Previsto);

        lancamento.DataPagamento.Should().BeNull();
    }

    [Fact]
    public async Task Resumo_SeparaRealizadoDePrevisto_EIgnoraCancelado()
    {
        var inicio = new DateOnly(2026, 7, 1);
        var fim = new DateOnly(2026, 7, 31);

        await _financeiro.LancarAsync(inicio, TipoLancamento.Entrada, "Particular", 300m);
        await _financeiro.LancarAsync(inicio, TipoLancamento.Saida, "Agulhas", 100m);
        await _financeiro.LancarAsync(inicio, TipoLancamento.Entrada, "Convênio a receber", 500m,
            status: StatusLancamento.Previsto);
        var cancelado = await _financeiro.LancarAsync(inicio, TipoLancamento.Entrada, "Erro", 999m);
        await _financeiro.CancelarAsync(cancelado.Id, "lançado em duplicidade");

        var resumo = await _financeiro.ResumoAsync(inicio, fim);

        resumo.EntradasRealizadas.Should().Be(300m);
        resumo.SaidasRealizadas.Should().Be(100m);
        resumo.SaldoRealizado.Should().Be(200m);
        resumo.EntradasPrevistas.Should().Be(500m);
        resumo.SaldoPrevisto.Should().Be(700m);
    }

    [Fact]
    public async Task Resumo_UsaAProjecaoENaoDependeDoLimiteDaLista()
    {
        // A tela do caixa corta a LISTA no banco; os totais têm que continuar sendo do
        // período inteiro, senão o saldo mentiria assim que o mês passasse do limite.
        var inicio = new DateOnly(2026, 7, 1);
        var fim = new DateOnly(2026, 7, 31);

        for (var i = 0; i < 5; i++)
            await _financeiro.LancarAsync(inicio, TipoLancamento.Entrada, $"Sessão {i}", 100m);

        var listaCurta = await _financeiro.DoPeriodoAsync(inicio, fim, limite: 2);
        var resumo = await _financeiro.ResumoAsync(inicio, fim);

        listaCurta.Should().HaveCount(2);
        resumo.EntradasRealizadas.Should().Be(500m, "o total é do mês, não da página");
    }

    [Fact]
    public async Task DoPeriodo_SemLimite_TrazTudo()
    {
        var inicio = new DateOnly(2026, 7, 1);
        var fim = new DateOnly(2026, 7, 31);

        for (var i = 0; i < 3; i++)
            await _financeiro.LancarAsync(inicio, TipoLancamento.Entrada, $"Sessão {i}", 100m);

        (await _financeiro.DoPeriodoAsync(inicio, fim)).Should().HaveCount(3);
    }

    [Fact]
    public async Task DoPeriodo_ComLimite_TrazOsMaisRecentesPrimeiro()
    {
        var inicio = new DateOnly(2026, 7, 1);

        await _financeiro.LancarAsync(inicio, TipoLancamento.Entrada, "Mais antigo", 100m);
        await _financeiro.LancarAsync(inicio.AddDays(10), TipoLancamento.Entrada, "Do meio", 100m);
        await _financeiro.LancarAsync(inicio.AddDays(20), TipoLancamento.Entrada, "Mais recente", 100m);

        var lista = await _financeiro.DoPeriodoAsync(inicio, inicio.AddDays(30), limite: 1);

        // Cortar tem que preservar a ordem da tela (o mais novo no topo), senão o corte
        // esconderia justamente o que a secretária acabou de lançar.
        lista.Should().ContainSingle();
        lista[0].Descricao.Should().Be("Mais recente");
    }

    [Fact]
    public async Task Cancelamento_PreservaHistoricoEExigeMotivo()
    {
        var lancamento = await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 5), TipoLancamento.Entrada, "Particular", 200m);

        var semMotivo = () => _financeiro.CancelarAsync(lancamento.Id, "  ");
        await semMotivo.Should().ThrowAsync<ArgumentException>();

        await _financeiro.CancelarAsync(lancamento.Id, "paciente desistiu");

        var salvo = await _repo.ObterLancamentoAsync(lancamento.Id);
        salvo!.Status.Should().Be(StatusLancamento.Cancelado);
        salvo.Observacoes.Should().Contain("paciente desistiu");
    }

    [Fact]
    public async Task Realizar_NaoAceitaLancamentoJaRealizadoNemCancelado()
    {
        var realizado = await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 5), TipoLancamento.Entrada, "Particular", 200m);

        var jaRealizado = () => _financeiro.RealizarAsync(realizado.Id);
        await jaRealizado.Should().ThrowAsync<InvalidOperationException>();

        var previsto = await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 6), TipoLancamento.Entrada, "Convênio", 300m,
            status: StatusLancamento.Previsto);
        await _financeiro.CancelarAsync(previsto.Id, "guia glosada");

        var cancelado = () => _financeiro.RealizarAsync(previsto.Id);
        await cancelado.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LancamentoGravaAuditoria()
    {
        await _financeiro.LancarAsync(
            new DateOnly(2026, 7, 5), TipoLancamento.Entrada, "Particular", 200m);

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().Contain(e => e.Acao == "LancamentoCriado");
    }

    // ---------- Conciliação com o faturamento ----------

    [Fact]
    public async Task GuiaBaixada_SemLancamento_ApareceNaConciliacao()
    {
        var paciente = await CriarPacienteAsync();
        var data = new DateOnly(2026, 7, 10);
        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        // A secretária efetiva a guia no convênio.
        var codigo = resultado.Atendimento.Codigos.First();
        codigo.DataBaixa = data;
        codigo.NumeroGuiaReal = "90123";
        await _db.SaveChangesAsync();

        var pendentes = await _financeiro.GuiasSemLancamentoAsync(data, data);

        pendentes.Should().ContainSingle(g => g.CodigoId == codigo.Id);
        pendentes.First().Paciente.Should().Be("Maria Souza");
        pendentes.First().NumeroGuiaReal.Should().Be("90123");
    }

    [Fact]
    public async Task GuiaNaoBaixada_NaoApareceNaConciliacao()
    {
        var paciente = await CriarPacienteAsync();
        var data = new DateOnly(2026, 7, 10);
        await _atendimentos.LancarAsync(paciente.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        var pendentes = await _financeiro.GuiasSemLancamentoAsync(data, data);

        pendentes.Should().BeEmpty();
    }

    [Fact]
    public async Task AoLancarReceitaDaGuia_ElaSaiDaConciliacao()
    {
        var paciente = await CriarPacienteAsync();
        var data = new DateOnly(2026, 7, 10);
        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        var codigo = resultado.Atendimento.Codigos.First();
        codigo.DataBaixa = data;
        await _db.SaveChangesAsync();

        var guia = (await _financeiro.GuiasSemLancamentoAsync(data, data)).Single();
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(guia, 80m);

        // O vínculo com o faturamento fica gravado no lançamento...
        lancamento.CodigoFaturamentoId.Should().Be(codigo.Id);
        lancamento.AtendimentoId.Should().Be(resultado.Atendimento.Id);
        lancamento.PacienteId.Should().Be(paciente.Id);
        lancamento.Tipo.Should().Be(TipoLancamento.Entrada);
        lancamento.FormaPagamento.Should().Be(FormaPagamento.Convenio);

        // ...e a guia não é mais cobrada na conciliação.
        var depois = await _financeiro.GuiasSemLancamentoAsync(data, data);
        depois.Should().BeEmpty();
    }

    [Fact]
    public async Task LancamentoCancelado_DevolveGuiaParaAConciliacao()
    {
        var paciente = await CriarPacienteAsync();
        var data = new DateOnly(2026, 7, 10);
        var resultado = await _atendimentos.LancarAsync(
            paciente.Id, data, ModalidadeAtendimento.AcupunturaComEletro);

        var codigo = resultado.Atendimento.Codigos.First();
        codigo.DataBaixa = data;
        await _db.SaveChangesAsync();

        var guia = (await _financeiro.GuiasSemLancamentoAsync(data, data)).Single();
        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(guia, 80m);
        await _financeiro.CancelarAsync(lancamento.Id, "valor errado");

        // Cancelado não conta como conciliado — a guia volta a cobrar lançamento.
        var depois = await _financeiro.GuiasSemLancamentoAsync(data, data);
        depois.Should().ContainSingle(g => g.CodigoId == codigo.Id);
    }

    // ---------- Categorias (plano de contas) ----------

    [Fact]
    public async Task Categoria_NaoAceitaCodigoDuplicado()
    {
        await _financeiro.CriarCategoriaAsync("PARTICULAR", "Consulta particular", TipoLancamento.Entrada);

        var duplicada = () => _financeiro.CriarCategoriaAsync(
            "particular", "Outra", TipoLancamento.Entrada);

        await duplicada.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CategoriasDoTipo_SoTrazemAsDoTipoEAsAtivas_NaOrdemDeExibicao()
    {
        // O combo do lançamento não pode oferecer categoria de despesa numa entrada.
        await _financeiro.CriarCategoriaAsync("ALUGUEL", "Aluguel", TipoLancamento.Saida, ordem: 2);
        await _financeiro.CriarCategoriaAsync("MATERIAL", "Material", TipoLancamento.Saida, ordem: 1);
        await _financeiro.CriarCategoriaAsync("PARTICULAR", "Consulta particular", TipoLancamento.Entrada);

        var inativa = await _financeiro.CriarCategoriaAsync("ANTIGA", "Categoria antiga", TipoLancamento.Saida);
        inativa.Ativa = false;
        await _repo.SalvarAsync();

        var saidas = await _financeiro.CategoriasAsync(TipoLancamento.Saida);

        saidas.Select(c => c.Nome).Should().Equal("Material", "Aluguel"); // pela Ordem, não pelo nome
        saidas.Should().NotContain(c => c.Codigo == "ANTIGA");

        var entradas = await _financeiro.CategoriasAsync(TipoLancamento.Entrada);
        entradas.Should().ContainSingle().Which.Nome.Should().Be("Consulta particular");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
