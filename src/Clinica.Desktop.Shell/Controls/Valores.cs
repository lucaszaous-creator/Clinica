using System.Globalization;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Leitura dos números que a clínica digita. Fica no shell porque dinheiro é digitado em
/// mais de um módulo — no lançamento do Financeiro e no fechamento da sessão da Recepção
/// — e duas cópias da mesma regra viram dois comportamentos diferentes no dia em que uma
/// delas for corrigida.
/// </summary>
public static class Valores
{
    /// <summary>Entrada decimal sem separador de milhar; vírgula e ponto indicam fração, independentemente do Windows.</summary>
    public static bool TentarLerNumeroExato(string? texto, out decimal valor)
        => decimal.TryParse(texto?.Trim().Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor);

    /// <summary>
    /// Aceita o valor como a clínica digita: "1.250,00" (pt-BR) e também "1250.00".
    /// O sinal nunca sai daqui — quem decide entrada ou saída é o tipo do lançamento.
    /// </summary>
    public static bool TentarLerDecimal(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpo = texto.Trim().Replace("R$", string.Empty).Trim();

        if (!TentarLerFormatado(limpo, NumberStyles.Currency, out valor))
            return false;

        return valor > 0;
    }

    /// <summary>
    /// Quantidade de insumo: aceita fração ("0,5" de um frasco) e recusa o negativo —
    /// quem tira do estoque é o tipo do movimento, não o sinal digitado. Campo vazio
    /// devolve zero e vale como "não consumiu", que é o caso comum.
    /// </summary>
    public static bool TentarLerQuantidade(string? texto, out decimal quantidade)
    {
        quantidade = 0;
        if (string.IsNullOrWhiteSpace(texto)) return true;

        var limpo = texto.Trim();

        if (!TentarLerFormatado(limpo, NumberStyles.Number, out quantidade))
            return false;

        return quantidade >= 0;
    }

    private static bool TentarLerFormatado(string texto, NumberStyles estilo, out decimal valor)
    {
        // O navegador e o teclado podem fornecer ponto decimal. Tentar pt-BR primeiro
        // aceita "12.5" como 125, sem falhar. O último separador define a fração:
        // 1.250,00 e 1,250.00 mantêm seus milhares; 12,5 e 12.5 valem ambos 12,5.
        var virgula = texto.LastIndexOf(',');
        var ponto = texto.LastIndexOf('.');
        var agrupamentoBrasileiro = virgula < 0 && ponto != texto.IndexOf('.');
        var agrupamentoInternacional = ponto < 0 && virgula != texto.IndexOf(',');
        var brasileiro = !agrupamentoInternacional && (agrupamentoBrasileiro || virgula > ponto);
        return decimal.TryParse(texto, estilo,
            brasileiro ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.InvariantCulture, out valor);
    }
}
