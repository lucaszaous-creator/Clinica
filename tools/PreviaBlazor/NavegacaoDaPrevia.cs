using System.Text.Json;
using Clinica.Ui.Blazor.Hospedagem;

namespace PreviaBlazor;

/// <summary>
/// A navegação REAL dos cinco apps, lida do <c>navegacao.json</c> que a ferramenta do
/// Windows grava (tools/treinamento/captura) — out/2026, para as videoaulas. O menu do topo,
/// as abas e o componente de cada chave vêm do ShellViewModel de verdade; escrevê-los aqui à
/// mão seria uma segunda definição do menu, e o vídeo mostraria um caminho que o app não tem.
/// Sem o arquivo (variável <c>CLINICA_NAVEGACAO</c> ausente), a página da aula diz isso.
/// </summary>
public sealed class NavegacaoDaPrevia
{
    public sealed record Grupo(string Rotulo, string? Direto, string[] Itens);
    public sealed record Aba(string Rotulo, string Chave);
    public sealed record Item(string Chave, string Rotulo, string? Descricao, bool Oculto, Aba[] Abas, string? Componente);
    public sealed record App(string AppId, string Nome, string Perfil, Grupo[] Barra, string[] TelasTreinamento, Item[] Itens)
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
                a.GetProperty("telasTreinamento").EnumerateArray().Select(x => x.GetString()!).ToArray(),
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
