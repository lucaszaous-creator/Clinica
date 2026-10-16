using System.Globalization;

namespace Clinica.Application.Formatacao;

/// <summary>
/// Leitura dos números que a clínica digita. Mora na Application (out/2026) porque passou
/// a ter DOIS desenhos de tela que a usam — o WPF (o <c>Valores</c> do shell, que agora só
/// repassa para cá) e as telas Blazor do Financeiro. Duas cópias da mesma regra viram dois
/// comportamentos diferentes no dia em que uma delas for corrigida.
/// </summary>
public static class LeituraDeValores
{
    private static readonly CultureInfo PtBr = new("pt-BR");

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

        if (!decimal.TryParse(limpo, NumberStyles.Currency, PtBr, out valor) &&
            !decimal.TryParse(limpo, NumberStyles.Currency, CultureInfo.InvariantCulture, out valor))
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

        if (!decimal.TryParse(limpo, NumberStyles.Number, PtBr, out quantidade) &&
            !decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out quantidade))
            return false;

        return quantidade >= 0;
    }
}
