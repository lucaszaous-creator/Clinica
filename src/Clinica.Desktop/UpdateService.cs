using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Clinica.Desktop;

/// <summary>
/// Atualização automática via GitHub Releases (Velopack): verifica se há versão nova,
/// baixa em segundo plano e agenda a aplicação para o fechamento do app — na próxima
/// abertura o sistema já está atualizado, sem baixar exe manualmente.
/// Só funciona no app INSTALADO pelo Setup.exe das Releases; o exe portátil
/// (publish-exe.bat / artefato do CI) não se atualiza.
/// </summary>
public static class UpdateService
{
    private const string RepoUrl = "https://github.com/lucaszaous-creator/Clinica";

    private static bool _atualizacaoAgendada;

    /// <summary>Versão instalada atual (ex.: "1.0.9"), ou nulo no exe portátil/dev.</summary>
    public static string? VersaoInstalada
    {
        get
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));
                return mgr.IsInstalled ? mgr.CurrentVersion?.ToString() : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Verifica e baixa a próxima versão. Retorna o número da versão baixada
    /// (para avisar o usuário), ou nulo se já está atualizado / portátil / offline.
    /// A aplicação em si acontece ao fechar o app (WaitExitThenApplyUpdates).
    /// </summary>
    public static async Task<string?> VerificarEBaixarAsync()
    {
        if (_atualizacaoAgendada)
            return null; // já há uma versão baixada aguardando o próximo fechamento

        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));

            if (!mgr.IsInstalled)
                return null;

            var novidade = await mgr.CheckForUpdatesAsync();
            if (novidade is null)
                return null;

            await mgr.DownloadUpdatesAsync(novidade);

            // Aplica ao fechar o app; na próxima abertura já estará atualizado.
            mgr.WaitExitThenApplyUpdates(novidade);
            _atualizacaoAgendada = true;

            return novidade.TargetFullRelease.Version.ToString();
        }
        catch
        {
            // Falha (offline, GitHub fora etc.) é silenciosa — nunca impede o uso.
            return null;
        }
    }
}
