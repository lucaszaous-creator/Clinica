using System.Globalization;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Clinica.Ui.Blazor.Moldura;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// A moldura da gestão no navegador: o MESMO <see cref="MolduraDoApp"/> que os apps desenham
/// (logo, menu do topo, abas, palco), com o menu vindo do <see cref="CatalogoDaGestao"/> e
/// filtrado pelo acesso de quem entrou. Uma por conexão (Scoped = circuito).
///
/// O que depende da máquina da clínica ou do shell WPF fica quieto aqui, e a tela diz isso
/// quando é o caso: o sino (pedidos e respostas são vigiados pelo ShellViewModel do app),
/// a fila de assinaturas e as videoaulas.
/// </summary>
public sealed class MolduraDaGestao : IMolduraDoApp
{
    private readonly CatalogoDaGestao _catalogo;
    private readonly SessaoUsuario _sessao;
    private readonly Action _sair;
    private CatalogoDaGestao.Item? _pai;
    private int _aba;
    private Notificacoes? _notificacoes;

    public MolduraDaGestao(CatalogoDaGestao catalogo, SessaoUsuario sessao, Action sair)
    {
        _catalogo = catalogo;
        _sessao = sessao;
        _sair = sair;
        Abrir(PrimeiraChave());
    }

    public string NomeDoApp => "Gestão";
    public string UsuarioRotulo => _sessao.Rotulo;
    public string DataHoje { get; } = DateTime.Today.ToString("ddd, dd/MM/yyyy", new CultureInfo("pt-BR"));
    public Palco Palco { get; } = new();

    /// <summary>A chave da tela à vista.</summary>
    public string ChaveAtual { get; private set; } = "";

    public IReadOnlyList<GrupoDaBarra> Barra => _catalogo.Areas
        .Select(a => new GrupoDaBarra(a.Rotulo, null, a.Itens.Select(_catalogo.ItemDe).OfType<CatalogoDaGestao.Item>()
            .Where(Visivel).Select(i => new ItemDaBarra(i.Chave, i.Rotulo, i.Descricao, i.Chave == _pai?.Chave)).ToList()))
        .Where(g => g.Itens.Count > 0)
        .ToList();

    private bool Visivel(CatalogoDaGestao.Item i) => CatalogoDaGestao.Alcanca(i, _sessao)
        && (i.Tela is not null || AbasVisiveis(i).Any());

    private IEnumerable<CatalogoDaGestao.Aba> AbasVisiveis(CatalogoDaGestao.Item i)
        => i.Abas.Where(a => _catalogo.ItemDe(a.Chave) is { } d && d.Tela is not null && CatalogoDaGestao.Alcanca(d, _sessao));

    private string PrimeiraChave()
        => Barra.SelectMany(g => g.Itens).Select(i => i.Chave).FirstOrDefault() ?? "";

    public string TituloTela { get; private set; } = "";

    public IReadOnlyList<AbaDaTela> Abas => _pai is { } p && AbasVisiveis(p).ToList() is { Count: > 1 } abas
        ? abas.Select((a, i) => new AbaDaTela(a.Rotulo, i == _aba)).ToList()
        : [];

    public string? FalhaDaTela { get; private set; }
    public bool AtualizandoTela => false;

    /// <summary>A chave existe e a pessoa a alcança (chave desconhecida não navega, como no shell).</summary>
    public bool Conhece(string chave)
        => _catalogo.ItemDe(chave) is { } i && CatalogoDaGestao.Alcanca(i, _sessao) && (i.Tela is not null || AbasVisiveis(i).Any());

    public void Abrir(string chave)
    {
        if (!Conhece(chave))
        {
            FalhaDaTela = string.IsNullOrEmpty(chave)
                ? "Seu acesso não alcança nenhuma tela da gestão. Fale com a direção da clínica."
                : "Esta tela não está disponível para o seu acesso.";
            Palco.Desativar();
            Mudou?.Invoke();
            return;
        }
        var item = _catalogo.ItemDe(chave)!;
        var abas = AbasVisiveis(item).ToList();
        if (abas.Count > 0)
        {
            _pai = item;
            _aba = 0;
            Mostrar(abas[0].Chave);
            return;
        }
        // A chave é aba de um item da barra? Abre o pai, na aba certa.
        var visiveis = Barra.SelectMany(g => g.Itens).Select(i => i.Chave).ToHashSet();
        _pai = _catalogo.Todos.FirstOrDefault(p => visiveis.Contains(p.Chave) && AbasVisiveis(p).Any(a => a.Chave == chave))
               ?? (visiveis.Contains(chave) ? item : null);
        _aba = _pai is null ? 0 : Math.Max(0, AbasVisiveis(_pai).ToList().FindIndex(a => a.Chave == chave));
        Mostrar(chave);
    }

    private void Mostrar(string chave)
    {
        ChaveAtual = chave;
        var item = _catalogo.ItemDe(chave);
        TituloTela = _pai?.Rotulo ?? item?.Rotulo ?? chave;
        if (item?.Tela is not { } tipo)
        {
            FalhaDaTela = "Esta tela não está disponível na gestão web.";
            Palco.Desativar();
        }
        else
        {
            FalhaDaTela = null;
            Palco.Ativar(tipo);
        }
        Mudou?.Invoke();
    }

    // ----- busca: as telas que a pessoa alcança (Ctrl+F) -----
    private string _texto = "";
    public string TextoPesquisa
    {
        get => _texto;
        set { _texto = value ?? ""; Mudou?.Invoke(); }
    }
    public bool PesquisaAberta => _texto.Trim().Length >= 2;
    public IReadOnlyList<ResultadoDaPesquisa> ResultadosPesquisa
    {
        get
        {
            var t = _texto.Trim();
            if (t.Length < 2) return [];
            var resultados = new List<ResultadoDaPesquisa>();
            foreach (var i in Barra.SelectMany(g => g.Itens).Select(x => _catalogo.ItemDe(x.Chave)!))
            {
                if (i.Rotulo.Contains(t, StringComparison.CurrentCultureIgnoreCase))
                    resultados.Add(new ResultadoDaPesquisa(i.Rotulo, null, i.Chave));
                foreach (var a in AbasVisiveis(i).Where(a => a.Rotulo.Contains(t, StringComparison.CurrentCultureIgnoreCase)))
                    resultados.Add(new ResultadoDaPesquisa(a.Rotulo, i.Rotulo, a.Chave));
            }
            return resultados.Take(12).ToList();
        }
    }
    public void AbrirResultado(ResultadoDaPesquisa? resultado)
    {
        var alvo = resultado?.Alvo as string ?? ResultadosPesquisa.FirstOrDefault()?.Alvo as string;
        _texto = "";
        if (alvo is not null) Abrir(alvo); else Mudou?.Invoke();
    }
    public void FecharPesquisa()
    {
        if (_texto.Length == 0) return;
        _texto = "";
        Mudou?.Invoke();
    }

    // ----- navegação -----
    public void Navegar(string chave) => Abrir(chave);

    public void SelecionarAba(int indice)
    {
        if (_pai is null) return;
        var abas = AbasVisiveis(_pai).ToList();
        if (indice < 0 || indice >= abas.Count) return;
        _aba = indice;
        Mostrar(abas[indice].Chave);
    }

    public void AbrirTreinamento() => _notificacoes?.Info("As videoaulas ficam no aplicativo do computador.");

    // ----- o que mora no shell do app: quieto na web -----
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
    public void AbrirConfirmacoes() => Abrir("agenda-confirmacoes");
    public void FecharSobreposicoes() => FecharPesquisa();

    /// <summary>"Trocar usuário" na web é SAIR: encerra a sessão do portal e volta ao login.</summary>
    public void TrocarUsuario() => _sair();

    public void Ligar(Dialogos dialogos, Notificacoes notificacoes) => _notificacoes = notificacoes;

    public event Action? Mudou;
}
