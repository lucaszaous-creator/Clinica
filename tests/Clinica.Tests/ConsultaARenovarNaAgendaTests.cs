using Clinica.Application.Formatacao;
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
/// O selo "Consulta vencida" / "Consulta a renovar" na agenda (nov/2026 — a direção: "a
/// secretária precisa ter visualmente que há uma consulta a renovar ou vencida").
///
/// O que estes testes fixam é QUANDO o selo fala — e quando ele se cala. Selo que acende
/// para todo mundo é selo que ninguém lê: ele só existe no horário em aberto, que vai
/// pelo convênio e que não é a própria consulta; e a data de referência é a do HORÁRIO.
/// </summary>
public class ConsultaARenovarNaAgendaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ConsultaService _consultas;

    public ConsultaARenovarNaAgendaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _consultas = new ConsultaService(new ClinicaRepositorio(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static readonly DateTime Dia = new(2026, 7, 16, 9, 0, 0);

    /// <summary>Paciente Unimed (validade de 22 dias) com a consulta emitida na data dada.</summary>
    private async Task<Paciente> PacienteAsync(DateOnly? emissao, Convenio convenio = Convenio.UnimedIntercambio)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        if (emissao is { } quando) await _consultas.RenovarAsync(p.Id, quando);
        return p;
    }

    private Agendamento Horario(Paciente p, DateTime? quando = null,
        ModalidadeAtendimento modalidade = ModalidadeAtendimento.AcupunturaSimples,
        bool particular = false, StatusAgendamento status = StatusAgendamento.Agendado,
        bool chegou = false, bool entrou = false)
    {
        var a = new Agendamento
        {
            PacienteId = p.Id, Paciente = p, DataHora = quando ?? Dia, DuracaoMinutos = 30,
            ModalidadePrevista = modalidade, Particular = particular, Status = status,
            ChegadaEm = chegou || entrou ? (quando ?? Dia).AddMinutes(-5) : null,
            InicioAtendimentoEm = entrou ? quando ?? Dia : null
        };
        _db.Agendamentos.Add(a);
        _db.SaveChanges();
        return a;
    }

    [Fact]
    public async Task Vencida_e_a_vencer_viram_selo_e_em_dia_nao()
    {
        var vencida = Horario(await PacienteAsync(new DateOnly(2026, 6, 1)));   // venceu 23/06
        var aVencer = Horario(await PacienteAsync(new DateOnly(2026, 6, 26)));  // vence 18/07 — dentro da janela de 5 dias
        var emDia = Horario(await PacienteAsync(new DateOnly(2026, 7, 10)));    // vence 01/08

        var selos = await _consultas.ARenovarNosHorariosAsync([vencida, aVencer, emDia]);

        selos.Keys.Should().BeEquivalentTo([vencida.Id, aVencer.Id]);
        selos[vencida.Id].SeloRenovacao.Should().Be("Consulta vencida");
        selos[vencida.Id].Vencida.Should().BeTrue();
        selos[aVencer.Id].SeloRenovacao.Should().Be("Consulta a renovar");
        selos[aVencer.Id].Vencida.Should().BeFalse();
    }

    [Fact]
    public async Task Quem_nunca_emitiu_consulta_nao_ganha_selo()
    {
        // Acenderia para a base inteira no primeiro dia — a mesma escolha do painel de pendências.
        var nunca = Horario(await PacienteAsync(emissao: null));

        (await _consultas.ARenovarNosHorariosAsync([nunca])).Should().BeEmpty();
    }

    [Fact]
    public async Task Sessao_particular_nao_cobra_consulta_nem_com_convenio_na_ficha()
    {
        var p = await PacienteAsync(new DateOnly(2026, 6, 1));
        var particular = Horario(p, particular: true);
        var peloConvenio = Horario(p, Dia.AddHours(1));

        var selos = await _consultas.ARenovarNosHorariosAsync([particular, peloConvenio]);

        selos.Keys.Should().BeEquivalentTo([peloConvenio.Id]);
    }

    [Fact]
    public async Task O_horario_de_CONSULTA_nao_cobra_a_renovacao_que_ele_mesmo_e()
    {
        var p = await PacienteAsync(new DateOnly(2026, 6, 1));
        var consulta = Horario(p, modalidade: ModalidadeAtendimento.Consulta);

        (await _consultas.ARenovarNosHorariosAsync([consulta])).Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusAgendamento.Cancelado)]
    [InlineData(StatusAgendamento.Faltou)]
    [InlineData(StatusAgendamento.Substituido)]
    [InlineData(StatusAgendamento.Realizado)]
    public async Task So_horario_em_aberto_ganha_selo(StatusAgendamento status)
    {
        var p = await PacienteAsync(new DateOnly(2026, 6, 1));

        (await _consultas.ARenovarNosHorariosAsync([Horario(p, status: status)])).Should().BeEmpty();
    }

    [Fact]
    public async Task No_local_e_em_atendimento_continuam_com_o_selo()
    {
        // É justamente com a pessoa na clínica que renovar custa uma frase.
        var p = await PacienteAsync(new DateOnly(2026, 6, 1));
        var noLocal = Horario(p, chegou: true);
        var atendendo = Horario(p, Dia.AddHours(1), entrou: true);
        noLocal.Etapa.Should().Be(EtapaFila.Chegou);
        atendendo.Etapa.Should().Be(EtapaFila.EmAtendimento);

        (await _consultas.ARenovarNosHorariosAsync([noLocal, atendendo])).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_referencia_e_o_dia_do_HORARIO_e_nao_hoje()
    {
        // Na grade da semana: em dia na segunda, vencida na sexta — é na sexta que fatura.
        var p = await PacienteAsync(new DateOnly(2026, 6, 26)); // vence 18/07
        var antes = Horario(p, new DateTime(2026, 7, 6, 9, 0, 0));
        var depois = Horario(p, new DateTime(2026, 7, 20, 9, 0, 0));

        var selos = await _consultas.ARenovarNosHorariosAsync([antes, depois]);

        selos.Keys.Should().BeEquivalentTo([depois.Id]);
        selos[depois.Id].Vencida.Should().BeTrue();
    }

    [Fact]
    public async Task A_agenda_concorda_com_a_aba_Consultas_sobre_o_mesmo_paciente()
    {
        var p = await PacienteAsync(new DateOnly(2026, 6, 1));
        var horario = Horario(p);

        var selo = (await _consultas.ARenovarNosHorariosAsync([horario]))[horario.Id];
        var daAba = (await _consultas.ListarAsync(DateOnly.FromDateTime(Dia))).Single(s => s.PacienteId == p.Id);

        selo.AvisoRenovacao.Should().Be(daAba.AvisoRenovacao);
        ConsultasNaTela.SituacaoFiltro(daAba).Should().Be(ConsultasNaTela.SituacaoPrecisaRenovar);
    }

    // ==================== o que a tela desenha ====================

    private static StatusConsultaPaciente Situacao(bool vencida) => new(
        7, "Paciente", Convenio.UnimedIntercambio, UsaConsulta: true,
        UltimaEmissao: new DateOnly(2026, 6, 1), Vencimento: vencida ? new DateOnly(2026, 6, 23) : new DateOnly(2026, 7, 18),
        DiasParaVencer: vencida ? -23 : 2, Vencida: vencida, PrecisaRenovar: true,
        Urgencia: vencida ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo);

    [Fact]
    public void Na_fila_vencida_e_vermelho_a_renovar_e_ambar_e_a_dica_traz_a_data()
    {
        var vencida = SelosDaFila.Montar(EtapaFila.Aguardando, false, false, false, null, null, null, null, Situacao(vencida: true));
        var aVencer = SelosDaFila.Montar(EtapaFila.Chegou, false, false, false, null, null, null, null, Situacao(vencida: false));

        vencida.Should().ContainSingle().Which.Should().BeEquivalentTo(new SeloFila("Consulta vencida", TomDoSelo.Erro,
            "Consulta vencida em 23/06/2026 — renove antes de faturar."));
        aVencer.Should().ContainSingle().Which.Tom.Should().Be(TomDoSelo.Aviso);
        aVencer[0].Texto.Should().Be("Consulta a renovar");
    }

    [Fact]
    public void O_selo_da_consulta_vem_depois_da_guia_e_antes_do_pacote_e_respeita_o_teto()
    {
        var selos = SelosDaFila.Montar(EtapaFila.Aguardando, termoPendente: true, termoAConferir: false,
            guiaPendente: true, sessoesUsadas: 10, sessoesContratadas: 10, atrasoMinutos: 5, encerradoEm: null,
            consulta: Situacao(vencida: true));

        selos.Select(s => s.Texto).Should().Equal("Termo pendente", "Guia pendente", "Consulta vencida");
    }

    [Fact]
    public void A_linha_do_dia_leva_o_selo_e_diz_a_frase_na_dica_mesmo_quando_o_teto_o_corta()
    {
        var p = new Paciente { Id = 7, Nome = "Ana", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        var a = new Agendamento
        {
            Id = 31, PacienteId = 7, Paciente = p, DataHora = Dia, DuracaoMinutos = 30,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples
        };
        var dados = DadosDaAgendaDoDia.Vazio(DateOnly.FromDateTime(Dia)) with
        {
            Horarios = [a],
            Consultas = new Dictionary<int, StatusConsultaPaciente> { [31] = Situacao(vencida: true) }
        };

        var linha = AgendaDoDiaNaTela.Montar(dados, Dia.AddMinutes(-30)).Single();

        linha.Selos.Select(s => s.Texto).Should().Contain("Consulta vencida");
        linha.ConsultaVencida.Should().BeTrue();
        linha.Detalhe.Should().Contain("Consulta vencida em 23/06/2026 — renove antes de faturar.");
    }

    [Fact]
    public void O_cartao_da_grade_leva_o_selo_o_tom_e_a_frase()
    {
        var p = new Paciente { Id = 7, Nome = "Ana", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        var a = new Agendamento { Id = 31, PacienteId = 7, Paciente = p, DataHora = Dia, DuracaoMinutos = 30 };
        var semNada = new Dictionary<int, RespostaDeConfirmacao>();

        var com = GradeDaAgendaNaTela.Cartao(a, semNada,
            consultas: new Dictionary<int, StatusConsultaPaciente> { [31] = Situacao(vencida: false) });
        var sem = GradeDaAgendaNaTela.Cartao(a, semNada);

        com.SeloDaConsulta.Should().Be("Consulta a renovar");
        com.ConsultaVencida.Should().BeFalse();
        com.AvisoDaConsulta.Should().StartWith("A consulta vence em 2 dia(s)");
        sem.SeloDaConsulta.Should().BeNull();
    }

    [Fact]
    public void O_Meu_dia_do_consultorio_leva_o_mesmo_selo_do_balcao()
    {
        SessaoDoDia Sessao(int id) => new(id, Dia, 7, "Ana", "Acupuntura", null,
            StatusAgendamento.Agendado, EtapaFila.Aguardando, false, null, null);
        var dia = new DiaDoProfissional(DateOnly.FromDateTime(Dia), 1, "Dra. Helena", [Sessao(31), Sessao(32)]);

        var linhas = MeuDiaNaTela.Montar(dia, perfilDeEnfermagem: false, DateOnly.FromDateTime(Dia),
            new Dictionary<int, StatusConsultaPaciente> { [31] = Situacao(vencida: true) });

        linhas[0].SeloDaConsulta.Should().Be("Consulta vencida");
        linhas[0].ConsultaVencida.Should().BeTrue();
        linhas[0].AvisoDaConsulta.Should().Be("Consulta vencida em 23/06/2026 — renove antes de faturar.");
        linhas[1].SeloDaConsulta.Should().BeNull();
    }

    [Fact]
    public void O_Inicio_conta_PACIENTES_e_nao_horarios_e_nunca_inventa_zero()
    {
        var doisHorariosDoMesmo = new Dictionary<int, StatusConsultaPaciente>
        {
            [31] = Situacao(vencida: true), [32] = Situacao(vencida: true)
        };

        InicioRecepcaoTela.ConsultasARenovarHoje(doisHorariosDoMesmo).Should().Be(1);
        InicioRecepcaoTela.ConsultasARenovarHoje(null).Should().BeNull();

        var lido = InicioRecepcaoTela.ItensDoBalcao(0, null, 0, consultas: 1).Single(i => i.Chave == "consultas");
        var falhou = InicioRecepcaoTela.ItensDoBalcao(0, null, 0, consultas: null).Single(i => i.Chave == "consultas");
        lido.Valor.Should().Be("1");
        lido.Detalhe.Should().Be("de pacientes que vêm hoje pelo convênio");
        falhou.Valor.Should().Be("—");
        falhou.Detalhe.Should().Be("não foi possível conferir");
    }
}
