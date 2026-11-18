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
    /// A ATUALIZAÇÃO ANTES DO LOGIN (nov/2026, "como nos jogos"). A regra mora em
    /// <see cref="Clinica.Atualizacao.AtualizacaoNaAbertura"/>, com teste: sem internet ou sem novidade segue
    /// direto; versão comum baixa por trás e instala ao fechar; versão OBRIGATÓRIA mostra a
    /// <see cref="JanelaDeAtualizacao"/>, baixa, instala e reabre — com 2 min de limite. O limite de tentativas
    /// (<see cref="Tentativas"/>, PR #324) vale antes de qualquer instalação.
    /// Retorna <c>true</c> quando o Velopack está reabrindo o app: o chamador aborta a abertura.
    /// Chamar na thread da interface (a janela nasce nela).
    /// </summary>
    public static async Task<bool> AtualizarAntesDoLoginAsync(string nomeApp)
    {
        JanelaDeAtualizacao? janela = null;
        try
        {
            var mgr = new UpdateManager(new Clinica.Atualizacao.FonteAtualizacaoGithub());

            // Exe portátil (artefato do CI): o Velopack se considera não instalado.
            if (!mgr.IsInstalled)
                return false;

            janela = new JanelaDeAtualizacao(nomeApp);
            var desfecho = await Clinica.Atualizacao.AtualizacaoNaAbertura.ExecutarAsync(
                new PassosVelopack(mgr), janela, (frase, ex) => LogSuite.Registrar(frase, ex ?? new InvalidOperationException(frase)));
            if (desfecho == Clinica.Atualizacao.DesfechoDaAbertura.Reiniciando) return true;
            janela.Fechar();
            return false;
        }
        catch (Exception ex)
        {
            // Abre normalmente na versão atual — mas registrada, senão "por que não
            // atualizou?" vira adivinhação.
            janela?.Fechar();
            LogSuite.Registrar("Atualização — verificação na abertura falhou", ex);
            return false;
        }
    }

    /// <summary>Os passos de verdade da regra: o Velopack e a memória de tentativas.</summary>
    private sealed class PassosVelopack(UpdateManager mgr) : Clinica.Atualizacao.IPassosDaAtualizacao
    {
        private UpdateInfo? _novidade;

        public async Task<Clinica.Atualizacao.NovidadeNaAbertura?> ProcurarAsync(CancellationToken ct)
        {
            if (Volatile.Read(ref _agendada) != 0) return null;
            _novidade = await mgr.CheckForUpdatesAsync().WaitAsync(ct).ConfigureAwait(false);
            if (_novidade is null) return null;
            var alvo = _novidade.TargetFullRelease;
            return new(alvo.Version.ToString(), Clinica.Atualizacao.AtualizacaoNaAbertura.EhObrigatoria(alvo.NotesMarkdown));
        }

        public async Task BaixarAsync(IProgress<int> progresso, CancellationToken ct)
        {
            await Exclusao.WaitAsync(ct).ConfigureAwait(false);
            try { await mgr.DownloadUpdatesAsync(_novidade!, progresso.Report, ct).ConfigureAwait(false); }
            finally { Exclusao.Release(); }
        }

        public void InstalarEReabrir() => mgr.ApplyUpdatesAndRestart(_novidade!); // encerra este processo
        public void InstalarAoFechar() => Agendar(mgr, _novidade!);
        public string? MotivoParaNaoInstalar(string versao) => Tentativas.MotivoParaNaoAplicar(versao, DateTime.UtcNow);
        public void RegistrarTentativa(string versao) => Tentativas.Registrar(versao, DateTime.UtcNow);
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
