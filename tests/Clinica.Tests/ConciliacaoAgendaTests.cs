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
/// A CONCILIAÇÃO DA AGENDA (parcela 93) — o que ficou pendurado entre o horário e a sessão.
///
/// O caso real que a motivou
/// -------------------------
/// A clínica ainda não trabalha o check-in pela agenda: a recepcionista vai direto ao Novo
/// atendimento e lança. Desde a parcela 91 o lançamento reconhece o horário DO DIA e nasce
/// pendurado nele, mas isso vale dali para a frente e só quando a data bate — e a agenda
/// importada do Smart Clinic trouxe centenas de horários que nunca terão check-in aqui.
///
/// Horário parado não é ruído: ele infla a ocupação, põe no "Meu dia" do médico um paciente
/// que não vem, e a evolução importada — distribuída pela ORDEM DA HORA MARCADA — vai parar
/// nele em vez de na sessão de verdade.
///
/// O que estes testes fixam
/// ------------------------
/// Que a pergunta tem <b>TRÊS</b> respostas e não duas. A linha carrega
/// <see cref="HorarioParado.TemSessaoNoDia"/> justamente porque "não faltou" se divide em
/// "aconteceu e não foi lançada" (lançar retroativo) e "aconteceu e JÁ foi lançada por
/// fora" — onde lançar de novo criaria um SEGUNDO jogo de guias para a mesma sessão, que é
/// a duplicata que a tela existe para acabar.
/// </summary>
public class ConciliacaoAgendaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AtendimentoService _atendimentos;
    private readonly AgendaService _agenda;
    private readonly ConciliacaoAgendaService _conciliacao;

    public ConciliacaoAgendaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _atendimentos = new AtendimentoService(_repo, parametros: _parametros);
        _agenda = new AgendaService(_repo, _atendimentos, _parametros);
        _conciliacao = new ConciliacaoAgendaService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Uma quinta-feira qualquer, para as contas de dia serem legíveis.</summary>
    private static readonly DateOnly Hoje = new(2026, 9, 10);

    private async Task<int> PacienteAsync(string nome = "Severino da Silva")
    {
        var p = new Paciente
        {
            Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Masculino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>Um horário marcado e nunca resolvido — o estado que a conciliação procura.</summary>
    private async Task<int> HorarioEmAbertoAsync(
        int pacienteId, DateTime quando, bool importado = false)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = quando,
            ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ModalidadeCodigo = ModalidadeAtendimento.Consulta.ToString(),
            Status = StatusAgendamento.Agendado,
            ChaveImportacao = importado ? $"IMPORT:smartclinic:agenda:{quando.Ticks}" : null
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag.Id;
    }

    // ================================================================
    // A FILA: o que entra e o que não entra
    // ================================================================

    /// <summary>
    /// O caso do Severino: horário importado de dias atrás, ainda em aberto. Passada a
    /// carência, ele é a pergunta.
    /// </summary>
    [Fact]
    public async Task Horario_em_aberto_vencido_entra_na_fila()
    {
        var paciente = await PacienteAsync();
        await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-5).ToDateTime(new TimeOnly(14, 20)), importado: true);

        var c = await _conciliacao.LevantarAsync(Hoje);

        c.Parados.Should().HaveCount(1);
        var linha = c.Parados[0];
        linha.Paciente.Should().Be("Severino da Silva");
        linha.DataHora.TimeOfDay.Should().Be(new TimeSpan(14, 20, 0));
        linha.Importado.Should().BeTrue("veio da agenda do sistema anterior");
        linha.DiasParado.Should().Be(5);
    }

    /// <summary>
    /// A CARÊNCIA. O horário de ontem pode estar só esperando o fechamento do dia —
    /// perguntar na hora transformaria a tela num alarme que se aprende a ignorar.
    /// </summary>
    [Fact]
    public async Task Horario_dentro_da_carencia_ainda_nao_e_perguntado()
    {
        var paciente = await PacienteAsync();
        await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-1).ToDateTime(new TimeOnly(9, 0)));
        await HorarioEmAbertoAsync(paciente, Hoje.ToDateTime(new TimeOnly(16, 0)));

        (await _conciliacao.LevantarAsync(Hoje, carenciaDias: 2)).Parados.Should().BeEmpty();
    }

    [Fact]
    public async Task Horario_fora_da_janela_nao_entra()
    {
        var paciente = await PacienteAsync();
        await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-200).ToDateTime(new TimeOnly(9, 0)));

        (await _conciliacao.LevantarAsync(Hoje, janelaDias: 120)).Parados.Should().BeEmpty();
    }

    /// <summary>Quem já foi resolvido saiu da fila — a tela é de trabalho, não de arquivo.</summary>
    [Fact]
    public async Task Cancelado_falta_e_realizado_com_atendimento_ficam_de_fora()
    {
        var paciente = await PacienteAsync();
        var quando = Hoje.AddDays(-5).ToDateTime(new TimeOnly(14, 20));

        var cancelado = await HorarioEmAbertoAsync(paciente, quando);
        await _agenda.CancelarAsync(cancelado, "flavia@");

        var faltou = await HorarioEmAbertoAsync(paciente, quando.AddHours(1));
        await _agenda.MarcarFaltaAsync(faltou, "flavia@");

        var realizado = await HorarioEmAbertoAsync(paciente, quando.AddHours(2));
        await _agenda.LancarNoHorarioAsync(realizado, null, operador: "flavia@");

        var c = await _conciliacao.LevantarAsync(Hoje);

        c.Parados.Should().BeEmpty();
        c.Orfaos.Should().BeEmpty("o realizado aponta para o atendimento que nasceu nele");
        c.Vazio.Should().BeTrue();
    }

    // ================================================================
    // AS TRÊS RESPOSTAS
    // ================================================================

    /// <summary>
    /// A resposta (3), e a razão de esta tela existir: o paciente VEIO, a recepcionista
    /// lançou por fora da agenda, e o horário ficou para trás. A linha precisa DIZER que
    /// já há sessão no dia — sem isso, "não faltou" levaria a lançar de novo e a gerar um
    /// segundo jogo de guias para a mesma sessão.
    /// </summary>
    [Fact]
    public async Task Horario_parado_com_sessao_lancada_por_fora_e_marcado_como_tal()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        await HorarioEmAbertoAsync(paciente, dia.ToDateTime(new TimeOnly(14, 20)), importado: true);

        // O encaixe da recepcionista: mesmo paciente, mesmo dia, por fora do horário.
        var avulso = await _agenda.LancarAvulsoAsync(
            paciente, dia.ToDateTime(new TimeOnly(15, 5)),
            ModalidadeAtendimento.AcupunturaComEletro, null, operador: "flavia@");

        var c = await _conciliacao.LevantarAsync(Hoje);

        var linha = c.Parados.Should().ContainSingle().Subject;
        linha.TemSessaoNoDia.Should().BeTrue();
        linha.SessoesDoDia.Should().ContainSingle()
            .Which.AtendimentoId.Should().Be(avulso.Lancamento.Atendimento.Id);
        linha.SessoesDoDia[0].GuiasFaturaveis.Should().BeGreaterThan(0);
        linha.Situacao.Should().Contain(avulso.Lancamento.Atendimento.Numero!);

        c.ComSessaoNoDia.Should().Be(1);
        c.SemSessaoNoDia.Should().Be(0);
    }

    /// <summary>
    /// As respostas (1) e (2) moram na mesma linha: sem sessão no dia, ou faltou, ou
    /// aconteceu e ninguém lançou. A conciliação não escolhe entre as duas — quem escolhe
    /// é quem estava no balcão.
    /// </summary>
    [Fact]
    public async Task Horario_parado_sem_sessao_no_dia_fica_com_as_duas_respostas_em_aberto()
    {
        var paciente = await PacienteAsync();
        await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-4).ToDateTime(new TimeOnly(8, 0)));

        var c = await _conciliacao.LevantarAsync(Hoje);

        var linha = c.Parados.Should().ContainSingle().Subject;
        linha.TemSessaoNoDia.Should().BeFalse();
        linha.SessoesDoDia.Should().BeEmpty();
        linha.Situacao.Should().Contain("Nenhuma sessão");
        c.SemSessaoNoDia.Should().Be(1);
    }

    /// <summary>
    /// A sessão de OUTRO dia não conta. Um tratamento de dez sessões tem atendimento em
    /// quase todo dia útil, e casar por paciente sem casar por DIA daria "já foi lançada"
    /// para a fila inteira — o erro que faria a tela mandar não lançar o que falta.
    /// </summary>
    [Fact]
    public async Task Sessao_de_outro_dia_nao_conta_para_o_horario_parado()
    {
        var paciente = await PacienteAsync();
        await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-5).ToDateTime(new TimeOnly(14, 20)));

        await _agenda.LancarAvulsoAsync(
            paciente, Hoje.AddDays(-4).ToDateTime(new TimeOnly(15, 0)),
            ModalidadeAtendimento.AcupunturaComEletro, null, operador: "flavia@");

        var linha = (await _conciliacao.LevantarAsync(Hoje)).Parados.Should().ContainSingle().Subject;
        linha.TemSessaoNoDia.Should().BeFalse();
    }

    /// <summary>E a sessão de OUTRO paciente no mesmo dia também não.</summary>
    [Fact]
    public async Task Sessao_de_outro_paciente_no_mesmo_dia_nao_conta()
    {
        var severino = await PacienteAsync();
        var outro = await PacienteAsync("Maria de Lourdes");
        var dia = Hoje.AddDays(-5);

        await HorarioEmAbertoAsync(severino, dia.ToDateTime(new TimeOnly(14, 20)));
        await _agenda.LancarAvulsoAsync(
            outro, dia.ToDateTime(new TimeOnly(14, 30)),
            ModalidadeAtendimento.AcupunturaComEletro, null, operador: "flavia@");

        var linha = (await _conciliacao.LevantarAsync(Hoje)).Parados
            .Should().ContainSingle(p => p.PacienteId == severino).Subject;
        linha.TemSessaoNoDia.Should().BeFalse();
    }

    // ================================================================
    // AS AÇÕES — as duas que já existem e estão corretas
    // ================================================================

    /// <summary>Resposta (1): faltou. Sai da fila e vira falta de verdade nos indicadores.</summary>
    [Fact]
    public async Task Marcar_falta_tira_o_horario_da_fila()
    {
        var paciente = await PacienteAsync();
        var id = await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-4).ToDateTime(new TimeOnly(8, 0)));

        await _agenda.MarcarFaltaAsync(id, "flavia@");

        (await _conciliacao.LevantarAsync(Hoje)).Parados.Should().BeEmpty();
        (await _repo.ObterAgendamentoAsync(id))!.Status.Should().Be(StatusAgendamento.Faltou);
    }

    /// <summary>
    /// Resposta (2): aconteceu e ninguém lançou. O lançamento retroativo pelo HORÁRIO já
    /// funciona e data o atendimento pela data DELE — não pela de hoje. É o que faz a guia
    /// nascer com a data prevista da sessão que de fato aconteceu, e é por isso que esta
    /// resposta não precisou de código novo.
    /// </summary>
    [Fact]
    public async Task Lancar_retroativo_pelo_horario_data_o_atendimento_no_dia_do_horario()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-4);
        var id = await HorarioEmAbertoAsync(paciente, dia.ToDateTime(new TimeOnly(8, 0)));

        var (ag, lancamento) = await _agenda.LancarNoHorarioAsync(
            id, null, modalidadeCodigo: ModalidadeAtendimento.AcupunturaComEletro.ToString(),
            operador: "flavia@");

        lancamento.Atendimento.Data.Should().Be(dia, "a sessão aconteceu no dia do horário, não hoje");
        ag.Status.Should().Be(StatusAgendamento.Realizado);
        ag.AtendimentoId.Should().Be(lancamento.Atendimento.Id);
        ag.ChegadaEm.Should().NotBeNull("lançar pelo horário carimba a chegada");

        (await _conciliacao.LevantarAsync(Hoje)).Parados.Should().BeEmpty();
    }

    // ================================================================
    // O ÓRFÃO — o estado que ninguém detectava
    // ================================================================

    /// <summary>
    /// Horário REALIZADO sem atendimento: o kanban diz "Finalizado" (a etapa é derivada do
    /// STATUS) e o repasse exclui a sessão em silêncio, porque exige <c>AtendimentoId</c>.
    /// É o estado dos três encaixes de 12/08/2026, e até esta parcela nenhuma consulta do
    /// sistema o listava.
    /// </summary>
    [Fact]
    public async Task Horario_realizado_sem_atendimento_aparece_como_orfao()
    {
        var paciente = await PacienteAsync();
        var id = await HorarioEmAbertoAsync(paciente, Hoje.AddDays(-3).ToDateTime(new TimeOnly(10, 0)));

        var ag = await _repo.ObterAgendamentoAsync(id);
        ag!.Status = StatusAgendamento.Realizado;
        await _repo.SalvarAsync();

        var c = await _conciliacao.LevantarAsync(Hoje);

        c.Parados.Should().BeEmpty("ele não está em aberto — está mentindo que terminou");
        var orfao = c.Orfaos.Should().ContainSingle().Subject;
        orfao.AgendamentoId.Should().Be(id);
        orfao.Paciente.Should().Be("Severino da Silva");
        orfao.DiasParado.Should().Be(3);
    }

    /// <summary>
    /// O órfão é olhado até HOJE, sem carência: ele não é uma pergunta que amadurece — é um
    /// horário que já está dizendo a coisa errada.
    /// </summary>
    [Fact]
    public async Task Orfao_de_hoje_ja_aparece_sem_esperar_a_carencia()
    {
        var paciente = await PacienteAsync();
        var id = await HorarioEmAbertoAsync(paciente, Hoje.ToDateTime(new TimeOnly(10, 0)));

        var ag = await _repo.ObterAgendamentoAsync(id);
        ag!.Status = StatusAgendamento.Realizado;
        await _repo.SalvarAsync();

        (await _conciliacao.LevantarAsync(Hoje)).Orfaos.Should().ContainSingle();
    }

    // ================================================================
    // GUARDAS DE PARÂMETRO
    // ================================================================

    [Fact]
    public async Task Janela_precisa_ser_positiva()
        => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _conciliacao.LevantarAsync(Hoje, janelaDias: 0));

    [Fact]
    public async Task Carencia_negativa_e_recusada()
        => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _conciliacao.LevantarAsync(Hoje, carenciaDias: -1));

    /// <summary>Fila vazia é o caso normal de uma clínica em dia — e não pode custar consulta.</summary>
    [Fact]
    public async Task Sem_horario_parado_nao_ha_o_que_conciliar()
    {
        await PacienteAsync();
        var c = await _conciliacao.LevantarAsync(Hoje);
        c.Vazio.Should().BeTrue();
        c.Parados.Should().BeEmpty();
        c.Orfaos.Should().BeEmpty();
    }
}
