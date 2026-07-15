using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Clinica.Desktop;

/// <summary>
/// Atualização automática via GitHub Releases: ao abrir, verifica se há versão nova,
/// baixa em segundo plano e aplica na próxima abertura (transparente para o usuário).
/// </summary>
public static class UpdateService
{
    private const string RepoUrl = "https://github.com/lucaszaous-creator/Clinica";

    public static async Task VerificarEAtualizarAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));

            // Só atualiza quando instalado pelo instalador (no .exe portátil/dev não faz nada).
            if (!mgr.IsInstalled)
                return;

            var novidade = await mgr.CheckForUpdatesAsync();
            if (novidade is null)
                return;

            await mgr.DownloadUpdatesAsync(novidade);

            // Aplica ao fechar o app; na próxima abertura já estará atualizado.
            mgr.WaitExitThenApplyUpdates(novidade);
        }
        catch
        {
            // Qualquer falha (offline, etc.) é silenciosa — nunca deve impedir o uso do sistema.
        }
    }
}
