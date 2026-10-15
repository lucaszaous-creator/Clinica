using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE AS TELAS DE RECEBÍVEIS E DE CONCILIAÇÃO AFIRMAM (out/2026, migração para Blazor).
///
/// As frases e o filtro moravam nos ViewModels WPF, que o `dotnet test` não alcança. Elas
/// decidem o que a pessoa faz a seguir: o "DEVIA TER CAÍDO" é a única linha de Recebíveis
/// que pede ação, e um filtro esquecido que respondesse "nenhuma guia esperando receita"
/// faria a clínica dar o mês por conciliado com guias pendentes.
/// </summary>
public class RecebiveisEConciliacaoNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 15);

    private static DepositoEsperado Esperado(DateOnly previsao, int quantidade = 1, decimal liquido = 100m)
        => new("Stone", previsao, liquido + 3m, 3m, liquido, quantidade, [1]);

    // ===================== Recebíveis =====================

    [Theory]
    [InlineData(-5, "2 crédito(s) · DEVIA TER CAÍDO faz 5 dia(s)")]
    [InlineData(0, "2 crédito(s) · cai hoje")]
    [InlineData(1, "2 crédito(s) · cai amanhã")]
    [InlineData(18, "2 crédito(s) · cai em 18 dia(s)")]
    public void O_prazo_do_deposito_e_dito_em_palavras(int dias, string esperado)
        => RecebiveisNaTela.DetalheDoEsperado(Esperado(Hoje.AddDays(dias), quantidade: 2), Hoje).Should().Be(esperado);

    [Fact]
    public void O_atraso_do_ja_creditado_continua_visivel_depois_de_o_dinheiro_entrar()
    {
        var atrasou = new DepositoConfirmado("Cielo", Hoje.AddDays(-3), Hoje.AddDays(-6), 100, 3, 97, 1, [1]);
        var adiantou = atrasou with { Creditado = Hoje.AddDays(-7) };
        var semPrevisao = atrasou with { Previsto = null };

        RecebiveisNaTela.DetalheDoConfirmado(atrasou).Should().Be("1 crédito(s) · caiu 3 dia(s) depois do previsto");
        RecebiveisNaTela.Atrasou(atrasou).Should().BeTrue();
        RecebiveisNaTela.DetalheDoConfirmado(adiantou).Should().Be("1 crédito(s) · caiu 1 dia(s) adiantado");
        RecebiveisNaTela.Atrasou(adiantou).Should().BeFalse();
        RecebiveisNaTela.DetalheDoConfirmado(semPrevisao).Should().EndWith("sem previsão registrada");
    }

    [Fact]
    public void O_resumo_dos_previstos_separa_o_que_atrasou()
    {
        var esperados = new[] { Esperado(Hoje.AddDays(-2), 3), Esperado(Hoje.AddDays(4)) };
        var r = RecebiveisService.Resumir(esperados, Hoje);

        RecebiveisNaTela.ResumoDosPrevistos(esperados.Length, r)
            .Should().Be("2 depósito(s) previsto(s) · 3 crédito(s) que a adquirente JÁ DEVIA TER DEPOSITADO.");
        RecebiveisNaTela.ResumoDosPrevistos(0, RecebiveisService.Resumir([], Hoje))
            .Should().Be("Nenhum recebimento de cartão pendente no período.");
    }

    /// <summary>"—" e não "R$ 0,00": não haver próximo depósito e ele ser zero são coisas diferentes.</summary>
    [Fact]
    public void Sem_proximo_deposito_o_cartao_diz_traco()
    {
        RecebiveisNaTela.Proximo(RecebiveisService.Resumir([], Hoje)).Should().Be("—");
        RecebiveisNaTela.Proximo(RecebiveisService.Resumir([Esperado(Hoje.AddDays(3))], Hoje)).Should().Be("18/10/2026");
    }

    [Fact]
    public void O_resumo_dos_confirmados_diz_a_janela_e_quantos_atrasaram()
    {
        var c = new DepositoConfirmado("Rede", Hoje.AddDays(-3), Hoje.AddDays(-6), 100, 3, 97, 1, [1]);
        RecebiveisNaTela.ResumoDosConfirmados([], 30).Should().Be("Nenhum depósito confirmado nos últimos 30 dias.");
        RecebiveisNaTela.ResumoDosConfirmados([c, c with { Previsto = c.Creditado }], 30)
            .Should().Be("2 depósito(s) confirmado(s) nos últimos 30 dias · 1 caiu(íram) depois do previsto.");
    }

    /// <summary>A data do crédito é INFORMADA — campo vazio não vira "hoje" em silêncio.</summary>
    [Fact]
    public void Sem_a_data_do_credito_a_confirmacao_nao_segue()
    {
        RecebiveisNaTela.ConferirDataDoCredito(null).Should().NotBeNull();
        RecebiveisNaTela.ConferirDataDoCredito(Hoje).Should().BeNull();
        RecebiveisNaTela.PerguntaDeConfirmar(Esperado(Hoje.AddDays(-5)), Hoje.AddDays(-1), Hoje)
            .Should().Contain("R$ 100,00").And.Contain("14/10/2026").And.Contain("DEVIA TER CAÍDO");
    }

    // ===================== Conciliação: filtro e resumo =====================

    [Fact]
    public void O_filtro_casa_operadora_paciente_sem_acento_trecho_da_guia_e_so_sem_valor()
    {
        var f = new FiltroConciliacao("Amil", "jose antonio", "112", SomenteSemValor: true);
        f.Ativo.Should().BeTrue();
        f.Casa("Amil", "José Antônio Pereira", "0041125", temValorProposto: false).Should().BeTrue();
        f.Casa("Amil", "José Antônio Pereira", "0041125", temValorProposto: true).Should().BeFalse("só as sem valor proposto");
        f.Casa("Sul América", "José Antônio Pereira", "0041125", false).Should().BeFalse("outra operadora");
        f.Casa("Amil", "José Antônio Pereira", "0049999", false).Should().BeFalse();
        FiltroConciliacao.Nenhum.Ativo.Should().BeFalse();
        FiltroConciliacao.Nenhum.Casa("Qualquer", "Qualquer", "—", true).Should().BeTrue();
    }

    [Fact]
    public void Os_convenios_do_filtro_sao_os_do_mes_e_a_escolha_sobrevive_quando_ainda_existe()
    {
        var opcoes = ConciliacaoNaTela.ConveniosDoMes(["Unimed", "Amil", "Unimed"]);
        opcoes.Should().Equal(ConciliacaoNaTela.TodosConvenios, "Amil", "Unimed");
        ConciliacaoNaTela.PreservarConvenio("Amil", opcoes).Should().Be("Amil");
        ConciliacaoNaTela.PreservarConvenio("Petrobras", opcoes).Should().Be(ConciliacaoNaTela.TodosConvenios);
        ConciliacaoNaTela.PreservarConvenio(null, opcoes).Should().Be(ConciliacaoNaTela.TodosConvenios);
    }

    /// <summary>"12 guia(s)" e "12 de 53 guia(s)" respondem perguntas diferentes.</summary>
    [Fact]
    public void O_resumo_DIZ_que_a_lista_esta_filtrada()
    {
        ConciliacaoNaTela.ResumoDasGuias(53, 12, 0, filtroAtivo: true).Should().StartWith("12 de 53 guia(s)");
        ConciliacaoNaTela.ResumoDasGuias(12, 12, 0, filtroAtivo: false).Should().StartWith("12 guia(s) efetivada(s)");
        ConciliacaoNaTela.ResumoDasGuias(53, 0, 0, filtroAtivo: true).Should().Be("Nenhuma das 53 guia(s) do mês bate com o filtro atual.");
        ConciliacaoNaTela.ResumoDasGuias(0, 0, 0, filtroAtivo: false).Should().Be("Nenhuma guia pendente de lançamento neste mês.");
        ConciliacaoNaTela.ResumoDasGuias(9, 9, 2, false).Should().EndWith("· 2 sem preço na tabela.");
        ConciliacaoNaTela.ResumoDasGuias(9, 9, 9, false).Should().Contain("informe o valor");
    }

    /// <summary>Os dois vazios pedem ações opostas: encerrar o mês ou limpar o filtro.</summary>
    [Fact]
    public void O_vazio_diz_quando_e_por_causa_do_filtro()
    {
        ConciliacaoNaTela.TituloDoVazio(true).Should().Be("Nenhuma guia bate com o filtro");
        ConciliacaoNaTela.DescricaoDoVazio(true, 53).Should().Contain("53 guia(s) pendente(s)");
        ConciliacaoNaTela.TituloDoVazio(false).Should().Be("Nenhuma guia baixada esperando receita");
    }

    // ===================== Conciliação: valor, glosa e retenção =====================

    [Fact]
    public void O_valor_proposto_vai_ao_campo_com_a_procedencia_e_sem_tabela_nao_se_inventa_valor()
    {
        var proposto = new PrecoProposto(62.5m, "tabela: Unimed · Eletroacupuntura — R$ 62,50");
        ConciliacaoNaTela.ValorDoCampo(proposto).Should().Be("62,5");
        ConciliacaoNaTela.Procedencia(proposto).Should().StartWith("tabela:");
        ConciliacaoNaTela.ValorDoCampo(PrecoProposto.Nenhum).Should().BeEmpty();
        ConciliacaoNaTela.Procedencia(PrecoProposto.Nenhum).Should().BeNull();
    }

    [Theory]
    [InlineData("145", true)]
    [InlineData("1.250,00", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void O_valor_digitado_precisa_ser_maior_que_zero(string texto, bool serve)
        => (ConciliacaoNaTela.LerValor(texto, out _) is null).Should().Be(serve);

    [Fact]
    public void A_guia_glosada_aparece_MARCADA_com_o_motivo()
    {
        var g = new GuiaSemLancamento(1, 1, 1, "Ana", Hoje, "001", Convenio.UnimedPadrao, TipoCodigo.Acupuntura)
        { Glosa = StatusGlosa.Glosada, DataGlosa = Hoje, MotivoGlosa = "Carteirinha vencida" };
        ConciliacaoNaTela.AvisoDeGlosa(g).Should().Be("Glosada em 15/10/2026 — Carteirinha vencida");
        ConciliacaoNaTela.PerguntaDeGuiaGlosada(g).Should().Contain("receita fantasma");
        ConciliacaoNaTela.AvisoDeGlosa(g with { Glosa = StatusGlosa.Recuperada }).Should().BeEmpty();
    }

    /// <summary>Sem retenção cadastrada a linha DIZ que não achou — líquido = bruto não passa por retenção zero.</summary>
    [Fact]
    public void A_retencao_e_dita_antes_de_gravar()
    {
        var retida = DeducoesRecebimento.Nenhuma with { ValorImposto = 15m } with { DetalheImposto = "IRRF 1,5%" };
        ConciliacaoNaTela.Retencao(1000m, retida).Should().Be("retém R$ 15,00 (IRRF 1,5%) · líquido R$ 985,00");
        ConciliacaoNaTela.Retencao(1000m, DeducoesRecebimento.Nenhuma).Should().Be("sem retenção cadastrada para este convênio");
        ConciliacaoNaTela.SucessoDaGuia(1000m, "Ana", retida).Should().Contain("R$ 15,00 retidos na fonte");
    }

    // ===================== Glosadas =====================

    private static ReceitaGlosada Receita(StatusLancamento status, DateOnly? atendimento = null)
        => new(7, 1, "Ana", Convenio.Amil, "001", Hoje, null, "1705", StatusGlosa.Glosada, Hoje.AddDays(-3),
            9, 145m, status, Hoje) { DataAtendimento = atendimento };

    [Theory]
    [InlineData(null, "—")]
    [InlineData(-3, "recurso vencido há 3 dia(s)")]
    [InlineData(0, "recurso vence hoje")]
    [InlineData(12, "12 dia(s) para recorrer")]
    public void O_prazo_do_recurso_e_o_que_decide_a_pressa(int? dias, string esperado)
        => ConciliacaoNaTela.PrazoDoRecurso(dias).Should().Be(esperado);

    [Fact]
    public void A_receita_ja_realizada_ganha_orientacao_e_o_motivo_cai_no_codigo()
    {
        var realizada = Receita(StatusLancamento.Realizado);
        ConciliacaoNaTela.OrientacaoDaReceita(realizada).Should().Contain("lance a devolução como saída");
        ConciliacaoNaTela.SituacaoDaReceita(realizada).Should().Be("receita já realizada");
        ConciliacaoNaTela.OrientacaoDaReceita(Receita(StatusLancamento.Previsto)).Should().BeNull();
        ConciliacaoNaTela.MotivoDaGlosa(realizada).Should().Be("1705");
        ConciliacaoNaTela.MotivoDaGlosa(realizada with { MotivoGlosaCodigo = null }).Should().Be("sem motivo registrado");
    }

    [Fact]
    public void O_resumo_das_glosadas_soma_so_o_que_ainda_e_previsto()
    {
        ConciliacaoNaTela.ResumoDasGlosadas([Receita(StatusLancamento.Previsto), Receita(StatusLancamento.Realizado)])
            .Should().Be("1 de 2 guia(s) glosada(s) ainda contam R$ 145,00 de receita que o convênio recusou.");
        ConciliacaoNaTela.ResumoDasGlosadas([Receita(StatusLancamento.Realizado)]).Should().Contain("todas com o dinheiro já recebido");
        ConciliacaoNaTela.ResumoDasGlosadas([]).Should().Be("Nenhuma guia glosada com receita lançada neste mês.");
    }

    /// <summary>A guia volta ao mês do ATENDIMENTO — sem dizer, quem cancela em outubro a procura em outubro.</summary>
    [Fact]
    public void Derrubar_diz_para_que_mes_a_guia_voltou()
    {
        var outubro = new DateOnly(2026, 10, 1);
        ConciliacaoNaTela.SucessoDeDerrubar(Receita(StatusLancamento.Previsto, new DateOnly(2026, 6, 20)), outubro)
            .Should().Contain("de junho/2026 (mês do atendimento)");
        ConciliacaoNaTela.SucessoDeDerrubar(Receita(StatusLancamento.Previsto, new DateOnly(2026, 10, 2)), outubro)
            .Should().EndWith("voltou para a conciliação.");
    }

    // ===================== Particulares =====================

    [Fact]
    public void O_particular_nao_e_pago_pela_operadora()
        => ConciliacaoNaTela.FormasDoParticular.Should().NotContain(FormaPagamento.Convenio)
            .And.Contain(ConciliacaoNaTela.FormaPadrao);

    [Fact]
    public void A_sessao_particular_confere_valor_forma_e_vencimento()
    {
        var sessao = Hoje;
        ConciliacaoNaTela.ConferirSessao("180", aReceber: false, FormaPagamento.Pix, null, sessao, out var v).Should().BeNull();
        v.Should().Be(180m);
        ConciliacaoNaTela.ConferirSessao("180", false, null, null, sessao, out _).Should().Contain("Diga como a sessão foi paga");
        ConciliacaoNaTela.ConferirSessao("180", true, null, sessao.AddDays(-1), sessao, out _).Should().Contain("anterior à sessão");
        ConciliacaoNaTela.ConferirSessao("180", true, null, null, sessao, out _).Should().Contain("vencimento");
        ConciliacaoNaTela.ConferirSessao("180", true, null, sessao.AddDays(30), sessao, out _).Should().BeNull();
        ConciliacaoNaTela.ConferirSessao("", false, FormaPagamento.Pix, null, sessao, out _).Should().Contain("maior que zero");
    }

    [Fact]
    public void O_resumo_dos_particulares_aponta_os_sem_preco()
    {
        ConciliacaoNaTela.ResumoDosParticulares(0, 0).Should().Be("Nenhuma sessão particular do mês sem dinheiro registrado.");
        ConciliacaoNaTela.ResumoDosParticulares(3, 0).Should().EndWith("valor proposto pela tabela.");
        ConciliacaoNaTela.ResumoDosParticulares(3, 1).Should().EndWith("1 sem preço na tabela: informe o valor.");
        ConciliacaoNaTela.SucessoDaSessao("Ana", 180m, Hoje).Should().Contain("a receber").And.Contain("15/10/2026");
    }
}
