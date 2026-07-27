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

    // Atualização já baixada em segundo plano e agendada para o fechamento (WaitExitThenApplyUpdates).
    // Guardadas para que o botão "Atualizar" possa aplicá-la NA HORA (ApplyUpdatesAndRestart).
    private static UpdateManager? _mgrPreparado;
    private static UpdateInfo? _updatePreparado;

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
            catch (Exception ex)
            {
                Configuracao.LogErros.Registrar("Atualização — versão instalada não pôde ser lida", ex);
                return null;
            }
        }
    }

    /// <summary>
    /// Atualização na abertura: se houver versão nova, baixa e APLICA imediatamente,
    /// reiniciando o app já atualizado. Retorna true quando o reinício foi disparado
    /// (o chamador deve abortar a inicialização). O limite de tempo garante que uma
    /// rede lenta/offline não trave a abertura — nesse caso o fluxo em segundo plano
    /// (verificação periódica) assume.
    /// </summary>
    public static async Task<bool> AtualizarNaAberturaAsync(TimeSpan limite)
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));

            if (!mgr.IsInstalled)
                return false;

            var baixar = ChecarEBaixarAsync(mgr);
            var vencedor = await Task.WhenAny(baixar, Task.Delay(limite));
            if (vencedor != baixar)
                return false; // demorou demais: abre normalmente; o ciclo de 2h cuida depois

            var novidade = await baixar;
            if (novidade is null)
                return false;

            mgr.ApplyUpdatesAndRestart(novidade); // encerra este processo e reabre atualizado
            return true;
        }
        catch (Exception ex)
        {
            // Qualquer falha: abre normalmente na versão atual — mas registrada, senão
            // "por que não atualizou?" vira adivinhação.
            Configuracao.LogErros.Registrar("Atualização — verificação na abertura falhou", ex);
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

    /// <summary>
    /// Verificação em segundo plano (ciclo periódico com o app aberto): baixa a próxima
    /// versão e retorna o número dela (para avisar o usuário), ou nulo se já está
    /// atualizado / portátil / offline. A aplicação acontece ao fechar o app.
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

            // Guarda o download pronto para o botão "Atualizar" poder aplicar na hora, se o usuário quiser.
            _mgrPreparado = mgr;
            _updatePreparado = novidade;

            return novidade.TargetFullRelease.Version.ToString();
        }
        catch (Exception ex)
        {
            // Falha (offline, GitHub fora etc.) nunca impede o uso, mas fica no log.
            Configuracao.LogErros.Registrar("Atualização — download em segundo plano falhou", ex);
            return null;
        }
    }

    /// <summary>
    /// Aplica NA HORA a atualização já baixada em segundo plano (por <see cref="VerificarEBaixarAsync"/>)
    /// e reinicia o app já atualizado. Usado pelo botão "Atualizar"; se ninguém chamar, o mesmo download
    /// é aplicado ao fechar o app (já agendado). Nunca lança.
    /// </summary>
    public static void AplicarEReiniciar()
    {
        try
        {
            if (_mgrPreparado is { } mgr && _updatePreparado is { } up)
                mgr.ApplyUpdatesAndRestart(up); // encerra este processo e reabre atualizado
        }
        catch (Exception ex)
        {
            // Se a aplicação imediata falhar, a versão segue agendada para o fechamento.
            Configuracao.LogErros.Registrar("Atualização — aplicação imediata falhou", ex);
        }
    }
}
