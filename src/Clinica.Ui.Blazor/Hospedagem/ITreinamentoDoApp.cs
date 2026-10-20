namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>Um capítulo da videoaula (o passo a passo que leva a um ponto do vídeo).</summary>
public sealed record CapituloDaAula(double Inicio, string Titulo, string Texto);

/// <summary>Uma videoaula do treinamento, já recortada para as telas que este app publica.</summary>
public sealed record AulaDoTreinamento(
    string Id, string Titulo, string Categoria, string Descricao,
    IReadOnlyList<string> Funcoes, IReadOnlyList<CapituloDaAula> Capitulos, double Duracao)
{
    public string DuracaoTexto => TimeSpan.FromSeconds(Duracao).ToString(@"mm\:ss");
}

/// <summary>Até onde a pessoa assistiu, neste computador.</summary>
public sealed record ProgressoDaAula(double Posicao = 0, bool Concluida = false);

/// <summary>
/// O que a tela de Treinamento pede ao hospedeiro (out/2026 — era a TreinamentoView do WPF,
/// com um MediaElement). O catálogo, o progresso por usuário e o vídeo moram no shell, que
/// sabe quais telas este executável publica; a tela só desenha e toca o vídeo no navegador.
/// </summary>
public interface ITreinamentoDoApp
{
    /// <summary>As aulas das telas que este app publica, na ordem do catálogo.</summary>
    IReadOnlyList<AulaDoTreinamento> Aulas();

    ProgressoDaAula Progresso(string aulaId);

    /// <summary>Grava o progresso. Lança quando o disco recusa (a tela avisa, não derruba).</summary>
    void Salvar(string aulaId, double posicao, bool concluida);

    /// <summary>
    /// Garante o vídeo neste computador (baixa e confere o SHA-256 se preciso) e devolve o
    /// endereço que o navegador toca. Lança quando não há vídeo ou a rede falhou.
    /// </summary>
    Task<string> EnderecoDoVideoAsync(string aulaId, CancellationToken ct);
}

/// <summary>
/// O padrão da prévia e dos testes: nenhuma aula. A tela abre com o estado vazio — é o que
/// ela mostra num executável que não publica tela com videoaula.
/// </summary>
public sealed class TreinamentoSemAulas : ITreinamentoDoApp
{
    public IReadOnlyList<AulaDoTreinamento> Aulas() => [];
    public ProgressoDaAula Progresso(string aulaId) => new();
    public void Salvar(string aulaId, double posicao, bool concluida) { }
    public Task<string> EnderecoDoVideoAsync(string aulaId, CancellationToken ct)
        => Task.FromException<string>(new InvalidOperationException("Esta aula ainda não tem um vídeo publicado."));
}
