using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Formatacao;

/// <summary>Uma linha de insumo como a pessoa a deixou no formulário (quantidade é TEXTO: "0," vira "0,5").</summary>
public sealed record InsumoDigitado(int ItemId, string Nome, string? Quantidade);

/// <summary>
/// O que a pessoa deixou no formulário do fechamento, cru — antes de qualquer leitura. É a
/// entrada de <see cref="FechamentoSessaoNaTela.Montar"/>, que decide se dá para concluir.
/// </summary>
public sealed record EntradaDoFechamento(
    int AgendamentoId,
    bool TemPacote,
    bool DebitarPacote,
    bool GerarLancamento,
    bool FicaAReceber,
    string? Valor,
    FormaPagamento? Forma,
    string? Parcelas,
    DateOnly DataPagamento,
    DateOnly Vencimento,
    string? Adquirente,
    string? Bandeira,
    int? CategoriaId,
    IReadOnlyList<InsumoDigitado> Insumos);

/// <summary>
/// O que o formulário de FECHAMENTO DA SESSÃO afirma e decide (out/2026, migrado do
/// <c>FechamentoSessaoViewModel</c> WPF da Recepção). As regras são as da parcela 6 e das
/// que vieram depois, e nenhuma delas pode morar no Razor:
///
/// - tudo é PROPOSTA confirmada, nunca automático (o valor sugerido vem com a procedência);
/// - o PACOTE que vence primeiro é o que o serviço debitaria sozinho — a tela não promete outro;
/// - o PARTICULAR é perguntado SEMPRE sobre o dinheiro, e a pergunta tem DUAS respostas:
///   pago agora, ou fica a receber com vencimento (set/2026);
/// - a forma de pagamento nunca é "Convênio" (o dinheiro do convênio entra pela guia);
/// - o resumo NÃO promete gerar a guia (parcela 65): quando o formulário abre ela já existe.
///
/// A leitura do que se digita é a MESMA do WPF (<see cref="LeituraDeValores"/>): valor por
/// <c>TentarLerDecimal</c>, quantidade de insumo por <c>TentarLerQuantidade</c>.
/// </summary>
public static class FechamentoSessaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A barreira do ato de concluir — o bit EXATO do <c>Exigir</c> do VM WPF.</summary>
    public const Permissao BitConcluir = Permissao.EditarAgenda;

    /// <summary>
    /// A barreira do CAIXA: <c>LancarAtendimento</c> OU <c>EditarFinanceiro</c> (parcela 62 — um
    /// OU, <c>PodeAlgum</c>/<c>ExigirAlgum</c>). Só o Financeiro fechava a quarta ponta para o
    /// perfil que faz as outras três.
    /// </summary>
    public const Permissao BitsDoCaixa = Permissao.LancarAtendimento | Permissao.EditarFinanceiro;

    public const string Titulo = "Concluir atendimento";

    /// <summary>As formas que o balcão escolhe — "Convênio" nunca: aquele dinheiro entra pela guia.</summary>
    public static IReadOnlyList<FormaPagamento> Formas { get; } = Enum.GetValues<FormaPagamento>()
        .Where(f => f != FormaPagamento.Convenio).ToArray();

    public const string TextoPacote =
        "É o pacote que vence primeiro — o mesmo que o sistema debitaria sozinho. Sessão comprada já foi paga: não cobre de novo no caixa.";

    public const string TextoInsumos =
        "Sugerido a partir do que a última sessão gastou. Corrija o que for diferente; em branco não baixa nada.";

    public const string TextoParticular =
        "Atendimento PARTICULAR, sem guia: o dinheiro desta sessão só entra no sistema por aqui. Sem registro, ela fica na Conciliação do Financeiro (aba Particulares) e o balcão é avisado na próxima visita.";

    public const string TextoSemPermissaoCaixa =
        "Seu usuário não tem permissão para lançar no caixa — o Financeiro lança pela Conciliação.";

    public const string TextoVencimento =
        "Vira conta a receber do paciente; vencida, aparece em “Quem me deve” e no aviso do balcão.";

    public const string TituloDosAvisos = "O atendimento foi concluído e a guia existe, MAS:";

    public const string RodapeDosAvisos = "Resolva pelo módulo Financeiro — a sessão não precisa ser refeita.";

    /// <summary>
    /// Os recados do LANÇAMENTO (parcela 62) — a NC reaberta, o anúncio do 2º código. Não são
    /// falha: ficam SEPARADOS dos avisos, com outra cor.
    /// </summary>
    public const string TituloDosRecados = "O lançamento do atendimento também avisou:";

    public static string Subtitulo(PropostaFechamento p) => $"{p.Paciente} · {p.Data.ToString("dd/MM/yyyy", PtBr)}";

    /// <summary>"Pacote 10 sessões — 7 de 10 restantes": o que vai ser debitado, por extenso.</summary>
    public static string? RotuloDoPacote(PropostaFechamento p)
        => p.PacoteADebitar is { } pacote ? $"{pacote.Nome} — {pacote.SaldoRotulo}" : null;

    /// <summary>O valor sugerido no formato do campo ("120,00"); vazio quando não há sugestão.</summary>
    public static string? ValorInicial(PropostaFechamento p) => p.ValorSugerido?.ToString("0.00", PtBr);

    /// <summary>
    /// De onde veio o valor. Sem preço sugerido no PARTICULAR, a frase diz POR QUE o campo está
    /// vazio — vazio sem explicação num particular se lê como "grátis".
    /// </summary>
    public static string? ProcedenciaDoValor(PropostaFechamento p)
        => p.ProcedenciaDoValor
           ?? (p.ParticularSemPreco
               ? "sem histórico nem tabela de preço para o particular — informe o valor combinado"
               : null);

    /// <summary>A cobrança nasce marcada só quando o serviço a sugere E quem está no balcão pode lançar.</summary>
    public static bool GerarLancamentoInicial(PropostaFechamento p, bool podeCaixa) => p.SugereLancamento && podeCaixa;

    /// <summary>
    /// Sugestão de insumo maior que o saldo entra ZERADA: preencher o campo com um número que a
    /// baixa vai recusar é oferecer um erro pronto.
    /// </summary>
    public static string? QuantidadeInicial(InsumoSugerido i) => i.SemSaldo ? null : i.Quantidade.ToString("0.##", PtBr);

    public static string SaldoDoInsumo(InsumoSugerido i) => $"saldo {i.Saldo.ToString("0.##", PtBr)} {i.Unidade}";

    public static bool EhCartao(FormaPagamento? forma) => forma is FormaPagamento.CartaoCredito or FormaPagamento.CartaoDebito;

    /// <summary>
    /// Uma frase com o que o botão vai fazer — lida ANTES do clique. Não promete gerar a guia
    /// (parcela 65): a guia já está no faturamento quando o formulário abre.
    /// </summary>
    public static string Resumo(bool temPacote, bool debitarPacote, bool gerarLancamento, bool ficaAReceber)
    {
        var partes = new List<string>();
        if (temPacote && debitarPacote) partes.Add("debita 1 sessão do pacote");
        if (gerarLancamento)
            partes.Add(ficaAReceber
                ? "registra a sessão como A RECEBER, com vencimento"
                : "lança a entrada no caixa");
        if (partes.Count == 0) return "A guia já está no faturamento. Não há mais nada a registrar.";
        return "A guia já está no faturamento. Concluir " + string.Join(" e ", partes) + ".";
    }

    /// <summary>
    /// Lê o formulário e devolve a decisão para o <see cref="FechamentoSessaoService.ConcluirAsync"/>,
    /// ou o erro a mostrar perto do botão. A ORDEM das conferências é a do WPF: valor, forma,
    /// parcelas do cartão, quantidade de cada insumo.
    /// </summary>
    public static DecisaoFechamento? Montar(EntradaDoFechamento e, out string? erro)
    {
        erro = null;

        decimal? valor = null;
        if (e.GerarLancamento)
        {
            if (!LeituraDeValores.TentarLerDecimal(e.Valor, out var lido))
            {
                erro = "Informe um valor maior que zero (ex.: 120,00) ou desmarque a cobrança desta sessão.";
                return null;
            }
            valor = lido;

            if (!e.FicaAReceber && e.Forma is null)
            {
                erro = "Diga como o paciente pagou (Pix, cartão, dinheiro…) — ou marque \"fica a receber\".";
                return null;
            }
        }

        var cartao = EhCartao(e.Forma);
        var parcelas = 1;
        if (e.GerarLancamento && !e.FicaAReceber && cartao &&
            (!int.TryParse(e.Parcelas, NumberStyles.Integer, PtBr, out parcelas) || parcelas < 1 || parcelas > 36))
        {
            erro = "Informe de 1 a 36 parcelas para o cartão.";
            return null;
        }

        var insumos = new List<InsumoAConsumir>();
        foreach (var linha in e.Insumos)
        {
            if (!LeituraDeValores.TentarLerQuantidade(linha.Quantidade, out var quantidade))
            {
                erro = $"A quantidade de {linha.Nome} não é um número válido.";
                return null;
            }
            if (quantidade > 0) insumos.Add(new InsumoAConsumir(linha.ItemId, quantidade));
        }

        return new DecisaoFechamento(
            e.AgendamentoId,
            DebitarPacote: e.TemPacote && e.DebitarPacote,
            GerarLancamento: e.GerarLancamento,
            Valor: valor,
            Forma: e.Forma,
            CategoriaId: e.CategoriaId,
            Insumos: insumos,
            FicaAReceber: e.GerarLancamento && e.FicaAReceber,
            Vencimento: e.GerarLancamento && e.FicaAReceber ? e.Vencimento : null,
            Adquirente: cartao ? e.Adquirente : null,
            Bandeira: cartao ? e.Bandeira : null,
            Parcelas: e.Forma == FormaPagamento.CartaoCredito ? parcelas : 1,
            DataPagamento: e.GerarLancamento && !e.FicaAReceber ? e.DataPagamento : null);
    }

    /// <summary>
    /// A mensagem inteira dos avisos (o texto que o WPF mostrava) — a parte que FALHOU e segura o
    /// formulário aberto. Fechar com um "pronto" mandaria a secretária embora achando que o
    /// pacote baixou.
    /// </summary>
    public static string MensagemDosAvisos(ResultadoFechamento r)
        => TituloDosAvisos + "\n• " + string.Join("\n• ", r.Avisos) + "\n\n" + RodapeDosAvisos;
}
