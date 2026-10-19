using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a GRADE da agenda AFIRMA (out/2026 — a tela virou Blazor e a montagem que morava no
/// <c>AgendaViewModel</c>/<c>AgendaPlanejamento</c>/<c>FiltroListaAgenda</c> WPF desceu para a
/// Application). Cada teste fixa um comportamento do WPF que a migração não pode perder: as
/// colunas (inativo, "sem profissional", "sem sala", a semana a partir da segunda), a linha do
/// tempo (janela esticada, continuação, cancelado que não cobre, linha do agora só hoje), o
/// fechamento pelo recurso da coluna, as raias das sobreposições, as vagas do período e a busca.
/// </summary>
public class GradeDaAgendaNaTelaTests
{
    // Quarta-feira, 15/10/2026, 11h.
    private static readonly DateTime Agora = new(2026, 10, 15, 11, 0, 0);
    private static readonly DateOnly Dia = DateOnly.FromDateTime(Agora);

    private static readonly Profissional Helena = new() { Id = 1, Nome = "Helena Prado", NomeCurto = "Dra. Helena" };
    private static readonly Profissional Marcos = new() { Id = 2, Nome = "Marcos Lima", NomeCurto = "Dr. Marcos" };

    private static int _id;

    private static Agendamento Ag(int hora, int minuto = 0, Profissional? prof = null, StatusAgendamento status = StatusAgendamento.Agendado,
        int duracao = 30, string? paciente = null, DateOnly? dia = null, Sala? sala = null, bool encaixe = false,
        ModalidadeAtendimento modalidade = ModalidadeAtendimento.AcupunturaSimples)
    {
        var id = Interlocked.Increment(ref _id);
        var p = new Paciente { Id = 5000 + id, Nome = paciente ?? $"Paciente {id}", Telefone = "(21) 98888-0000" };
        return new Agendamento
        {
            Id = id, PacienteId = p.Id, Paciente = p,
            ProfissionalId = prof?.Id, Profissional = prof,
            SalaId = sala?.Id, Sala = sala,
            DataHora = (dia ?? Dia).ToDateTime(new TimeOnly(hora, minuto)), DuracaoMinutos = duracao,
            ModalidadePrevista = modalidade, Status = status, Encaixe = encaixe
        };
    }

    private static LeituraDaGrade Leitura(IReadOnlyList<Agendamento> horarios, IReadOnlyList<Profissional>? profissionais = null,
        IReadOnlyList<Sala>? salas = null, IReadOnlyList<BloqueioAgenda>? bloqueios = null, bool verificados = true,
        IReadOnlyDictionary<int, RespostaDeConfirmacao>? confirmacoes = null)
        => new(horarios, [], profissionais ?? [Helena, Marcos], salas ?? [], bloqueios ?? [], verificados,
            confirmacoes ?? new Dictionary<int, RespostaDeConfirmacao>());

    private static FiltroDaGrade Filtro(int? profissional = null, bool semana = false, bool porSala = false, int? sala = null, string duracao = "30", DateOnly? dia = null)
        => new(dia ?? Dia, semana, porSala, profissional, sala, duracao);

    [Fact]
    public void Uma_coluna_por_profissional_ativo_mais_o_inativo_com_horario_e_o_sem_profissional()
    {
        var inativa = new Profissional { Id = 9, Nome = "Joana Sá", NomeCurto = "Dra. Joana" };
        var grade = GradeDaAgendaNaTela.Montar(Leitura([
            Ag(9, prof: Helena), Ag(10, prof: inativa), Ag(11), Ag(12, prof: Marcos, status: StatusAgendamento.Cancelado)
        ]), Filtro(), Agora);

        Assert.Equal(["Dra. Helena", "Dr. Marcos", "Dra. Joana (inativo)", "Sem profissional"], grade.Colunas.Select(c => c.Nome));
        // O cancelado não ocupa: o cabeçalho conta o que a agenda TEM.
        Assert.Equal("3 horário(s) no dia · 4 coluna(s)", grade.Resumo);
        Assert.Equal(4, grade.ColunasVisuais.Count);
    }

    [Fact]
    public void Com_profissional_filtrado_so_a_coluna_dele_sem_inativo_nem_orfao()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([Ag(9, prof: Helena), Ag(10, prof: Marcos), Ag(11)]), Filtro(profissional: Helena.Id), Agora);

        Assert.Equal(["Dra. Helena"], grade.Colunas.Select(c => c.Nome));
        Assert.Equal("1 horário(s) no dia · 1 coluna(s)", grade.Resumo);
    }

    [Fact]
    public void A_sessao_longa_cobre_a_faixa_seguinte_e_o_cancelado_nao_cobre()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([
            Ag(9, prof: Helena, duracao: 60), Ag(10, prof: Marcos, duracao: 60, status: StatusAgendamento.Cancelado)
        ]), Filtro(), Agora);

        var helena = 0; var marcos = 1;
        var nove30 = grade.Faixas.Single(f => f.Hora == new TimeOnly(9, 30));
        var dez30 = grade.Faixas.Single(f => f.Hora == new TimeOnly(10, 30));
        Assert.True(nove30.Celulas[helena].Continuacao);
        Assert.False(nove30.Celulas[helena].Livre);
        // O horário cancelado VAGOU: a meia hora seguinte volta a ser oferecida.
        Assert.False(dez30.Celulas[marcos].Continuacao);
        Assert.True(dez30.Celulas[marcos].Livre);
    }

    [Fact]
    public void A_janela_padrao_estica_para_o_horario_fora_dela_e_nunca_vai_a_meia_noite()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([Ag(6, 30, prof: Helena)]), Filtro(), Agora);

        Assert.Equal(new TimeOnly(6, 30), grade.Faixas[0].Hora);
        Assert.Equal(Agendamento.FechamentoPadraoGrade, grade.Faixas[^1].Hora);
        Assert.Equal(grade.Faixas.Count * 36d, grade.Altura, 3);
        Assert.Equal(0, grade.Regua[0].Topo);
    }

    [Fact]
    public void A_linha_do_agora_marca_a_faixa_de_hoje_so_no_modo_dia()
    {
        var hojeDia = GradeDaAgendaNaTela.Montar(Leitura([Ag(9, prof: Helena)]), Filtro(), Agora.AddMinutes(10));
        Assert.Equal(new TimeOnly(11, 0), hojeDia.Faixas.Single(f => f.Agora).Hora);
        Assert.Single(hojeDia.Regua, h => h.Agora);

        var outroDia = GradeDaAgendaNaTela.Montar(Leitura([]), Filtro(dia: Dia.AddDays(1)), Agora);
        Assert.DoesNotContain(outroDia.Faixas, f => f.Agora);

        var semana = GradeDaAgendaNaTela.Montar(Leitura([]), Filtro(semana: true), Agora);
        Assert.DoesNotContain(semana.Faixas, f => f.Agora);
    }

    [Fact]
    public void A_semana_comeca_na_segunda_e_conta_o_que_ocupa()
    {
        var segunda = new DateOnly(2026, 10, 12);
        var grade = GradeDaAgendaNaTela.Montar(Leitura([
            Ag(9, prof: Helena, dia: segunda), Ag(9, prof: Marcos, dia: segunda.AddDays(4)),
            Ag(10, prof: Marcos, dia: segunda.AddDays(4), status: StatusAgendamento.Faltou)
        ]), Filtro(semana: true), Agora);

        Assert.Equal(7, grade.Colunas.Count);
        Assert.Equal("seg 12/10", grade.Colunas[0].Nome);
        Assert.Equal("dom 18/10", grade.Colunas[6].Nome);
        Assert.Equal("2 horário(s) na semana de 12/10 a 18/10", grade.Resumo);
        Assert.All(grade.Colunas, c => Assert.Null(c.ProfissionalId));
    }

    [Fact]
    public void Na_semana_de_um_profissional_a_coluna_tem_dono_e_diz_as_ferias_dele()
    {
        var ferias = new BloqueioAgenda
        {
            ProfissionalId = Helena.Id, Profissional = Helena, Motivo = "Férias",
            Inicio = Dia.ToDateTime(TimeOnly.MinValue), Fim = Dia.AddDays(1).ToDateTime(TimeOnly.MinValue)
        };
        var grade = GradeDaAgendaNaTela.Montar(Leitura([], bloqueios: [ferias]), Filtro(profissional: Helena.Id, semana: true), Agora);

        var quarta = grade.Colunas.Single(c => c.Data == Dia);
        Assert.Equal(Helena.Id, quarta.ProfissionalId);
        Assert.Equal("Dra. Helena", quarta.Fechamentos);
        // O vão do dia de férias leva o motivo — e o da quinta, não.
        var vaoQuarta = grade.Faixas[0].Celulas[grade.Colunas.ToList().IndexOf(quarta)];
        Assert.Equal("Férias", vaoQuarta.Bloqueio);
        Assert.True(vaoQuarta.Bloqueada);
    }

    [Fact]
    public void Por_sala_o_sem_sala_e_coluna_de_primeira_classe_e_a_inativa_aparece()
    {
        var sala1 = new Sala { Id = 1, Nome = "Sala 1" };
        var removida = new Sala { Id = 7, Nome = "Sala 7" };
        var grade = GradeDaAgendaNaTela.Montar(Leitura([
            Ag(9, prof: Helena, sala: sala1), Ag(10, prof: Marcos), Ag(11, prof: Marcos, sala: removida)
        ], salas: [sala1]), Filtro(porSala: true), Agora);

        Assert.Equal(["Sala 1", "Sala 7 (inativa)", "Sem sala"], grade.Colunas.Select(c => c.Nome));
        Assert.False(grade.SemSalas);

        var semSalas = GradeDaAgendaNaTela.Montar(Leitura([Ag(9, prof: Helena)], salas: []), Filtro(porSala: true), Agora);
        Assert.True(semSalas.SemSalas);
    }

    [Fact]
    public void O_fechamento_da_sala_nao_fecha_o_vao_do_profissional_mas_o_da_clinica_fecha_todos()
    {
        var salaFechada = new BloqueioAgenda
        {
            SalaId = 3, Motivo = "Manutenção", Inicio = Dia.ToDateTime(new TimeOnly(14, 0)), Fim = Dia.ToDateTime(new TimeOnly(15, 0))
        };
        var feriado = new BloqueioAgenda
        {
            Motivo = "Feriado", Inicio = Dia.ToDateTime(new TimeOnly(16, 0)), Fim = Dia.ToDateTime(new TimeOnly(17, 0))
        };
        var grade = GradeDaAgendaNaTela.Montar(Leitura([], bloqueios: [salaFechada, feriado]), Filtro(), Agora);

        var quatorze = grade.Faixas.Single(f => f.Hora == new TimeOnly(14, 0));
        var dezesseis = grade.Faixas.Single(f => f.Hora == new TimeOnly(16, 0));
        Assert.All(quatorze.Celulas, c => Assert.Null(c.Bloqueio));
        Assert.All(dezesseis.Celulas, c => Assert.Equal("Feriado", c.Bloqueio));

        // O vão fechado vira UM bloco com a duração do fechamento, rotulado com o motivo.
        var bloco = grade.ColunasVisuais[0].Blocos.Single(b => b.Rotulo == "Feriado");
        Assert.Equal(60 * GradeDaAgendaNaTela.PixelsPorMinuto - 2, bloco.Altura, 3);
        Assert.True(bloco.Bloqueado);
    }

    [Fact]
    public void Desde_setembro_o_vao_fechado_continua_clicavel_salvo_a_trava_do_profissional()
    {
        var feriado = new BloqueioAgenda { Motivo = "Feriado", Inicio = Dia.ToDateTime(new TimeOnly(16, 0)), Fim = Dia.ToDateTime(new TimeOnly(17, 0)) };
        var travado = new Profissional { Id = 3, Nome = "Rui Teles", NomeCurto = "Dr. Rui", AgendaProtegida = true };
        var grade = GradeDaAgendaNaTela.Montar(Leitura([], profissionais: [Helena, travado], bloqueios: [feriado]), Filtro(), Agora);

        var dezesseis = grade.Faixas.Single(f => f.Hora == new TimeOnly(16, 0));
        Assert.True(dezesseis.Celulas[0].PodeMarcar);
        Assert.Equal("Marcar neste horário — atenção: Feriado", dezesseis.Celulas[0].DicaDoVao);
        Assert.False(dezesseis.Celulas[1].PodeMarcar);
    }

    [Fact]
    public void Horarios_sobrepostos_vao_para_raias_distintas_e_o_mais_so_existe_sem_trava()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([
            Ag(14, prof: Helena, duracao: 60), Ag(14, 30, prof: Helena, encaixe: true)
        ]), Filtro(profissional: Helena.Id), Agora);

        var cartoes = grade.ColunasVisuais[0].Blocos.Where(b => b.TemCartao).OrderBy(b => b.Topo).ToList();
        Assert.Equal(2, cartoes.Count);
        Assert.Equal([0, 1], cartoes.Select(c => c.Raia));
        Assert.All(cartoes, c => Assert.Equal(2, c.Raias));
        Assert.All(cartoes, c => Assert.True(c.PodeSobrepor));
        Assert.StartsWith("Encaixe · 14:30–15:00", cartoes[1].Cartao!.TituloPlanejamento);
    }

    [Fact]
    public void O_cartao_so_mostra_a_confirmacao_em_aguardando_e_nunca_escreve_sala_vazia()
    {
        var aguardando = Ag(14, prof: Helena);
        var chegou = Ag(15, prof: Helena);
        chegou.ChegadaEm = Agora;
        var confirmacoes = new Dictionary<int, RespostaDeConfirmacao>
        {
            [aguardando.Id] = new(StatusContato.Respondido, Agora.AddDays(-1), null, CanalContato.WhatsApp),
            [chegou.Id] = new(StatusContato.Respondido, Agora.AddDays(-1), null, CanalContato.WhatsApp)
        };
        var c1 = GradeDaAgendaNaTela.Cartao(aguardando, confirmacoes);
        var c2 = GradeDaAgendaNaTela.Cartao(chegou, confirmacoes);

        Assert.True(c1.ConfirmouPresenca);
        Assert.Equal(string.Empty, c2.ConfirmacaoRotulo);
        Assert.DoesNotContain("—", c1.Contexto);
        Assert.StartsWith("No local · ", c2.Contexto);
        Assert.Equal("Dra. Helena", c1.Profissional);
        Assert.EndsWith("sem autoria.", c1.Lancamento);
    }

    [Fact]
    public void Sem_os_fechamentos_verificados_nada_e_oferecido_e_o_vao_diz_que_nao_verificou()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([], verificados: false), Filtro(profissional: Helena.Id), Agora);

        Assert.True(grade.DisponibilidadeNaoVerificada);
        Assert.Empty(grade.Vagas);
        Assert.All(grade.ColunasVisuais[0].Blocos.Where(b => b.TemVao), b => Assert.Equal("Não verificado", b.Rotulo));
        Assert.Equal("Disponibilidade não verificada. Atualize a agenda.", grade.EstadoVagas);
    }

    [Fact]
    public void As_vagas_do_periodo_seguem_o_profissional_do_filtro_e_comecam_agora()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([Ag(11, 30, prof: Helena)]), Filtro(profissional: Helena.Id), Agora);

        Assert.NotEmpty(grade.Vagas);
        Assert.True(grade.Vagas[0].Inicio >= Agora);
        Assert.DoesNotContain(grade.Vagas, v => v.Inicio == Agora.Date.AddHours(11).AddMinutes(30));
        Assert.True(grade.Vagas.Count <= 10);
        Assert.Equal("Horários do período aberto. Ao agendar, os conflitos serão conferidos novamente.", grade.EstadoVagas);

        var semFiltro = GradeDaAgendaNaTela.Montar(Leitura([]), Filtro(), Agora);
        Assert.Empty(semFiltro.Vagas);
        Assert.Equal("Escolha um profissional para consultar vagas.", semFiltro.EstadoVagas);
    }

    [Fact]
    public void A_busca_casa_sem_acento_e_as_pilulas_contam_antes_do_clique()
    {
        var grade = GradeDaAgendaNaTela.Montar(Leitura([
            Ag(9, prof: Helena, paciente: "José Antônio"), Ag(10, prof: Helena, paciente: "Joana Prado", status: StatusAgendamento.Cancelado),
            Ag(11, prof: Marcos, paciente: "Carlos Dias")
        ]), Filtro(), Agora);
        var cartoes = grade.Colunas.SelectMany(c => c.Horarios).ToList();

        var busca = GradeDaAgendaNaTela.Recortar(cartoes, new FiltroAgendaDoDia(Busca: "jo"), carregando: false);
        Assert.Equal(2, busca.Visiveis.Count);
        Assert.Equal(2, busca.Contagens["todos"]);
        Assert.Equal(1, busca.Contagens["cancelado"]);
        Assert.Equal("2 de 3 horários no período selecionado", busca.Resumo);

        var semAcento = GradeDaAgendaNaTela.Recortar(cartoes, new FiltroAgendaDoDia(Busca: "jose antonio"), carregando: false);
        Assert.Single(semAcento.Visiveis);

        var nada = GradeDaAgendaNaTela.Recortar(cartoes, new FiltroAgendaDoDia(Busca: "zzz"), carregando: false);
        Assert.True(nada.NenhumCorresponde);

        var situacao = GradeDaAgendaNaTela.Recortar(cartoes, new FiltroAgendaDoDia(Situacao: "cancelado"), carregando: false);
        Assert.Single(situacao.Visiveis);
        // A contagem da pílula não depende da pílula escolhida.
        Assert.Equal(3, situacao.Contagens["todos"]);
    }

    [Fact]
    public void A_previa_da_serie_lista_ate_dez_datas_e_diz_quantas_sobram()
    {
        var datas = Enumerable.Range(0, 12).Select(i => new DateTime(2026, 10, 20, 9, 0, 0).AddDays(7 * i)).ToList();
        var frase = GradeDaAgendaNaTela.PreviaDaSerie("Ana", datas);

        Assert.StartsWith("Cancelar as 12 sessão(ões) ainda marcadas de Ana?", frase);
        Assert.Contains("20/10/2026 09:00", frase);
        Assert.Contains("… e mais 2.", frase);
        Assert.DoesNotContain("05/01/2027", frase);
    }

    [Fact]
    public void A_lista_de_espera_diz_o_que_mostra()
    {
        var horario = new DateTime(2026, 10, 15, 14, 0, 0);
        Assert.Equal("Quem chamar para 14:00 (2)", GradeDaAgendaNaTela.RotuloDaEspera(horario, 2));
        Assert.Equal("Lista de espera (0)", GradeDaAgendaNaTela.RotuloDaEspera(null, 0));
        Assert.Equal("Quem chamar para 15/10 14:00", GradeDaAgendaNaTela.TituloDaEspera(horario));
        Assert.StartsWith("Ninguém da lista serve para 15/10 14:00", GradeDaAgendaNaTela.EsperaVazia(horario));
        Assert.Equal("Ninguém na lista de espera.", GradeDaAgendaNaTela.EsperaVazia(null));
        Assert.Equal("qualquer profissional · tarde · hoje a 30/10",
            GradeDaAgendaNaTela.PreferenciasDaEspera(new ListaEspera { Periodo = PeriodoPreferido.Tarde, DisponivelAte = new DateOnly(2026, 10, 30) }));
    }
}
