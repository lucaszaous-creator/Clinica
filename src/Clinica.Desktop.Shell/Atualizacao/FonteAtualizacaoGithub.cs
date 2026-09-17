using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack.Sources;

namespace Clinica.Atualizacao;

/// <summary>Os cinco canais precisam ser encontrados mesmo fora das dez primeiras releases.</summary>
public sealed class FonteAtualizacaoGithub : GithubSource
{
    public FonteAtualizacaoGithub(IFileDownloader? downloader = null)
        : base("https://github.com/lucaszaous-creator/Clinica", null, false, downloader) { }

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        var porCanal = new Dictionary<string, (Version Versao, GithubRelease Release)>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var url = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases?per_page=100&page={pagina}");
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
                return porCanal.Values.Select(r => r.Release).ToArray();
        }
        // Não declarar "atualizado" se a consulta ficou incompleta.
        throw new InvalidOperationException("A lista de atualizações excedeu o limite de consulta. Tente novamente ou procure o suporte.");
    }
}
