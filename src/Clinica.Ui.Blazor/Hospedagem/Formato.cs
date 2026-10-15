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
}
