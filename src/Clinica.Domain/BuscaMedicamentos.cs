using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Domain;

public sealed record MedicamentoSugerido(string Codigo, string Nome, string? Apresentacao, string? Fabricante, string? PrincipioAtivo)
{
    public string Texto => string.IsNullOrWhiteSpace(Apresentacao) ? Nome : $"{Nome} — {Apresentacao}";
    public string Rotulo => string.IsNullOrWhiteSpace(Fabricante) ? Texto : $"{Texto} · {Fabricante}";
}

public static class BuscaMedicamentos
{
    public static IReadOnlyList<MedicamentoSugerido> Catalogo(IEnumerable<MedicamentoCadastro> itens) => itens
        .Where(i => i.Ativo)
        .OrderBy(i => i.Nome).ThenBy(i => i.Apresentacao)
        .Select(i => new MedicamentoSugerido(i.Codigo, i.Nome, i.Apresentacao, i.Fabricante, i.PrincipioAtivo)).ToArray();

    public static IReadOnlyList<MedicamentoSugerido> Buscar(IEnumerable<MedicamentoSugerido> catalogo, string? termo)
    {
        var texto = termo?.Trim() ?? "";
        if (texto.Length < 2) return [];
        var comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        const CompareOptions opcoes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return catalogo.Where(i => comparador.IndexOf(i.Rotulo + " " + i.PrincipioAtivo, texto, opcoes) >= 0)
            .OrderByDescending(i => i.Codigo.StartsWith("clinica:"))
            .ThenByDescending(i => comparador.IsPrefix(i.Nome, texto, opcoes)).ThenBy(i => i.Nome)
            .Take(20).ToArray();
    }
}
