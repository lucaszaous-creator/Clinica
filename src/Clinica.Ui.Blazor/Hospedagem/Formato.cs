using System.Globalization;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// Dinheiro e porcentagem em pt-BR FIXO. A cultura do processo não serve: a mesma tela
/// roda no Windows da clínica e na prévia em Linux, onde a cultura é a invariante — e
/// "¤180.00" numa clínica brasileira é a lição que a parcela de set/2026 já pagou.
/// </summary>
public static class Formato
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Dinheiro(decimal valor) => valor.ToString("C", PtBr);

    /// <summary>Dinheiro sem centavos, para eixo e rótulo curto ("R$ 12 mil").</summary>
    public static string DinheiroCurto(decimal valor)
    {
        var abs = Math.Abs(valor);
        var sinal = valor < 0 ? "−" : "";
        if (abs >= 1_000_000m) return $"{sinal}R$ {(abs / 1_000_000m).ToString("0.#", PtBr)} mi";
        if (abs >= 1_000m) return $"{sinal}R$ {(abs / 1_000m).ToString("0.#", PtBr)} mil";
        return $"{sinal}R$ {abs.ToString("0", PtBr)}";
    }

    public static string Porcentagem(double fracao) => fracao.ToString("P0", PtBr);

    /// <summary>"setembro de 2026" — o mês escrito como a clínica fala.</summary>
    public static string Mes(DateOnly mes)
    {
        var t = mes.ToString("MMMM 'de' yyyy", PtBr);
        return char.ToUpper(t[0], PtBr) + t[1..];
    }

    /// <summary>"12/09/2026".</summary>
    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", PtBr);

    /// <summary>"12/09".</summary>
    public static string DataCurta(DateOnly data) => data.ToString("dd/MM", PtBr);

    /// <summary>"12/09/2026 14:32".</summary>
    public static string DataHora(DateTime quando) => quando.ToString("dd/MM/yyyy HH:mm", PtBr);

    /// <summary>Número com casas fixas em pt-BR ("1.234,50"), sem símbolo de moeda.</summary>
    public static string Numero(decimal valor, int casas = 2) => valor.ToString("N" + casas, PtBr);

    /// <summary>
    /// O valor como ele volta para um CAMPO de digitação ("1250,00", sem milhar): o que a
    /// <c>LeituraDeValores</c> lê de volta sem ambiguidade.
    /// </summary>
    public static string ParaCampo(decimal valor) => valor.ToString("0.00", PtBr);
}
