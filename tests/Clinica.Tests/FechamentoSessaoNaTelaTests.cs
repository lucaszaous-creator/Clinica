using Clinica.Application.Formatacao;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o formulário "Concluir atendimento" (Blazor, out/2026) afirma e decide — migrado do
/// <c>FechamentoSessaoViewModel</c> WPF. Cada caso aqui é uma regra que o ViewModel decidia e
/// que não pode morar no Razor: a leitura do que se digita (a MESMA LeituraDeValores), a ordem
/// das recusas, o que vai na decisão e as frases que o balcão lê antes e depois do clique.
/// </summary>
public class FechamentoSessaoNaTelaTests
{
    private static readonly DateOnly Dia = new(2026, 8, 20);

    private static EntradaDoFechamento Entrada(
        bool gerar = true, bool aReceber = false, string? valor = "120,00",
        FormaPagamento? forma = FormaPagamento.Pix, string? parcelas = "1",
        bool temPacote = false, bool debitar = true, IReadOnlyList<InsumoDigitado>? insumos = null,
        string? adquirente = "Stone", string? bandeira = "Visa")
        => new(7, temPacote, debitar, gerar, aReceber, valor, forma, parcelas,
            Dia, Dia.AddDays(30), adquirente, bandeira, 3, insumos ?? []);

    private static PropostaFechamento Proposta(
        SaldoPacote? pacote = null, decimal? valor = null, string? procedencia = null,
        bool sugere = false, bool particular = false)
        => new(7, 1, "Maria Souza", Dia, pacote, valor, procedencia, FormaPagamento.Pix, sugere, [], particular);

    private static SaldoPacote Pacote() => new(5, 1, "Maria Souza", "Pacote 10 sessões", TipoPacote.Sessoes,
        StatusPacote.Ativo, 10, 3, 7, 1000m, Dia.AddDays(-10), null);

    // ==================== o resumo — lido ANTES do clique ====================

    [Fact]
    public void Resumo_nao_promete_gerar_a_guia_ela_ja_existe()
    {
        FechamentoSessaoNaTela.Resumo(false, false, false, false)
            .Should().Be("A guia já está no faturamento. Não há mais nada a registrar.");
        FechamentoSessaoNaTela.Resumo(true, true, true, false)
            .Should().Be("A guia já está no faturamento. Concluir debita 1 sessão do pacote e lança a entrada no caixa.");
        FechamentoSessaoNaTela.Resumo(true, false, true, true)
            .Should().Be("A guia já está no faturamento. Concluir registra a sessão como A RECEBER, com vencimento.");
        FechamentoSessaoNaTela.Resumo(false, true, false, false)
            .Should().Contain("Não há mais nada", "sem pacote não há o que debitar, mesmo com a caixinha marcada");
    }

    // ==================== a proposta, como a tela a mostra ====================

    [Fact]
    public void Particular_sem_preco_diz_por_que_o_valor_esta_vazio()
    {
        FechamentoSessaoNaTela.ProcedenciaDoValor(Proposta(particular: true, sugere: true))
            .Should().Contain("informe o valor combinado");
        FechamentoSessaoNaTela.ProcedenciaDoValor(Proposta(valor: 150m, procedencia: "última entrada em 01/08/2026", particular: true))
            .Should().Be("última entrada em 01/08/2026");
        FechamentoSessaoNaTela.ProcedenciaDoValor(Proposta()).Should().BeNull("convênio sem histórico não tem o que explicar");
    }

    [Fact]
    public void Valor_sugerido_vai_ao_campo_em_pt_BR()
    {
        FechamentoSessaoNaTela.ValorInicial(Proposta(valor: 1250.5m)).Should().Be("1250,50");
        FechamentoSessaoNaTela.ValorInicial(Proposta()).Should().BeNull();
    }

    [Fact]
    public void A_cobranca_so_nasce_marcada_quando_sugerida_e_permitida()
    {
        FechamentoSessaoNaTela.GerarLancamentoInicial(Proposta(sugere: true), podeCaixa: true).Should().BeTrue();
        FechamentoSessaoNaTela.GerarLancamentoInicial(Proposta(sugere: true), podeCaixa: false).Should().BeFalse();
        FechamentoSessaoNaTela.GerarLancamentoInicial(Proposta(sugere: false), podeCaixa: true).Should().BeFalse();
    }

    [Fact]
    public void O_pacote_e_descrito_com_o_saldo_que_vai_ser_debitado()
    {
        var p = Proposta(Pacote());
        FechamentoSessaoNaTela.RotuloDoPacote(p).Should().StartWith("Pacote 10 sessões — ").And.Contain("7");
        FechamentoSessaoNaTela.RotuloDoPacote(Proposta()).Should().BeNull();
    }

    [Fact]
    public void Insumo_com_sugestao_maior_que_o_saldo_entra_zerado()
    {
        FechamentoSessaoNaTela.QuantidadeInicial(new InsumoSugerido(1, "Agulha", "un", 12m, 5m)).Should().BeNull();
        FechamentoSessaoNaTela.QuantidadeInicial(new InsumoSugerido(1, "Álcool", "ml", 0.5m, 100m)).Should().Be("0,5");
        FechamentoSessaoNaTela.SaldoDoInsumo(new InsumoSugerido(1, "Álcool", "ml", 0.5m, 99.25m)).Should().Be("saldo 99,25 ml");
    }

    [Fact]
    public void Forma_de_pagamento_nunca_e_Convenio()
    {
        FechamentoSessaoNaTela.Formas.Should().NotContain(FormaPagamento.Convenio);
        FechamentoSessaoNaTela.Formas.Should().Contain([FormaPagamento.Pix, FormaPagamento.Dinheiro, FormaPagamento.CartaoCredito]);
    }

    [Fact]
    public void As_barreiras_sao_os_bits_exatos_do_ViewModel_WPF()
    {
        FechamentoSessaoNaTela.BitConcluir.Should().Be(Permissao.EditarAgenda);
        FechamentoSessaoNaTela.BitsDoCaixa.Should().Be(Permissao.LancarAtendimento | Permissao.EditarFinanceiro);
    }

    // ==================== a decisão — a ordem das recusas do WPF ====================

    [Fact]
    public void Cobranca_sem_valor_e_recusada_antes_de_tudo()
    {
        FechamentoSessaoNaTela.Montar(Entrada(valor: "abc", forma: null), out var erro).Should().BeNull();
        erro.Should().Be("Informe um valor maior que zero (ex.: 120,00) ou desmarque a cobrança desta sessão.");
        FechamentoSessaoNaTela.Montar(Entrada(valor: "0"), out erro).Should().BeNull("zero não é cobrança");
    }

    [Fact]
    public void Pago_agora_sem_forma_e_recusado_e_fica_a_receber_nao_pede_forma()
    {
        FechamentoSessaoNaTela.Montar(Entrada(forma: null), out var erro).Should().BeNull();
        erro.Should().Contain("Diga como o paciente pagou");

        var d = FechamentoSessaoNaTela.Montar(Entrada(aReceber: true, forma: null), out erro)!;
        erro.Should().BeNull();
        d.FicaAReceber.Should().BeTrue();
        d.Vencimento.Should().Be(Dia.AddDays(30));
        d.DataPagamento.Should().BeNull("a forma e o dia de uma parcela futura são decididos quando ela for paga");
        d.Valor.Should().Be(120m);
    }

    [Fact]
    public void Valor_e_lido_como_a_clinica_digita()
    {
        FechamentoSessaoNaTela.Montar(Entrada(valor: "1.250,00"), out _)!.Valor.Should().Be(1250m);
        FechamentoSessaoNaTela.Montar(Entrada(valor: "R$ 80"), out _)!.Valor.Should().Be(80m);
    }

    [Fact]
    public void Parcelas_do_cartao_vao_de_1_a_36_e_so_o_credito_as_leva()
    {
        FechamentoSessaoNaTela.Montar(Entrada(forma: FormaPagamento.CartaoCredito, parcelas: "37"), out var erro).Should().BeNull();
        erro.Should().Be("Informe de 1 a 36 parcelas para o cartão.");
        FechamentoSessaoNaTela.Montar(Entrada(forma: FormaPagamento.CartaoDebito, parcelas: "x"), out erro).Should().BeNull();

        var credito = FechamentoSessaoNaTela.Montar(Entrada(forma: FormaPagamento.CartaoCredito, parcelas: "3"), out _)!;
        credito.Parcelas.Should().Be(3);
        credito.Adquirente.Should().Be("Stone");
        credito.Bandeira.Should().Be("Visa");

        var debito = FechamentoSessaoNaTela.Montar(Entrada(forma: FormaPagamento.CartaoDebito, parcelas: "3"), out _)!;
        debito.Parcelas.Should().Be(1);

        var pix = FechamentoSessaoNaTela.Montar(Entrada(forma: FormaPagamento.Pix, parcelas: "x"), out erro)!;
        erro.Should().BeNull("parcela só é conferida no cartão");
        pix.Adquirente.Should().BeNull("maquininha só existe no cartão");
        pix.Parcelas.Should().Be(1);
        pix.DataPagamento.Should().Be(Dia);
    }

    [Fact]
    public void Sem_cobranca_nada_de_dinheiro_vai_na_decisao()
    {
        var d = FechamentoSessaoNaTela.Montar(Entrada(gerar: false, aReceber: true, valor: null, forma: null), out var erro)!;
        erro.Should().BeNull();
        d.GerarLancamento.Should().BeFalse();
        d.Valor.Should().BeNull();
        d.FicaAReceber.Should().BeFalse();
        d.Vencimento.Should().BeNull();
        d.DataPagamento.Should().BeNull();
        d.CategoriaId.Should().Be(3);
    }

    [Fact]
    public void Pacote_so_e_debitado_quando_ha_pacote_e_a_caixinha_esta_marcada()
    {
        FechamentoSessaoNaTela.Montar(Entrada(gerar: false, temPacote: true, debitar: true), out _)!.DebitarPacote.Should().BeTrue();
        FechamentoSessaoNaTela.Montar(Entrada(gerar: false, temPacote: true, debitar: false), out _)!.DebitarPacote.Should().BeFalse();
        FechamentoSessaoNaTela.Montar(Entrada(gerar: false, temPacote: false, debitar: true), out _)!.DebitarPacote.Should().BeFalse();
    }

    [Fact]
    public void Insumo_em_branco_nao_baixa_e_insumo_invalido_e_recusado_pelo_nome()
    {
        var d = FechamentoSessaoNaTela.Montar(Entrada(gerar: false, insumos:
        [
            new InsumoDigitado(1, "Agulha", "12"),
            new InsumoDigitado(2, "Álcool", "0,5"),
            new InsumoDigitado(3, "Algodão", null),
            new InsumoDigitado(4, "Luva", "0")
        ]), out var erro)!;
        erro.Should().BeNull();
        d.Insumos.Should().BeEquivalentTo([new InsumoAConsumir(1, 12m), new InsumoAConsumir(2, 0.5m)]);

        FechamentoSessaoNaTela.Montar(Entrada(gerar: false, insumos: [new InsumoDigitado(1, "Agulha", "-2")]), out erro).Should().BeNull();
        erro.Should().Be("A quantidade de Agulha não é um número válido.");
    }

    // ==================== depois do clique ====================

    [Fact]
    public void Avisos_dizem_que_a_guia_existe_e_onde_resolver()
    {
        var r = new ResultadoFechamento(new Atendimento(), null, null, [], ["O pacote NÃO foi debitado: sem saldo"]);
        FechamentoSessaoNaTela.MensagemDosAvisos(r).Should().Be(
            "O atendimento foi concluído e a guia existe, MAS:\n• O pacote NÃO foi debitado: sem saldo\n\n"
            + "Resolva pelo módulo Financeiro — a sessão não precisa ser refeita.");
    }

    [Fact]
    public void Subtitulo_e_o_paciente_e_o_dia_da_sessao()
        => FechamentoSessaoNaTela.Subtitulo(Proposta()).Should().Be("Maria Souza · 20/08/2026");
}
