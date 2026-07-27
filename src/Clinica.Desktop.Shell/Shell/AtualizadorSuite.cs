using Clinica.Desktop.Shell.Configuracao;
using Velopack;
using Velopack.Sources;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Auto-update dos apps da suíte (Recepção, Financeiro, Gerente Geral) via
/// Velopack + GitHub Releases, no mesmo repositório do faturamento.
///
/// Cada app tem seu próprio <b>canal</b> de release (definido no <c>vpk pack</c>), e é o
/// canal que separa as versões dentro da mesma release do GitHub: o app instalado só
/// enxerga o <c>releases.&lt;canal&gt;.json</c> dele. O faturamento continua no canal
/// padrão (<c>win</c>) — é o que preserva as instalações que já existem.
///
/// Aqui só existe a atualização NA ABERTURA (baixa e reinicia já atualizado). O ciclo
/// periódico com aviso ao usuário é do faturamento, que tem snackbar e rodapé para
/// mostrá-lo; quando o faturamento virar módulo (Fase 4), os dois se juntam.
///
/// Nunca lança: falha de rede não pode impedir o app de abrir.
/// </summary>
public static class AtualizadorSuite
{
    private const string RepoUrl = "https://github.com/lucaszaous-creator/Clinica";

    /// <summary>Versão instalada (ex.: "1.0.9"), ou nulo no exe portátil/dev.</summary>
    public static string? VersaoInstalada
    {
        get
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));
                return mgr.IsInstalled ? mgr.CurrentVersion?.ToString() : null;
            }
            catch (Exception ex)
            {
                LogSuite.Registrar("Atualização — versão instalada não pôde ser lida", ex);
                return null;
            }
        }
    }

    /// <summary>
    /// Havendo versão nova, baixa e aplica na hora, reiniciando o app já atualizado.
    /// Retorna <c>true</c> quando o reinício foi disparado — o chamador deve abortar a
    /// abertura. O limite de tempo garante que rede lenta/offline não trave o app.
    /// </summary>
    public static async Task<bool> AtualizarNaAberturaAsync(TimeSpan limite)
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));

            // Exe portátil (artefato do CI): o Velopack se considera não instalado.
            if (!mgr.IsInstalled)
                return false;

            var baixar = ChecarEBaixarAsync(mgr);
            if (await Task.WhenAny(baixar, Task.Delay(limite)) != baixar)
                return false; // demorou demais: abre na versão atual

            var novidade = await baixar;
            if (novidade is null)
                return false;

            mgr.ApplyUpdatesAndRestart(novidade); // encerra este processo e reabre atualizado
            return true;
        }
        catch (Exception ex)
        {
            // Abre normalmente na versão atual — mas registrada, senão "por que não
            // atualizou?" vira adivinhação.
            LogSuite.Registrar("Atualização — verificação na abertura falhou", ex);
            return false;
        }
    }

    private static async Task<UpdateInfo?> ChecarEBaixarAsync(UpdateManager mgr)
    {
        var novidade = await mgr.CheckForUpdatesAsync();
        if (novidade is null)
            return null;

        await mgr.DownloadUpdatesAsync(novidade);
        return novidade;
    }
}
