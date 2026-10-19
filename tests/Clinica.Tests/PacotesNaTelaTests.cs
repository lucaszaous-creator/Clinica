using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela PACOTES e os formulários dela afirmam (Blazor desde out/2026) — antes no
/// <c>PacotesViewModel</c>, <c>PacoteCatalogoEdicaoViewModel</c>, <c>PacoteVendaViewModel</c> e
/// <c>ConsumosPacoteViewModel</c> do WPF.
/// </summary>
public class PacotesNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 18);

    private static LinhaDePacoteVendido Vendido(string paciente, bool ativo, int? saldo = 3, int id = 1)
        => new(id, 10 + id, paciente, "10 sessões", "3 de 10 sessões", ativo ? "Ativo" : "Cancelado",
            "R$ 1.500,00", "01/10/2026", ativo, "pago", false, saldo);

    private static PacotesNaTela.EscolhaDePagamento AVista(FormaPagamento? forma = FormaPagamento.Pix)
        => new(true, forma, null, null, "1", null, null, Hoje.AddDays(30));

    private static PacotesNaTela.EscolhaDePagamento APrazo(string? entrada, string? parcelas, FormaPagamento? forma = FormaPagamento.Pix)
        => new(false, forma, null, null, "1", entrada, parcelas, Hoje.AddDays(30));

    // ---------------------------------------------------------------- catálogo

    [Fact]
    public void Linha_do_catalogo_escreve_o_rotulo_do_tipo_e_o_valor_em_pt_BR_e_carrega_o_que_a_tela_nao_mostra()
    {
        var p = new PacoteCatalogo
        {
            Id = 4, Nome = "10 sessões", Tipo = TipoPacote.Sessoes, SessoesIncluidas = 10, Valor = 1250.5m,
            ValidadeDias = 90, Ordem = 3, Observacoes = "Reajuste de março", Ativo = false
        };
        var l = PacotesNaTela.LinhaDe(p);
        l.Resumo.Should().Be("Pacote de sessões · 10 sessões · vale 90 dias");
        l.Resumo.Should().NotContain("Sessoes ·", "o enum cru não vai para a tela (parcela 41)");
        l.Valor.Should().Be("R$ 1.250,50");
        l.Ordem.Should().Be(3);
        l.Observacoes.Should().Be("Reajuste de março");
        l.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Plano_sem_sessoes_e_sem_validade_diz_sessoes_livres()
        => PacotesNaTela.ResumirCatalogo(new PacoteCatalogo { Tipo = TipoPacote.Plano })
            .Should().Be("Plano · sessões livres");

    [Fact]
    public void Editar_o_catalogo_devolve_intactos_ordem_observacoes_e_ativo()
    {
        var original = PacotesNaTela.LinhaDe(new PacoteCatalogo
        {
            Id = 9, Nome = "Mensal", Tipo = TipoPacote.Plano, Valor = 300m, Ordem = 2,
            Observacoes = "Não mexer", Ativo = false
        });

        PacotesNaTela.MontarCatalogo(original, "Mensal novo", TipoPacote.Plano, "", "350,00", "30",
            out var pacote, out var erro).Should().BeTrue(erro);

        pacote!.Id.Should().Be(9, "regravar mantém a procedência dos pacotes já vendidos");
        pacote.Valor.Should().Be(350m);
        pacote.ValidadeDias.Should().Be(30);
        pacote.SessoesIncluidas.Should().BeNull();
        pacote.Ordem.Should().Be(2);
        pacote.Observacoes.Should().Be("Não mexer");
        pacote.Ativo.Should().BeFalse("o formulário não mostra o 'ativo' — quem não edita, preserva");
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("1.250,00", 1250)]
    [InlineData("1250", 1250)]
    public void Valor_do_catalogo_aceita_a_cortesia_e_o_valor_em_pt_BR(string texto, decimal esperado)
    {
        PacotesNaTela.LerValor(texto, out var v, out var erro).Should().BeTrue(erro);
        v.Should().Be(esperado);
    }

    [Fact]
    public void Numero_que_nao_se_le_diz_qual_campo_corrigir()
    {
        PacotesNaTela.MontarCatalogo(null, "X", TipoPacote.Sessoes, "dez", "100", null, out _, out var e1).Should().BeFalse();
        e1.Should().Contain("as sessões");
        PacotesNaTela.MontarCatalogo(null, "X", TipoPacote.Sessoes, "10", "cem", null, out _, out var e2).Should().BeFalse();
        e2.Should().Contain("valor");
        PacotesNaTela.MontarCatalogo(null, "X", TipoPacote.Sessoes, "10", "100", "90d", out _, out var e3).Should().BeFalse();
        e3.Should().Contain("a validade");
    }

    [Fact]
    public void Opcao_da_venda_mostra_sessoes_e_valor_em_pt_BR()
    {
        PacotesNaTela.OpcaoDe(new PacoteCatalogo { Id = 1, Nome = "Pacote 10", SessoesIncluidas = 10, Valor = 1500m })
            .Rotulo.Should().Be("Pacote 10 — 10 sessões — R$ 1.500,00");
        PacotesNaTela.OpcaoDe(new PacoteCatalogo { Id = 2, Nome = "Mensal", Tipo = TipoPacote.Plano, Valor = 300m })
            .Rotulo.Should().Be("Mensal — R$ 300,00");
    }

    // ---------------------------------------------------------------- vendidos

    [Fact]
    public void Linha_vendida_escreve_situacao_pelo_rotulo_e_marca_parcela_vencida()
    {
        var saldo = new SaldoPacote(5, 7, "Maria", "10 sessões", TipoPacote.Sessoes, StatusPacote.Ativo,
            10, 3, 7, 1500m, new DateOnly(2026, 9, 1), null)
        {
            ValorPago = 500m, ValorAReceber = 1000m, ParcelasEmAberto = 2, ParcelasVencidas = 1
        };
        var l = PacotesNaTela.LinhaDe(saldo);
        l.Saldo.Should().Be("7 de 10 sessões");
        l.Compra.Should().Be("01/09/2026");
        l.Valor.Should().Be("R$ 1.500,00");
        l.PagamentoAtrasado.Should().BeTrue();
        l.SaldoSessoes.Should().Be(7);
        l.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Filtro_casa_o_paciente_sem_acento_e_so_ativos()
    {
        var todos = new[] { Vendido("José Antônio", true, id: 1), Vendido("Maria", false, id: 2), Vendido("Joana", true, id: 3) };
        PacotesNaTela.Filtrar(todos, "jose antonio", false).Select(v => v.Id).Should().Equal(1);
        PacotesNaTela.Filtrar(todos, null, true).Select(v => v.Id).Should().Equal(1, 3);
        PacotesNaTela.Filtrar(todos, "jo", true).Select(v => v.Id).Should().Equal(1, 3);
    }

    [Fact]
    public void Resumo_diz_que_esta_filtrado_e_os_totais_sao_os_da_carteira()
    {
        var todos = new[] { Vendido("A", true, 3, 1), Vendido("B", true, null, 2), Vendido("C", false, 9, 3) };
        PacotesNaTela.Resumo(todos, 3, false).Should().Be("3 pacote(s) vendidos · 2 ativo(s) · 3 sessão(ões) em saldo");
        PacotesNaTela.Resumo(todos, 1, true).Should().Be("1 de 3 pacote(s) no filtro · 2 ativo(s) no total");
    }

    [Fact]
    public void Vazio_separa_carteira_vazia_de_nada_bate_com_o_filtro()
    {
        PacotesNaTela.FiltroAtivo(" ", false).Should().BeFalse();
        PacotesNaTela.Vazio(true).Should().Be(PacotesNaTela.VazioComFiltro);
        PacotesNaTela.Vazio(false).Should().Be(PacotesNaTela.VazioSemFiltro);
    }

    // ---------------------------------------------------------------- sessões

    [Fact]
    public void Sessoes_listam_a_mais_recente_primeiro_com_a_origem_e_a_devolvida_marcada()
    {
        var pacote = new PacotePaciente
        {
            SessoesContratadas = 10, DataCompra = Hoje.AddDays(-30),
            Consumos =
            [
                new ConsumoPacote { Id = 1, Data = Hoje.AddDays(-20), AtendimentoId = 77, CriadoPor = "ana" },
                new ConsumoPacote { Id = 2, Data = Hoje.AddDays(-10), Observacao = "Baixa manual",
                                    CanceladoEm = DateTime.Now, MotivoCancelamento = "não veio" },
                new ConsumoPacote { Id = 3, Data = Hoje.AddDays(-5) }
            ]
        };

        var s = PacotesNaTela.Sessoes(pacote, Hoje);
        s.Linhas.Select(l => l.ConsumoId).Should().Equal(3, 2, 1);
        s.Linhas[0].Origem.Should().Be("—");
        s.Linhas[1].Situacao.Should().Be("devolvida ao saldo — não veio");
        s.Linhas[2].Origem.Should().Be("atendimento #77 · por ana");
        s.Saldo.Should().Be("8 de 10 sessões");
        s.Resumo.Should().Be("2 sessão(ões) usada(s) · 1 devolvida(s) ao saldo · situação hoje: ativo.");
        s.Resumo.Should().NotContain("Ativo", "o enum cru não vai para a tela (o WPF escrevia 'situação hoje: Ativo')");
    }

    [Fact]
    public void Pacote_sem_consumo_e_plano_livre_dizem_o_saldo_por_extenso()
    {
        var s = PacotesNaTela.Sessoes(new PacotePaciente { DataCompra = Hoje, ValidoAte = new DateOnly(2026, 12, 31) }, Hoje);
        s.Saldo.Should().Be("livre até 31/12/2026");
        s.Resumo.Should().Be("Este pacote ainda não debitou nenhuma sessão.");
        PacotesNaTela.Sessoes(new PacotePaciente { DataCompra = Hoje }, Hoje).Saldo.Should().Be("livre, sem prazo");
    }

    // ---------------------------------------------------------------- venda

    [Fact]
    public void Formas_da_venda_nao_oferecem_convenio()
        => PacotesNaTela.Formas.Should().NotContain(FormaPagamento.Convenio).And.Contain(FormaPagamento.Pix);

    [Fact]
    public void A_vista_monta_tudo_agora_com_a_forma()
    {
        var p = PacotesNaTela.Pagamento(AVista(FormaPagamento.Dinheiro));
        p.TudoAgora.Should().BeTrue();
        p.FormaDoPagoAgora.Should().Be(FormaPagamento.Dinheiro);
    }

    [Fact]
    public void A_prazo_monta_entrada_e_parcelas_e_a_forma_so_vale_para_a_entrada()
    {
        var com = PacotesNaTela.Pagamento(APrazo("200,00", "3"));
        com.TudoAgora.Should().BeFalse();
        com.EntradaAgora.Should().Be(200m);
        com.Parcelas.Should().Be(3);
        com.FormaDoPagoAgora.Should().Be(FormaPagamento.Pix);

        var sem = PacotesNaTela.Pagamento(APrazo(null, "3"));
        sem.EntradaAgora.Should().Be(0m);
        sem.FormaDoPagoAgora.Should().BeNull("sem entrada não há o que pagar agora");
    }

    [Fact]
    public void Credito_exige_de_1_a_36_parcelas_na_maquininha()
    {
        var e = new PacotesNaTela.EscolhaDePagamento(true, FormaPagamento.CartaoCredito, "Maq", "Visa", "40", null, null, Hoje);
        var acao = () => PacotesNaTela.Pagamento(e);
        acao.Should().Throw<InvalidOperationException>().WithMessage("*1 a 36*");
        PacotesNaTela.Pagamento(e with { ParcelasCartao = "3" }).ParcelasCartao.Should().Be(3);
        PacotesNaTela.EhCartao(FormaPagamento.CartaoDebito).Should().BeTrue();
        PacotesNaTela.EhCartao(FormaPagamento.Pix).Should().BeFalse();
    }

    [Fact]
    public void Previa_e_o_mesmo_desenho_que_o_servico_grava_e_quando_nao_fecha_diz_o_motivo()
    {
        PacotesNaTela.Previa("1000", null, Hoje, APrazo("abc", "3")).Should().Contain("Não entendi a entrada");
        PacotesNaTela.Previa("1000", null, Hoje, APrazo(null, "x")).Should().Contain("quantas parcelas");
        PacotesNaTela.Previa(null, null, Hoje, AVista()).Should().Contain("Escolha o pacote");
        PacotesNaTela.Previa(null, 0m, Hoje, AVista()).Should().Be("Pacote sem valor: nada a receber.");
        PacotesNaTela.Previa("1000", null, Hoje, AVista(null)).Should().Contain("Diga como o paciente pagou");
        PacotesNaTela.Previa("1000", null, Hoje, AVista()).Should().StartWith("Vai gravar: à vista");
        // Em branco, vale o de TABELA — o mesmo que a venda grava.
        PacotesNaTela.Previa(null, 900m, Hoje, APrazo("100", "4")).Should().StartWith("Vai gravar: entrada")
            .And.Contain("4 parcelas");
    }

    [Fact]
    public void Venda_confere_na_ordem_do_WPF_paciente_pacote_valor_e_pagamento()
    {
        var opcao = new OpcaoDePacote(1, "Pacote", 1000m);

        PacotesNaTela.ConferirVenda(null, opcao, false, null, Hoje, AVista(), out _, out _, out var e1).Should().BeFalse();
        e1.Should().Be("Escolha o paciente que está comprando.");

        PacotesNaTela.ConferirVenda(3, null, false, null, Hoje, AVista(), out _, out _, out var e2).Should().BeFalse();
        e2.Should().Be("Escolha o pacote.");

        PacotesNaTela.ConferirVenda(3, null, true, null, Hoje, AVista(), out _, out _, out var e3).Should().BeFalse();
        e3.Should().Contain("Não há pacote no catálogo", "catálogo vazio não é 'escolha o pacote'");

        PacotesNaTela.ConferirVenda(3, opcao, false, "1.000,x", Hoje, AVista(), out _, out _, out var e4).Should().BeFalse();
        e4.Should().Contain("Não entendi o valor");

        // Valor em branco: vale o de tabela, e o pagamento é desenhado sobre ele.
        PacotesNaTela.ConferirVenda(3, opcao, false, null, Hoje, APrazo("2000", "2"), out _, out _, out var e5).Should().BeFalse();
        e5.Should().Contain("entrada é maior");

        PacotesNaTela.ConferirVenda(3, opcao, false, "900", Hoje, APrazo("100", "4"), out var valor, out var pagamento, out var ok)
            .Should().BeTrue(ok);
        valor.Should().Be(900m);
        ParcelasDaVenda.Desenhar(valor!.Value, Hoje, pagamento!).Should().HaveCount(5, "a entrada e as 4 parcelas");
    }

    [Fact]
    public void Permissoes_tem_os_bits_do_WPF()
    {
        PacotesNaTela.QuemVende.Should().Be(Permissao.VenderPacote | Permissao.EditarFinanceiro);
        PacotesNaTela.QuemCancela.Should().Be(Permissao.EditarFinanceiro);
    }
}
