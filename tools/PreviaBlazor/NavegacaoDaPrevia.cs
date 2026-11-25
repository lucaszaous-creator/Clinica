using System.Text.Json;
using Clinica.Ui.Blazor.Hospedagem;

namespace PreviaBlazor;

/// <summary>
/// A navegação de um app, lida de um <c>navegacao.json</c> (o menu do topo, as abas e o componente
/// de cada chave). O da gestão web é o próprio <c>src/Clinica.Gestao.Web/catalogo-gestao.json</c>
/// (gerado por <c>tools/gerar-catalogo-gestao-web.py</c>). A ferramenta do Windows que gravava o dos
/// apps saiu com as videoaulas (nov/2026).
/// Sem o arquivo (variável <c>CLINICA_NAVEGACAO</c> ausente), a página da aula diz isso.
/// </summary>
public sealed class NavegacaoDaPrevia
{
    public sealed record Grupo(string Rotulo, string? Direto, string[] Itens);
    public sealed record Aba(string Rotulo, string Chave);
    public sealed record Item(string Chave, string Rotulo, string? Descricao, bool Oculto, Aba[] Abas, string? Componente);
    public sealed record App(string AppId, string Nome, string Perfil, Grupo[] Barra, Item[] Itens)
    {
        public Item? ItemDe(string chave) => Itens.FirstOrDefault(i => i.Chave == chave);
    }

    private readonly Lazy<(IReadOnlyList<App> Apps, string? Erro)> _carga;

    public NavegacaoDaPrevia() => _carga = new(Carregar);

    public string? Erro => _carga.Value.Erro;

    public App? Do(string appId) => _carga.Value.Apps.FirstOrDefault(a => a.AppId == appId);

    /// <summary>O componente Razor pelo nome da classe (o mesmo que a página /tela usa).</summary>
    public static Type? Tipo(string? nome) => string.IsNullOrEmpty(nome)
        ? null
        : typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas." + nome);

    private static (IReadOnlyList<App>, string?) Carregar()
    {
        var caminho = Environment.GetEnvironmentVariable("CLINICA_NAVEGACAO");
        if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
            return ([], "A navegação dos apps não foi carregada (defina CLINICA_NAVEGACAO com o navegacao.json).");
        using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
        var apps = new List<App>();
        foreach (var a in doc.RootElement.EnumerateArray())
        {
            if (a.TryGetProperty("erro", out _)) continue;
            apps.Add(new App(
                a.GetProperty("app").GetString()!,
                a.GetProperty("nome").GetString()!,
                a.GetProperty("perfil").GetString()!,
                a.GetProperty("barra").EnumerateArray().Select(g => new Grupo(
                    g.GetProperty("rotulo").GetString()!,
                    g.GetProperty("direto").ValueKind == JsonValueKind.String ? g.GetProperty("direto").GetString() : null,
                    g.GetProperty("itens").EnumerateArray().Select(x => x.GetString()!).ToArray())).ToArray(),
                a.GetProperty("itens").EnumerateArray().Select(i => new Item(
                    i.GetProperty("chave").GetString()!,
                    i.GetProperty("rotulo").GetString()!,
                    i.GetProperty("descricao").ValueKind == JsonValueKind.String ? i.GetProperty("descricao").GetString() : null,
                    i.GetProperty("oculto").GetBoolean(),
                    i.GetProperty("abas").EnumerateArray().Select(x => new Aba(x.GetProperty("rotulo").GetString()!, x.GetProperty("chave").GetString()!)).ToArray(),
                    i.GetProperty("componente").ValueKind == JsonValueKind.String ? i.GetProperty("componente").GetString() : null)).ToArray()));
        }
        return (apps, null);
    }
}
