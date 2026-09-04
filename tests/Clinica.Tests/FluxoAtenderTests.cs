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
/// O FLUXO QUE A DIREÇÃO PEDIU (parcela 95): a secretária marca → o horário cai na agenda
/// do médico → ele clica em <b>Atender</b>, faz o atendimento e <b>finaliza</b>.
///
/// O que mudou, e por que esta suíte existe
/// ----------------------------------------
/// Até aqui o "Finalizar" do Consultório carimbava <c>FimAtendimentoEm</c> e deixava o
/// <c>Status</c> em <c>Agendado</c>: quem tirava o horário da agenda era o <b>Concluir</b>
/// do balcão. O argumento (parcela 61) era que concluir são quatro fatos e três são do
/// balcão — verdadeiro para pacote, insumo e caixa, e não para a GUIA, que é o fato do
/// atendimento. No caso mais comum (convênio, sem pacote, sem insumo) o clique do balcão
/// não abria janela nenhuma: era cerimônia para carimbar o que o médico já sabia.
///
/// Os testes cobrem as três emendas do fluxo, e o que elas NÃO podem quebrar:
/// <list type="number">
/// <item>marcar exige quem vai atender — senão o horário não cai na agenda de ninguém;</item>
/// <item>encerrar → concluir é UMA sequência, e a ordem inversa é recusada;</item>
/// <item>o dinheiro continua alcançável pelo balcão DEPOIS de a sessão estar concluída.</item>
/// </list>
/// </summary>
public class FluxoAtenderTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly FechamentoSessaoService _fechamento;
    private readonly PacoteService _pacotes;
    private readonly EstoqueService _estoque;

    private static readonly DateTime Sessao = new(2026, 8, 20, 14, 0, 0);
    private static DateOnly Dia => DateOnly.FromDateTime(Sessao);

    private readonly int _profissionalId;

    public FluxoAtenderTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();

        var prof = new Profissional { Nome = "Dra. Ana" };
        _db.Profissionais.Add(prof);
        _db.SaveChanges();
        _profissionalId = prof.Id;

        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _pacotes = new PacoteService(_repo);
        _estoque = new EstoqueService(_repo);
        _fechamento = new FechamentoSessaoService(
            _repo, _agenda, _pacotes, _estoque, new FinanceiroService(_repo));
    }

    private async Task<int> CriarPacienteAsync(string nome = "Maria")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Agendamento> MarcarAsync(int? pacienteId = null, int minutos = 0)
        => await _agenda.AgendarAsync(
            pacienteId ?? await CriarPacienteAsync(), Sessao.AddMinutes(minutos),
            ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: _profissionalId, operador: "recepcao");

    // ==================== 1) O horário cai na agenda de alguém ====================

    /// <summary>
    /// A primeira seta do fluxo. Sem <c>ProfissionalId</c> o horário não aparece no "Meu
    /// dia" nem na "Minha semana" — o médico nunca o vê, e ele fica fora do repasse.
    /// </summary>
    [Fact]
    public async Task Horario_marcado_cai_na_agenda_do_profissional()
    {
        var ag = await MarcarAsync();

        var doDia = await _agenda.DoDiaAsync(Dia);
        doDia.Should().ContainSingle(a => a.Id == ag.Id && a.ProfissionalId == _profissionalId);
    }

    // ==================== 2) Atender → finalizar ====================

    /// <summary>
    /// O caminho inteiro do médico, na ordem em que a tela o executa: entrar na sala
    /// (o que o botão "Atender" passou a carimbar sozinho), encerrar e concluir.
    /// </summary>
    [Fact]
    public async Task Atender_e_finalizar_deixam_a_sessao_CONCLUIDA_com_guia()
    {
        var ag = await MarcarAsync();

        // "Atender" — o clique único do médico.
        await _agenda.IniciarAtendimentoAsync(ag.Id, "medica");

        // "Finalizar atendimento": encerra e conclui.
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Sessao.AddMinutes(30));
        var registro = await _fechamento.RegistrarAtendimentoAsync(ag.Id, "medica");

        var depois = await _agenda.ObterAsync(ag.Id);
        depois!.Status.Should().Be(StatusAgendamento.Realizado,
            "é isto que tira o horário da agenda como finalizado");
        depois.Etapa.Should().Be(EtapaFila.Finalizado);
        depois.FimAtendimentoEm.Should().NotBeNull("o carimbo do encerramento sobrevive à conclusão");
        depois.AtendimentoId.Should().NotBeNull();

        registro.GuiasGeradas.Should().BeGreaterThan(0, "a guia é o fato do atendimento");
        registro.Atendimento.RealizadoEm.Should().NotBeNull(
            "é o carimbo que diz que a sessão ACONTECEU — BI, retenção e origem ancoram nele");
    }

    /// <summary>
    /// A ORDEM dos passos é amarra, não estilo: <c>EncerrarAtendimentoAsync</c> recusa
    /// horário que já saiu de <c>Agendado</c>. Concluir primeiro deixaria a sessão sem o
    /// carimbo de fim — e o balcão sem saber a que horas a sala vagou.
    /// </summary>
    [Fact]
    public async Task Concluir_antes_de_encerrar_deixa_o_encerramento_impossivel()
    {
        var ag = await MarcarAsync();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "medica");
        await _fechamento.RegistrarAtendimentoAsync(ag.Id, "medica");

        var encerrar = () => _agenda.EncerrarAtendimentoAsync(ag.Id, "medica");

        await encerrar.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Depois de concluída, desfazer é ESTORNO — e é por isso que o botão "Reabrir" some
    /// da tela do médico. Botão que só existe para levar recusa é o defeito da parcela 41.
    /// </summary>
    [Fact]
    public async Task Reabrir_depois_de_concluida_e_recusado()
    {
        var ag = await MarcarAsync();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "medica");
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Sessao.AddMinutes(30));
        await _fechamento.RegistrarAtendimentoAsync(ag.Id, "medica");

        var reabrir = () => _agenda.ReabrirAtendimentoAsync(ag.Id, "medica");

        await reabrir.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Finalizar duas vezes (o duplo clique, a tela reaberta) não pode produzir um segundo
    /// jogo de guias: <c>GarantirAtendimentoAsync</c> reaproveita a presença já
    /// confirmada. É a idempotência da parcela 65, agora exercitada pela porta nova.
    /// </summary>
    [Fact]
    public async Task Finalizar_duas_vezes_nao_gera_a_segunda_guia()
    {
        var ag = await MarcarAsync();
        await _agenda.IniciarAtendimentoAsync(ag.Id, "medica");

        var primeiro = await _fechamento.RegistrarAtendimentoAsync(ag.Id, "medica");
        var segundo = await _fechamento.RegistrarAtendimentoAsync(ag.Id, "medica");

        segundo.JaExistia.Should().BeTrue();
        segundo.Atendimento.Id.Should().Be(primeiro.Atendimento.Id);
        (await _db.Atendimentos.CountAsync()).Should().Be(1);
    }

    // ==================== 3) O dinheiro continua com o balcão ====================

    /// <summary>
    /// O que a mudança NÃO podia quebrar: com a sessão já concluída pelo Consultório, o
    /// balcão ainda precisa debitar o pacote, baixar o insumo e lançar o caixa. Elo
    /// partido aqui não vira erro — vira pacote que não debita e caixa que não bate, e o
    /// mês fecha com uma diferença que não tem nome (a lição da parcela 60).
    /// </summary>
    [Fact]
    public async Task Balcao_fecha_o_dinheiro_DEPOIS_de_o_medico_concluir()
    {
        var pacienteId = await CriarPacienteAsync();
        var pacote = await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = pacienteId,
            Nome = "Pacote 10 sessões",
            Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10,
            Valor = 1000m,
            DataCompra = Dia.AddDays(-10)
        });

        var ag = await MarcarAsync(pacienteId);
        await _agenda.IniciarAtendimentoAsync(ag.Id, "medica");
        await _agenda.EncerrarAtendimentoAsync(ag.Id, "medica", Sessao.AddMinutes(30));
        var registro = await _fechamento.RegistrarAtendimentoAsync(ag.Id, "medica");

        // Agora o balcão: a MESMA janela de fechamento, sobre a sessão já concluída.
        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(ag.Id, GerarLancamento: true, Valor: 150m,
                                  Forma: FormaPagamento.Pix),
            operador: "recepcao");

        resultado.Atendimento.Id.Should().Be(registro.Atendimento.Id,
            "fechar não pode criar um segundo atendimento");
        resultado.Consumo.Should().NotBeNull();
        resultado.Lancamento.Should().NotBeNull();

        var saldos = await _pacotes.DoPacienteAsync(pacienteId, Dia);
        saldos.Single(s => s.PacoteId == pacote.Id).SaldoSessoes.Should().Be(9);
    }

    /// <summary>
    /// A leitura que faz o botão "Fechar sessão" SUMIR da raia FINALIZADO quando não há
    /// mais o que fechar. Ela é EM LOTE: uma ida ao banco por cartão daria trinta a cada
    /// batida do relógio do quadro.
    ///
    /// ⚠️ Ela nasce com um teste que a EXECUTA — consulta LINQ só se prova executando, e
    /// método de repositório sem chamador em teste é código que ninguém rodou.
    /// </summary>
    [Fact]
    public async Task O_balcao_so_ve_pendencia_de_fechamento_no_que_falta_fechar()
    {
        var comPacote = await CriarPacienteAsync("Com pacote");
        await _pacotes.RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = comPacote,
            Nome = "Pacote",
            Tipo = TipoPacote.Sessoes,
            SessoesContratadas = 10,
            Valor = 1000m,
            DataCompra = Dia.AddDays(-10)
        });

        var fechada = await MarcarAsync(comPacote);
        var aberta = await MarcarAsync(await CriarPacienteAsync("Sem nada"), minutos: 60);

        var umaFechada = await _fechamento.RegistrarAtendimentoAsync(fechada.Id, "medica");
        var umaAberta = await _fechamento.RegistrarAtendimentoAsync(aberta.Id, "medica");
        await _fechamento.ConcluirAsync(new DecisaoFechamento(fechada.Id), operador: "recepcao");

        var jaFechados = await _repo.AtendimentosComFechamentoAsync(
            [umaFechada.Atendimento.Id, umaAberta.Atendimento.Id]);

        jaFechados.Should().ContainSingle().Which.Should().Be(umaFechada.Atendimento.Id);
    }

    /// <summary>Lista vazia não vai ao banco — e não devolve o mundo.</summary>
    [Fact]
    public async Task Sem_atendimentos_a_conferir_a_leitura_devolve_vazio()
        => (await _repo.AtendimentosComFechamentoAsync([])).Should().BeEmpty();

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
