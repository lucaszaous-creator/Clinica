using Clinica.Application.Abstracoes;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as quatro telas da Recepção migradas para Blazor (out/2026) AFIRMAM: Lançamentos (e o
/// estorno), Consultas de convênio, Pagamentos e a Visão geral da agenda. As regras saíram dos
/// ViewModels WPF — onde o <c>dotnet test</c> não alcançava — para a Application.
/// </summary>
public sealed class RecepcaoBlazorNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 14); // uma quarta-feira

    // ==================== Lançamentos ====================

    private static Atendimento Atendimento(params CodigoFaturamento[] codigos) => new()
    {
        Id = 7, Numero = "2026/0042", Data = Hoje, Modalidade = ModalidadeAtendimento.AcupunturaComEletro,
        Paciente = new Paciente { Nome = "Maria Souza", Convenio = Convenio.UnimedPadrao, ConvenioCodigo = "UnimedPadrao" },
        LancadoPor = "Ana", LancadoEm = Hoje.ToDateTime(new TimeOnly(14, 32)), Codigos = codigos.ToList()
    };

    [Fact]
    public void A_guia_que_libera_depois_vira_pendencia_com_a_data()
    {
        var l = LancamentosNaTela.Montar(Atendimento(
            new CodigoFaturamento { DataPrevistaFaturamento = Hoje },
            new CodigoFaturamento { DataPrevistaFaturamento = Hoje.AddDays(1) }));

        Assert.Equal("2 guias", l.Guias);
        Assert.Equal(2, l.QuantidadeGuias);
        Assert.True(l.TemPendencia);
        Assert.Equal("1 libera(m) a partir de 15/10", l.Pendencia);
        Assert.Equal("por Ana às 14:32", l.Lancamento);
        Assert.Equal("Ana", l.Operador);
        Assert.Equal("14/10", l.DataRotulo);
        Assert.Equal("2026/0042", l.Numero);
    }

    [Fact]
    public void Particular_sem_guia_nao_diz_todas_liberadas()
    {
        var l = LancamentosNaTela.Montar(Atendimento(new CodigoFaturamento { Status = StatusCodigo.NaoAplicavel, DataPrevistaFaturamento = Hoje }));
        Assert.Equal("sem guia", l.Guias);
        Assert.Equal("nada vai ao convênio", l.Pendencia);
        Assert.False(l.TemPendencia);
    }

    [Fact]
    public void Guia_ja_baixada_nao_e_pendencia_e_sem_operador_vira_opcao_de_filtro()
    {
        var a = Atendimento(new CodigoFaturamento { DataPrevistaFaturamento = Hoje.AddDays(1), DataBaixa = Hoje });
        a.LancadoPor = null;
        a.Numero = null;
        var l = LancamentosNaTela.Montar(a);
        Assert.Equal("todas liberadas", l.Pendencia);
        Assert.Equal(LancamentosNaTela.SemRegistroDeOperador, l.Operador);
        Assert.Equal("sem registro de quem lançou", l.Lancamento);
        Assert.Equal("#7", l.Numero);
    }

    [Fact]
    public void Periodo_invertido_e_ordenado_e_o_teto_recusa_com_a_frase()
    {
        var (inicio, fim, recusa) = LancamentosNaTela.Periodo(Hoje, Hoje.AddDays(-3));
        Assert.Equal(Hoje.AddDays(-3), inicio);
        Assert.Equal(Hoje, fim);
        Assert.Null(recusa);
        Assert.NotNull(LancamentosNaTela.Periodo(Hoje.AddDays(-400), Hoje).Recusa);
        Assert.Null(LancamentosNaTela.Periodo(Hoje.AddDays(-365), Hoje).Recusa);
    }

    [Fact]
    public void Volta_a_aba_so_rele_quando_o_periodo_inclui_hoje()
    {
        Assert.True(LancamentosNaTela.RelerAoVoltar(Hoje.AddDays(-2), Hoje, Hoje));
        Assert.False(LancamentosNaTela.RelerAoVoltar(Hoje.AddDays(-9), Hoje.AddDays(-2), Hoje));
    }

    [Fact]
    public void Filtro_e_opcoes_e_escolha_que_sumiu_volta_a_Todos()
    {
        var a = LancamentosNaTela.Montar(Atendimento(new CodigoFaturamento { DataPrevistaFaturamento = Hoje.AddDays(1) }));
        var b = a with { AtendimentoId = 8, Operador = "Bruno", TemPendencia = false, Convenio = "Amil" };
        var linhas = new[] { a, b };

        Assert.True(LancamentosNaTela.Passa(b, FiltroLancamentos.Nenhum));
        Assert.False(LancamentosNaTela.Passa(b, FiltroLancamentos.Nenhum with { SomenteComPendencia = true }));
        Assert.False(LancamentosNaTela.Passa(a, new FiltroLancamentos(LancamentosNaTela.Todos, null, "Bruno", false)));
        Assert.True(new FiltroLancamentos(null, null, "Bruno", false).Ativo);
        Assert.False(new FiltroLancamentos(LancamentosNaTela.Todos, LancamentosNaTela.Todos, null, false).Ativo);

        var opcoes = LancamentosNaTela.Opcoes(linhas.Select(l => l.Operador));
        Assert.Equal([LancamentosNaTela.Todos, "Ana", "Bruno"], opcoes);
        Assert.Equal(LancamentosNaTela.Todos, LancamentosNaTela.Preservar("Carla", opcoes));
        Assert.Equal("Ana", LancamentosNaTela.Preservar("Ana", opcoes));
        Assert.Equal("1 de 2 atendimento(s)", LancamentosNaTela.Resumo(1, 2));
        Assert.Equal(1, LancamentosNaTela.ComPendencia(linhas));
    }

    [Fact]
    public void O_recado_do_estorno_leva_os_avisos_das_reversoes()
    {
        Assert.EndsWith("caixa não pôde ser desfeito.", LancamentosNaTela.RecadoDoEstorno("2026/0042", ["O caixa não pôde ser desfeito."]));
        Assert.StartsWith("Atendimento nº 2026/0042 estornado.", LancamentosNaTela.RecadoDoEstorno("2026/0042", []));
    }

    [Fact]
    public void Estorno_so_oferece_o_que_o_atendimento_produziu_e_exige_motivo()
    {
        var simples = new PreviaDoEstorno(7, "2026/0042", "Maria", Hoje, "Acupuntura", 2, null, null, null, 0, null, null);
        Assert.Null(EstornoNaTela.RotuloCaixa(simples));
        Assert.Null(EstornoNaTela.RotuloInsumo(simples));
        Assert.Null(EstornoNaTela.ConsultaRenovada(simples));
        Assert.Null(EstornoNaTela.Horario(simples));
        Assert.Equal("2 guias serão anuladas (ficam no histórico, marcadas como estornadas).", EstornoNaTela.Guias(simples));
        Assert.False(EstornoNaTela.PodeEstornar(simples, " ", temPermissao: true));
        Assert.False(EstornoNaTela.PodeEstornar(simples, "engano", temPermissao: false));
        Assert.True(EstornoNaTela.PodeEstornar(simples, "engano", temPermissao: true));

        var completo = simples with { EntradaNoCaixa = 180m, InsumosBaixados = 1, ConsultaRenovadaEm = Hoje, AgendamentoId = 3 };
        Assert.Equal("Cancelar a entrada no caixa (R$ 180,00)", EstornoNaTela.RotuloCaixa(completo));
        Assert.Equal("Devolver 1 insumo ao estoque (entrada compensatória)", EstornoNaTela.RotuloInsumo(completo));
        Assert.Contains("14/10/2026", EstornoNaTela.ConsultaRenovada(completo));
        Assert.NotNull(EstornoNaTela.Horario(completo));

        var recusado = simples with { Impedimento = "A guia já foi baixada." };
        Assert.False(EstornoNaTela.PodeEstornar(recusado, "engano", temPermissao: true));
    }

    // ==================== Consultas de convênio ====================

    private static StatusConsultaPaciente Consulta(string nome, bool usa, DateOnly? emissao, bool precisa, NivelUrgencia u, int? dias = null)
        => new(1, nome, Convenio.UnimedPadrao, usa, emissao, emissao?.AddDays(22), dias, dias is < 0, precisa, u)
        { ConvenioCodigo = "UnimedPadrao" };

    [Fact]
    public void Situacao_de_filtro_e_frase_saem_do_mesmo_estado()
    {
        var naoUsa = Consulta("A", usa: false, null, false, NivelUrgencia.Verde);
        var nunca = Consulta("B", usa: true, null, precisa: true, NivelUrgencia.Vermelho);
        var emDia = Consulta("C", usa: true, Hoje, false, NivelUrgencia.Verde, 22);
        var breve = Consulta("D", usa: true, Hoje.AddDays(-19), false, NivelUrgencia.Amarelo, 3);

        Assert.Equal(ConsultasNaTela.SituacaoNaoUsa, ConsultasNaTela.SituacaoFiltro(naoUsa));
        Assert.Equal(ConsultasNaTela.SituacaoPrecisaRenovar, ConsultasNaTela.SituacaoFiltro(nunca));
        Assert.Equal(ConsultasNaTela.SituacaoEmDia, ConsultasNaTela.SituacaoFiltro(emDia));
        Assert.Equal(ConsultasNaTela.SituacaoVenceEmBreve, ConsultasNaTela.SituacaoFiltro(breve));
        Assert.Equal("O convênio não usa consulta renovável.", ConsultasNaTela.Situacao(naoUsa));
        Assert.Equal("Nunca emitiu consulta.", ConsultasNaTela.Situacao(nunca));
        Assert.Equal("Em dia.", ConsultasNaTela.Situacao(emDia));

        var ordem = ConsultasNaTela.Ordenar([emDia, breve, nunca]);
        Assert.Equal("B", ordem[0].Paciente); // quem precisa renovar vem primeiro
        Assert.Equal("nunca emitida", ordem[0].UltimaEmissao);
        Assert.True(ordem[0].EhVermelha);
        Assert.False(ConsultasNaTela.Linha(naoUsa).TemRenovacao);
    }

    [Fact]
    public void Resumo_e_vazio_dizem_quando_esta_filtrado()
    {
        Assert.Equal("3 de 90 paciente(s) no filtro · 5 precisam renovar no total.", ConsultasNaTela.Resumo(3, 90, 5, filtroAtivo: true));
        Assert.Equal("90 paciente(s) — nenhum precisa renovar hoje.", ConsultasNaTela.Resumo(90, 90, 0, false));
        Assert.Equal("Nenhum paciente de convênio com consulta renovável.", ConsultasNaTela.Resumo(0, 0, 0, false));
        Assert.Contains("filtro", ConsultasNaTela.VazioDescricao(true));
        Assert.DoesNotContain("filtro", ConsultasNaTela.VazioDescricao(false));
        Assert.True(ConsultasNaTela.FiltroAtivo(ConsultasNaTela.TodasSituacoes, ConsultasNaTela.TodosConvenios, "mar"));
        Assert.False(ConsultasNaTela.FiltroAtivo(ConsultasNaTela.TodasSituacoes, ConsultasNaTela.TodosConvenios, " "));
        var linha = ConsultasNaTela.Linha(Consulta("Márcia", true, Hoje, false, NivelUrgencia.Verde, 22));
        Assert.True(ConsultasNaTela.Passa(linha, ConsultasNaTela.TodasSituacoes, ConsultasNaTela.TodosConvenios, "marcia"));
        Assert.False(ConsultasNaTela.Passa(linha, ConsultasNaTela.SituacaoPrecisaRenovar, ConsultasNaTela.TodosConvenios, null));
    }

    // ==================== Pagamentos ====================

    [Fact]
    public void Pagamentos_dizem_situacao_e_somam_so_o_em_aberto_em_reais()
    {
        var vencida = new LancamentoFinanceiro { Valor = 180m, Status = StatusLancamento.Previsto, DataVencimento = Hoje.AddDays(-1) };
        var aReceber = new LancamentoFinanceiro { Valor = 120.5m, Status = StatusLancamento.Previsto, DataVencimento = Hoje.AddDays(5) };
        var paga = new LancamentoFinanceiro { Valor = 250m, Status = StatusLancamento.Realizado, DataPagamento = Hoje, FormaPagamento = FormaPagamento.Pix };

        Assert.Equal("Vencido", PagamentosNaTela.Situacao(vencida, Hoje));
        Assert.Equal("A receber", PagamentosNaTela.Situacao(aReceber, Hoje));
        Assert.StartsWith("Recebido em 14/10/2026 · ", PagamentosNaTela.Situacao(paga, Hoje));
        Assert.True(PagamentosNaTela.Vencido(vencida, Hoje));
        Assert.False(PagamentosNaTela.Vencido(paga, Hoje));
        Assert.Equal("R$ 300,50 a receber · 2 cobrança(s) em aberto", PagamentosNaTela.Resumo([vencida, aReceber, paga]));
        Assert.True(PagamentosNaTela.QuemRecebe.HasFlag(Permissao.VenderPacote));
        Assert.True(PagamentosNaTela.QuemRecebe.HasFlag(Permissao.EditarFinanceiro));
        Assert.True(PagamentosNaTela.QuemRecebe.HasFlag(Permissao.LancarAtendimento));
    }

    // ==================== Visão geral da agenda ====================

    private static MedidaOcupacao Medida(int atend = 4, int ocupados = 120, int disponiveis = 480, int semJornada = 0, int fora = 0)
        => new(atend, 1, 2, ocupados, disponiveis, 60, semJornada, fora);

    [Fact]
    public void Periodos_comecam_na_segunda_e_andar_respeita_o_modo()
    {
        Assert.Equal((Hoje, Hoje), VisaoGeralDaAgendaNaTela.Periodo(ModoVisaoAgenda.Dia, Hoje));
        Assert.Equal((new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18)), VisaoGeralDaAgendaNaTela.Periodo(ModoVisaoAgenda.Semana, Hoje));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), VisaoGeralDaAgendaNaTela.Periodo(ModoVisaoAgenda.Mes, Hoje));
        Assert.Equal(Hoje.AddDays(7), VisaoGeralDaAgendaNaTela.Andar(ModoVisaoAgenda.Semana, Hoje, 1));
        Assert.Equal(Hoje.AddMonths(-1), VisaoGeralDaAgendaNaTela.Andar(ModoVisaoAgenda.Mes, Hoje, -1));
        Assert.Null(VisaoGeralDaAgendaNaTela.Andar(ModoVisaoAgenda.Dia, new DateOnly(2199, 12, 31), 1));
    }

    [Fact]
    public void Indicadores_sem_jornada_dizem_que_nao_ha_calculo()
    {
        Assert.Equal("2h", VisaoGeralDaAgendaNaTela.Tempo(120));
        Assert.Equal("1h 30min", VisaoGeralDaAgendaNaTela.Tempo(90));
        var r = Medida();
        Assert.Equal("25%", VisaoGeralDaAgendaNaTela.OcupacaoTexto(r));
        Assert.Equal("2h de 8h disponíveis", VisaoGeralDaAgendaNaTela.OcupacaoDetalhe(r));
        Assert.Equal("6h", VisaoGeralDaAgendaNaTela.LivreTexto(r));
        Assert.Equal("1 cancelados · 2 faltas", VisaoGeralDaAgendaNaTela.BloqueadoDetalhe(r));
        Assert.Null(VisaoGeralDaAgendaNaTela.TextoForaDaJornada(r));

        var sem = Medida(semJornada: 1, fora: 2);
        Assert.Equal("—", VisaoGeralDaAgendaNaTela.OcupacaoTexto(sem));
        Assert.Equal("—", VisaoGeralDaAgendaNaTela.LivreTexto(sem));
        Assert.Equal("1 sem horário de atendimento cadastrado", VisaoGeralDaAgendaNaTela.OcupacaoDetalhe(sem));
        Assert.Equal("Cadastre os horários de atendimento para calcular", VisaoGeralDaAgendaNaTela.LivreDetalhe(sem));
        Assert.StartsWith("2 atendimento(s) fora do horário de atendimento", VisaoGeralDaAgendaNaTela.TextoForaDaJornada(sem));
        Assert.Equal(0, VisaoGeralDaAgendaNaTela.Fracao(null));
        Assert.Equal(1, VisaoGeralDaAgendaNaTela.Fracao(130m));
    }

    private static OcupacaoAgenda Agenda(DateOnly inicio, DateOnly fim, IReadOnlyList<HorarioOcupacao> horarios,
        IReadOnlyList<VaoLivre>? vagas = null, IReadOnlyList<DiaFechado>? fechados = null, bool semJornada = false, int semJornadaResumo = 0)
    {
        var dias = Enumerable.Range(0, fim.DayNumber - inicio.DayNumber + 1)
            .Select(i => new DiaOcupacao(inicio.AddDays(i), Medida(atend: horarios.Count(h => DateOnly.FromDateTime(h.Inicio) == inicio.AddDays(i)))))
            .ToList();
        return new OcupacaoAgenda(inicio, fim, Hoje.ToDateTime(new TimeOnly(9, 5)), Medida(semJornada: semJornadaResumo), dias,
            [new ProfissionalOcupacao(1, "Dra. Ana", "Seg–Sex, 8h–17h", Medida(semJornada: semJornadaResumo))], horarios)
        { Vagas = vagas ?? [], Fechados = fechados ?? [], SemJornadaParaVagas = semJornada };
    }

    private static HorarioOcupacao Horario(DateOnly dia, int hora, bool ocupa = true, int? prof = 1)
        => new(hora, dia.ToDateTime(new TimeOnly(hora, 0)), dia.ToDateTime(new TimeOnly(hora, 50)), prof, "Dra. Ana", $"Paciente {hora}",
            "Acupuntura", ocupa ? "Agendado" : "Cancelado", ocupa);

    [Fact]
    public void Semana_junta_atendimentos_e_vaos_em_ordem_e_pula_o_domingo_vazio()
    {
        var (seg, dom) = VisaoGeralDaAgendaNaTela.Periodo(ModoVisaoAgenda.Semana, Hoje);
        var d = Agenda(seg, dom, [Horario(Hoje, 11), Horario(Hoje, 9, ocupa: false)],
            vagas: [new VaoLivre(Hoje, Hoje.ToDateTime(new TimeOnly(10, 0)), Hoje.ToDateTime(new TimeOnly(11, 0)))],
            fechados: [new DiaFechado(seg, "Agenda fechada · Feriado")]);

        var colunas = VisaoGeralDaAgendaNaTela.Semana(d, filtrando: true, hoje: Hoje);
        Assert.Equal(6, colunas.Count); // domingo vazio saiu
        var quarta = colunas.Single(c => c.Dia == Hoje);
        Assert.True(quarta.EhHoje);
        Assert.Equal("Qua 14", quarta.Titulo);
        Assert.Equal(["09:00", "10:00", "11:00"], quarta.Itens.Select(i => i.Hora));
        Assert.True(quarta.Itens[0].Inativo);
        Assert.True(quarta.Itens[1].EhVao);
        Assert.Equal("hoje · 2 atendimentos · 1 vaga", quarta.Subtitulo);
        Assert.Equal("Feriado", colunas[0].Subtitulo);
        Assert.Equal("Agenda fechada · Feriado", colunas[0].Fechado);

        Assert.Equal("Escolha um profissional para ver os horários livres", VisaoGeralDaAgendaNaTela.DicaDaSemana(false, d));
        Assert.Equal("Clique numa vaga para marcar ali", VisaoGeralDaAgendaNaTela.DicaDaSemana(true, d));
        Assert.Equal("Cadastre o horário de atendimento do profissional para ver os horários livres",
            VisaoGeralDaAgendaNaTela.DicaDaSemana(true, d with { SemJornadaParaVagas = true }));
        // O rótulo termina na última coluna MOSTRADA (o domingo vazio saiu) — como no WPF.
        Assert.Equal("12 a 17 de outubro", VisaoGeralDaAgendaNaTela.PeriodoCurto(ModoVisaoAgenda.Semana, Hoje, d, colunas[^1].Dia));
    }

    [Fact]
    public void Mes_comeca_com_celulas_vazias_ate_a_segunda_certa()
    {
        var (ini, fim) = VisaoGeralDaAgendaNaTela.Periodo(ModoVisaoAgenda.Mes, Hoje);
        var d = Agenda(ini, fim, [Horario(Hoje, 9), Horario(Hoje, 10), Horario(Hoje, 11), Horario(Hoje, 14)]);
        var cartoes = VisaoGeralDaAgendaNaTela.Calendario(d, Hoje);
        // 1º/10/2026 é quinta: três vazias (seg, ter, qua) antes dele.
        Assert.Equal(3, cartoes.TakeWhile(c => c.Vazio).Count());
        Assert.Equal(31 + 3, cartoes.Count);
        var dia14 = cartoes.Single(c => !c.Vazio && c.Dia == Hoje);
        Assert.True(dia14.Selecionado);
        Assert.Equal("14", dia14.Rotulo);
        Assert.True(dia14.TemMais);
        Assert.Equal("+1 atendimentos", dia14.Mais);
        Assert.Equal("Outubro de 2026", VisaoGeralDaAgendaNaTela.TituloPeriodo(ModoVisaoAgenda.Mes, Hoje, d));
    }

    [Fact]
    public void Sem_jornada_nomeia_quem_e_o_atendimento_sem_profissional()
    {
        var d = Agenda(Hoje, Hoje, [Horario(Hoje, 9, prof: null)], semJornadaResumo: 1);
        var texto = VisaoGeralDaAgendaNaTela.TextoSemJornada(d);
        Assert.NotNull(texto);
        Assert.Contains("Sem horário de atendimento cadastrado: Dra. Ana.", texto);
        Assert.Contains("sem profissional vinculado", texto);
        Assert.Null(VisaoGeralDaAgendaNaTela.TextoSemJornada(Agenda(Hoje, Hoje, [])));

        var linhas = VisaoGeralDaAgendaNaTela.HorariosDoDia(d);
        Assert.Equal("09:00", linhas[0].Inicio);
        Assert.Equal("até 09:50", linhas[0].Ate);
        var equipe = VisaoGeralDaAgendaNaTela.Equipe(d);
        Assert.Equal("Sem horário cadastrado", equipe[0].Livres);
        Assert.Equal("—", equipe[0].Percentual);
    }
}
