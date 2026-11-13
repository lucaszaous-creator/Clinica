using System.IO;
using Clinica.Desktop.Shell.Configuracao;
using Velopack;

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
/// Downloads demorados continuam em segundo plano e são aplicados ao fechar.
/// A verificação periódica recebe releases publicadas com o sistema já aberto.
///
/// Cada versão tem no máximo <see cref="Clinica.Atualizacao.TentativasDeAtualizacao.Limite"/>
/// tentativas de instalação por janela de horas: quando o Velopack não consegue trocar a
/// pasta, ele reabre o app antigo, e sem esse limite a tela de atualização se repetia sem
/// fim (clínica, 26/10/2026 — ver <c>docs/agentes/convencoes.md</c>).
///
/// Nunca lança: falha de rede não pode impedir o app de abrir.
/// </summary>
public static class AtualizadorSuite
{
    private static readonly SemaphoreSlim Exclusao = new(1, 1);
    private static int _agendada;
    private static Task? _ciclo;

    // Ao lado dos logs (fora da pasta versionada), um arquivo por app: no fallback de
    // %APPDATA% a pasta de logs é dividida pelos quatro.
    private static readonly Clinica.Atualizacao.TentativasDeAtualizacao Tentativas = new(Path.Combine(
        LogSuite.Pasta, $"atualizacao-{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "app"}.json"));

    /// <summary>Versão instalada (ex.: "1.0.9"), ou nulo no exe portátil/dev.</summary>
    public static string? VersaoInstalada
    {
        get
        {
            try
            {
                var mgr = new UpdateManager(new Clinica.Atualizacao.FonteAtualizacaoGithub());
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
            var mgr = new UpdateManager(new Clinica.Atualizacao.FonteAtualizacaoGithub());

            // Exe portátil (artefato do CI): o Velopack se considera não instalado.
            if (!mgr.IsInstalled)
                return false;

            var novidade = await Clinica.Atualizacao.DownloadComPrazo.AguardarAsync(
                ChecarEBaixarAsync(mgr), limite,
                pronta => Agendar(mgr, pronta),
                ex => LogSuite.Registrar("Atualização — download após abertura falhou", ex));
            if (novidade is null)
                return false;

            // A instalação que falha reabre ESTE app na versão antiga, e ele chegaria aqui de
            // novo com a mesma novidade: sem o limite, a tela de atualização não sai mais.
            var alvo = novidade.TargetFullRelease.Version.ToString();
            if (Tentativas.MotivoParaNaoAplicar(alvo, DateTime.UtcNow) is { } motivo)
            {
                LogSuite.Registrar("Atualização — instalação suspensa para não repetir a tela",
                    new InvalidOperationException(motivo));
                return false;
            }
            Tentativas.Registrar(alvo, DateTime.UtcNow);

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
        await Exclusao.WaitAsync();
        try
        {
            if (Volatile.Read(ref _agendada) != 0) return null;
            var novidade = await mgr.CheckForUpdatesAsync();
            if (novidade is null) return null;
            await mgr.DownloadUpdatesAsync(novidade);
            return novidade;
        }
        finally { Exclusao.Release(); }
    }

    private static void Agendar(UpdateManager mgr, UpdateInfo pronta)
    {
        // Suspensa: a abertura já registrou o motivo; a cada 30 min seria o mesmo log.
        var alvo = pronta.TargetFullRelease.Version.ToString();
        if (Tentativas.MotivoParaNaoAplicar(alvo, DateTime.UtcNow) is not null) return;
        if (Interlocked.CompareExchange(ref _agendada, 1, 0) != 0) return;
        Tentativas.Registrar(alvo, DateTime.UtcNow);
        try { mgr.WaitExitThenApplyUpdates(pronta); }
        catch { Volatile.Write(ref _agendada, 0); throw; }
    }

    public static void IniciarVerificacaoPeriodica()
        => _ciclo ??= VerificarPeriodicamenteAsync();

    private static async Task VerificarPeriodicamenteAsync()
    {
        // Sem reinício automático durante o atendimento. Aplicação somente ao sair.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (Volatile.Read(ref _agendada) == 0 && await timer.WaitForNextTickAsync())
        {
            try
            {
                var mgr = new UpdateManager(new Clinica.Atualizacao.FonteAtualizacaoGithub());
                if (!mgr.IsInstalled) return;
                if (await ChecarEBaixarAsync(mgr) is { } pronta) Agendar(mgr, pronta);
            }
            catch (Exception ex) { LogSuite.Registrar("Atualização — verificação em segundo plano falhou", ex); }
        }
    }
}
