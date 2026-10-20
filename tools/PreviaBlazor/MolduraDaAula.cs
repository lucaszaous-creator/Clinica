using Clinica.Ui.Blazor.Hospedagem;
using Clinica.Ui.Blazor.Moldura;

namespace PreviaBlazor;

/// <summary>
/// A moldura do app na prévia (out/2026, para as videoaulas): o MESMO <see cref="MolduraDoApp"/>
/// que os cinco executáveis desenham — logo, menu do topo, abas das telas compostas —, com o
/// menu lido da navegação real (<see cref="NavegacaoDaPrevia"/>). Clicar no menu ou numa aba
/// troca a tela do palco, como no app; o sino, a busca e a conta ficam quietos, porque a aula
/// é sobre a tela, e um sino com números inventados ensinaria a ler o que não existe.
/// </summary>
public sealed class MolduraDaAula : IMolduraDoApp
{
    private readonly NavegacaoDaPrevia.App _app;
    private NavegacaoDaPrevia.Item? _pai;
    private int _aba;

    public MolduraDaAula(NavegacaoDaPrevia.App app, string chave, string usuario)
    {
        _app = app;
        UsuarioRotulo = usuario;
        Abrir(chave);
    }

    public string NomeDoApp => _app.Nome;
    public string UsuarioRotulo { get; }
    public string DataHoje { get; } = DateTime.Today.ToString("ddd, dd/MM/yyyy", new System.Globalization.CultureInfo("pt-BR"));
    public Palco Palco { get; } = new();

    public IReadOnlyList<GrupoDaBarra> Barra => _app.Barra
        .Select(g => new GrupoDaBarra(g.Rotulo, g.Direto is { } d ? Item(d) : null, g.Itens.Select(Item).ToList()))
        .ToList();

    private ItemDaBarra Item(string chave)
    {
        var i = _app.ItemDe(chave);
        return new ItemDaBarra(chave, i?.Rotulo ?? chave, i?.Descricao, chave == _pai?.Chave);
    }

    public string TituloTela { get; private set; } = "";

    public IReadOnlyList<AbaDaTela> Abas => _pai is { Abas.Length: > 1 } p
        ? p.Abas.Select((a, i) => new AbaDaTela(a.Rotulo, i == _aba)).ToList()
        : [];

    public string? FalhaDaTela { get; private set; }
    public bool AtualizandoTela => false;

    /// <summary>A chave da tela à vista — a gravação a lê para conferir onde está.</summary>
    public string ChaveAtual { get; private set; } = "";

    public void Abrir(string chave)
    {
        var item = _app.ItemDe(chave);
        if (item is { Abas.Length: > 0 })
        {
            _pai = item;
            _aba = 0;
            Mostrar(item.Abas[0].Chave);
            return;
        }
        var visiveis = _app.Barra.SelectMany(g => g.Direto is { } d ? g.Itens.Append(d) : g.Itens).ToHashSet();
        _pai = _app.Itens.FirstOrDefault(p => visiveis.Contains(p.Chave) && p.Abas.Any(a => a.Chave == chave))
               ?? (visiveis.Contains(chave) ? item : null);
        _aba = _pai is null ? 0 : Math.Max(0, Array.FindIndex(_pai.Abas, a => a.Chave == chave));
        Mostrar(chave);
    }

    private void Mostrar(string chave)
    {
        ChaveAtual = chave;
        var item = _app.ItemDe(chave);
        TituloTela = _pai?.Rotulo ?? item?.Rotulo ?? chave;
        var tipo = NavegacaoDaPrevia.Tipo(item?.Componente);
        if (tipo is null)
        {
            FalhaDaTela = $"A tela \"{chave}\" não tem componente na prévia.";
            Palco.Desativar();
        }
        else
        {
            FalhaDaTela = null;
            Palco.Ativar(tipo);
        }
        Mudou?.Invoke();
    }

    // ----- busca: quieta na aula -----
    public string TextoPesquisa { get; set; } = "";
    public bool PesquisaAberta => false;
    public IReadOnlyList<ResultadoDaPesquisa> ResultadosPesquisa => [];
    public void AbrirResultado(ResultadoDaPesquisa? resultado) { }
    public void FecharPesquisa() { }

    // ----- navegação -----
    public void Navegar(string chave) => Abrir(chave);

    public void SelecionarAba(int indice)
    {
        if (_pai is null || indice < 0 || indice >= _pai.Abas.Length) return;
        _aba = indice;
        Mostrar(_pai.Abas[indice].Chave);
    }

    public void AbrirTreinamento() { }

    // ----- o resto do cabeçalho: quieto -----
    public int AssinaturasPendentes => 0;
    public string ResumoAssinaturas => "";
    public void AbrirFilaDeInfusao() { }
    public bool SinoAberto => false;
    public string ContadorDoSino => "";
    public string ResumoDoSino => "Notificações";
    public SecaoDoSino<PedidoNoSino> Pedidos => SecaoDoSino<PedidoNoSino>.Oculta;
    public SecaoDoSino<RespostaNoSino> Respostas => SecaoDoSino<RespostaNoSino>.Oculta;
    public IReadOnlyList<AvisoDaSessao> Historico => [];
    public int NaoLidas => 0;
    public void MarcarTodasComoLidas() { }
    public void AlternarSino() { }
    public void AbrirPedido(PedidoNoSino pedido) { }
    public void AbrirTodosOsPedidos() { }
    public void AbrirResposta(RespostaNoSino resposta) { }
    public void AbrirConfirmacoes() { }
    public void FecharSobreposicoes() { }
    public void TrocarUsuario() { }
    public void Ligar(Dialogos dialogos, Notificacoes notificacoes) { }

    public event Action? Mudou;
}
