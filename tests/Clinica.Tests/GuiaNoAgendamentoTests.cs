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
/// O regime "A GUIA NASCE QUANDO O HORÁRIO ENTRA NO SISTEMA" (parcela 70 —
/// docs/guia-no-agendamento.md), atrás da chave <c>GuiaNoAgendamento</c>.
///
/// O que estes testes fixam é o ciclo de vida completo: a guia nasce na marcação (e NÃO
/// é pendência enquanto a data não chega), a presença só carimba, o no-show suspende, a
/// reabertura devolve, a remarcação desloca, a mudança de modalidade regera — e a chave
/// DESLIGADA preserva o regime atual byte a byte, porque a janela de atualização dos
/// dois apps é o desenho.
/// </summary>
public class GuiaNoAgendamentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AgendaService _agenda;

    public GuiaNoAgendamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _agenda = new AgendaService(
            _repo, new AtendimentoService(_repo, parametros: _parametros), _parametros);
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task LigarChaveAsync() => _parametros.DefinirGuiaNoAgendamentoAsync(true);

    private static DateTime SemanaQueVem => DateTime.Today.AddDays(7).AddHours(14);

    [Fact]
    public async Task Com_a_chave_DESLIGADA_marcar_nao_cria_atendimento_nenhum()
    {
        var pacienteId = await CriarPacienteAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);

        ag.AtendimentoId.Should().BeNull(
            "desligada, a chave preserva o regime atual — a janela de atualização dos dois apps é o desenho");
        (await _db.Atendimentos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Com_a_chave_LIGADA_a_guia_nasce_na_marcacao_e_nao_e_pendencia()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);

        ag.AtendimentoId.Should().NotBeNull("o pedido da direção: a guia nasce quando o horário entra");
        var atendimento = await _db.Atendimentos.Include(a => a.Codigos).SingleAsync();
        atendimento.Numero.Should().NotBeNullOrEmpty();
        atendimento.RealizadoEm.Should().BeNull("a sessão ainda não aconteceu — quem carimba é a presença");
        atendimento.Codigos.Should().NotBeEmpty();

        // Guia de sessão futura NÃO é pendência: a secretária pode efetivá-la cedo, e o
        // painel só a cobra quando a data prevista chega.
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        atendimento.Codigos.Should().OnlyContain(c => !c.EstaPendente(hoje));
    }

    [Fact]
    public async Task Confirmar_a_presenca_so_carimba_e_nao_duplica_codigo_nenhum()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, DateTime.Today.AddHours(9), ModalidadeAtendimento.AcupunturaComEletro, null);
        var antes = await _db.Codigos.CountAsync();

        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "ana");

        (await _db.Codigos.CountAsync()).Should().Be(antes, "a guia já existia — a presença não cria nada");
        resultado.Atendimento.Id.Should().Be(ag.AtendimentoId!.Value);
        resultado.Atendimento.RealizadoEm.Should().NotBeNull("a presença é o que carimba a sessão como realizada");
        (await _db.Agendamentos.SingleAsync(a => a.Id == ag.Id))
            .Status.Should().Be(StatusAgendamento.Realizado);
    }

    [Fact]
    public async Task Cancelar_suspende_as_guias_abertas_e_reabrir_as_devolve()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);

        var avisos = await _agenda.CancelarAsync(ag.Id, "ana");

        avisos.Should().Contain(a => a.Contains("suspensa"));
        var codigos = await _db.Codigos.AsNoTracking().ToListAsync();
        codigos.Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel,
            "sessão que não aconteceu não fatura — e não pode virar pendência eterna");
        codigos.Should().OnlyContain(c => c.ObservacaoPendencia!.StartsWith("Sessão não realizada"));
        (await _db.Atendimentos.AsNoTracking().SingleAsync()).RealizadoEm.Should().BeNull();

        // Remarcar o cancelado traz o horário de volta — e as guias JUNTO.
        _db.ChangeTracker.Clear();
        var devolvidos = new List<string>();
        await _agenda.RemarcarAsync(ag.Id, SemanaQueVem.AddDays(1), null,
            operador: "ana", avisosGuia: devolvidos);

        devolvidos.Should().Contain(a => a.Contains("voltaram a valer"));
        (await _db.Codigos.AsNoTracking().ToListAsync())
            .Should().OnlyContain(c => c.Status == StatusCodigo.Aberto);
    }

    [Fact]
    public async Task Falta_suspende_como_o_cancelamento()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, DateTime.Today.AddHours(9), ModalidadeAtendimento.AcupunturaComEletro, null);

        await _agenda.MarcarFaltaAsync(ag.Id, "ana");

        (await _db.Codigos.AsNoTracking().ToListAsync())
            .Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
    }

    [Fact]
    public async Task Guia_ja_baixada_nao_se_toca_no_cancelamento_e_o_aviso_diz()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);

        // A secretária efetivou a 1ª guia no portal com antecedência (a feature!)…
        var baixada = await _db.Codigos.FirstAsync();
        baixada.DataBaixa = DateOnly.FromDateTime(DateTime.Today);
        await _db.SaveChangesAsync();

        // …e a sessão caiu.
        var avisos = await _agenda.CancelarAsync(ag.Id, "ana");

        avisos.Should().Contain(a => a.Contains("BAIXADA"),
            "guia efetivada de sessão que caiu é decisão humana — o sistema avisa, nunca mexe");
        (await _db.Codigos.AsNoTracking().SingleAsync(c => c.Id == baixada.Id))
            .DataBaixa.Should().NotBeNull();
    }

    [Fact]
    public async Task Remarcar_a_data_desloca_as_previstas_das_guias_abertas()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);
        var previstasAntes = await _db.Codigos.AsNoTracking()
            .OrderBy(c => c.Id).Select(c => c.DataPrevistaFaturamento).ToListAsync();

        _db.ChangeTracker.Clear();
        await _agenda.RemarcarAsync(ag.Id, SemanaQueVem.AddDays(3), null, operador: "ana");

        var previstasDepois = await _db.Codigos.AsNoTracking()
            .OrderBy(c => c.Id).Select(c => c.DataPrevistaFaturamento).ToListAsync();
        previstasDepois.Should().Equal(previstasAntes.Select(d => d.AddDays(3)),
            "as regras derivam a prevista da data por deslocamentos fixos — o delta preserva o desenho");
        (await _db.Atendimentos.AsNoTracking().SingleAsync())
            .Data.Should().Be(DateOnly.FromDateTime(SemanaQueVem.AddDays(3)));
    }

    [Fact]
    public async Task Mudar_a_modalidade_regera_as_guias_e_aposenta_as_antigas_marcadas()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null,
            modalidadeCodigo: ModalidadeAtendimento.AcupunturaComEletro.ToString());
        var antigas = await _db.Codigos.AsNoTracking().Select(c => c.Id).ToListAsync();

        _db.ChangeTracker.Clear();
        var avisos = new List<string>();
        await _agenda.RemarcarAsync(ag.Id, SemanaQueVem, null,
            modalidadeCodigo: ModalidadeAtendimento.AcupunturaSimples.ToString(),
            operador: "ana", avisosGuia: avisos);

        avisos.Should().Contain(a => a.Contains("regeradas"));
        var codigos = await _db.Codigos.AsNoTracking().ToListAsync();
        codigos.Where(c => antigas.Contains(c.Id)).Should().OnlyContain(
            c => c.Status == StatusCodigo.NaoAplicavel
                 && c.ObservacaoPendencia!.Contains("Substituída"),
            "a guia antiga aparece MARCADA, nunca some — e a marca não é a da suspensão, "
            + "para a reabertura de um cancelamento não a reviver");
        codigos.Where(c => !antigas.Contains(c.Id)).Should().NotBeEmpty()
            .And.OnlyContain(c => c.Status == StatusCodigo.Aberto);
    }

    [Fact]
    public async Task Mudar_a_modalidade_com_guia_baixada_e_recusado()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null,
            modalidadeCodigo: ModalidadeAtendimento.AcupunturaComEletro.ToString());
        var baixada = await _db.Codigos.FirstAsync();
        baixada.DataBaixa = DateOnly.FromDateTime(DateTime.Today);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var acao = () => _agenda.RemarcarAsync(ag.Id, SemanaQueVem, null,
            modalidadeCodigo: ModalidadeAtendimento.AcupunturaSimples.ToString(), operador: "ana");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já baixada*");
    }

    [Fact]
    public async Task A_serie_nasce_com_guias_em_todas_as_sessoes()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var serie = await _agenda.AgendarSerieAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaSimples, quantidade: 3);

        serie.Marcados.Should().HaveCount(3);
        (await _db.Atendimentos.CountAsync()).Should().Be(3,
            "marcar dez sessões é registrar dez atendimentos — cada um com a sua guia");
        serie.Marcados.Should().OnlyContain(a => a.AtendimentoId != null);
    }

    [Fact]
    public async Task Ligar_a_chave_faz_o_backfill_do_RealizadoEm()
    {
        // Uma linha gravada por um app ANTIGO depois da migration: realizada, sem carimbo.
        var pacienteId = await CriarPacienteAsync();
        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = pacienteId,
            Data = DateOnly.FromDateTime(DateTime.Today.AddDays(-3)),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            LancadoEm = DateTime.Now.AddDays(-3)
        });
        await _db.SaveChangesAsync();

        await LigarChaveAsync();

        (await _db.Atendimentos.AsNoTracking().SingleAsync()).RealizadoEm.Should().NotBeNull(
            "no momento da ativação, tudo o que existe é por definição sessão realizada — "
            + "sem o backfill, os leitores de 'realizado' perderiam essas linhas para sempre");
    }

    /// <summary>
    /// A CAPA do dia (parcela 70, pedido literal da cliente): número do atendimento,
    /// modalidade legível, quem lançou e o placar das baixas — o que a pergunta de
    /// duplicidade mostra em vez de um número seco. Guia NaoAplicavel não conta.
    /// </summary>
    [Fact]
    public async Task Capa_do_dia_diz_numero_modalidade_autoria_e_baixas()
    {
        var pacienteId = await CriarPacienteAsync();
        var dia = DateOnly.FromDateTime(DateTime.Today);

        _db.Atendimentos.Add(new Atendimento
        {
            PacienteId = pacienteId,
            Data = dia,
            Modalidade = ModalidadeAtendimento.AcupunturaComEletro,
            Numero = "2026-000123",
            LancadoPor = "ana",
            LancadoEm = DateTime.Today.AddHours(9).AddMinutes(12),
            RealizadoEm = DateTime.Today.AddHours(9),
            Codigos =
            {
                new CodigoFaturamento
                {
                    Tipo = TipoCodigo.Acupuntura, DataPrevistaFaturamento = dia,
                    DataBaixa = dia, Status = StatusCodigo.Baixado
                },
                new CodigoFaturamento
                {
                    Tipo = TipoCodigo.Eletroacupuntura,
                    DataPrevistaFaturamento = dia.AddDays(1)
                },
                new CodigoFaturamento
                {
                    Tipo = TipoCodigo.Consulta, DataPrevistaFaturamento = dia,
                    Status = StatusCodigo.NaoAplicavel
                }
            }
        });
        await _db.SaveChangesAsync();

        var capas = await new AtendimentoService(_repo).CapasDoDiaAsync(pacienteId, dia);

        var capa = capas.Should().ContainSingle().Subject;
        capa.Numero.Should().Be("2026-000123");
        capa.Modalidade.Should().NotContain("ComEletro",
            "o identificador do enum não vaza para a tela (parcela 41)");
        capa.Lancamento.Should().Contain("ana");
        capa.ResumoGuias.Should().Be("2 guias — 1 já baixada pelo faturamento",
            "a NaoAplicavel não é guia que alguém vá baixar");
    }

    /// <summary>
    /// A marcação futura (chave ligada) entra na capa do DIA MARCADO: é o que faz a
    /// pergunta de duplicidade enxergar o horário já marcado antes de criar outro.
    /// </summary>
    [Fact]
    public async Task A_marcacao_futura_aparece_na_capa_do_dia_marcado()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();
        await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);

        var capas = await new AtendimentoService(_repo).CapasDoDiaAsync(
            pacienteId, DateOnly.FromDateTime(SemanaQueVem));

        capas.Should().ContainSingle()
            .Which.ResumoGuias.Should().Contain("nenhuma baixada");
    }

    /// <summary>
    /// A marcação (chave ligada) NÃO entra no repasse por atendimento antes da presença —
    /// repasse é dinheiro, e "valor por atendimento" pagaria sessão que ninguém deu; a
    /// cancelada mantém o AtendimentoId (com as guias suspensas) e também fica fora.
    /// Achado da auditoria da parcela 70: o repasse não estava no inventário de leitores
    /// da Fase 3, e o alimentador dele filtrava só por "tem atendimento".
    /// </summary>
    [Fact]
    public async Task Marcacao_nao_entra_no_repasse_por_atendimento_antes_da_presenca()
    {
        var pacienteId = await CriarPacienteAsync();
        var prof = new Profissional { Nome = "Dra. Paula" };
        _db.Profissionais.Add(prof);
        await _db.SaveChangesAsync();
        _db.RegrasRepasse.Add(new RegraRepasse
        {
            ProfissionalId = prof.Id,
            Base = BaseRepasse.ValorPorAtendimento,
            ValorPorAtendimento = 50m
        });
        await _db.SaveChangesAsync();
        await LigarChaveAsync();

        var quando = SemanaQueVem;
        var ag = await _agenda.AgendarAsync(
            pacienteId, quando, ModalidadeAtendimento.AcupunturaComEletro, null,
            profissionalId: prof.Id);

        var inicio = DateOnly.FromDateTime(quando).AddDays(-1);
        var fim = DateOnly.FromDateTime(quando).AddDays(1);
        var repasses = new RepasseService(_repo);

        var antes = await repasses.CalcularAsync(inicio, fim);
        antes.Should().NotContain(r => r.ProfissionalId == prof.Id,
            "a sessão foi marcada, não realizada — pagar aqui seria pagar sessão que ninguém deu");

        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "ana");

        var depois = await repasses.CalcularAsync(inicio, fim);
        depois.Single(r => r.ProfissionalId == prof.Id).Valor.Should().Be(50m,
            "a presença confirmada é o que faz a sessão contar no repasse");
    }

    /// <summary>
    /// A corrida entre as duas máquinas do balcão (validação de ago/2026): o cartão "Em
    /// atendimento" da máquina A só relê a cada minuto, e a máquina B cancela o horário
    /// nesse meio tempo — as guias já estão SUSPENSAS. Confirmar por cima carimbaria
    /// Realizado sem devolver guia nenhuma: sessão realizada sem guia, calada, uma das
    /// duas coisas que a direção disse não aceitar. A recusa mora no SERVIÇO, porque as
    /// telas só guardam a metade visível.
    /// </summary>
    [Fact]
    public async Task Confirmar_presenca_de_horario_cancelado_e_recusado_e_nada_muda()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, DateTime.Today.AddHours(9), ModalidadeAtendimento.AcupunturaComEletro, null);
        await _agenda.CancelarAsync(ag.Id, "ana");
        _db.ChangeTracker.Clear();

        var acao = () => _agenda.ConfirmarPresencaAsync(ag.Id, operador: "bia");

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cancelado*");
        (await _db.Agendamentos.AsNoTracking().SingleAsync()).Status
            .Should().Be(StatusAgendamento.Cancelado);
        (await _db.Atendimentos.AsNoTracking().SingleAsync()).RealizadoEm.Should().BeNull(
            "a sessão não aconteceu — quem reabre o horário (e devolve as guias) é o Remarcar");
        (await _db.Codigos.AsNoTracking().ToListAsync())
            .Should().OnlyContain(c => c.Status == StatusCodigo.NaoAplicavel);
    }

    /// <summary>
    /// RELIGAR a chave não pode carimbar <c>RealizadoEm</c> em sessão marcada (nem na
    /// cancelada): "sem carimbo" só é sinônimo de "sessão antiga" na PRIMEIRA ativação.
    /// Desligar e religar é o ritual que a própria caixinha ensina ("desligue até
    /// atualizar a máquina X") — e o backfill cego transformaria toda sessão futura em
    /// visita que nunca houve, corrompendo retenção, origem e estreia sem sintoma.
    /// </summary>
    [Fact]
    public async Task Religar_a_chave_nao_carimba_a_sessao_marcada_nem_a_cancelada()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, null);
        var cancelada = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem.AddDays(1), ModalidadeAtendimento.AcupunturaComEletro, null);
        await _agenda.CancelarAsync(cancelada.Id, "ana");

        await _parametros.DefinirGuiaNoAgendamentoAsync(false);
        _db.ChangeTracker.Clear();
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);

        (await _db.Atendimentos.AsNoTracking().ToListAsync())
            .Should().OnlyContain(a => a.RealizadoEm == null,
                "sessão marcada e sessão cancelada não viraram visita por a chave piscar");
    }

    /// <summary>
    /// A especialidade da consulta VAI NA GUIA (é a informação que a operadora cobra), e
    /// trocar "Consulta/Psiquiatria" por "Consulta/Geriatria" mantém o código "Consulta"
    /// — só olhar a modalidade deixava o horário com a especialidade nova e a guia com a
    /// antiga, sem aviso nenhum.
    /// </summary>
    [Fact]
    public async Task Trocar_so_a_especialidade_da_consulta_regera_as_guias()
    {
        var pacienteId = await CriarPacienteAsync();
        await LigarChaveAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, SemanaQueVem, ModalidadeAtendimento.Consulta, null,
            modalidadeCodigo: ModalidadeAtendimento.Consulta.ToString(),
            especialidadeConsulta: Especialidade.Psiquiatria,
            especialidadeConsultaCodigo: Especialidade.Psiquiatria.ToString());
        var antigas = await _db.Codigos.AsNoTracking().Select(c => c.Id).ToListAsync();
        antigas.Should().NotBeEmpty();

        _db.ChangeTracker.Clear();
        var avisos = new List<string>();
        await _agenda.RemarcarAsync(ag.Id, SemanaQueVem, null,
            modalidadeCodigo: ModalidadeAtendimento.Consulta.ToString(),
            especialidadeConsultaCodigo: Especialidade.Geriatria.ToString(),
            operador: "ana", avisosGuia: avisos);

        avisos.Should().Contain(a => a.Contains("regeradas"));
        var atendimento = await _db.Atendimentos.AsNoTracking().SingleAsync();
        atendimento.EspecialidadeConsultaCodigo.Should().Be(Especialidade.Geriatria.ToString(),
            "o atendimento acompanha o horário — é dele que a guia sai");
        (await _db.Codigos.AsNoTracking().ToListAsync())
            .Where(c => !antigas.Contains(c.Id)).Should().NotBeEmpty()
            .And.OnlyContain(c => c.Status == StatusCodigo.Aberto);
    }

    /// <summary>
    /// O <c>primeiroCodigo</c> escolhido na tela vale para a SÉRIE inteira: a escolha é
    /// feita uma vez e a prévia a mostra — descartá-la faria as dez guias nascerem na
    /// ordem padrão da regra, contradizendo o que a tela prometeu (prévia que não bate
    /// com o lançamento é pior do que prévia nenhuma, parcela 47).
    /// </summary>
    [Fact]
    public async Task A_serie_respeita_a_escolha_do_primeiro_codigo()
    {
        var p = new Paciente
        {
            Nome = "Marta", Convenio = Convenio.UnimedPadrao, Sexo = Sexo.Feminino, PossuiApp = true
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        await LigarChaveAsync();

        var serie = await _agenda.AgendarSerieAsync(
            p.Id, SemanaQueVem, ModalidadeAtendimento.AcupunturaComEletro, quantidade: 2,
            primeiroCodigo: TipoCodigo.Eletroacupuntura);

        serie.Marcados.Should().HaveCount(2);
        var atendimentos = await _db.Atendimentos.Include(a => a.Codigos).AsNoTracking().ToListAsync();
        atendimentos.Should().HaveCount(2);
        foreach (var a in atendimentos)
            a.Codigos.Single(c => c.Ordem == OrdemCodigo.Primeiro).Tipo
                .Should().Be(TipoCodigo.Eletroacupuntura,
                    "a escolha da tela atravessa cada sessão da série");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    /// <summary>
    /// O Novo atendimento lança SOBRE o horário do dia (set/2026). Com a chave ligada o
    /// horário marcado JÁ tem atendimento e guias: a modalidade escolhida na tela regera
    /// as guias pelo MESMO caminho do Remarcar, a presença é confirmada e o atendimento da
    /// marcação é reaproveitado — nunca um segundo.
    /// </summary>
    [Fact]
    public async Task Lancar_sobre_o_horario_marcado_com_guia_regera_pela_modalidade_escolhida_e_reaproveita_o_atendimento()
    {
        await LigarChaveAsync();
        var pacienteId = await CriarPacienteAsync();
        var hoje14 = DateTime.Today.AddHours(14);
        var ag = await _agenda.AgendarAsync(
            pacienteId, hoje14, ModalidadeAtendimento.AcupunturaSimples, null, operador: "recepcao");
        ag.AtendimentoId.Should().NotBeNull("com a chave ligada a guia nasce na marcação");
        var daMarcacao = _db.Codigos.Count(c => c.AtendimentoId == ag.AtendimentoId);
        daMarcacao.Should().BeGreaterThan(0);

        var (mesmo, lancamento) = await _agenda.LancarNoHorarioAsync(
            ag.Id, null, modalidadeCodigo: nameof(ModalidadeAtendimento.AcupunturaComEletro), operador: "recepcao");

        mesmo.Id.Should().Be(ag.Id);
        mesmo.Status.Should().Be(StatusAgendamento.Realizado);
        lancamento.Atendimento.Id.Should().Be(ag.AtendimentoId!.Value,
            "o atendimento da marcação é reaproveitado, nunca um segundo");
        lancamento.Atendimento.Modalidade.Should().Be(ModalidadeAtendimento.AcupunturaComEletro);
        _db.Atendimentos.Count().Should().Be(1);
        _db.Agendamentos.Count().Should().Be(1);

        var codigos = _db.Codigos.Where(c => c.AtendimentoId == ag.AtendimentoId).ToList();
        codigos.Count(c => c.Status == StatusCodigo.NaoAplicavel).Should().Be(daMarcacao,
            "as guias da modalidade antiga foram substituídas, não apagadas");
        codigos.Count(c => c.Status == StatusCodigo.Aberto).Should().Be(2,
            "acupuntura + eletro gera dois códigos na Unimed Intercâmbio");
    }
}
