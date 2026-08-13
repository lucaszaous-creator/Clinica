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
/// A GUIA NASCE QUANDO O ATENDIMENTO É REGISTRADO (parcela 65).
///
/// O defeito que motivou a parcela
/// -------------------------------
/// A parcela 60 unificou a esteira e, ao unificá-la, pendurou a criação da guia na
/// CONFIRMAÇÃO da janela de fechamento: quem fechasse a janela ficava com um horário na
/// agenda, o paciente marcado como presente e <b>nenhuma guia</b>. Como o encaixe tinha
/// sido criado, a tela parecia ter funcionado pela metade.
///
/// Em 12/08/2026, em produção, a mesma sessão foi lançada <b>três vezes em 71 segundos</b>
/// (agendamentos 161, 162 e 163, todos com check-in carimbado e <c>AtendimentoId</c> nulo):
/// a recepcionista não via guia nenhuma e tentava de novo. Nenhuma guia foi gerada, e três
/// cartões nasceram na fila para uma sessão só.
///
/// A regra que a direção fixou, e que estes testes amarram
/// ------------------------------------------------------
/// <b>Atendimento que entra no sistema já gera guia e vai para o faturamento</b> — agendado
/// ou avulso, sem depender de mais nenhum clique. Pacote, insumo e caixa são o passo
/// SEGUINTE: eles não podem condicionar nem desfazer a guia.
///
/// É a mesma hierarquia que o <c>ConcluirAsync</c> já aplicava entre os quatro fatos
/// (só a conclusão do atendimento derruba a operação; os outros três viram aviso), agora
/// estendida ao MOMENTO em que cada um acontece.
/// </summary>
public class GuiaNoRegistroDoAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly FechamentoSessaoService _fechamento;
    private readonly PacoteService _pacotes;

    public GuiaNoRegistroDoAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _pacotes = new PacoteService(_repo);
        _fechamento = new FechamentoSessaoService(
            _repo, _agenda, _pacotes, new EstoqueService(_repo), new FinanceiroService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly DateTime Quando = new(2026, 8, 12, 16, 10, 0);

    private async Task<int> PacienteAsync(Convenio convenio = Convenio.UnimedIntercambio)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Marca o horário e faz o check-in — o estado em que os três encaixes do dia 12 pararam.</summary>
    private async Task<int> HorarioComCheckInAsync(int pacienteId, DateTime? quando = null)
    {
        var ag = await _agenda.AgendarAsync(
            pacienteId, quando ?? Quando, ModalidadeAtendimento.AcupunturaComEletro, null,
            encaixe: true, operador: "flavia@");
        await _agenda.RegistrarChegadaAsync(ag.Id);
        return ag.Id;
    }

    private async Task VenderPacoteAsync(int pacienteId)
    {
        var catalogo = new PacoteCatalogo
        {
            Nome = "10 sessões", SessoesIncluidas = 10, ValidadeDias = 180, Valor = 1000m, Ativo = true
        };
        _db.PacotesCatalogo.Add(catalogo);
        await _db.SaveChangesAsync();
        await _pacotes.VenderAsync(pacienteId, catalogo.Id, DateOnly.FromDateTime(Quando));
    }

    // ================================================================
    // O REGISTRO GERA A GUIA — SEM JANELA NENHUMA
    // ================================================================

    /// <summary>
    /// O teste central da parcela: registrar o atendimento basta para a guia existir e
    /// chegar ao faturamento. Nenhuma decisão de pacote/caixa foi tomada aqui.
    /// </summary>
    [Fact]
    public async Task Registrar_o_atendimento_gera_a_guia_sem_a_janela_de_fechamento()
    {
        var paciente = await PacienteAsync();
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");

        registro.Atendimento.Id.Should().BeGreaterThan(0);
        registro.Atendimento.Codigos.Should().NotBeEmpty("a guia é consequência imediata do registro");
        registro.GuiasGeradas.Should().Be(2, "acupuntura com eletro gera o 1º código e o 2º de +24h");

        // E o horário deixou de ser um encaixe pendurado: ele aponta para o atendimento.
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId);
        ag!.Status.Should().Be(StatusAgendamento.Realizado);
        ag.AtendimentoId.Should().Be(registro.Atendimento.Id);
    }

    /// <summary>
    /// A regressão do dia 12, escrita como teste: o que existia antes desta parcela era
    /// exatamente isto — check-in carimbado, <c>AtendimentoId</c> nulo e nenhuma guia. A
    /// consulta que a clínica rodou em produção é a asserção deste teste.
    /// </summary>
    [Fact]
    public async Task Sem_o_registro_nao_ha_guia_alguma_e_o_horario_fica_pendurado()
    {
        var paciente = await PacienteAsync();
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var ag = await _repo.ObterAgendamentoAsync(agendamentoId);
        ag!.AtendimentoId.Should().BeNull();
        ag.ChegadaEm.Should().NotBeNull();

        (await _repo.CodigosEmAbertoAsync()).Should().BeEmpty(
            "é o estado dos agendamentos 161/162/163 de 12/08/2026 — e é o que a parcela 65 impede");
    }

    /// <summary>
    /// A guia chega a quem fatura. Não basta a linha existir na tabela: ela tem de aparecer
    /// para o faturamento, que é o que a clínica olha.
    /// </summary>
    [Fact]
    public async Task A_guia_do_registro_chega_ao_faturamento()
    {
        var paciente = await PacienteAsync();
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");

        var pendencias = await new PendenciaService(_repo)
            .CodigosPendentesAsync(DateOnly.FromDateTime(Quando));

        pendencias.Should().NotBeEmpty("o 1º código já nasce pendente de baixa no dia do atendimento");
    }

    /// <summary>
    /// Fechar a janela de pacote/caixa não desfaz nem apaga a guia. É a inversão da
    /// hierarquia antiga, e a razão de a parcela existir.
    /// </summary>
    [Fact]
    public async Task Nao_concluir_o_pacote_e_o_caixa_nao_desfaz_a_guia()
    {
        var paciente = await PacienteAsync();
        await VenderPacoteAsync(paciente);
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");

        // A recepcionista fechou a janela: nenhum ConcluirAsync aconteceu.
        registro.GuiasGeradas.Should().Be(2);
        (await _repo.CodigosEmAbertoAsync()).Should().NotBeEmpty();

        // O que ficou pendente é só o pacote — e ele continua inteiro para ser debitado depois.
        (await _pacotes.DoPacienteAsync(paciente)).Single().SaldoSessoes.Should().Be(10);
    }

    // ================================================================
    // DUPLICIDADE
    // ================================================================

    /// <summary>
    /// Registrar duas vezes o MESMO horário devolve o mesmo atendimento. Sem isso, o duplo
    /// clique no Finalizar mandaria dois jogos de guias para a operadora — e guia duplicada
    /// só aparece semanas depois, no retorno.
    /// </summary>
    [Fact]
    public async Task Registrar_duas_vezes_o_mesmo_horario_nao_gera_segunda_guia()
    {
        var paciente = await PacienteAsync();
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var primeiro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");
        var segundo = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");

        segundo.JaExistia.Should().BeTrue();
        segundo.Atendimento.Id.Should().Be(primeiro.Atendimento.Id);

        (await _db.Atendimentos.CountAsync()).Should().Be(1);
        (await _db.Codigos.CountAsync()).Should().Be(2, "as guias do primeiro registro, e só elas");
    }

    /// <summary>
    /// Concluir DEPOIS do registro não cria um segundo atendimento — e o pacote debita uma
    /// vez só. É o caminho normal da tela: a guia nasce, a janela abre, a pessoa confirma.
    /// </summary>
    [Fact]
    public async Task Concluir_depois_de_registrar_nao_duplica_nada()
    {
        var paciente = await PacienteAsync();
        await VenderPacoteAsync(paciente);
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");
        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId, DebitarPacote: true, GerarLancamento: true,
                Valor: 150m, Forma: FormaPagamento.Dinheiro),
            operador: "flavia@");

        resultado.Atendimento.Id.Should().Be(registro.Atendimento.Id);
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
        (await _db.Codigos.CountAsync()).Should().Be(2);

        // Os outros três fatos aconteceram, uma vez cada.
        resultado.Consumo.Should().NotBeNull();
        resultado.Lancamento.Should().NotBeNull();
        (await _pacotes.DoPacienteAsync(paciente)).Single().SaldoSessoes.Should().Be(9);
        resultado.Avisos.Should().BeEmpty();
    }

    /// <summary>
    /// O <c>ConcluirAsync</c> chamado direto (sem registro anterior) continua funcionando e
    /// gerando a guia. É o que mantém a esteira da parcela 60 de pé para quem o chama de
    /// outro lugar — e o que impede esta parcela de virar uma terceira porta.
    /// </summary>
    [Fact]
    public async Task Concluir_sem_registro_previo_continua_gerando_a_guia()
    {
        var paciente = await PacienteAsync();
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamentoId), operador: "flavia@");

        resultado.Atendimento.Codigos.Should().HaveCount(2);
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
    }

    // ================================================================
    // A JANELA SÓ ABRE QUANDO HÁ O QUE DECIDIR
    // ================================================================

    /// <summary>
    /// Paciente de convênio, sem pacote e sem histórico de pagamento no balcão: não há uma
    /// única pergunta a fazer. Abrir a janela aí é pedir confirmação de uma tela vazia — e
    /// é assim que se ensina alguém a fechar janela sem ler.
    /// </summary>
    [Fact]
    public async Task Sem_pacote_sem_cobranca_e_sem_insumo_nao_ha_o_que_decidir()
    {
        var paciente = await PacienteAsync();
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");

        registro.TemDecisao.Should().BeFalse();
    }

    /// <summary>
    /// Com pacote a debitar, a janela abre: aí existe uma decisão de verdade, e é
    /// justamente a que a clínica perdia quando ninguém a fazia (sessão comprada e não
    /// debitada é atendimento de graça).
    /// </summary>
    [Fact]
    public async Task Com_pacote_a_debitar_ha_o_que_decidir()
    {
        var paciente = await PacienteAsync();
        await VenderPacoteAsync(paciente);
        var agendamentoId = await HorarioComCheckInAsync(paciente);

        var registro = await _fechamento.RegistrarAtendimentoAsync(agendamentoId, "flavia@");

        registro.TemDecisao.Should().BeTrue();
        registro.Proposta.TemPacote.Should().BeTrue();
    }
}
