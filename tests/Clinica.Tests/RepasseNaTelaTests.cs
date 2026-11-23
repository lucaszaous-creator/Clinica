using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas "Repasses" e "Repasse médico" AFIRMAM (nov/2026, repasse por guia recebida):
/// os cartões do topo, a situação de cada profissional, o agrupamento do demonstrativo, o que
/// acende cada botão e as frases da tabela de regras. Sob a cultura INVARIANTE de propósito —
/// é a da prévia em Linux, e o pt-BR tem de sair fixo mesmo assim.
/// </summary>
public class RepasseNaTelaTests
{
    private static T SobInvariante<T>(Func<T> f)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return f();
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    private static int _chave;

    private static ItemRepasseElegivel Item(decimal valor, string convenio = "Amil", string? referencia = "GRD 1",
        BloqueioItemRepasse bloqueio = BloqueioItemRepasse.Nenhum, NaturezaItemRepasse natureza = NaturezaItemRepasse.Credito,
        int diaRecebido = 10, string paciente = "Paciente", TipoCodigo? tipo = TipoCodigo.Consulta)
        => new($"G:{++_chave}", OrigemItemRepasse.Convenio, natureza, 1, _chave, _chave, _chave, null, 1, paciente,
            new DateOnly(2026, 10, 6), convenio, convenio, tipo, "Consulta", "123", referencia,
            new DateOnly(2026, 11, diaRecebido), 210m, bloqueio == BloqueioItemRepasse.SemRegra ? null : 1, "regra", valor, bloqueio);

    private static GuiaAguardandoConvenio Aguardando(bool prevista = false)
        => new(++_chave, _chave, "Paciente", new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4), "Amil", "Consulta", "999", prevista, false);

    private static PreviaRepasseProfissional Previa(IReadOnlyList<ItemRepasseElegivel> itens,
        IReadOnlyList<GuiaAguardandoConvenio>? aguardando = null, IReadOnlyList<RepasseFechamento>? fechados = null)
        => new(1, "Dra. Helena", "CRM-RJ 1", itens, aguardando ?? [], fechados ?? []);

    private static RepasseFechamento Fechamento(decimal valor, bool pago = false, bool cancelado = false) => new()
    {
        Id = ++_chave, ProfissionalNome = "Dra. Helena", Competencia = new DateOnly(2026, 9, 1), Valor = valor,
        ConferidoPor = "Ana", ConferidoEm = new DateTime(2026, 10, 2, 14, 10, 0),
        Lancamento = new LancamentoFinanceiro
        {
            Status = pago ? StatusLancamento.Realizado : StatusLancamento.Previsto,
            DataPagamento = pago ? new DateOnly(2026, 10, 5) : null
        },
        CanceladoEm = cancelado ? new DateTime(2026, 10, 12, 9, 0, 0) : null,
        MotivoCancelamento = cancelado ? "Fechado antes da hora" : null
    };

    // ==================== Totais ====================

    [Fact]
    public void Aguardando_e_contado_e_nunca_somado_e_bloqueado_nao_entra_no_a_fechar()
    {
        var helena = Previa(
            [Item(65m), Item(65m), Item(72m, bloqueio: BloqueioItemRepasse.SemRegra), Item(-65m, natureza: NaturezaItemRepasse.Estorno)],
            [Aguardando(), Aguardando(prevista: true)]);
        var t = RepasseNaTela.Totais([helena], [Fechamento(130m, pago: true), Fechamento(80m), Fechamento(500m, cancelado: true)]);

        t.AFechar.Should().Be(65m, "créditos prontos menos o estorno; o bloqueado não soma");
        t.ItensAFechar.Should().Be(2);
        t.Aguardando.Should().Be(2);
        t.AguardandoComReceitaPrevista.Should().Be(1);
        t.Fechado.Should().Be(210m, "cancelado não conta");
        t.Pago.Should().Be(130m);
        t.SemRegra.Should().Be(1);
        t.Bloqueados.Should().Be(1);
    }

    [Fact]
    public void Quem_nao_pode_fechar_nao_entra_no_a_fechar()
    {
        var negativo = Previa([Item(65m), Item(-130m, natureza: NaturezaItemRepasse.Estorno)]);
        RepasseNaTela.Totais([negativo], []).AFechar.Should().Be(0m);
    }

    [Fact]
    public void Aviso_dos_bloqueados_some_sem_bloqueio_e_nomeia_cada_motivo()
    {
        RepasseNaTela.AvisoDosBloqueados(RepasseNaTela.Totais([Previa([Item(65m)])], [])).Should().BeNull();
        var t = RepasseNaTela.Totais([Previa([
            Item(0m, bloqueio: BloqueioItemRepasse.SemRegra),
            Item(0m, bloqueio: BloqueioItemRepasse.SemProfissional),
            Item(0m, bloqueio: BloqueioItemRepasse.ApuradoNoModeloAntigo)])], []);
        RepasseNaTela.AvisoDosBloqueados(t).Should()
            .StartWith("3 recebimentos não podem entrar ainda:")
            .And.Contain("sem regra").And.Contain("sem profissional").And.Contain("apuração antiga");
    }

    // ==================== Situação e botões ====================

    [Fact]
    public void Situacao_do_profissional()
    {
        RepasseNaTela.Situacao(Previa([Item(65m)])).Should().Be(("A fechar", "info"));
        RepasseNaTela.Situacao(Previa([Item(0m, bloqueio: BloqueioItemRepasse.SemRegra)])).Should().Be(("Bloqueado", "aviso"));
        RepasseNaTela.Situacao(Previa([Item(-10m, natureza: NaturezaItemRepasse.Estorno)])).Texto.Should().Be("Saldo negativo");
        RepasseNaTela.Situacao(Previa([], fechados: [Fechamento(65m, pago: true)])).Should().Be(("Pago", "sucesso"));
        RepasseNaTela.Situacao(Previa([], fechados: [Fechamento(65m)])).Should().Be(("Conferido", "sucesso"));
        RepasseNaTela.Situacao(Previa([], [Aguardando()])).Texto.Should().Be("Aguardando o convênio");
    }

    [Fact]
    public void Dica_de_fechar_explica_o_botao_apagado_e_diz_o_que_fica_para_depois()
    {
        RepasseNaTela.DicaDeFechar(Previa([Item(65m)]), podeEditar: false).Should().Contain("não permite");
        RepasseNaTela.DicaDeFechar(Previa([]), true).Should().StartWith("Nada pronto para fechar");
        RepasseNaTela.DicaDeFechar(Previa([Item(0m, bloqueio: BloqueioItemRepasse.SemRegra)]), true).Should().Contain("bloqueados");
        SobInvariante(() => RepasseNaTela.DicaDeFechar(Previa([Item(65m), Item(0m, bloqueio: BloqueioItemRepasse.SemRegra)]), true))
            .Should().Be("Confere 1 guia (R$ 65,00) e cria a saída prevista no caixa. 1 item bloqueado fica para depois.");
    }

    [Fact]
    public void Pagamento_e_cancelamento_so_no_conferido_vivo_e_nao_pago()
    {
        var conferido = Fechamento(65m);
        var pago = Fechamento(65m, pago: true);
        var cancelado = Fechamento(65m, cancelado: true);
        RepasseNaTela.PodeRegistrarPagamento(conferido).Should().BeTrue();
        RepasseNaTela.PodeRegistrarPagamento(pago).Should().BeFalse();
        RepasseNaTela.PodeRegistrarPagamento(cancelado).Should().BeFalse();
        RepasseNaTela.PodeCancelar(conferido).Should().BeTrue();
        RepasseNaTela.PodeCancelar(pago).Should().BeFalse("pago não se cancela: estorna-se no Caixa antes");
        RepasseNaTela.DicaDeCancelar(pago, true).Should().Contain("estorne o pagamento no Caixa");
    }

    [Fact]
    public void Situacao_do_fechamento_diz_quando()
    {
        RepasseNaTela.SituacaoDoFechamento(Fechamento(65m)).Should().Be("Conferido");
        RepasseNaTela.SituacaoDoFechamento(Fechamento(65m, pago: true)).Should().Be("Pago em 05/10/2026");
        RepasseNaTela.SituacaoDoFechamento(Fechamento(65m, cancelado: true)).Should().Be("Cancelado em 12/10/2026");
        RepasseNaTela.Competencia(new DateOnly(2026, 9, 1)).Should().Be("09/2026");
    }

    // ==================== Agrupamento (o demonstrativo) ====================

    [Fact]
    public void Itens_agrupam_por_convenio_e_pagamento_com_estorno_fora_e_bloqueado_sem_somar()
    {
        var grupos = RepasseNaTela.Grupos([
            Item(80m, "Petrobras", "Conta 1"),
            Item(65m, "Amil", "GRD 2", diaRecebido: 12, paciente: "Bruno"),
            Item(65m, "Amil", "GRD 2", diaRecebido: 10, paciente: "Carla"),
            Item(0m, "Amil", "GRD 2", bloqueio: BloqueioItemRepasse.SemRegra),
            Item(65m, "Amil", null),
            Item(-65m, "Amil", "GRD 2", natureza: NaturezaItemRepasse.Estorno)]);

        grupos.Select(g => (g.Convenio, g.ReferenciaPagamento)).Should().Equal(
            ("Amil", "GRD 2"), ("Amil", null), ("Petrobras", "Conta 1"));
        grupos[0].Itens.Should().HaveCount(3, "o bloqueado aparece; o estorno vai para a linha própria");
        grupos[0].Itens[0].Paciente.Should().Be("Carla", "ordem do recebimento");
        grupos[0].Total.Should().Be(130m);
        RepasseNaTela.RotuloDaReferencia(null).Should().Be("Sem referência do pagamento");
        RepasseNaTela.RotuloDaReferencia(" GRD 2 ").Should().Be("Pagamento GRD 2");
    }

    // ==================== Tabela de regras ====================

    private static RegraRepasse Regra(decimal valor = 65m, bool ativa = true, DateOnly? de = null, DateOnly? ate = null,
        string? convenio = null, TipoCodigo? tipo = null) => new()
    {
        Id = ++_chave, ProfissionalId = 1, Profissional = new Profissional { Nome = "Dra. Helena Duarte", NomeCurto = "Dra. Helena" },
        Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = valor, Ativa = ativa, VigenteDe = de, VigenteAte = ate,
        ConvenioCodigo = convenio, TipoGuia = tipo
    };

    [Fact]
    public void Valor_da_regra_em_pt_BR_fixo()
    {
        SobInvariante(() => RepasseNaTela.ValorDaRegra(Regra(65m))).Should().Be("R$ 65,00 por guia");
        SobInvariante(() => RepasseNaTela.ValorDaRegra(new RegraRepasse { Base = BaseRepasse.PercentualDaReceita, Percentual = 37.5m }))
            .Should().Be("37,5% do recebido");
    }

    [Fact]
    public void Situacao_da_regra()
    {
        var hoje = new DateOnly(2026, 11, 20);
        RepasseNaTela.SituacaoDaRegra(Regra(), hoje).Should().Be(("Vale hoje", "sucesso"));
        RepasseNaTela.SituacaoDaRegra(Regra(ativa: false), hoje).Texto.Should().Be("Desativada");
        RepasseNaTela.SituacaoDaRegra(Regra(ate: new DateOnly(2026, 11, 30)), hoje).Texto.Should().Be("Vale hoje");
        RepasseNaTela.SituacaoDaRegra(Regra(ate: new DateOnly(2026, 11, 19)), hoje).Texto.Should().Be("Encerrada");
        RepasseNaTela.SituacaoDaRegra(Regra(de: new DateOnly(2026, 12, 1)), hoje).Should().Be(("Ainda não vale", "info"));
    }

    [Fact]
    public void Linha_da_regra_escreve_o_alcance_e_todos_quando_nao_restringe()
    {
        var hoje = new DateOnly(2026, 11, 20);
        var geral = RepasseNaTela.LinhaDe(Regra(), hoje);
        geral.Convenio.Should().Be(RepasseNaTela.TodosOsConvenios);
        geral.TipoGuia.Should().Be("Todos os tipos");
        geral.Modalidade.Should().Be("Todas");
        var amil = RepasseNaTela.LinhaDe(Regra(convenio: "Amil", tipo: TipoCodigo.Consulta), hoje);
        amil.Convenio.Should().Be("Amil");
        amil.TipoGuia.Should().Be("Consulta");
        amil.Profissional.Should().Be("Dra. Helena");
    }

    [Fact]
    public void Filtro_por_convenio_traz_tambem_a_regra_geral_do_medico()
    {
        var hoje = new DateOnly(2026, 11, 20);
        var linhas = new[]
        {
            RepasseNaTela.LinhaDe(Regra(convenio: "Amil"), hoje),
            RepasseNaTela.LinhaDe(Regra(convenio: "Petrobras"), hoje),
            RepasseNaTela.LinhaDe(Regra(), hoje),
            RepasseNaTela.LinhaDe(Regra(convenio: "Amil", ativa: false), hoje)
        };
        RepasseNaTela.Filtrar(linhas, RepasseNaTela.TodosOsProfissionais, "Amil", soVigentes: false).Should().HaveCount(3);
        RepasseNaTela.Filtrar(linhas, RepasseNaTela.TodosOsProfissionais, "Amil", soVigentes: true).Should().HaveCount(2);
        RepasseNaTela.OpcoesDeConvenio(linhas).Should().Equal(RepasseNaTela.TodosOsConvenios, "Amil", "Petrobras");
    }

    [Fact]
    public void Sem_regra_lista_por_medico_os_convenios_com_guia_paga_esperando()
    {
        var semRegra = RepasseNaTela.SemRegra([
            Previa([Item(0m, "Sul América", bloqueio: BloqueioItemRepasse.SemRegra),
                    Item(0m, "Sul América", bloqueio: BloqueioItemRepasse.SemRegra),
                    Item(65m, "Amil")]),
            new PreviaRepasseProfissional(2, "Dr. Marcos", null, [Item(80m, "Petrobras")], [], [])]);
        semRegra.Should().ContainSingle();
        semRegra[0].Convenios.Should().ContainSingle().Which.Guias.Should().Be(2);
        RepasseNaTela.RotuloSemRegra(semRegra[0].Convenios[0]).Should().Be("Sul América · Consulta (2 guias)");
    }

    [Fact]
    public void Reajuste_diz_o_que_nao_muda()
        => SobInvariante(() => RepasseNaTela.ExplicacaoDoReajuste(Regra(65m), new DateOnly(2026, 12, 1)))
            .Should().Contain("até 30/11/2026").And.Contain("começa em 01/12/2026").And.Contain("O que já foi fechado não muda");

    // ==================== Conciliação ====================

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  GRD   004471 ", "GRD 004471")]
    public void Referencia_do_pagamento_e_aparada_e_vazia_vira_nula(string? digitada, string? gravada)
        => RepasseNaTela.ReferenciaParaGravar(digitada).Should().Be(gravada);

    [Fact]
    public void Referencia_longa_e_cortada_no_limite()
        => RepasseNaTela.ReferenciaParaGravar(new string('9', 80))!.Length.Should().Be(60);
}
