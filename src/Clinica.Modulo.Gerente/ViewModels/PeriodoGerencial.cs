namespace Clinica.Gerente.ViewModels;

/// <summary>
/// O seletor de período das telas da direção, num lugar só.
///
/// Nasceu duplicado em Indicadores e Faturamento — mesmas três opções, mesma conta de
/// intervalo, dois arquivos. É exatamente o começo do padrão que o projeto já pagou
/// para desfazer no seletor de paciente: enquanto são duas cópias iguais ninguém
/// percebe; quando uma ganha "últimos 12 meses" e a outra não, a direção compara dois
/// números que não falam do mesmo intervalo.
/// </summary>
public static class PeriodoGerencial
{
    public const string EsteMes = "Este mês";
    public const string UltimosTresMeses = "Últimos 3 meses";
    public const string EsteAno = "Este ano";

    /// <summary>As opções, na ordem em que a direção pensa: do mais curto ao mais longo.</summary>
    public static IReadOnlyList<string> Opcoes { get; } = [EsteMes, UltimosTresMeses, EsteAno];

    /// <summary>
    /// Intervalo correspondente à opção. O fim é sempre HOJE, nunca o fim do mês:
    /// mostrar um mês que ainda não terminou como se tivesse terminado faria a
    /// ocupação e a taxa de baixa parecerem piores do que são.
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim) Intervalo(string opcao, DateOnly hoje) => opcao switch
    {
        UltimosTresMeses => (new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-2), hoje),
        EsteAno => (new DateOnly(hoje.Year, 1, 1), hoje),
        _ => (new DateOnly(hoje.Year, hoje.Month, 1), hoje)
    };

    /// <summary>Como o intervalo aparece no cabeçalho da tela.</summary>
    public static string Rotular((DateOnly Inicio, DateOnly Fim) intervalo)
        => $"{intervalo.Inicio:dd/MM/yyyy} a {intervalo.Fim:dd/MM/yyyy}";
}
