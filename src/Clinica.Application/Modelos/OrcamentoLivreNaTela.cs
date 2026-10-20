using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma linha do orçamento livre como a pessoa a digita — texto cru, lido só aqui.</summary>
public sealed class LinhaDoOrcamentoLivre
{
    public string Descricao { get; set; } = string.Empty;
    public string Quantidade { get; set; } = "1";
    public string? ValorUnitario { get; set; }
}

/// <summary>
/// O ORÇAMENTO LIVRE do paciente (parcela 24; Blazor desde out/2026 — era a
/// <c>OrcamentoWindow</c>/<c>OrcamentoViewModel</c> WPF da Recepção). Pura, pela regra da casa:
/// o que a tela afirma (o total de cada linha, o total geral, o que recusa) mora onde o
/// <c>dotnet test</c> alcança.
///
/// - O total é CALCULADO, nunca digitado: guardar o total digitado daria duas verdades sobre o
///   mesmo item — e a que o paciente somaria no papel seria a outra.
/// - "—" e não "R$ 0,00": orçamento sem nenhuma linha preenchida não custa zero, ele ainda não existe.
/// - Linha sem descrição é ignorada (a linha em branco que sobra no fim não é erro); linha com
///   descrição e número ruim RECUSA, dizendo qual.
/// - Os valores são lidos pela <see cref="LeituraDeValores"/> — a mesma do resto do app.
/// </summary>
public static class OrcamentoLivreNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A validade padrão: 30 dias, a mesma da janela WPF.</summary>
    public const int DiasDeValidade = 30;

    /// <summary>"R$ 360,00" — ou "—" quando quantidade ou valor não se leem.</summary>
    public static string TotalDaLinha(LinhaDoOrcamentoLivre l)
        => Ler(l) is { } v ? (v.Quantidade * v.Unitario).ToString("C2", PtBr) : "—";

    /// <summary>A soma das linhas válidas (com descrição); "—" quando nenhuma está completa.</summary>
    public static string TotalGeral(IEnumerable<LinhaDoOrcamentoLivre> linhas)
    {
        var total = 0m;
        var algum = false;
        foreach (var l in linhas)
        {
            if (string.IsNullOrWhiteSpace(l.Descricao) || Ler(l) is not { } v) continue;
            total += v.Quantidade * v.Unitario;
            algum = true;
        }
        return algum ? total.ToString("C2", PtBr) : "—";
    }

    /// <summary>
    /// Os itens a gravar, na ordem da tela — ou a frase que diz o que corrigir. A validade
    /// anterior à emissão também recusa: o papel nasceria vencido.
    /// </summary>
    public static (IReadOnlyList<ItemDocumentoFinanceiro> Itens, string? Erro) Montar(
        IEnumerable<LinhaDoOrcamentoLivre> linhas, DateOnly data, DateOnly validoAte)
    {
        var itens = new List<ItemDocumentoFinanceiro>();
        foreach (var l in linhas)
        {
            if (string.IsNullOrWhiteSpace(l.Descricao)) continue;
            var descricao = l.Descricao.Trim();
            if (!LeituraDeValores.TentarLerNumeroExato(l.Quantidade, out var qtd) || qtd <= 0)
                return ([], $"Quantidade inválida em \"{descricao}\" — informe um número maior que zero.");
            if (!LeituraDeValores.TentarLerDecimal(l.ValorUnitario, out var unit))
                return ([], $"Valor inválido em \"{descricao}\" (ex.: 180,00).");
            itens.Add(new ItemDocumentoFinanceiro
            {
                Ordem = itens.Count + 1,
                Descricao = descricao,
                Quantidade = qtd,
                ValorUnitario = unit
            });
        }

        if (itens.Count == 0)
            return ([], "Um orçamento precisa de ao menos um item: diga o que está sendo orçado.");
        if (validoAte < data)
            return ([], "A validade é anterior à data de emissão — confira as datas.");
        return (itens, null);
    }

    private static (decimal Quantidade, decimal Unitario)? Ler(LinhaDoOrcamentoLivre l)
        => LeituraDeValores.TentarLerNumeroExato(l.Quantidade, out var qtd) && qtd > 0
           && LeituraDeValores.TentarLerDecimal(l.ValorUnitario, out var unit)
            ? (qtd, unit)
            : null;
}
