using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Repasses AFIRMA sobre as regras e as apurações ANTIGAS (out/2026, na
/// migração para Blazor). Desde nov/2026 o repasse é por guia recebida (<see cref="RepasseNaTela"/>):
/// o "Apurar" por período saiu da tela, e daqui ficou só o que o histórico das apurações antigas
/// e a vigência das regras ainda leem.
///
/// Dinheiro e data em pt-BR FIXO: a mesma tela roda no Windows da clínica e na prévia em
/// Linux, onde a cultura do processo é a invariante.
/// </summary>
public static class LeituraDosRepasses
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"sempre", "a partir de …", "até …" ou "de … a …".</summary>
    public static string Vigencia(DateOnly? de, DateOnly? ate) => (de, ate) switch
    {
        (null, null) => "sempre",
        ({ } d, null) => $"a partir de {Data(d)}",
        (null, { } a) => $"até {Data(a)}",
        var (d, a) => $"{Data(d!.Value)} a {Data(a!.Value)}"
    };

    /// <summary>"Apurado" ou "Cancelado em 12/09/2026".</summary>
    public static string SituacaoDaApuracao(RepasseApurado a) =>
        a.CanceladoEm is { } quando ? $"Cancelado em {quando.ToString("dd/MM/yyyy", PtBr)}" : "Apurado";

    /// <summary>"01/09/2026 a 30/09/2026".</summary>
    public static string Periodo(DateOnly inicio, DateOnly fim) => $"{Data(inicio)} a {Data(fim)}";

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
}
