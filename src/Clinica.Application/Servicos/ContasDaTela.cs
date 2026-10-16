using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de Contas a pagar e a receber AFIRMA (out/2026, migração para Blazor).
///
/// Morava no <c>ContasViewModel</c> do WPF (<c>LinhaConta.De</c>, <c>LinhaRecorrente.De</c>,
/// a frase do resumo e a da geração das fixas) — e decisão dentro de ViewModel WPF é
/// decisão que o <c>dotnet test</c> não alcança. Subiu inteira para cá, pura, para a tela
/// Blazor só formatar e desenhar.
///
/// Datas em pt-BR FIXO: a prévia roda em Linux com a cultura invariante.
/// </summary>
public static class ContasDaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Os horizontes oferecidos na lista, em dias. 30 é o padrão: o mês que a clínica enxerga.</summary>
    public static readonly IReadOnlyList<int> Horizontes = [7, 15, 30, 60, 90];

    public const int HorizontePadrao = 30;

    /// <summary>
    /// O prazo em palavras, porque é assim que se pensa nele no balcão: ninguém calcula
    /// "10/08 menos hoje", pensa "venceu faz cinco dias".
    /// </summary>
    public static string Prazo(int? dias) => dias switch
    {
        null => "sem vencimento",
        0 => "vence HOJE",
        1 => "vence amanhã",
        < 0 and var d => $"venceu faz {-d} dia(s)",
        var d => $"em {d} dia(s)"
    };

    /// <summary>A descrição com a parcela do parcelamento ("Reforma — 2/6").</summary>
    public static string Descricao(LancamentoFinanceiro l) =>
        l.NumeroParcelaConta is { } numero ? $"{l.Descricao} — {numero}/{l.TotalParcelasConta}" : l.Descricao;

    /// <summary>
    /// De quem é, que documento e que categoria — o que vier preenchido, nessa ordem. O
    /// fornecedor digitado vence o paciente (a conta a pagar não tem paciente; a a receber
    /// de paciente quase nunca tem contraparte digitada).
    /// </summary>
    public static string Contexto(LancamentoFinanceiro l) =>
        string.Join(" · ", new[] { l.Contraparte ?? l.Paciente?.Nome, l.DocumentoReferencia, l.Categoria?.Nome }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>Veio de conta fixa — a lista diz de onde ela veio.</summary>
    public static bool DeContaFixa(LancamentoFinanceiro l) => l.OrigemRecorrencia is not null;

    /// <summary>
    /// O recorte da lista por tipo. O RESUMO dos cartões não passa por aqui: ele é o retrato
    /// do horizonte e mudaria de significado se encolhesse junto com a lista.
    /// </summary>
    public static IReadOnlyList<LancamentoFinanceiro> Filtrar(
        IReadOnlyList<LancamentoFinanceiro> abertas, TipoLancamento? tipo) =>
        tipo is null ? abertas : abertas.Where(l => l.Tipo == tipo).ToList();

    /// <summary>
    /// A frase acima da lista. Conta as vencidas DA LISTA, não do horizonte inteiro: com o
    /// filtro "Só a pagar", "3 conta(s) em aberto · 5 JÁ VENCIDA(S)" (o WPF contava as
    /// vencidas a receber junto) é uma frase que se contradiz.
    /// </summary>
    public static string Resumo(IReadOnlyList<LancamentoFinanceiro> lista, TipoLancamento? tipo, int horizonteDias, DateOnly hoje)
    {
        var oQue = tipo switch
        {
            TipoLancamento.Saida => "a pagar",
            TipoLancamento.Entrada => "a receber",
            _ => "em aberto"
        };
        if (lista.Count == 0) return $"Nada {oQue} vencendo nos próximos {horizonteDias} dias.";
        var vencidas = lista.Count(l => l.EstaVencido(hoje));
        return vencidas > 0
            ? $"{lista.Count} conta(s) {oQue} · {vencidas} JÁ VENCIDA(S)."
            : $"{lista.Count} conta(s) {oQue}, nenhuma vencida.";
    }

    /// <summary>"de 05/01/2026 até 05/12/2026" ou "desde 05/01/2026, sem prazo".</summary>
    public static string Vigencia(LancamentoRecorrente r) =>
        r.UltimoVencimento is { } fim
            ? $"de {r.PrimeiroVencimento.ToString("dd/MM/yyyy", PtBr)} até {fim.ToString("dd/MM/yyyy", PtBr)}"
            : $"desde {r.PrimeiroVencimento.ToString("dd/MM/yyyy", PtBr)}, sem prazo";

    /// <summary>
    /// O desfecho do "Gerar fixas". Aviso não é sucesso: com falha, a frase é ERRO e fica na
    /// tela; sem falha, ela distingue "não fez nada" de "não rodou" — a contagem do que já
    /// existia é o que separa as duas.
    /// </summary>
    public static (bool EhErro, string Texto) DesfechoDaGeracao(ResultadoGeracaoContas r, DateOnly ate)
    {
        if (r.Avisos.Count > 0)
            return (true, $"{r.Geradas} conta(s) gerada(s), mas houve falha em: " + string.Join(" · ", r.Avisos));
        if (r.FezAlgo)
            return (false, $"{r.Geradas} conta(s) prevista(s) criada(s) até {ate.ToString("dd/MM/yyyy", PtBr)}.");
        return (false, r.JaExistiam > 0
            ? $"Nada a criar — as {r.JaExistiam} ocorrência(s) do período já existem."
            : "Nenhuma conta fixa ativa com vencimento no período.");
    }
}
