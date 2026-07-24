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

    // Atualização baixada e pronta (pelo botão "Atualizar agora"), aguardando a decisão do usuário
    // de reiniciar na hora ou aplicar ao fechar. Guardados para reusar o mesmo download nas duas vias.
    private static UpdateManager? _mgrPreparado;
    private static UpdateInfo? _updatePreparado;

    /// <summary>True quando a instalação suporta auto-update (app instalado pelo Setup.exe, não portátil).</summary>
    public static bool SuportaAutoUpdate
    {
        get
        {
            try { return new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false)).IsInstalled; }
            catch { return false; }
        }
    }

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
        catch
        {
            return false; // qualquer falha: abre normalmente na versão atual
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

            return novidade.TargetFullRelease.Version.ToString();
        }
        catch
        {
            // Falha (offline, GitHub fora etc.) é silenciosa — nunca impede o uso.
            return null;
        }
    }

    /// <summary>Situação de uma verificação manual de atualização (botão "Atualizar agora").</summary>
    public enum SituacaoAtualizacao
    {
        /// <summary>Instalação portátil/dev: não há auto-update.</summary>
        SemSuporte,
        /// <summary>Já está na versão mais recente.</summary>
        JaAtualizado,
        /// <summary>Há versão nova, já baixada e pronta para aplicar.</summary>
        Pronta,
        /// <summary>Falha ao verificar/baixar (offline, GitHub fora etc.).</summary>
        Falha
    }

    /// <summary>Resultado da verificação manual: situação e, quando pronta, a versão baixada.</summary>
    public readonly record struct AtualizacaoManual(SituacaoAtualizacao Situacao, string? Versao);

    /// <summary>
    /// Verificação manual (botão "Atualizar agora"): checa e BAIXA a versão nova, deixando-a pronta
    /// para aplicar. Não reinicia sozinha — quem chama decide entre <see cref="AplicarEReiniciar"/>
    /// (na hora) ou <see cref="AplicarAoFechar"/>. Nunca lança.
    /// </summary>
    public static async Task<AtualizacaoManual> ProcurarEBaixarAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, prerelease: false));
            if (!mgr.IsInstalled)
                return new AtualizacaoManual(SituacaoAtualizacao.SemSuporte, null);

            var novidade = await mgr.CheckForUpdatesAsync();
            if (novidade is null)
                return new AtualizacaoManual(SituacaoAtualizacao.JaAtualizado, null);

            await mgr.DownloadUpdatesAsync(novidade);
            _mgrPreparado = mgr;
            _updatePreparado = novidade;
            return new AtualizacaoManual(SituacaoAtualizacao.Pronta, novidade.TargetFullRelease.Version.ToString());
        }
        catch
        {
            return new AtualizacaoManual(SituacaoAtualizacao.Falha, null);
        }
    }

    /// <summary>Aplica a atualização já baixada e reinicia o app na hora. Só após <see cref="ProcurarEBaixarAsync"/>.</summary>
    public static void AplicarEReiniciar()
    {
        if (_mgrPreparado is { } mgr && _updatePreparado is { } up)
            mgr.ApplyUpdatesAndRestart(up); // encerra este processo e reabre atualizado
    }

    /// <summary>Agenda a atualização já baixada para o fechamento do app (aplica na próxima abertura).</summary>
    public static void AplicarAoFechar()
    {
        if (_mgrPreparado is { } mgr && _updatePreparado is { } up)
        {
            mgr.WaitExitThenApplyUpdates(up);
            _atualizacaoAgendada = true; // evita que o ciclo periódico baixe de novo
        }
    }
}
