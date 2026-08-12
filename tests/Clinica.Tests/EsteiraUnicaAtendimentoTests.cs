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
/// A ESTEIRA ÚNICA do atendimento (parcela 60).
///
/// Havia duas portas para criar atendimento, e elas <b>não faziam a mesma coisa</b>:
///
/// <list type="bullet">
/// <item><b>Pela agenda</b> (Fila → Finalizar), o <c>FechamentoSessaoService</c> fazia
/// QUATRO coisas: a guia nascia, o pacote debitava, o insumo saía do estoque e o dinheiro
/// entrava no caixa.</item>
/// <item><b>Pelo avulso</b> ("Novo atendimento"), acontecia UMA: a guia. O paciente com
/// pacote de dez sessões era atendido de graça, o particular que pagou no balcão não
/// aparecia no caixa, e o horário virava um fantasma às 9h fixo, sem profissional,
/// contando na ocupação do dia.</item>
/// </list>
///
/// Do ponto de vista do faturamento as duas eram idênticas — a guia nascia certa —, e foi
/// por isso que ninguém notou. Não havia uma linha de comentário explicando a diferença:
/// <b>ela não foi decidida, foi esquecida.</b>
///
/// Estes testes são a amarra. Eles falham se alguém abrir uma terceira porta que pule
/// algum dos fatos — que é como a segunda foi aberta.
/// </summary>
public class EsteiraUnicaAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly FechamentoSessaoService _fechamento;
    private readonly PacoteService _pacotes;

    public EsteiraUnicaAtendimentoTests()
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

    private static readonly DateTime Quando = new(2026, 8, 11, 15, 30, 0);

    private async Task<int> PacienteAsync(Convenio convenio = Convenio.Amil, bool comApp = false)
    {
        var p = new Paciente
        {
            Nome = "Paciente", Convenio = convenio, Sexo = Sexo.Feminino, PossuiApp = comApp
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Vende um pacote de 10 sessões, para provar que ele debita nos dois caminhos.</summary>
    private async Task VenderPacoteAsync(int pacienteId)
    {
        var catalogo = new PacoteCatalogo
        {
            Nome = "10 sessões", SessoesIncluidas = 10, ValidadeDias = 180,
            Valor = 1000m, Ativo = true
        };
        _db.PacotesCatalogo.Add(catalogo);
        await _db.SaveChangesAsync();

        await _pacotes.VenderAsync(pacienteId, catalogo.Id, DateOnly.FromDateTime(Quando));
    }

    /// <summary>
    /// O caminho da AGENDA: marca, faz check-in e conclui — é o que a Fila faz.
    /// </summary>
    private async Task<ResultadoFechamento> PelaAgendaAsync(int pacienteId, DateTime quando)
    {
        var ag = await _agenda.AgendarAsync(
            pacienteId, quando, ModalidadeAtendimento.AcupunturaSimples, null,
            operador: "recepcao");
        await _agenda.RegistrarChegadaAsync(ag.Id);

        return await _fechamento.ConcluirAsync(
            new DecisaoFechamento(ag.Id, DebitarPacote: true, GerarLancamento: true,
                Valor: 150m, Forma: FormaPagamento.Dinheiro),
            operador: "recepcao");
    }

    /// <summary>
    /// O caminho do AVULSO, como a tela "Novo atendimento" passou a fazer: encaixe na hora
    /// real + a MESMA janela de fechamento. Se este método precisar um dia divergir do de
    /// cima, é sinal de que a esteira voltou a ser duas.
    /// </summary>
    private async Task<ResultadoFechamento> PeloAvulsoAsync(int pacienteId, DateTime quando)
    {
        var ag = await _agenda.AgendarAsync(
            pacienteId, quando, ModalidadeAtendimento.AcupunturaSimples, null,
            encaixe: true, operador: "recepcao");
        await _agenda.RegistrarChegadaAsync(ag.Id);

        return await _fechamento.ConcluirAsync(
            new DecisaoFechamento(ag.Id, DebitarPacote: true, GerarLancamento: true,
                Valor: 150m, Forma: FormaPagamento.Dinheiro),
            operador: "recepcao");
    }

    // ================================================================
    // OS QUATRO FATOS, NAS DUAS PORTAS
    // ================================================================

    /// <summary>
    /// O teste central da parcela. Cada porta atende um paciente com pacote, e os dois
    /// produzem os MESMOS fatos: guia, débito de pacote e entrada no caixa.
    ///
    /// Antes, o avulso produzia só o primeiro — e o silêncio dos outros dois era exatamente
    /// o defeito: a clínica atendia de graça quem tinha comprado dez sessões, e o caixa não
    /// batia no fim do mês por uma diferença que não tinha nome.
    /// </summary>
    [Fact]
    public async Task Avulso_e_agendado_produzem_os_mesmos_fatos()
    {
        var pelaAgenda = await PacienteAsync();
        var peloAvulso = await PacienteAsync();
        await VenderPacoteAsync(pelaAgenda);
        await VenderPacoteAsync(peloAvulso);

        var a = await PelaAgendaAsync(pelaAgenda, Quando);
        var b = await PeloAvulsoAsync(peloAvulso, Quando);

        // 1 — a guia
        a.Atendimento.Codigos.Should().NotBeEmpty();
        b.Atendimento.Codigos.Count.Should().Be(a.Atendimento.Codigos.Count,
            "as duas portas passam pelo mesmo motor de regras");

        // 2 — o pacote
        a.Consumo.Should().NotBeNull();
        b.Consumo.Should().NotBeNull("o avulso debitava ZERO sessões antes desta parcela");

        // 3 — o caixa
        a.Lancamento.Should().NotBeNull();
        b.Lancamento!.Valor.Should().Be(a.Lancamento!.Valor,
            "o dinheiro do avulso não entrava no caixa antes desta parcela");

        // E nenhuma das duas engoliu falha em silêncio.
        a.Avisos.Should().BeEmpty();
        b.Avisos.Should().BeEmpty();
    }

    /// <summary>
    /// O saldo do pacote cai igual pelos dois caminhos. É a asserção que mais interessa à
    /// clínica: sessão comprada e não debitada é atendimento de graça.
    /// </summary>
    [Fact]
    public async Task O_pacote_debita_pelas_duas_portas()
    {
        var id = await PacienteAsync();
        await VenderPacoteAsync(id);

        await PelaAgendaAsync(id, Quando);
        await PeloAvulsoAsync(id, Quando.AddHours(1));

        var saldos = await _pacotes.DoPacienteAsync(id);
        saldos.Single().SaldoSessoes.Should().Be(8, "duas sessões foram atendidas");
    }

    // ================================================================
    // O FANTASMA DAS 9h
    // ================================================================

    /// <summary>
    /// O horário do avulso é o REAL, e é um encaixe.
    ///
    /// Antes, o lançamento avulso criava um <c>Agendamento</c> sintético às <b>9h fixo</b>
    /// — porque <c>Atendimento</c> só guarda <c>DateOnly</c> e não havia hora para copiar.
    /// Ele aparecia na grade da agenda num horário em que ninguém foi atendido, na coluna
    /// "Sem profissional", e contava na ocupação do dia.
    /// </summary>
    [Fact]
    public async Task O_avulso_marca_o_horario_REAL_e_nao_as_9h()
    {
        var id = await PacienteAsync();
        await PeloAvulsoAsync(id, Quando);

        var doDia = await _agenda.DoDiaAsync(DateOnly.FromDateTime(Quando));

        var ag = doDia.Should().ContainSingle().Subject;
        ag.DataHora.TimeOfDay.Should().Be(new TimeSpan(15, 30, 0));
        ag.Encaixe.Should().BeTrue("quem chega sem hora marcada é um encaixe");
        ag.Status.Should().Be(StatusAgendamento.Realizado);
    }

    /// <summary>
    /// Uma sessão, um horário. O caminho da agenda nunca duplicou, e o avulso passou a não
    /// duplicar também — antes ele criava o horário fantasma ALÉM do que já existisse.
    /// </summary>
    [Fact]
    public async Task Cada_sessao_gera_UM_horario_e_UM_atendimento()
    {
        var id = await PacienteAsync();

        await PelaAgendaAsync(id, Quando);
        await PeloAvulsoAsync(id, Quando.AddHours(2));

        (await _agenda.DoDiaAsync(DateOnly.FromDateTime(Quando))).Should().HaveCount(2);
        (await _db.Atendimentos.CountAsync()).Should().Be(2);
    }

    // ================================================================
    // A ESCOLHA QUE NÃO PODIA SE PERDER
    // ================================================================

    /// <summary>
    /// Nas modalidades duplas, qual código o convênio libera primeiro é escolha da tela — e
    /// ela precisava atravessar o horário para chegar ao motor, agora que o avulso passa
    /// pelo agendamento. Sem <c>Agendamento.PrimeiroCodigo</c>, unificar as portas custaria
    /// a feature.
    /// </summary>
    [Fact]
    public async Task A_escolha_do_primeiro_codigo_atravessa_o_agendamento()
    {
        // Unimed: são as duas regras que HONRAM a escolha (`PrimeiroCodigoPreferido`).
        // Nas demais o convênio decide sozinho, e o campo não teria o que provar.
        var id = await PacienteAsync(Convenio.UnimedPadrao, comApp: true);

        var ag = await _agenda.AgendarAsync(
            id, Quando, ModalidadeAtendimento.AcupunturaComEletro, null,
            encaixe: true, primeiroCodigo: TipoCodigo.Eletroacupuntura);

        var r = await _agenda.ConfirmarPresencaAsync(ag.Id);

        r.Atendimento.Codigos
            .Single(c => c.Ordem == OrdemCodigo.Primeiro)
            .Tipo.Should().Be(TipoCodigo.Eletroacupuntura);
    }

    /// <summary>
    /// Sem escolha informada, o convênio decide — é o que toda linha anterior à coluna
    /// vale, e o comportamento que a agenda sempre teve.
    /// </summary>
    [Fact]
    public async Task Sem_escolha_o_convenio_decide_como_sempre()
    {
        var id = await PacienteAsync(Convenio.UnimedPadrao, comApp: true);

        var ag = await _agenda.AgendarAsync(
            id, Quando, ModalidadeAtendimento.AcupunturaComEletro, null);

        ag.PrimeiroCodigo.Should().BeNull();

        var r = await _agenda.ConfirmarPresencaAsync(ag.Id);
        r.Atendimento.Codigos.Should().HaveCountGreaterThan(1);
    }
}
