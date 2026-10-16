namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O palco: UM navegador por janela, e as telas Blazor trocando DENTRO dele (out/2026).
///
/// A primeira versão criava um BlazorWebView por tela, e cada criação mostrava ~100 ms
/// PRETOS (a clínica mandou o vídeo: uma tela preta a cada troca). A janela nativa do
/// WebView2 aparece antes de o navegador existir dentro dela, e nenhuma cor configurada vale
/// nesse instante — o único jeito de não haver o instante é não criar navegador a cada tela.
/// É o desenho dos aplicativos que rodam sobre um motor web (VS Code, Teams): o motor nasce
/// uma vez, e navegar é trocar o conteúdo.
///
/// As telas já abertas continuam MONTADAS, só ocultas: voltar a uma delas é instantâneo e
/// devolve a tela como a pessoa deixou (filtro, mês, rolagem) — e ela relê os dados em
/// silêncio (<see cref="SinalDeVolta"/>), porque outra máquina pode ter gravado no meio.
/// </summary>
public sealed class Palco
{
    public sealed record Entrada(int Id, Type Tela, SinalDeVolta Sinal);

    private readonly List<Entrada> _telas = [];
    private int _sequencia;
    private bool _montou;

    public IReadOnlyList<Entrada> Telas => _telas;

    /// <summary>A tela à vista. Nulo quando o hospedeiro está mostrando uma tela WPF.</summary>
    public int? Atual { get; private set; }

    /// <summary>A lista ou a tela atual mudou — o componente redesenha.</summary>
    public event Action? Mudou;

    /// <summary>
    /// O navegador desenhou a primeira tela. O hospedeiro WPF só mostra a janela nativa a
    /// partir daqui (antes disso ela é preta).
    /// </summary>
    public event Action? Montou;

    /// <summary>
    /// Põe a tela à vista: monta na primeira vez; nas seguintes, reaproveita e avisa a volta
    /// (releitura silenciosa) — só quando ela não era a que já estava à vista.
    /// </summary>
    public void Ativar(Type tela)
    {
        var entrada = _telas.Find(e => e.Tela == tela);
        var nova = entrada is null;
        if (entrada is null)
        {
            entrada = new Entrada(++_sequencia, tela, new SinalDeVolta());
            _telas.Add(entrada);
        }
        var voltou = !nova && Atual != entrada.Id;
        Atual = entrada.Id;
        Mudou?.Invoke();
        if (voltou) entrada.Sinal.Avisar();
    }

    /// <summary>O hospedeiro passou a mostrar uma tela WPF: nenhuma tela Blazor à vista.</summary>
    public void Desativar()
    {
        if (Atual is null) return;
        Atual = null;
        Mudou?.Invoke();
    }

    /// <summary>Chamado pelo componente depois de desenhar com alguma tela montada.</summary>
    public void AvisarMontagem()
    {
        if (_montou || _telas.Count == 0) return;
        _montou = true;
        Montou?.Invoke();
    }

    /// <summary>Já houve a primeira renderização com tela — o hospedeiro pode mostrar.</summary>
    public bool Montado => _montou;
}
