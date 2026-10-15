using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As regras que as telas novas da Recepção AFIRMAM (out/2026, mockups aprovados): os vãos
/// livres e os dias fechados da Visão geral, o chip "Já marcados" dos retornos e o placar
/// da rodada de confirmação. Moram na Application — o projeto WPF não compila aqui.
/// </summary>
public class RecepcaoNovaUiTests
{
    private static readonly DateOnly Segunda = new(2026, 10, 12);
    private static readonly DateTime Antes = new(2026, 10, 11, 8, 0, 0);

    private static Profissional Helena() => new()
    {
        Id = 7, Nome = "Helena", AtendeDas = new TimeOnly(8, 0), AtendeAte = new TimeOnly(12, 0)
    };

    private static HorarioOcupacao Horario(DateTime inicio, int minutos, bool ocupa = true, int? profissional = 7)
        => new(1, inicio, inicio.AddMinutes(minutos), profissional, "Helena", "Paciente", "Acupuntura",
            ocupa ? "Agendado" : "Cancelado", ocupa);

    // ==================== Vãos livres ====================

    [Fact]
    public void Trechos_livres_contiguos_viram_um_vao_so()
    {
        var vaos = VaosDaAgenda.Calcular(Helena(), Segunda, Segunda,
            [Horario(Segunda.ToDateTime(new TimeOnly(8, 0)), 60)], [], Antes);

        // Das 9h às 12h livre: UM vão, que começa às 9h — não seis botões de meia hora.
        vaos.Should().ContainSingle();
        vaos[0].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(9, 0)));
        vaos[0].Ate.Should().Be(Segunda.ToDateTime(new TimeOnly(12, 0)));
        vaos[0].Rotulo.Should().Be("+ 09:00 livre");
    }

    [Fact]
    public void Cancelado_devolve_o_horario_e_bloqueio_fecha_o_trecho()
    {
        var bloqueio = new BloqueioAgenda
        {
            ProfissionalId = 7, Inicio = Segunda.ToDateTime(new TimeOnly(10, 0)),
            Fim = Segunda.ToDateTime(new TimeOnly(11, 0)), Motivo = "reunião"
        };
        var vaos = VaosDaAgenda.Calcular(Helena(), Segunda, Segunda,
            [Horario(Segunda.ToDateTime(new TimeOnly(8, 0)), 60, ocupa: false)], [bloqueio], Antes);

        vaos.Select(v => (v.Inicio.Hour, v.Ate.Hour)).Should().Equal((8, 10), (11, 12));
    }

    [Fact]
    public void Sem_horas_de_jornada_nao_ha_vao_nenhum_e_passado_nao_tem_vao()
    {
        var semJornada = new Profissional { Id = 7, Nome = "Helena" };
        VaosDaAgenda.Calcular(semJornada, Segunda, Segunda, [], [], Antes).Should().BeEmpty();

        var depoisDoDia = Segunda.AddDays(1).ToDateTime(new TimeOnly(8, 0));
        VaosDaAgenda.Calcular(Helena(), Segunda, Segunda, [], [], depoisDoDia).Should().BeEmpty();
    }

    [Fact]
    public void Hoje_comeca_agora()
    {
        var agora = Segunda.ToDateTime(new TimeOnly(10, 10));
        var vaos = VaosDaAgenda.Calcular(Helena(), Segunda, Segunda, [], [], agora);
        vaos.Should().ContainSingle();
        vaos[0].Inicio.Should().Be(Segunda.ToDateTime(new TimeOnly(10, 0)));
    }

    [Fact]
    public void Dia_fechado_exige_a_jornada_inteira_e_para_a_equipe_so_conta_o_da_clinica()
    {
        var ferias = new BloqueioAgenda
        {
            ProfissionalId = 7, Inicio = Segunda.ToDateTime(TimeOnly.MinValue),
            Fim = Segunda.AddDays(1).ToDateTime(TimeOnly.MinValue), Motivo = "férias"
        };
        VaosDaAgenda.Fechados(Helena(), Segunda, Segunda, [ferias])
            .Should().ContainSingle().Which.Motivo.Should().Be("Agenda fechada · férias");
        // As férias da Helena não fecham a agenda da equipe.
        VaosDaAgenda.Fechados(null, Segunda, Segunda, [ferias]).Should().BeEmpty();

        var feriado = new BloqueioAgenda
        {
            Inicio = Segunda.ToDateTime(TimeOnly.MinValue), Fim = Segunda.AddDays(1).ToDateTime(TimeOnly.MinValue),
            Motivo = "feriado municipal"
        };
        VaosDaAgenda.Fechados(null, Segunda, Segunda, [feriado]).Should().ContainSingle();
    }

    // ==================== Retornos: "Já marcados" ====================

    private static readonly DateOnly Hoje = new(2026, 9, 10);

    private static RetornoSugerido Sugestao(int paciente, DateOnly sessao, DateOnly retorno)
        => new(paciente, paciente, $"P{paciente}", "21999990000", sessao, retorno, null, 7, null, "Helena");

    [Fact]
    public void Ja_marcado_fica_no_chip_dele_com_a_data_e_sai_da_fila()
    {
        var sugestoes = new[]
        {
            Sugestao(1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 8)),
            Sugestao(2, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15))
        };
        var horarios = new[] { new HorarioPosterior(1, new DateTime(2026, 9, 9, 10, 0, 0)) };

        var todos = RetornosAMarcar.MontarComMarcados(sugestoes, horarios, Hoje);
        var fila = RetornosAMarcar.Montar(sugestoes, horarios, Hoje);

        fila.Select(l => l.PacienteId).Should().Equal(2);
        var marcado = todos.Single(l => l.PacienteId == 1);
        marcado.Faixa.Should().Be(FaixaRetorno.JaMarcado);
        marcado.SituacaoTexto.Should().Be("marcado para 09/09");
        todos.Single(l => l.PacienteId == 2).Faixa.Should().Be(FaixaRetorno.EstaSemana);
    }

    // ==================== Confirmações ====================

    [Fact]
    public void Rodada_classifica_cada_sessao_e_o_pedido_de_remarcar_e_resposta_propria()
    {
        RodadaDeConfirmacao.Classificar(StatusContato.Pendente, null, null).Should().Be(SituacaoConfirmacao.NaoAvisado);
        RodadaDeConfirmacao.Classificar(StatusContato.Enviado, DateTime.Now, null).Should().Be(SituacaoConfirmacao.AvisadoSemResposta);
        RodadaDeConfirmacao.Classificar(StatusContato.Respondido, DateTime.Now, "confirmou").Should().Be(SituacaoConfirmacao.Confirmou);
        RodadaDeConfirmacao.Classificar(StatusContato.Respondido, DateTime.Now, RodadaDeConfirmacao.RespostaRemarcar)
            .Should().Be(SituacaoConfirmacao.PediuRemarcar);
        RodadaDeConfirmacao.Classificar(StatusContato.Dispensado, null, "sem telefone").Should().Be(SituacaoConfirmacao.Dispensado);
    }

    [Fact]
    public void Progresso_nao_conta_o_dispensado_e_arredonda()
    {
        var (baseCalc, avisados, pct) = RodadaDeConfirmacao.Progresso([
            SituacaoConfirmacao.Confirmou, SituacaoConfirmacao.AvisadoSemResposta,
            SituacaoConfirmacao.NaoAvisado, SituacaoConfirmacao.Dispensado]);
        baseCalc.Should().Be(3);
        avisados.Should().Be(2);
        pct.Should().Be(67);
        RodadaDeConfirmacao.Progresso([]).Percentual.Should().Be(0);
    }

    [Fact]
    public void Titulo_do_dia_diz_amanha_hoje_ou_a_data()
    {
        var hoje = new DateOnly(2026, 10, 15);
        RodadaDeConfirmacao.TituloDoDia(new DateOnly(2026, 10, 16), hoje).Should().Be("Amanhã · sex, 16/10");
        RodadaDeConfirmacao.TituloDoDia(hoje, hoje).Should().StartWith("Hoje · ");
        RodadaDeConfirmacao.TituloDoDia(new DateOnly(2026, 10, 20), hoje).Should().Be("ter, 20/10");
    }

    // ==================== Início ====================

    private static Agendamento Horario(int id, int hora, int minuto = 0)
        => new()
        {
            Id = id, PacienteId = id, DataHora = Segunda.ToDateTime(new TimeOnly(hora, minuto)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            Paciente = new Paciente { Id = id, Nome = $"Paciente {id}", Telefone = "21999990000" },
            Profissional = new Profissional { Id = 7, Nome = "Helena" }
        };

    [Fact]
    public void Proximos_a_chegar_sao_quem_ainda_nao_entrou_na_ordem_da_hora()
    {
        var agora = Segunda.ToDateTime(new TimeOnly(14, 12));
        var naSala = Horario(1, 13); naSala.InicioAtendimentoEm = agora.AddMinutes(-20);
        var faltou = Horario(2, 13, 30); faltou.Status = StatusAgendamento.Faltou;
        var noLocal = Horario(3, 14); noLocal.ChegadaEm = agora.AddMinutes(-12);
        var confirmou = Horario(4, 14, 30);
        var avisado = Horario(5, 15);
        var semContato = Horario(6, 15, 30);
        var confirmacoes = new Dictionary<int, StatusContato> { [4] = StatusContato.Respondido, [5] = StatusContato.Enviado };

        var lista = InicioDoBalcao.Proximos([semContato, avisado, confirmou, noLocal, faltou, naSala], confirmacoes, agora);

        lista.Select(l => l.AgendamentoId).Should().Equal(3, 4, 5, 6);
        lista[0].Pilula.Should().Be("No local · espera 12 min");
        lista[0].Acao.Should().Be("Abrir");
        lista[1].Situacao.Should().Be(SituacaoChegada.Confirmou);
        lista[1].Acao.Should().Be("Chegou");
        lista[2].Pilula.Should().Be("Não confirmou");
        // Sem contato registrado não há pílula: "não confirmou" seria acusar quem nunca foi avisado.
        lista[3].Situacao.Should().Be(SituacaoChegada.Marcado);
        lista[3].Pilula.Should().BeEmpty();
        lista[3].Atendimento.Should().EndWith("· Helena");
    }

    [Fact]
    public void Saudacao_e_linha_do_dia_dizem_a_hora_e_o_que_falta()
    {
        InicioDoBalcao.Saudacao(new DateTime(2026, 10, 15, 9, 0, 0), "Ana Lima").Should().Be("Bom dia, Ana");
        InicioDoBalcao.Saudacao(new DateTime(2026, 10, 15, 14, 0, 0), null).Should().Be("Boa tarde");
        InicioDoBalcao.Saudacao(new DateTime(2026, 10, 15, 19, 0, 0), "Ana").Should().Be("Boa noite, Ana");
        InicioDoBalcao.LinhaDoDia(new DateOnly(2026, 10, 15), 28, 9)
            .Should().Be("Quinta-feira, 15 de outubro · 28 horários hoje, 9 ainda por chegar");
        InicioDoBalcao.LinhaDoDia(new DateOnly(2026, 10, 15), 0, 0).Should().EndWith("nenhum horário hoje");
        InicioDoBalcao.FaltasECancelamentos(2, 1).Should().Be("2 faltas · 1 cancelamento");
    }

    [Fact]
    public void Confirmar_amanha_sem_rodada_e_nulo_e_nao_zero()
    {
        InicioDoBalcao.ConfirmarAmanha([]).Valor.Should().BeNull();
        InicioDoBalcao.ConfirmarAmanha([]).ValorTexto.Should().Be("—");

        var avisados = InicioDoBalcao.ConfirmarAmanha([
            SituacaoConfirmacao.Confirmou, SituacaoConfirmacao.AvisadoSemResposta,
            SituacaoConfirmacao.AvisadoSemResposta, SituacaoConfirmacao.Dispensado]);
        avisados.Valor.Should().Be(2);
        avisados.Detalhe.Should().Be("2 sem resposta de 3 avisados");

        var faltaAvisar = InicioDoBalcao.ConfirmarAmanha([SituacaoConfirmacao.NaoAvisado, SituacaoConfirmacao.AvisadoSemResposta]);
        faltaAvisar.Valor.Should().Be(2);
        faltaAvisar.Detalhe.Should().Be("1 a avisar · 1 sem resposta");
    }

    // ==================== Pacientes ====================

    [Fact]
    public void Linha_do_paciente_diz_documento_idade_e_visitas()
    {
        var hoje = new DateOnly(2026, 10, 15);
        ListaDePacientes.Identificacao(new Paciente { Nome = "A", Documento = "12345678900", DataNascimento = new DateOnly(1972, 3, 1) }, hoje)
            .Should().Be("CPF 123.456.789-00 · 54 anos");
        ListaDePacientes.Identificacao(new Paciente { Nome = "B", Documento = "RG 12.345.678-9" }, hoje)
            .Should().Be("RG 12.345.678-9");
        ListaDePacientes.Identificacao(new Paciente { Nome = "C", DataNascimento = new DateOnly(1851, 1, 1) }, hoje)
            .Should().Be("sem documento · data de nascimento a conferir");

        var v = new VisitaDoPaciente(1, new DateTime(2026, 10, 8, 9, 0, 0), "Acupuntura", new DateTime(2026, 10, 15, 14, 0, 0), "Dra. Helena");
        ListaDePacientes.UltimaVisita(v, hoje).Should().Be("08/10 · Acupuntura");
        ListaDePacientes.ProximoHorario(v, hoje).Should().Be("hoje 14:00 · Dra. Helena");
        ListaDePacientes.ProximoHorario(v with { Proximo = new DateTime(2026, 10, 22, 9, 0, 0) }, hoje)
            .Should().Be("qui, 22/10 · Dra. Helena");
        ListaDePacientes.UltimaVisita(v with { Ultima = new DateTime(2025, 6, 11) }, hoje).Should().StartWith("11/06/25");
        ListaDePacientes.Rodape(6, "maria s", null).Should().Be("6 pacientes para “maria s” · Enter abre a ficha quando sobra um só");
        ListaDePacientes.DiasDoAniversario(new DateOnly(2026, 12, 29)).Should().Equal(1229, 1230, 1231, 101, 102, 103, 104);
    }
}
