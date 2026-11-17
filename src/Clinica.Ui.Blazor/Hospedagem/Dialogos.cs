namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// Confirmação, pergunta de texto e aviso desenhados em HTML, dentro da própria tela.
///
/// Um por tela (Scoped: o BlazorWebView abre um escopo por controle, e a prévia um por
/// circuito). Quem DESENHA é a <c>CamadaDeDialogos</c> da <c>Raiz</c>; quem pergunta só
/// espera a resposta. As regras são as do shell, porque a pessoa não pode aprender duas:
/// - Cancelar uma pergunta de texto devolve <c>null</c>, NUNCA texto vazio (checagem 39 —
///   a porta rotulada "desisti" não pode ser a que efetiva o ato);
/// - pergunta <c>obrigatorio: false</c> aceita o vazio e devolve <c>string.Empty</c>;
/// - um diálogo por vez: o segundo espera o primeiro fechar.
/// </summary>
public sealed class Dialogos
{
    private readonly Queue<DialogoAberto> _fila = new();

    /// <summary>O diálogo no ar, ou null.</summary>
    public DialogoAberto? Atual => _fila.Count > 0 ? _fila.Peek() : null;

    /// <summary>A camada se redesenha quando abre ou fecha um diálogo.</summary>
    public event Action? Mudou;

    public Task<bool> ConfirmarAsync(string titulo, string mensagem, bool perigoso = false,
        string? confirmar = null)
    {
        var d = new DialogoAberto(TipoDialogo.Confirmacao, titulo, mensagem)
        {
            Perigoso = perigoso,
            RotuloConfirmar = confirmar ?? (perigoso ? "Sim, continuar" : "Confirmar")
        };
        return Abrir(d).ContinueWith(t => t.Result is not null, TaskScheduler.Default);
    }

    public Task<string?> PerguntarTextoAsync(string titulo, string pergunta,
        string? textoInicial = null, bool obrigatorio = true)
    {
        var d = new DialogoAberto(TipoDialogo.Texto, titulo, pergunta)
        {
            Texto = textoInicial ?? string.Empty,
            Obrigatorio = obrigatorio,
            RotuloConfirmar = "Confirmar"
        };
        return Abrir(d);
    }

    /// <summary>
    /// Uma pergunta com DUAS OU MAIS respostas nomeadas (nov/2026 — "é pelo plano ou é
    /// particular?"). Devolve o índice da opção, ou <c>null</c> quando a pessoa desiste:
    /// Cancelar e Esc nunca escolhem por ela, e nenhuma opção é a do Enter.
    /// </summary>
    public Task<int?> EscolherAsync(string titulo, string mensagem, IReadOnlyList<string> opcoes)
    {
        var d = new DialogoAberto(TipoDialogo.Escolha, titulo, mensagem) { Opcoes = opcoes };
        return Abrir(d).ContinueWith(
            t => int.TryParse(t.Result, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : (int?)null,
            TaskScheduler.Default);
    }

    /// <summary>Chamado pela camada: a pessoa clicou numa das opções da pergunta de escolha.</summary>
    public void Escolher(DialogoAberto d, int opcao)
    {
        if (_fila.Count == 0 || !ReferenceEquals(_fila.Peek(), d) || d.Tipo != TipoDialogo.Escolha
            || opcao < 0 || opcao >= d.Opcoes.Count) return;
        _fila.Dequeue();
        Mudou?.Invoke();
        d.Resposta.TrySetResult(opcao.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public Task AvisoAsync(string titulo, string mensagem)
    {
        var d = new DialogoAberto(TipoDialogo.Aviso, titulo, mensagem) { RotuloConfirmar = "Entendi" };
        return Abrir(d);
    }

    private Task<string?> Abrir(DialogoAberto d)
    {
        _fila.Enqueue(d);
        Mudou?.Invoke();
        return d.Resposta.Task;
    }

    /// <summary>
    /// A pessoa saiu da tela com uma pergunta aberta (trocou pelo menu): todas desistem.
    /// O serviço é UM por navegador e o navegador é um por janela (out/2026), então a
    /// pergunta da tela que saiu ficaria por cima da próxima — respondida ali, ela agiria
    /// sobre a tela que já não está à vista. Desistir é seguro por construção: a porta
    /// "desisti" nunca é a que efetiva o ato (checagem 39).
    /// </summary>
    public void DesistirDeTodos()
    {
        if (_fila.Count == 0) return;
        var abertos = _fila.ToList();
        _fila.Clear();
        Mudou?.Invoke();
        foreach (var d in abertos) d.Resposta.TrySetResult(null);
    }

    /// <summary>Chamado pela camada: confirma (com o texto, quando há) ou desiste (null).</summary>
    public void Responder(DialogoAberto d, bool confirmou)
    {
        if (_fila.Count == 0 || !ReferenceEquals(_fila.Peek(), d)) return;
        // A pergunta de escolha só se responde por uma OPÇÃO (Escolher); por aqui só se desiste.
        if (confirmou && d.Tipo == TipoDialogo.Escolha) return;
        if (confirmou && d.Tipo == TipoDialogo.Texto && d.Obrigatorio && string.IsNullOrWhiteSpace(d.Texto))
        {
            d.Erro = "Escreva a resposta para continuar — ou cancele.";
            Mudou?.Invoke();
            return;
        }
        _fila.Dequeue();
        Mudou?.Invoke();
        d.Resposta.TrySetResult(confirmou ? (d.Tipo == TipoDialogo.Texto ? d.Texto.Trim() : "ok") : null);
    }
}

public enum TipoDialogo { Confirmacao, Texto, Aviso, Escolha }

public sealed class DialogoAberto(TipoDialogo tipo, string titulo, string mensagem)
{
    public TipoDialogo Tipo { get; } = tipo;
    public string Titulo { get; } = titulo;
    public string Mensagem { get; } = mensagem;
    public bool Perigoso { get; init; }
    public bool Obrigatorio { get; init; }
    public string RotuloConfirmar { get; init; } = "Confirmar";

    /// <summary>As respostas da pergunta de escolha, na ordem dos botões.</summary>
    public IReadOnlyList<string> Opcoes { get; init; } = [];
    public string Texto { get; set; } = string.Empty;
    public string? Erro { get; set; }
    internal TaskCompletionSource<string?> Resposta { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
