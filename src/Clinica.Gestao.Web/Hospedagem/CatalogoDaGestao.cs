using System.Reflection;
using System.Text.Json;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// O menu da gestão web: as quatro áreas (Gestão, Financeiro, Recepção, Clínico) com os itens,
/// as abas e a tela de cada chave — lido do <c>catalogo-gestao.json</c>, que
/// <c>tools/gerar-catalogo-gestao-web.py</c> GERA a partir dos módulos do Gerente Geral (e o
/// <c>verificar-suite</c> confere a cada PR). Não há lista de telas escrita à mão aqui.
/// </summary>
public sealed class CatalogoDaGestao
{
    public sealed record Aba(string Rotulo, string Chave);
    public sealed record Item(string Chave, string Rotulo, string? Descricao, bool Oculto,
        Permissao Requer, Permissao RequerAlgum, PerfilAcesso? PerfilExclusivo, Aba[] Abas, Type? Tela);
    public sealed record Area(string Rotulo, string[] Itens);

    public IReadOnlyList<Area> Areas { get; }
    private readonly Dictionary<string, Item> _itens;

    public CatalogoDaGestao(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var app = doc.RootElement[0];
        Areas = app.GetProperty("barra").EnumerateArray()
            .Select(g => new Area(g.GetProperty("rotulo").GetString()!, g.GetProperty("itens").EnumerateArray().Select(x => x.GetString()!).ToArray()))
            .ToList();
        _itens = app.GetProperty("itens").EnumerateArray().Select(i => new Item(
                i.GetProperty("chave").GetString()!,
                i.GetProperty("rotulo").GetString()!,
                i.GetProperty("descricao").ValueKind == JsonValueKind.String ? i.GetProperty("descricao").GetString() : null,
                i.GetProperty("oculto").GetBoolean(),
                Bits(i.GetProperty("requer")),
                Bits(i.GetProperty("requerAlgum")),
                i.GetProperty("perfilExclusivo").ValueKind == JsonValueKind.String
                    ? Enum.Parse<PerfilAcesso>(i.GetProperty("perfilExclusivo").GetString()!) : null,
                i.GetProperty("abas").EnumerateArray().Select(a => new Aba(a.GetProperty("rotulo").GetString()!, a.GetProperty("chave").GetString()!)).ToArray(),
                Tipo(i.GetProperty("componente").ValueKind == JsonValueKind.String ? i.GetProperty("componente").GetString() : null)))
            .ToDictionary(i => i.Chave);
    }

    /// <summary>O catálogo embutido na DLL.</summary>
    public static CatalogoDaGestao Embutido()
    {
        using var s = typeof(CatalogoDaGestao).Assembly.GetManifestResourceStream("catalogo-gestao.json")
                      ?? throw new InvalidOperationException("catalogo-gestao.json não está embutido.");
        using var r = new StreamReader(s);
        return new CatalogoDaGestao(r.ReadToEnd());
    }

    public IEnumerable<Item> Todos => _itens.Values;

    public Item? ItemDe(string chave) => _itens.GetValueOrDefault(chave);

    /// <summary>
    /// A pessoa alcança o item? A MESMA régua do menu do app (ShellViewModel): o bit exigido, um
    /// dos bits de <c>RequerAlgum</c> e o perfil exclusivo. A tela ainda confere de novo (Exigir).
    /// </summary>
    public static bool Alcanca(Item i, SessaoUsuario s)
        => s.Pode(i.Requer)
           && (i.RequerAlgum == Permissao.Nenhuma || s.PodeAlgum(i.RequerAlgum))
           && (i.PerfilExclusivo is null || s.Perfil == i.PerfilExclusivo);

    private static Permissao Bits(JsonElement lista)
        => lista.EnumerateArray().Aggregate(Permissao.Nenhuma, (acc, x) => acc | Enum.Parse<Permissao>(x.GetString()!));

    /// <summary>O componente Razor pelo nome da classe (as telas moram em Clinica.Ui.Blazor.Telas).</summary>
    public static Type? Tipo(string? nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        var t = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas." + nome);
        return t is not null && typeof(IComponent).IsAssignableFrom(t) ? t : null;
    }
}
