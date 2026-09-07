using Clinica.Application.Modelos;
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
/// A TERCEIRA RESPOSTA da conciliação da agenda (set/2026): o horário SUBSTITUÍDO por uma
/// sessão lançada por fora.
///
/// A conciliação (parcela 93) nasceu com três respostas e saída para duas. A do meio —
/// "aconteceu e já foi lançada por fora", a mais comum do backlog da migração — ficava
/// com o botão apagado, porque nenhum status servia: cancelado inflaria o indicador de
/// cancelamento com sessões que aconteceram, falta culparia o paciente, realizado dobraria
/// a ocupação e o repasse (o atendimento já está pendurado no encaixe). O horário ficava
/// "Aguardando" para sempre.
///
/// O que estes testes fixam: o status novo sai de TUDO o que conta (ocupação, falta,
/// cancelamento, fila, disputa pela evolução, retorno coberto), aponta para a sessão, tem
/// volta pelo Remarcar, e as recusas dizem o que fazer — inclusive nas duas portas que
/// criam atendimento a partir do horário, onde o substituído produziria o segundo jogo de
/// guias que a conciliação existe para impedir.
/// </summary>
public class SubstituicaoDeHorarioTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AtendimentoService _atendimentos;
    private readonly AgendaService _agenda;
    private readonly ConciliacaoAgendaService _conciliacao;

    public SubstituicaoDeHorarioTests()
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

    private static readonly DateOnly Hoje = new(2026, 9, 10);

    private async Task<int> PacienteAsync(string nome = "Severino da Silva")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Masculino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> HorarioEmAbertoAsync(int pacienteId, DateTime quando, int? profissionalId = null)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = quando,
            ProfissionalId = profissionalId,
            ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ModalidadeCodigo = ModalidadeAtendimento.Consulta.ToString(),
            Status = StatusAgendamento.Agendado,
            ChaveImportacao = $"IMPORT:smartclinic:agenda:{quando.Ticks}"
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag.Id;
    }

    /// <summary>O caso real: horário importado parado + encaixe do mesmo dia lançado pelo balcão.</summary>
    private async Task<(int Horario, int Sessao)> CasoRealAsync(int pacienteId, DateOnly dia)
    {
        var horario = await HorarioEmAbertoAsync(pacienteId, dia.ToDateTime(new TimeOnly(9, 0)));
        var avulso = await _agenda.LancarAvulsoAsync(
            pacienteId, dia.ToDateTime(new TimeOnly(9, 12)),
            ModalidadeAtendimento.AcupunturaComEletro, null, operador: "flavia@");
        return (horario, avulso.Lancamento.Atendimento.Id);
    }

    private Task<Agendamento> HorarioAsync(int id)
        => _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == id);

    // ================================================================

    [Fact]
    public async Task Substituir_encerra_o_horario_apontando_para_a_sessao_e_ele_some_da_conciliacao()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);

        (await _conciliacao.LevantarAsync(Hoje)).Parados.Should().ContainSingle(
            "antes de encerrar, o horário é a pergunta");

        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        var ag = await HorarioAsync(horario);
        ag.Status.Should().Be(StatusAgendamento.Substituido);
        ag.AtendimentoSubstitutoId.Should().Be(sessao, "a procedência: para onde foi esta sessão");
        ag.AtendimentoId.Should().BeNull(
            "NUNCA por AtendimentoId — um segundo horário apontando para o atendimento por "
            + "aquela coluna o deixaria fora do backfill de RealizadoEm para sempre");

        var c = await _conciliacao.LevantarAsync(Hoje);
        c.Parados.Should().BeEmpty("resolvido sai da fila — a tela é de trabalho, não de arquivo");
        c.Orfaos.Should().BeEmpty();

        (await _db.Auditoria.AsNoTracking().SingleAsync(e => e.Acao == "AgendamentoSubstituido"))
            .Operador.Should().Be("flavia@");
    }

    [Fact]
    public async Task Substituido_nao_conta_como_cancelamento_nem_falta_nem_ocupacao()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);
        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        var indicadores = new IndicadoresService(_repo, _parametros);
        var painel = await indicadores.GerarAsync(dia, dia);
        painel.Agenda.Cancelados.Should().Be(0, "a sessão ACONTECEU — cancelamento inflado era a razão de não usar Cancelado");
        painel.Agenda.Faltas.Should().Be(0, "o paciente veio — Faltou o culparia por uma falta que não houve");
        painel.Agenda.Agendados.Should().Be(1, "só o encaixe ocupa a agenda; o horário substituído saiu da ocupação");
        painel.Agenda.Atendidos.Should().Be(1, "uma sessão, um atendido — nunca dois pelo mesmo paciente no mesmo dia");

        var relacionamento = new RelacionamentoService(_repo, _agenda);
        var faltas = await relacionamento.FaltasDoPacienteAsync(paciente);
        faltas.Faltas.Should().Be(0);
        faltas.Cancelamentos.Should().Be(0);
    }

    [Fact]
    public async Task Substituido_esta_fora_da_fila_tem_palavra_propria_e_nao_ocupa_a_agenda()
    {
        var ag = new Agendamento { Status = StatusAgendamento.Substituido, DataHora = Hoje.ToDateTime(new TimeOnly(9, 0)) };

        ag.Etapa.Should().Be(EtapaFila.ForaDaFila, "no kanban ele é como cancelado e falta: fica apagado, sem passo");
        ag.OcupaAgenda.Should().BeFalse("o vão volta a ser livre na grade");
        ag.Substituido.Should().BeTrue();
        StatusDaFila.ForaDaFila(StatusAgendamento.Substituido).Should().BeTrue();
        StatusDaFila.Palavra(StatusAgendamento.Substituido, ag.Etapa).Should().Be("Substituído",
            "nem 'Cancelado' nem 'Faltou': a palavra diz o que aconteceu");
        ag.AtrasoMinutos(Hoje.ToDateTime(new TimeOnly(12, 0))).Should().BeNull("não há quem cobrar");
    }

    [Fact]
    public async Task Substituido_nao_disputa_a_evolucao_avulsa_do_dia()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);
        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        // A evolução importada, sem vínculo, distribuída pela ordem da hora marcada: o
        // horário fantasma das 09h00 a capturava e a sessão de verdade (09h12) ficava em
        // "Sessões sem evolução" — é o estrago que a conciliação existe para desfazer.
        var avulsa = new Evolucao { PacienteId = paciente, Data = dia, TextoEvolucao = "importada" };
        _db.Evolucoes.Add(avulsa);
        await _db.SaveChangesAsync();

        var sessoes = await _db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == paciente).ToListAsync();
        var encaixe = sessoes.Single(a => a.Status == StatusAgendamento.Realizado);

        ConsultorioService.EvolucaoDoHorario([avulsa], encaixe.Id, paciente, dia, sessoes)
            .Should().BeSameAs(avulsa, "a sessão de verdade fica com a evolução");
        ConsultorioService.EvolucaoDoHorario([avulsa], horario, paciente, dia, sessoes)
            .Should().BeNull("o substituído não disputa: ele não é sessão nenhuma");
    }

    [Fact]
    public async Task Recusa_sessao_de_outro_paciente_de_outro_dia_estornada_e_horario_fora_do_aberto()
    {
        var paciente = await PacienteAsync();
        var outro = await PacienteAsync("Maria Souza");
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);

        var deOutro = await _agenda.LancarAvulsoAsync(outro, dia.ToDateTime(new TimeOnly(10, 0)),
            ModalidadeAtendimento.Consulta, null, operador: "flavia@");
        var acao = () => _agenda.SubstituirPorSessaoAsync(horario, deOutro.Lancamento.Atendimento.Id);
        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*OUTRO paciente*");

        var deOutroDia = await _agenda.LancarAvulsoAsync(paciente, dia.AddDays(1).ToDateTime(new TimeOnly(10, 0)),
            ModalidadeAtendimento.Consulta, null, operador: "flavia@");
        acao = () => _agenda.SubstituirPorSessaoAsync(horario, deOutroDia.Lancamento.Atendimento.Id);
        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*MESMO dia*");

        var estornado = await _db.Atendimentos.SingleAsync(a => a.Id == sessao);
        estornado.EstornadoEm = DateTime.Now;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        acao = () => _agenda.SubstituirPorSessaoAsync(horario, sessao);
        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*ESTORNADA*");

        // Horário que não está em aberto: já cancelado.
        await _agenda.CancelarAsync(horario, "flavia@");
        var valido = await _agenda.LancarAvulsoAsync(paciente, dia.ToDateTime(new TimeOnly(11, 0)),
            ModalidadeAtendimento.Consulta, null, operador: "flavia@");
        acao = () => _agenda.SubstituirPorSessaoAsync(horario, valido.Lancamento.Atendimento.Id);
        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*não está em aberto*");

        (await HorarioAsync(horario)).AtendimentoSubstitutoId.Should().BeNull("nenhuma recusa gravou");
    }

    [Fact]
    public async Task Remarcar_reabre_o_horario_e_solta_o_vinculo()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);
        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        await _agenda.RemarcarAsync(horario, Hoje.AddDays(2).ToDateTime(new TimeOnly(9, 0)), null,
            operador: "flavia@", encaixe: true, manterRecursos: false);

        var ag = await HorarioAsync(horario);
        ag.Status.Should().Be(StatusAgendamento.Agendado);
        ag.AtendimentoSubstitutoId.Should().BeNull(
            "horário reaberto é horário sem substituto — a conciliação volta a perguntar por ele");
    }

    [Fact]
    public async Task Confirmar_presenca_e_lancar_no_horario_recusam_o_substituido_dizendo_o_que_fazer()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);
        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        var confirmar = () => _agenda.ConfirmarPresencaAsync(horario, operador: "flavia@");
        (await confirmar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*segundo jogo de guias*");

        var lancar = () => _agenda.LancarNoHorarioAsync(horario, null, operador: "flavia@");
        (await lancar.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*OUTRO jogo de guias*");

        (await _db.Atendimentos.CountAsync(a => a.PacienteId == paciente)).Should().Be(1,
            "as duas portas recusaram ANTES de criar atendimento");
    }

    [Fact]
    public async Task Com_guia_no_agendamento_as_guias_proprias_do_horario_sao_suspensas_e_a_reabertura_devolve()
    {
        var paciente = await PacienteAsync();
        var prof = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(prof);
        await _db.SaveChangesAsync();
        var dia = Hoje.AddDays(-5);

        // Regime "guia no agendamento": o horário MARCADO já tem atendimento e guias.
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);
        var marcado = await _agenda.AgendarAsync(paciente, dia.ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: prof.Id, operador: "flavia@");
        marcado.AtendimentoId.Should().NotBeNull();

        // O balcão lançou a sessão por fora, como encaixe: um SEGUNDO jogo de guias.
        var avulso = await _agenda.LancarAvulsoAsync(paciente, dia.ToDateTime(new TimeOnly(9, 12)),
            ModalidadeAtendimento.AcupunturaComEletro, null, operador: "flavia@");

        var avisos = await _agenda.SubstituirPorSessaoAsync(marcado.Id, avulso.Lancamento.Atendimento.Id, "flavia@");

        var proprio = await _db.Atendimentos.AsNoTracking().Include(a => a.Codigos)
            .SingleAsync(a => a.Id == marcado.AtendimentoId);
        proprio.Codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel,
            "as guias do horário substituído eram o segundo jogo — suspensas como na falta");
        proprio.RealizadoEm.Should().BeNull();
        avisos.Should().ContainSingle(a => a.Contains("suspensa"));

        var encaixe = await _db.Atendimentos.AsNoTracking().Include(a => a.Codigos)
            .SingleAsync(a => a.Id == avulso.Lancamento.Atendimento.Id);
        encaixe.Codigos.Should().Contain(c => c.Status == StatusCodigo.Aberto,
            "as guias da sessão de verdade ficam");

        // Reabrir devolve — o mesmo caminho da falta.
        await _agenda.RemarcarAsync(marcado.Id, Hoje.AddDays(3).ToDateTime(new TimeOnly(9, 0)), null,
            operador: "flavia@");
        proprio = await _db.Atendimentos.AsNoTracking().Include(a => a.Codigos)
            .SingleAsync(a => a.Id == marcado.AtendimentoId);
        proprio.Codigos.Should().Contain(c => c.Status == StatusCodigo.Aberto);
    }

    /// <summary>
    /// Estornar a sessão que substituiu o horário devolve o horário à pergunta: a sessão que
    /// o encerrou deixou de existir, e um horário "encerrado por nada" seria a ponta solta
    /// que a conciliação nunca mais veria.
    /// </summary>
    [Fact]
    public async Task Estornar_a_sessao_substituta_reabre_o_horario()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);
        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        var estorno = new EstornoAtendimentoService(_repo);
        var r = await estorno.EstornarAsync(sessao, new DecisaoDeEstorno("Lançado no paciente errado"), "flavia@");

        r.Avisos.Should().ContainSingle(a => a.Contains("voltou"));
        var ag = await HorarioAsync(horario);
        ag.Status.Should().Be(StatusAgendamento.Agendado);
        ag.AtendimentoSubstitutoId.Should().BeNull();
        // Dois parados agora, e os dois são a verdade: o horário reaberto E o encaixe que o
        // estorno soltou (ele também fica sem sessão). A conciliação pergunta pelos dois.
        (await _conciliacao.LevantarAsync(Hoje)).Parados.Select(p => p.AgendamentoId)
            .Should().Contain(horario, "o horário voltou a ser pergunta");
    }

    [Fact]
    public async Task Horario_posterior_substituido_nao_cobre_o_retorno_a_marcar()
    {
        var paciente = await PacienteAsync();
        var dia = Hoje.AddDays(-5);
        var (horario, sessao) = await CasoRealAsync(paciente, dia);

        (await _repo.HorariosAtivosDosPacientesAsync([paciente], dia)).Should().HaveCount(2,
            "antes: o horário parado e o encaixe");

        await _agenda.SubstituirPorSessaoAsync(horario, sessao, "flavia@");

        (await _repo.HorariosAtivosDosPacientesAsync([paciente], dia)).Should().ContainSingle(
            "a lista é POSITIVA (agendado, realizado): o substituído não é 'horário posterior'");
    }
}
