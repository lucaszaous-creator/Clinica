namespace Clinica.Atualizacao;

/// <summary>Uma versão nova encontrada na abertura.</summary>
public sealed record NovidadeNaAbertura(string Versao, bool Obrigatoria);

/// <summary>O que a abertura faz depois da verificação.</summary>
public enum DesfechoDaAbertura
{
    /// <summary>Segue para o login na versão atual (sem novidade, sem internet, demorou, não obrigatória, tentativas esgotadas).</summary>
    Seguir,
    /// <summary>O Velopack está instalando e reabrindo o app: a abertura para aqui.</summary>
    Reiniciando
}

/// <summary>Os passos de verdade (Velopack, disco) — a regra não os conhece, e os testes os trocam.</summary>
public interface IPassosDaAtualizacao
{
    /// <summary>Procura versão nova. Nulo = está atualizado. Sem internet, lança.</summary>
    Task<NovidadeNaAbertura?> ProcurarAsync(CancellationToken ct);

    /// <summary>Baixa a versão encontrada, avisando o progresso (0–100).</summary>
    Task BaixarAsync(IProgress<int> progresso, CancellationToken ct);

    /// <summary>Instala agora e reabre o app (encerra este processo).</summary>
    void InstalarEReabrir();

    /// <summary>Deixa instalada para quando o app fechar (a próxima abertura já entra atualizada).</summary>
    void InstalarAoFechar();

    /// <summary>Nulo = ainda pode tentar instalar esta versão; senão, o motivo (o limite do PR #324).</summary>
    string? MotivoParaNaoInstalar(string versao);

    /// <summary>Anota uma tentativa de instalar (ANTES de entregar ao Velopack).</summary>
    void RegistrarTentativa(string versao);
}

/// <summary>A tela "Atualizando…" (só aparece para a versão obrigatória).</summary>
public interface ITelaDeAtualizacao
{
    void Mostrar(string versao);
    void Progresso(int porcento);
    void Fechar();
}

/// <summary>
/// A ATUALIZAÇÃO NA ABERTURA, "como nos jogos" (nov/2026). Antes do login o app pergunta se há versão nova:
/// <list type="bullet">
/// <item><b>Sem internet, sem novidade ou procura lenta</b> (mais que <see cref="LimiteDaProcura"/>): segue
/// direto para o login, sem tela nenhuma.</item>
/// <item><b>Versão comum</b>: não segura ninguém — baixa em segundo plano e instala quando o app fechar.</item>
/// <item><b>Versão OBRIGATÓRIA</b> (marcada nas notas da release, <see cref="MarcaObrigatoria"/>: pedida no
/// disparo ou com migration): mostra "Atualizando…" com a barra, baixa, instala e reabre já na versão nova.
/// Passado <see cref="LimiteDaObrigatoria"/>, libera o login na versão atual e a instalação fica para o
/// fechamento.</item>
/// <item><b>O limite de tentativas do PR #324</b> vale antes de qualquer instalação: a instalação que falha
/// reabre o app antigo, e sem ele a tela voltaria sem fim.</item>
/// </list>
/// Nunca lança: nada aqui pode impedir a clínica de abrir o app.
/// </summary>
public static class AtualizacaoNaAbertura
{
    /// <summary>A linha que o release.yml escreve nas notas da versão obrigatória.</summary>
    public const string MarcaObrigatoria = "<!-- clinica:obrigatoria -->";

    /// <summary>Quanto a abertura espera a resposta "há versão nova?" — sem internet, o tempo de uma conexão recusada.</summary>
    public static readonly TimeSpan LimiteDaProcura = TimeSpan.FromSeconds(10);

    /// <summary>Quanto a tela "Atualizando…" segura o login na versão obrigatória.</summary>
    public static readonly TimeSpan LimiteDaObrigatoria = TimeSpan.FromMinutes(2);

    /// <summary>A versão é obrigatória? Lê a marca nas notas (markdown) da release.</summary>
    public static bool EhObrigatoria(string? notas)
        => !string.IsNullOrEmpty(notas) && notas.Contains(MarcaObrigatoria, StringComparison.OrdinalIgnoreCase);

    public static async Task<DesfechoDaAbertura> ExecutarAsync(IPassosDaAtualizacao passos, ITelaDeAtualizacao tela,
        Action<string, Exception?> registrar, TimeSpan? limiteDaProcura = null, TimeSpan? limiteDaObrigatoria = null)
    {
        NovidadeNaAbertura? novidade;
        try
        {
            using var prazo = new CancellationTokenSource(limiteDaProcura ?? LimiteDaProcura);
            var procura = passos.ProcurarAsync(prazo.Token);
            if (await Task.WhenAny(procura, Task.Delay(Timeout.Infinite, prazo.Token)).ConfigureAwait(false) != procura)
            {
                Observar(procura, registrar);
                registrar("Atualização — a procura passou do tempo; o app abre na versão atual", null);
                return DesfechoDaAbertura.Seguir;
            }
            novidade = await procura.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Sem internet, GitHub fora do ar, proxy: abre direto.
            registrar("Atualização — não foi possível procurar versão nova; o app abre na versão atual", ex);
            return DesfechoDaAbertura.Seguir;
        }
        if (novidade is null) return DesfechoDaAbertura.Seguir;

        if (passos.MotivoParaNaoInstalar(novidade.Versao) is { } motivo)
        {
            registrar("Atualização — instalação suspensa para não repetir a tela", new InvalidOperationException(motivo));
            return DesfechoDaAbertura.Seguir;
        }

        if (!novidade.Obrigatoria)
        {
            // Não segura ninguém: baixa por trás e instala ao fechar.
            _ = EmSegundoPlanoAsync(passos, new Progress<int>(), registrar);
            return DesfechoDaAbertura.Seguir;
        }

        var progresso = new Progress<int>(tela.Progresso);
        tela.Mostrar(novidade.Versao);
        Task download;
        try { download = passos.BaixarAsync(progresso, CancellationToken.None); }
        catch (Exception ex)
        {
            tela.Fechar();
            registrar("Atualização obrigatória — o download não começou; o app abre na versão atual", ex);
            return DesfechoDaAbertura.Seguir;
        }
        var venceu = await Task.WhenAny(download, Task.Delay(limiteDaObrigatoria ?? LimiteDaObrigatoria)).ConfigureAwait(false);
        if (venceu != download)
        {
            tela.Fechar();
            registrar($"Atualização obrigatória {novidade.Versao} — passou de {(limiteDaObrigatoria ?? LimiteDaObrigatoria).TotalMinutes:0} min; " +
                      "o login abre na versão atual e a instalação fica para o fechamento", null);
            _ = AoTerminarAsync(download, passos, registrar);
            return DesfechoDaAbertura.Seguir;
        }
        try
        {
            await download.ConfigureAwait(false);
            passos.RegistrarTentativa(novidade.Versao);
            passos.InstalarEReabrir();
            return DesfechoDaAbertura.Reiniciando;
        }
        catch (Exception ex)
        {
            tela.Fechar();
            registrar("Atualização obrigatória — falhou; o app abre na versão atual", ex);
            return DesfechoDaAbertura.Seguir;
        }
    }

    private static async Task EmSegundoPlanoAsync(IPassosDaAtualizacao passos, IProgress<int> progresso, Action<string, Exception?> registrar)
    {
        try
        {
            await passos.BaixarAsync(progresso, CancellationToken.None).ConfigureAwait(false);
            passos.InstalarAoFechar();
        }
        catch (Exception ex) { registrar("Atualização — download em segundo plano falhou", ex); }
    }

    private static async Task AoTerminarAsync(Task download, IPassosDaAtualizacao passos, Action<string, Exception?> registrar)
    {
        try
        {
            await download.ConfigureAwait(false);
            passos.InstalarAoFechar();
        }
        catch (Exception ex) { registrar("Atualização — download depois do tempo limite falhou", ex); }
    }

    private static void Observar(Task tarefa, Action<string, Exception?> registrar)
        => tarefa.ContinueWith(t => registrar("Atualização — procura tardia falhou", t.Exception?.GetBaseException()),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
}
