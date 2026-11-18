using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack.Sources;

namespace Clinica.Atualizacao;

/// <summary>
/// De onde os cinco apps se atualizam (Velopack + GitHub Releases).
///
/// <para><b>Nov/2026 — o código vai para um repositório privado.</b> As releases (instaladores, pacotes e
/// <c>releases.&lt;canal&gt;.json</c>) passam a morar num repositório PÚBLICO só de instaladores,
/// <see cref="Repositorio"/>, sem código. Durante a transição o app lê ali primeiro e, enquanto aquele
/// repositório não existir ou não tiver release de algum app, completa canal a canal com o
/// <see cref="RepositorioAnterior"/> (vence a versão maior) —
/// assim a versão que muda o endereço pode sair ANTES de o repositório novo ficar pronto sem prender
/// nenhuma máquina. Roteiro em <c>docs/atualizacoes.md</c>. Nenhum token vai no app: os dois são públicos.</para>
///
/// <para>Os cinco canais precisam ser encontrados mesmo fora das dez primeiras releases (o
/// <see cref="GithubSource"/> padrão só lê a primeira página).</para>
/// </summary>
public sealed class FonteAtualizacaoGithub : GithubSource
{
    /// <summary>O repositório público só de instaladores — de onde os apps se atualizam.</summary>
    public const string Repositorio = "https://github.com/lucaszaous-creator/clinica-atualizacoes";

    /// <summary>O repositório do código, que publicava as releases até nov/2026 (fica privado).</summary>
    public const string RepositorioAnterior = "https://github.com/lucaszaous-creator/Clinica";

    public FonteAtualizacaoGithub(IFileDownloader? downloader = null)
        : base(Repositorio, null, false, downloader) { }

    /// <summary>Os cinco canais: faturamento (win) e os quatro apps da suíte.</summary>
    public static readonly IReadOnlyList<string> Canais = ["win", "recepcao", "financeiro", "gerente", "clinico"];

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        Dictionary<string, (Version Versao, GithubRelease Release)>? doNovo = null;
        try
        {
            doNovo = await ListarAsync(new Uri(Repositorio), includePrereleases).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // O repositório novo ainda não existe: a transição ainda não começou.
        }
        if (doNovo is not null && Canais.All(doNovo.ContainsKey))
            return doNovo.Values.Select(r => r.Release).ToArray();

        // Transição: falta algum canal no novo — o de antes completa, canal a canal, e vence a versão maior.
        // Depois de o Clinica ficar privado, ele responde 404 e o novo basta.
        Dictionary<string, (Version Versao, GithubRelease Release)> doAnterior;
        try
        {
            doAnterior = await ListarAsync(new Uri(RepositorioAnterior), includePrereleases).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && doNovo is not null)
        {
            doAnterior = [];
        }
        var juntos = new Dictionary<string, (Version Versao, GithubRelease Release)>(doAnterior);
        foreach (var (canal, novo) in doNovo ?? [])
            if (!juntos.TryGetValue(canal, out var antigo) || novo.Versao >= antigo.Versao)
                juntos[canal] = novo;
        return juntos.Values.Select(r => r.Release).ToArray();
    }

    private async Task<Dictionary<string, (Version Versao, GithubRelease Release)>> ListarAsync(Uri repositorio, bool includePrereleases)
    {
        var porCanal = new Dictionary<string, (Version Versao, GithubRelease Release)>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var url = new Uri(GetApiBaseUrl(repositorio), $"repos{repositorio.AbsolutePath}/releases?per_page=100&page={pagina}");
            var resposta = await Downloader.DownloadString(url.ToString(), GetRequestHeaders("application/vnd.github+json"))
                .ConfigureAwait(false);
            using var documento = JsonDocument.Parse(resposta);
            if (documento.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("O GitHub não retornou a lista de atualizações.");
            foreach (var item in documento.RootElement.EnumerateArray())
            {
                if (item.GetProperty("draft").GetBoolean() || (!includePrereleases && item.GetProperty("prerelease").GetBoolean())) continue;
                var tag = item.GetProperty("tag_name").GetString() ?? "";
                var partes = Regex.Match(tag, @"^(?:(clinico|gerente|recepcao|financeiro)-)?v(\d+\.\d+\.\d+)$");
                if (!partes.Success || !Version.TryParse(partes.Groups[2].Value, out var versao)) continue;
                var canal = partes.Groups[1].Success ? partes.Groups[1].Value : "win";
                var release = JsonSerializer.Deserialize<GithubRelease>(item.GetRawText());
                if (release is null || !release.Assets.Any(a => a.Name == $"releases.{canal}.json")) continue;
                if (!porCanal.TryGetValue(canal, out var anterior) || versao > anterior.Versao)
                    porCanal[canal] = (versao, release);
            }
            if (documento.RootElement.GetArrayLength() < 100)
                return porCanal;
        }
        // Não declarar "atualizado" se a consulta ficou incompleta.
        throw new InvalidOperationException("A lista de atualizações excedeu o limite de consulta. Tente novamente ou procure o suporte.");
    }
}
