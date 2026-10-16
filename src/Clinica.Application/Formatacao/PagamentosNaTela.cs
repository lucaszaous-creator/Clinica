using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Formatacao;

/// <summary>
/// O que a tela "Pagamentos" da Recepção AFIRMA (out/2026, migrado do
/// <c>PagamentosViewModel</c> WPF). Dinheiro em pt-BR FIXO — o WPF usava a cultura da
/// máquina (<c>:C2</c>), e a prévia em Linux escreveria "¤".
/// </summary>
public static class PagamentosNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Quem pode RECEBER a cobrança no balcão — o bit do pacote (balcão), o do lançamento
    /// (passo do caixa do Finalizar) ou o do Financeiro. É o mesmo conjunto do
    /// <c>CobrancaDoPacienteViewModel.QuemRecebe</c> do shell; aqui para a tela Blazor não
    /// depender do WPF.
    /// </summary>
    public const Permissao QuemRecebe = Permissao.EditarFinanceiro | Permissao.LancarAtendimento | Permissao.VenderPacote;

    public static bool EmAberto(LancamentoFinanceiro l) => l.Status == StatusLancamento.Previsto;

    public static bool Recebido(LancamentoFinanceiro l) => l.Status == StatusLancamento.Realizado;

    public static bool Vencido(LancamentoFinanceiro l, DateOnly hoje) => EmAberto(l) && l.EstaVencido(hoje);

    public static string Situacao(LancamentoFinanceiro l, DateOnly hoje) => l.Status switch
    {
        StatusLancamento.Realizado =>
            $"Recebido em {l.DataPagamento?.ToString("dd/MM/yyyy", PtBr) ?? "—"} · {RotulosEnum.De(l.FormaPagamento)}",
        StatusLancamento.Cancelado => "Cancelado",
        _ => l.EstaVencido(hoje) ? "Vencido" : "A receber"
    };

    public static decimal AReceber(IEnumerable<LancamentoFinanceiro> itens) => itens.Where(EmAberto).Sum(l => l.Valor);

    public static int EmAbertoContagem(IEnumerable<LancamentoFinanceiro> itens) => itens.Count(EmAberto);

    /// <summary>"R$ 180,00 a receber · 2 cobrança(s) em aberto".</summary>
    public static string Resumo(IReadOnlyList<LancamentoFinanceiro> itens)
        => $"{AReceber(itens).ToString("C", PtBr)} a receber · {EmAbertoContagem(itens)} cobrança(s) em aberto";
}
