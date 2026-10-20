using Microsoft.AspNetCore.Components;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// Os FORMULÁRIOS abertos por CÓDIGO (out/2026 — a conversão da área do SafeID/assinatura para
/// Blazor). Até aqui todo formulário Blazor era aberto pela MARCAÇÃO da tela dona
/// (<c>@if (_aberto) { &lt;XForm /&gt; }</c>). Os que substituíram as janelas WPF da assinatura
/// (o seletor do A1, a coleta do termo, a folha de execução, a prescrição de infusão) são abertos
/// por um SERVIÇO — a ponte que a tela já chamava (<c>IJanelasDaSalaDeInfusao.AbrirFolhaAsync</c>)
/// e, de dentro de um formulário, outro formulário (a folha pede o certificado). Este serviço é a
/// porta deles: abre, espera a resposta e devolve — como o <see cref="Dialogos"/>, mas para um
/// componente inteiro.
///
/// Um por navegador (Scoped). Quem DESENHA é a <c>CamadaDeJanelas</c> (no <c>PalcoDeTelas</c> e na
/// <c>Raiz</c>); quem abre só espera. As regras:
/// - todos os abertos ficam MONTADOS, empilhados — o de cima por último —, porque o de baixo
///   espera a resposta do de cima com o estado dele vivo (a folha com o certificado por cima);
/// - cada formulário pertence à TELA em que foi aberto: trocar de tela pelo menu o esconde
///   (sem desmontar — perder a folha pela metade é o pior desfecho) e voltar o mostra;
/// - o formulário responde por <c>AoConcluir</c> (com o resultado) ou <c>AoFechar</c> (= desistiu,
///   devolve o padrão do tipo): é a convenção de todos os formulários da casa.
/// </summary>
public sealed class JanelasNaTela
{
    private readonly List<JanelaAberta> _abertas = [];

    /// <summary>A tela à vista (o Palco avisa); nulo fora do palco (a prévia, o bUnit, a janela avulsa).</summary>
    public int? TelaAtual { get; set; }

    /// <summary>Os formulários abertos, de baixo para cima.</summary>
    public IReadOnlyList<JanelaAberta> Abertas => _abertas;

    /// <summary>A camada se redesenha quando abre ou fecha um formulário.</summary>
    public event Action? Mudou;

    /// <summary>
    /// Abre <typeparamref name="TFormulario"/> com os parâmetros e espera a resposta. O formulário
    /// recebe <c>AoConcluir</c> (<see cref="EventCallback{T}"/> de <typeparamref name="T"/>) e
    /// <c>AoFechar</c> (<see cref="EventCallback"/>); fechar devolve <c>default</c>.
    /// </summary>
    public Task<T?> AbrirAsync<TFormulario, T>(IDictionary<string, object?>? parametros = null)
        where TFormulario : IComponent
    {
        var janela = new JanelaAberta(typeof(TFormulario), TelaAtual);
        var resposta = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var p = new Dictionary<string, object?>(parametros ?? new Dictionary<string, object?>())
        {
            ["AoConcluir"] = EventCallback.Factory.Create<T>(janela, (T valor) => Fechar(janela, () => resposta.TrySetResult(valor))),
            ["AoFechar"] = EventCallback.Factory.Create(janela, () => Fechar(janela, () => resposta.TrySetResult(default)))
        };
        janela.Parametros = p;
        janela.Desistir = () => resposta.TrySetResult(default);
        _abertas.Add(janela);
        Mudou?.Invoke();
        return resposta.Task;
    }

    private void Fechar(JanelaAberta janela, Action responder)
    {
        if (_abertas.Remove(janela)) Mudou?.Invoke();
        responder();
    }

    /// <summary>
    /// A camada saiu de cena (o navegador ou a janela avulsa fechou): quem esperava desiste. Seguro
    /// por construção — desistir nunca é a porta que grava.
    /// </summary>
    public void DesistirDeTodos()
    {
        if (_abertas.Count == 0) return;
        var abertas = _abertas.ToList();
        _abertas.Clear();
        Mudou?.Invoke();
        foreach (var j in abertas) j.Desistir?.Invoke();
    }
}

/// <summary>Um formulário aberto pela <see cref="JanelasNaTela"/>.</summary>
public sealed class JanelaAberta(Type tipo, int? tela)
{
    public Type Tipo { get; } = tipo;

    /// <summary>A tela em que foi aberto — some quando ela sai de vista, volta quando ela volta.</summary>
    public int? Tela { get; } = tela;

    public IDictionary<string, object?> Parametros { get; internal set; } = new Dictionary<string, object?>();
    internal Action? Desistir { get; set; }

    /// <summary>A chave estável do componente na camada (não remonta ao redesenhar).</summary>
    public Guid Chave { get; } = Guid.NewGuid();
}
