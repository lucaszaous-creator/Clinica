using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Os números do topo da tela de Repasses, contados sobre o cálculo do período.</summary>
public sealed record TotaisDosRepasses(
    int Profissionais,
    decimal ARepassar,
    int AApurar,
    int JaApurados,
    int SemRegra);

/// <summary>
/// O que a tela de Repasses AFIRMA (out/2026, na migração para Blazor). Morava escrito à mão
/// no <c>RepassesViewModel</c> do WPF; subiu para cá porque o que decide uma frase de tela
/// — e principalmente o que decide se o botão "Apurar" acende — precisa morar onde o
/// <c>dotnet test</c> alcança. Projeto WPF não compila no teste.
///
/// Dinheiro e data em pt-BR FIXO: a mesma tela roda no Windows da clínica e na prévia em
/// Linux, onde a cultura do processo é a invariante.
/// </summary>
public static class LeituraDosRepasses
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Só se apura o que tem regra, ainda não foi apurado no período e deu valor. É a
    /// metade VISÍVEL da recusa do <see cref="RepasseService.ApurarAsync"/>, que impede
    /// os três casos de novo — o botão explica, o serviço garante.
    /// </summary>
    public static bool PodeApurar(RepasseCalculado c) => c.TemRegra && !c.JaApurado && c.Valor > 0m;

    public static TotaisDosRepasses Totais(IReadOnlyList<RepasseCalculado> calculados) => new(
        calculados.Count,
        calculados.Sum(c => c.Valor),
        calculados.Count(PodeApurar),
        calculados.Count(c => c.JaApurado),
        calculados.Count(c => !c.TemRegra));

    /// <summary>
    /// A frase do período, como o WPF a escrevia. Quem não tem regra é CONTADO na frase:
    /// sumir com ele esconderia justamente o cadastro que falta fazer.
    /// </summary>
    public static string Resumo(IReadOnlyList<RepasseCalculado> calculados)
    {
        var t = Totais(calculados);
        return $"{t.Profissionais} profissional(is) com atendimento no período · "
               + $"{t.ARepassar.ToString("C", PtBr)} a repassar"
               + (t.SemRegra > 0 ? $" · {t.SemRegra} sem regra cadastrada" : string.Empty);
    }

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
