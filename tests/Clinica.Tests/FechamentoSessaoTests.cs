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
/// Fechamento da sessão: a ponte Recepção → Financeiro.
///
/// Estes testes existem por causa de um buraco concreto: <c>PacoteService</c> e
/// <c>EstoqueService</c> estavam prontos e testados, e **ninguém os chamava**. Concluir a
/// sessão na fila gerava a guia e mais nada — o pacote do paciente não baixava, o insumo
/// não saía do estoque e o dinheiro não entrava no caixa. Serviço testado sem chamador é
/// código que passa no CI e não faz nada na clínica.
///
/// A regra que carrega a suíte: **a conclusão do atendimento é o que não pode falhar**.
/// As outras três partes viram AVISO quando falham, nunca desfazem a guia — a sessão
/// aconteceu de verdade, o paciente foi embora, e o faturamento precisa dela.
/// </summary>
public class FechamentoSessaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly PacoteService _pacotes;
    private readonly EstoqueService _estoque;
    private readonly FinanceiroService _financeiro;
    private readonly FechamentoSessaoService _fechamento;

    private static readonly DateTime Sessao = new(2026, 8, 20, 14, 0, 0);
    private static DateOnly Dia => DateOnly.FromDateTime(Sessao);

    public FechamentoSessaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _pacotes = new PacoteService(_repo);
        _estoque = new EstoqueService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _fechamento = new FechamentoSessaoService(_repo, _agenda, _pacotes, _estoque, _financeiro);
    }

    // ==================== Cenário ====================

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> AgendarAsync(int pacienteId)
    {
        var ag = await _agenda.AgendarAsync(
            pacienteId, Sessao, ModalidadeAtendimento.AcupunturaComEletro, null);
        return ag.Id;
    }

    private async Task<PacotePaciente> VenderPacoteAsync(
        int pacienteId, int sessoes = 10, DateOnly? validoAte = null)
        => await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = pacienteId,
            Nome = $"Pacote {sessoes} sessões",
            Tipo = TipoPacote.Sessoes,
            SessoesContratadas = sessoes,
            Valor = 1000m,
            DataCompra = Dia.AddDays(-10),
            ValidoAte = validoAte
        });

    private async Task<ItemEstoque> CriarItemComSaldoAsync(
        string nome, decimal entrada, string unidade = "un")
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque { Nome = nome, Unidade = unidade });
        await _estoque.EntrarAsync(item.Id, entrada, custoUnitario: 1m, data: Dia.AddDays(-5));
        return item;
    }

    // ==================== O buraco que este serviço fecha ====================

    [Fact]
    public async Task Concluir_DebitaOPacoteDoPaciente()
    {
        var pacienteId = await CriarPacienteAsync();
        var pacote = await VenderPacoteAsync(pacienteId);
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId), operador: "recepcao");

        resultado.Consumo.Should().NotBeNull();
        resultado.Avisos.Should().BeEmpty();

        var saldos = await _pacotes.DoPacienteAsync(pacienteId, Dia);
        saldos.Single(s => s.PacoteId == pacote.Id).SaldoSessoes.Should().Be(9);
    }

    [Fact]
    public async Task Concluir_BaixaOsInsumosEDaCustoAoAtendimento()
    {
        var pacienteId = await CriarPacienteAsync();
        var agulha = await CriarItemComSaldoAsync("Agulha", 100m);
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId, Insumos: [new InsumoAConsumir(agulha.Id, 12m)]));

        resultado.Movimentos.Should().ContainSingle();
        resultado.Avisos.Should().BeEmpty();

        var saldos = await _estoque.SaldosAsync();
        saldos.Single(s => s.ItemId == agulha.Id).Saldo.Should().Be(88m);

        // O vínculo com o atendimento é o que dá custo à sessão — sem ele o movimento
        // seria uma saída solta, e o custo por atendimento voltaria a ser zero.
        var custo = await _estoque.CustoDoAtendimentoAsync(resultado.Atendimento.Id);
        custo.Custo.Should().Be(12m);
    }

    [Fact]
    public async Task Concluir_LancaAEntradaNoCaixaLigadaAoAtendimento()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(
            agendamentoId,
            GerarLancamento: true,
            Valor: 150m,
            Forma: FormaPagamento.Pix));

        resultado.Lancamento.Should().NotBeNull();
        resultado.Lancamento!.AtendimentoId.Should().Be(resultado.Atendimento.Id);
        resultado.Lancamento.PacienteId.Should().Be(pacienteId);
        resultado.Lancamento.Tipo.Should().Be(TipoLancamento.Entrada);

        var resumo = await _financeiro.ResumoAsync(Dia, Dia);
        resumo.EntradasRealizadas.Should().Be(150m);
    }

    [Fact]
    public async Task Concluir_SemMarcarNada_GeraSoAGuiaEDeixaOPacoteIntacto()
    {
        var pacienteId = await CriarPacienteAsync();
        var pacote = await VenderPacoteAsync(pacienteId);
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId, DebitarPacote: false));

        resultado.Atendimento.Codigos.Should().NotBeEmpty();
        resultado.Consumo.Should().BeNull();
        resultado.Lancamento.Should().BeNull();
        resultado.Movimentos.Should().BeEmpty();

        var saldos = await _pacotes.DoPacienteAsync(pacienteId, Dia);
        saldos.Single(s => s.PacoteId == pacote.Id).SaldoSessoes.Should().Be(10);
    }

    // ==================== Falha parcial ====================

    [Fact]
    public async Task Concluir_ComInsumoSemSaldo_MantemAGuiaEDevolveAviso()
    {
        var pacienteId = await CriarPacienteAsync();
        var agulha = await CriarItemComSaldoAsync("Agulha", 5m);
        var agendamentoId = await AgendarAsync(pacienteId);

        // 20 de um item que só tem 5: o estoque recusa (negativo não existe no mundo).
        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId, Insumos: [new InsumoAConsumir(agulha.Id, 20m)]));

        // O atendimento aconteceu de verdade: desfazê-lo porque uma agulha não bateu
        // deixaria a clínica sem a guia de uma sessão que o paciente recebeu.
        resultado.Atendimento.Id.Should().BeGreaterThan(0);
        resultado.Atendimento.Codigos.Should().NotBeEmpty();

        resultado.TudoCerto.Should().BeFalse();
        resultado.Avisos.Should().ContainSingle().Which.Should().Contain("NÃO baixou");

        (await _estoque.SaldosAsync()).Single(s => s.ItemId == agulha.Id).Saldo.Should().Be(5m);
    }

    [Fact]
    public async Task Concluir_ComLancamentoMarcadoESemValor_MantemAGuiaEAvisa()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamentoId = await AgendarAsync(pacienteId);

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId, GerarLancamento: true, Valor: null));

        resultado.Atendimento.Codigos.Should().NotBeEmpty();
        resultado.Lancamento.Should().BeNull();
        resultado.Avisos.Should().ContainSingle().Which.Should().Contain("NÃO entrou no caixa");
    }

    [Fact]
    public async Task Concluir_DuasVezes_NaoConcluiDeNovo()
    {
        var pacienteId = await CriarPacienteAsync();
        await VenderPacoteAsync(pacienteId);
        var agendamentoId = await AgendarAsync(pacienteId);

        await _fechamento.ConcluirAsync(new DecisaoFechamento(agendamentoId));

        // A segunda tentativa morre no ConfirmarPresencaAsync — antes de debitar
        // qualquer coisa. Uma sessão, uma guia, um débito.
        var repetir = () => _fechamento.ConcluirAsync(new DecisaoFechamento(agendamentoId));
        await repetir.Should().ThrowAsync<InvalidOperationException>();

        var saldos = await _pacotes.DoPacienteAsync(pacienteId, Dia);
        saldos.Single().SaldoSessoes.Should().Be(9);
    }

    // ==================== A proposta ====================

    [Fact]
    public async Task Preparar_EscolheOPacoteQueVencePrimeiro()
    {
        var pacienteId = await CriarPacienteAsync();
        var longe = await VenderPacoteAsync(pacienteId, validoAte: Dia.AddDays(90));
        var perto = await VenderPacoteAsync(pacienteId, validoAte: Dia.AddDays(5));
        var agendamentoId = await AgendarAsync(pacienteId);

        var proposta = await _fechamento.PrepararAsync(agendamentoId);

        // A tela precisa prometer o MESMO pacote que o serviço vai debitar; senão a
        // recepção confirma um saldo e outro baixa.
        proposta.PacoteADebitar!.PacoteId.Should().Be(perto.Id);
        proposta.PacoteADebitar.PacoteId.Should().NotBe(longe.Id);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(agendamentoId));
        resultado.Consumo!.PacotePacienteId.Should().Be(perto.Id);
    }

    [Fact]
    public async Task Preparar_ComPacote_NaoSugereCobrarDeNovo()
    {
        var pacienteId = await CriarPacienteAsync();
        await VenderPacoteAsync(pacienteId);
        await _financeiro.LancarAsync(
            Dia.AddDays(-7), TipoLancamento.Entrada, "Sessão anterior", 150m,
            pacienteId: pacienteId, formaPagamento: FormaPagamento.Pix);
        var agendamentoId = await AgendarAsync(pacienteId);

        var proposta = await _fechamento.PrepararAsync(agendamentoId);

        // Sessão comprada no pacote já foi paga: sugerir a cobrança seria propor cobrar
        // duas vezes pela mesma sessão.
        proposta.TemPacote.Should().BeTrue();
        proposta.SugereLancamento.Should().BeFalse();
    }

    [Fact]
    public async Task Preparar_SugereOValorDaUltimaEntradaComProcedencia()
    {
        var pacienteId = await CriarPacienteAsync();
        await _financeiro.LancarAsync(
            Dia.AddDays(-30), TipoLancamento.Entrada, "Sessão antiga", 100m,
            pacienteId: pacienteId, formaPagamento: FormaPagamento.Dinheiro);
        await _financeiro.LancarAsync(
            Dia.AddDays(-7), TipoLancamento.Entrada, "Sessão recente", 150m,
            pacienteId: pacienteId, formaPagamento: FormaPagamento.Pix);
        var agendamentoId = await AgendarAsync(pacienteId);

        var proposta = await _fechamento.PrepararAsync(agendamentoId);

        proposta.ValorSugerido.Should().Be(150m);
        proposta.FormaSugerida.Should().Be(FormaPagamento.Pix);
        // A procedência vai junto de propósito: número sem origem é número que a
        // recepção confirma sem saber de onde veio.
        proposta.ProcedenciaDoValor.Should().Contain(Dia.AddDays(-7).ToString("dd/MM/yyyy"));
        proposta.SugereLancamento.Should().BeTrue();
    }

    [Fact]
    public async Task Preparar_SemHistorico_NaoInventaValor()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamentoId = await AgendarAsync(pacienteId);

        var proposta = await _fechamento.PrepararAsync(agendamentoId);

        proposta.ValorSugerido.Should().BeNull();
        proposta.SugereLancamento.Should().BeFalse();
    }

    [Fact]
    public async Task Preparar_NaoContaPrevistoComoRecebido()
    {
        var pacienteId = await CriarPacienteAsync();
        await _financeiro.LancarAsync(
            Dia.AddDays(-2), TipoLancamento.Entrada, "A receber", 300m,
            status: StatusLancamento.Previsto, pacienteId: pacienteId);
        var agendamentoId = await AgendarAsync(pacienteId);

        var proposta = await _fechamento.PrepararAsync(agendamentoId);

        // Previsto é dinheiro que ainda não chegou: sugerir o valor dele seria repetir
        // no balcão uma cobrança que a clínica ainda nem recebeu.
        proposta.ValorSugerido.Should().BeNull();
    }

    [Fact]
    public async Task Preparar_SugereOsInsumosDaUltimaSessao()
    {
        var pacienteId = await CriarPacienteAsync();
        var agulha = await CriarItemComSaldoAsync("Agulha", 100m);
        var moxa = await CriarItemComSaldoAsync("Moxa", 50m);

        // Uma sessão anterior, já fechada com consumo.
        var anterior = await AgendarAsync(pacienteId);
        await _fechamento.ConcluirAsync(new DecisaoFechamento(anterior, Insumos:
        [
            new InsumoAConsumir(agulha.Id, 10m),
            new InsumoAConsumir(moxa.Id, 2m)
        ]));

        var pacienteId2 = await CriarPacienteAsync("João");
        var proximo = await AgendarAsync(pacienteId2);

        var proposta = await _fechamento.PrepararAsync(proximo);

        proposta.Insumos.Should().HaveCount(2);
        proposta.Insumos.Single(i => i.ItemId == agulha.Id).Quantidade.Should().Be(10m);
        proposta.Insumos.Single(i => i.ItemId == moxa.Id).Quantidade.Should().Be(2m);
        proposta.Insumos.Should().OnlyContain(i => !i.SemSaldo);
    }

    [Fact]
    public async Task Preparar_SemConsumoAnterior_NaoSugereInsumo()
    {
        var pacienteId = await CriarPacienteAsync();
        await CriarItemComSaldoAsync("Agulha", 100m);
        var agendamentoId = await AgendarAsync(pacienteId);

        var proposta = await _fechamento.PrepararAsync(agendamentoId);

        // Entrada de estoque não é consumo de sessão: sugerir a compra inteira como
        // gasto do atendimento zeraria o estoque no primeiro fechamento.
        proposta.Insumos.Should().BeEmpty();
    }

    [Fact]
    public async Task Preparar_AgendamentoJaRealizado_Recusa()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamentoId = await AgendarAsync(pacienteId);
        await _fechamento.ConcluirAsync(new DecisaoFechamento(agendamentoId));

        var preparar = () => _fechamento.PrepararAsync(agendamentoId);
        await preparar.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
