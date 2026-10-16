using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de Contas (Blazor, out/2026) afirma — as frases que moravam no
/// ContasViewModel do WPF e subiram para <see cref="ContasDaTela"/>.
/// </summary>
public class ContasDaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 15);

    private static LancamentoFinanceiro Conta(TipoLancamento tipo, int diasAteVencer, decimal valor = 100m) => new()
    {
        Tipo = tipo,
        Descricao = "Conta",
        Valor = valor,
        Status = StatusLancamento.Previsto,
        Data = Hoje,
        DataVencimento = Hoje.AddDays(diasAteVencer)
    };

    [Theory]
    [InlineData(null, "sem vencimento")]
    [InlineData(0, "vence HOJE")]
    [InlineData(1, "vence amanhã")]
    [InlineData(-5, "venceu faz 5 dia(s)")]
    [InlineData(12, "em 12 dia(s)")]
    public void O_prazo_sai_em_palavras(int? dias, string esperado)
        => Assert.Equal(esperado, ContasDaTela.Prazo(dias));

    [Fact]
    public void A_parcela_entra_na_descricao()
    {
        var l = Conta(TipoLancamento.Saida, 3);
        l.Descricao = "Reforma da recepção";
        Assert.Equal("Reforma da recepção", ContasDaTela.Descricao(l));
        l.NumeroParcelaConta = 2;
        l.TotalParcelasConta = 6;
        Assert.Equal("Reforma da recepção — 2/6", ContasDaTela.Descricao(l));
    }

    [Fact]
    public void O_contexto_pula_o_que_nao_esta_preenchido_e_o_fornecedor_vence_o_paciente()
    {
        var l = Conta(TipoLancamento.Entrada, 3);
        l.Paciente = new Paciente { Nome = "Ana" };
        l.Categoria = new CategoriaFinanceira { Nome = "Particular" };
        Assert.Equal("Ana · Particular", ContasDaTela.Contexto(l));

        l.Contraparte = "Convênio X";
        l.DocumentoReferencia = "NF 123";
        Assert.Equal("Convênio X · NF 123 · Particular", ContasDaTela.Contexto(l));

        var vazio = Conta(TipoLancamento.Saida, 1);
        Assert.Equal(string.Empty, ContasDaTela.Contexto(vazio));
    }

    [Fact]
    public void O_resumo_conta_as_vencidas_DA_LISTA_filtrada()
    {
        var abertas = new[]
        {
            Conta(TipoLancamento.Saida, -2), Conta(TipoLancamento.Saida, 4),
            Conta(TipoLancamento.Entrada, -10), Conta(TipoLancamento.Entrada, -1)
        };
        var aPagar = ContasDaTela.Filtrar(abertas, TipoLancamento.Saida);
        Assert.Equal(2, aPagar.Count);
        // A receber vencida não pode aparecer na frase de "só a pagar".
        Assert.Equal("2 conta(s) a pagar · 1 JÁ VENCIDA(S).", ContasDaTela.Resumo(aPagar, TipoLancamento.Saida, 30, Hoje));
        Assert.Equal("4 conta(s) em aberto · 3 JÁ VENCIDA(S).", ContasDaTela.Resumo(abertas, null, 30, Hoje));
    }

    [Fact]
    public void O_resumo_diz_nenhuma_vencida_e_diz_o_recorte_quando_esta_vazio()
    {
        var abertas = new[] { Conta(TipoLancamento.Saida, 4) };
        Assert.Equal("1 conta(s) em aberto, nenhuma vencida.", ContasDaTela.Resumo(abertas, null, 30, Hoje));
        Assert.Equal("Nada a receber vencendo nos próximos 15 dias.",
            ContasDaTela.Resumo(ContasDaTela.Filtrar(abertas, TipoLancamento.Entrada), TipoLancamento.Entrada, 15, Hoje));
        Assert.Equal("Nada em aberto vencendo nos próximos 7 dias.", ContasDaTela.Resumo([], null, 7, Hoje));
    }

    [Fact]
    public void A_vigencia_da_conta_fixa_diz_o_prazo_ou_a_falta_dele()
    {
        var r = new LancamentoRecorrente { PrimeiroVencimento = new DateOnly(2026, 1, 5) };
        Assert.Equal("desde 05/01/2026, sem prazo", ContasDaTela.Vigencia(r));
        r.UltimoVencimento = new DateOnly(2026, 12, 5);
        Assert.Equal("de 05/01/2026 até 05/12/2026", ContasDaTela.Vigencia(r));
    }

    [Fact]
    public void O_desfecho_da_geracao_separa_falha_nada_a_fazer_e_nada_para_rodar()
    {
        var ate = new DateOnly(2026, 11, 14);
        var (erro, texto) = ContasDaTela.DesfechoDaGeracao(new ResultadoGeracaoContas(2, 0, ["Luz"]), ate);
        Assert.True(erro);
        Assert.Equal("2 conta(s) gerada(s), mas houve falha em: Luz", texto);

        Assert.Equal((false, "3 conta(s) prevista(s) criada(s) até 14/11/2026."),
            ContasDaTela.DesfechoDaGeracao(new ResultadoGeracaoContas(3, 1, []), ate));
        Assert.Equal((false, "Nada a criar — as 4 ocorrência(s) do período já existem."),
            ContasDaTela.DesfechoDaGeracao(new ResultadoGeracaoContas(0, 4, []), ate));
        Assert.Equal((false, "Nenhuma conta fixa ativa com vencimento no período."),
            ContasDaTela.DesfechoDaGeracao(new ResultadoGeracaoContas(0, 0, []), ate));
    }
}
