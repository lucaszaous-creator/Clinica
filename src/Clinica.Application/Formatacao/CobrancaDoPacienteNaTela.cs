using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Formatacao;

/// <summary>
/// O que a COBRANÇA NO BALCÃO afirma (out/2026: subido do <c>CobrancaDoPacienteViewModel</c> do
/// shell quando a janela virou o <c>CobrancaDoPacienteForm</c> Blazor). As contas vencidas do
/// paciente, a mais antiga primeiro, e a frase do resumo. Dinheiro em pt-BR FIXO — a web de
/// leitura roda em Linux, e `:C2` na cultura invariante escreveria "¤".
///
/// O formulário NÃO grava dinheiro por conta própria: receber passa pelo
/// <c>PagamentosRecepcaoService.ReceberAsync</c>, o MESMO caixa do Financeiro, com a auditoria no
/// mesmo SaveChanges. Quem pode receber é <see cref="PagamentosNaTela.QuemRecebe"/>.
/// </summary>
public static class CobrancaDoPacienteNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Uma conta vencida como o formulário a mostra.</summary>
    public sealed record ContaVencida(int LancamentoId, string Descricao, string Vencimento, string Atraso, decimal Valor)
    {
        public string ValorTexto => Valor.ToString("C2", PtBr);
    }

    /// <summary>O terceiro estado: a leitura falhou, e a lista vazia NÃO é "nada a receber".</summary>
    public const string FalhaDeLeitura = "Não foi possível ler as contas vencidas agora. A lista NÃO está vazia por "
        + "não haver dívida — ela não pôde ser lida.";

    /// <summary>A confirmação de um recebimento — escrita DEPOIS da recarga, senão a recarga a apaga.</summary>
    public const string Recebido = "Recebimento registrado. Confira o saldo atualizado na lista.";

    /// <summary>As contas do devedor, da que venceu primeiro para a mais recente.</summary>
    public static IReadOnlyList<ContaVencida> Contas(PacienteInadimplente? devedor)
        => (devedor?.Detalhe ?? [])
            .OrderBy(c => c.Vencimento)
            .Select(c => new ContaVencida(c.LancamentoId, c.Descricao,
                c.Vencimento.ToString("dd/MM/yyyy", PtBr), Atraso(c.DiasEmAtraso), c.Valor))
            .ToList();

    /// <summary>"vencida há 1 dia" / "vencida há 12 dias".</summary>
    public static string Atraso(int dias) => dias == 1 ? "vencida há 1 dia" : $"vencida há {dias} dias";

    /// <summary>
    /// "2 conta(s) vencida(s), somando R$ 360,00 · a mais antiga de 03/09/2026." — e, sem devedor, a
    /// frase que diz o que a tela de trás vai fazer (o alerta some ao voltar).
    /// </summary>
    public static string Resumo(PacienteInadimplente? devedor)
        => devedor is null
            ? "Nenhuma conta vencida — o alerta deve sumir ao voltar à tela."
            : $"{devedor.Contas} conta(s) vencida(s), somando {devedor.Total.ToString("C2", PtBr)}"
              + $" · a mais antiga de {devedor.VencimentoMaisAntigo.ToString("dd/MM/yyyy", PtBr)}.";
}
