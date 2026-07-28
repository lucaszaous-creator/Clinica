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
/// Repasse por profissional, estoque e os documentos financeiros (parcela 4).
///
/// Duas regras que não são óbvias no código:
/// 1. quem atendeu vem do AGENDAMENTO — o atendimento é entidade do faturamento
///    congelado e não guarda profissional;
/// 2. o repasse incide sobre a receita que ENTROU, não sobre o que foi faturado:
///    pagar percentual de dinheiro não recebido descapitaliza a clínica.
/// </summary>
public class RepasseEstoqueTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;
    private readonly RepasseService _repasses;
    private readonly EstoqueService _estoque;
    private readonly DocumentoFinanceiroService _documentos;

    private static readonly DateOnly Inicio = new(2026, 8, 1);
    private static readonly DateOnly Fim = new(2026, 8, 31);

    public RepasseEstoqueTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _financeiro = new FinanceiroService(_repo);
        _repasses = new RepasseService(_repo);
        _estoque = new EstoqueService(_repo);
        _documentos = new DocumentoFinanceiroService(_repo);
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> CriarProfissionalAsync(string nome = "Dra. Ana")
    {
        var p = new Profissional { Nome = nome, RegistroConselho = "CRM-SP 1234" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Sessão realizada: atendimento + o agendamento que aponta para ele.</summary>
    private async Task<int> CriarSessaoAsync(int pacienteId, int profissionalId, DateOnly dia)
    {
        var atendimento = new Atendimento { PacienteId = pacienteId, Data = dia };
        _db.Atendimentos.Add(atendimento);
        await _db.SaveChangesAsync();

        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            AtendimentoId = atendimento.Id,
            DataHora = dia.ToDateTime(new TimeOnly(14, 0)),
            Status = StatusAgendamento.Realizado
        });
        await _db.SaveChangesAsync();

        return atendimento.Id;
    }

    // ==================== Repasse ====================

    [Fact]
    public async Task Percentual_incide_so_sobre_a_receita_que_entrou()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        var recebido = await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(4));
        var naoRecebido = await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(5));

        await _financeiro.LancarAsync(
            Inicio.AddDays(4), TipoLancamento.Entrada, "sessão paga", 200m,
            StatusLancamento.Realizado, atendimentoId: recebido);

        // Cancelado não entra na base: o dinheiro não existe.
        var cancelado = await _financeiro.LancarAsync(
            Inicio.AddDays(5), TipoLancamento.Entrada, "sessão estornada", 200m,
            StatusLancamento.Realizado, atendimentoId: naoRecebido);
        await _financeiro.CancelarAsync(cancelado.Id, "estorno");

        await _repasses.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissionalId,
            Base = BaseRepasse.PercentualDaReceita,
            Percentual = 40m,
            VigenteDe = Inicio,
            Ativa = true
        }, "gerente");

        var calculado = (await _repasses.CalcularAsync(Inicio, Fim)).Single();

        calculado.Atendimentos.Should().Be(2);
        calculado.ReceitaVinculada.Should().Be(200m);
        calculado.Valor.Should().Be(80m);
    }

    [Fact]
    public async Task Valor_por_atendimento_ignora_a_receita()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();

        await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(1));
        await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(2));
        await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(3));

        await _repasses.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissionalId,
            Base = BaseRepasse.ValorPorAtendimento,
            ValorPorAtendimento = 50m,
            Ativa = true
        });

        var calculado = (await _repasses.CalcularAsync(Inicio, Fim)).Single();

        calculado.ReceitaVinculada.Should().Be(0m);
        calculado.Valor.Should().Be(150m);
    }

    [Fact]
    public async Task Profissional_sem_regra_aparece_na_lista_dizendo_que_falta_cadastrar()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(1));

        var calculado = (await _repasses.CalcularAsync(Inicio, Fim)).Single();

        // Sumir com ele esconderia justamente o cadastro que falta fazer.
        calculado.TemRegra.Should().BeFalse();
        calculado.Valor.Should().Be(0m);
        calculado.Situacao.Should().Contain("Sem regra");

        var apurar = () => _repasses.ApurarAsync(profissionalId, Inicio, Fim);
        await apurar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cadastre a regra*");
    }

    [Fact]
    public async Task Apurar_cria_saida_prevista_no_caixa_e_trava_o_periodo()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var atendimentoId = await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(4));

        await _financeiro.LancarAsync(
            Inicio.AddDays(4), TipoLancamento.Entrada, "sessão paga", 300m,
            StatusLancamento.Realizado, atendimentoId: atendimentoId);

        await _repasses.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissionalId,
            Base = BaseRepasse.PercentualDaReceita,
            Percentual = 50m,
            Ativa = true
        });

        var apurado = await _repasses.ApurarAsync(profissionalId, Inicio, Fim, operador: "gerente");

        apurado.Valor.Should().Be(150m);
        apurado.LancamentoFinanceiroId.Should().NotBeNull();

        var lancamento = await _repo.ObterLancamentoAsync(apurado.LancamentoFinanceiroId!.Value);
        lancamento!.Tipo.Should().Be(TipoLancamento.Saida);
        lancamento.Status.Should().Be(StatusLancamento.Previsto);
        lancamento.Valor.Should().Be(150m);

        // Repasse pago duas vezes é dinheiro que não volta.
        var deNovo = () => _repasses.ApurarAsync(profissionalId, Inicio, Fim);
        await deNovo.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Já existe repasse*");

        // E um período que ENCOSTA no já apurado também é recusado.
        var sobreposto = () => _repasses.ApurarAsync(profissionalId, Inicio.AddDays(15), Fim.AddDays(15));
        await sobreposto.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancelar_a_apuracao_cancela_a_saida_no_caixa_junto()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var atendimentoId = await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(4));

        await _financeiro.LancarAsync(
            Inicio.AddDays(4), TipoLancamento.Entrada, "sessão paga", 300m,
            StatusLancamento.Realizado, atendimentoId: atendimentoId);

        await _repasses.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissionalId,
            Base = BaseRepasse.PercentualDaReceita,
            Percentual = 50m,
            Ativa = true
        });

        var apurado = await _repasses.ApurarAsync(profissionalId, Inicio, Fim);
        await _repasses.CancelarApuracaoAsync(apurado.Id, "erro no fechamento", "gerente");

        var lancamento = await _repo.ObterLancamentoAsync(apurado.LancamentoFinanceiroId!.Value);
        lancamento!.Status.Should().Be(StatusLancamento.Cancelado);

        // Cancelada a apuração, o período volta a poder ser apurado.
        var novo = await _repasses.ApurarAsync(profissionalId, Inicio, Fim);
        novo.Valor.Should().Be(150m);
    }

    [Fact]
    public async Task Regra_fora_de_vigencia_nao_e_aplicada()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(4));

        await _repasses.SalvarRegraAsync(new RegraRepasse
        {
            ProfissionalId = profissionalId,
            Base = BaseRepasse.ValorPorAtendimento,
            ValorPorAtendimento = 50m,
            VigenteAte = Inicio.AddDays(-1),
            Ativa = true
        });

        (await _repasses.CalcularAsync(Inicio, Fim)).Single().TemRegra.Should().BeFalse();
    }

    // ==================== Estoque ====================

    private Task<ItemEstoque> CriarItemAsync(string nome = "Agulha 0,25", decimal minimo = 100m)
        => _estoque.SalvarItemAsync(new ItemEstoque
        {
            Nome = nome,
            Unidade = "un",
            EstoqueMinimo = minimo,
            Ativo = true
        }, "gerente");

    [Fact]
    public async Task Saldo_e_a_soma_dos_movimentos()
    {
        var item = await CriarItemAsync();

        await _estoque.EntrarAsync(item.Id, 500m, custoUnitario: 0.20m, data: Inicio);
        await _estoque.BaixarAsync(item.Id, 30m, data: Inicio.AddDays(1));
        await _estoque.PerderAsync(item.Id, 20m, "caixa molhada", Inicio.AddDays(2));

        var saldo = (await _estoque.SaldosAsync()).Single();
        saldo.Saldo.Should().Be(450m);
        saldo.CustoMedio.Should().Be(0.20m);
        saldo.AbaixoDoMinimo.Should().BeFalse();
    }

    [Fact]
    public async Task Baixa_maior_que_o_saldo_e_recusada()
    {
        var item = await CriarItemAsync();
        await _estoque.EntrarAsync(item.Id, 10m, data: Inicio);

        var baixar = () => _estoque.BaixarAsync(item.Id, 11m, data: Inicio);

        // Estoque negativo não existe no mundo: aceitar o número esconderia o erro
        // de contagem em vez de mostrá-lo.
        await baixar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*não dá para baixar*");
    }

    [Fact]
    public async Task Perda_sem_motivo_e_recusada()
    {
        var item = await CriarItemAsync();
        await _estoque.EntrarAsync(item.Id, 10m, data: Inicio);

        var perder = () => _estoque.PerderAsync(item.Id, 1m, "   ", Inicio);
        await perder.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Item_abaixo_do_minimo_entra_no_alerta_de_reposicao()
    {
        var item = await CriarItemAsync(minimo: 100m);
        await _estoque.EntrarAsync(item.Id, 120m, data: Inicio);
        await _estoque.BaixarAsync(item.Id, 30m, data: Inicio.AddDays(1));

        var alertas = await _estoque.AbaixoDoMinimoAsync();

        alertas.Should().ContainSingle();
        alertas.Single().Saldo.Should().Be(90m);
    }

    [Fact]
    public async Task Validade_alerta_o_lote_e_ignora_item_zerado()
    {
        var comSaldo = await CriarItemAsync("Moxa", minimo: 0m);
        var zerado = await CriarItemAsync("Ventosa", minimo: 0m);

        await _estoque.EntrarAsync(comSaldo.Id, 10m, validade: Inicio.AddDays(20), data: Inicio, lote: "L1");
        await _estoque.EntrarAsync(zerado.Id, 5m, validade: Inicio.AddDays(10), data: Inicio, lote: "L2");
        await _estoque.BaixarAsync(zerado.Id, 5m, data: Inicio);

        var validades = await _estoque.ValidadesAsync(Inicio, dias: 60);

        // Alertar sobre lote de item zerado seria barulho: não há o que descartar.
        validades.Should().ContainSingle();
        validades.Single().Lote.Should().Be("L1");
        validades.Single().Vencido(Inicio).Should().BeFalse();
    }

    [Fact]
    public async Task Custo_do_atendimento_soma_o_que_a_sessao_consumiu()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissionalId = await CriarProfissionalAsync();
        var atendimentoId = await CriarSessaoAsync(pacienteId, profissionalId, Inicio.AddDays(3));

        var agulha = await CriarItemAsync("Agulha", minimo: 0m);
        var moxa = await CriarItemAsync("Moxa", minimo: 0m);

        await _estoque.EntrarAsync(agulha.Id, 1000m, custoUnitario: 0.20m, data: Inicio);
        await _estoque.EntrarAsync(moxa.Id, 100m, custoUnitario: 3m, data: Inicio);

        await _estoque.BaixarAsync(agulha.Id, 12m, atendimentoId, pacienteId, Inicio.AddDays(3));
        await _estoque.BaixarAsync(moxa.Id, 2m, atendimentoId, pacienteId, Inicio.AddDays(3));

        var custo = await _estoque.CustoDoAtendimentoAsync(atendimentoId);

        custo.Custo.Should().Be(8.40m);
        custo.Itens.Should().HaveCount(2);
    }

    // ==================== Recibo e orçamento ====================

    [Fact]
    public async Task Recibo_e_orcamento_tem_sequencias_separadas()
    {
        var pacienteId = await CriarPacienteAsync();

        var lancamento = await _financeiro.LancarAsync(
            Inicio, TipoLancamento.Entrada, "sessão avulsa", 150m,
            StatusLancamento.Realizado, pacienteId: pacienteId);

        var recibo = await _documentos.EmitirReciboDoLancamentoAsync(lancamento.Id, operador: "secretaria");
        var orcamento = await _documentos.EmitirAsync(new DocumentoFinanceiro
        {
            Tipo = TipoDocumentoFinanceiro.Orcamento,
            PacienteId = pacienteId,
            Data = Inicio,
            Itens = [new ItemDocumentoFinanceiro { Descricao = "10 sessões", Quantidade = 1, ValorUnitario = 1000m }]
        }, "secretaria");

        recibo.Numero.Should().Be("REC 2026/0001");
        orcamento.Numero.Should().Be("ORC 2026/0001");
        recibo.Destinatario.Should().Be("Maria");
        recibo.ValorTotal.Should().Be(150m);
        // Orçamento nasce com validade mesmo quando ninguém informa.
        orcamento.ValidoAte.Should().Be(Inicio.AddDays(DocumentoFinanceiroService.ValidadePadraoOrcamentoDias));
    }

    [Fact]
    public async Task Recibo_de_saida_e_recusado()
    {
        var lancamento = await _financeiro.LancarAsync(
            Inicio, TipoLancamento.Saida, "aluguel", 2000m, StatusLancamento.Realizado);

        var emitir = () => _documentos.EmitirReciboDoLancamentoAsync(lancamento.Id);

        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dinheiro que entrou*");
    }

    [Fact]
    public async Task Documento_financeiro_cancelado_continua_na_lista()
    {
        var pacienteId = await CriarPacienteAsync();
        var lancamento = await _financeiro.LancarAsync(
            Inicio, TipoLancamento.Entrada, "sessão avulsa", 150m,
            StatusLancamento.Realizado, pacienteId: pacienteId);

        var recibo = await _documentos.EmitirReciboDoLancamentoAsync(lancamento.Id);
        await _documentos.CancelarAsync(recibo.Id, "valor errado", "secretaria");

        var depois = await _documentos.ObterAsync(recibo.Id);
        depois!.Cancelado.Should().BeTrue();
        depois.MotivoCancelamento.Should().Be("valor errado");

        (await _documentos.DoPeriodoAsync(Inicio, Fim)).Should().ContainSingle();
    }

    [Fact]
    public async Task Documento_sem_nenhuma_linha_e_recusado()
    {
        var emitir = () => _documentos.EmitirAsync(new DocumentoFinanceiro
        {
            Tipo = TipoDocumentoFinanceiro.Orcamento,
            Destinatario = "Fulano",
            Data = Inicio
        });

        await emitir.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
