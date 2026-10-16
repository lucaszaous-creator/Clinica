namespace Clinica.Desktop.Controls;

/// <summary>
/// Leitura dos números que a clínica digita. A REGRA mora em
/// <see cref="Clinica.Application.Formatacao.LeituraDeValores"/> desde out/2026, porque as
/// telas Blazor do Financeiro também a usam; este tipo só repassa, para as telas WPF não
/// precisarem mudar.
/// </summary>
public static class Valores
{
    public static bool TentarLerNumeroExato(string? texto, out decimal valor)
        => global::Clinica.Application.Formatacao.LeituraDeValores.TentarLerNumeroExato(texto, out valor);

    public static bool TentarLerDecimal(string? texto, out decimal valor)
        => global::Clinica.Application.Formatacao.LeituraDeValores.TentarLerDecimal(texto, out valor);

    public static bool TentarLerQuantidade(string? texto, out decimal quantidade)
        => global::Clinica.Application.Formatacao.LeituraDeValores.TentarLerQuantidade(texto, out quantidade);
}
