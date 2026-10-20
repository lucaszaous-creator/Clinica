using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Ui.Blazor.Hospedagem;
using Clinica.Ui.Blazor.Moldura;
using Microsoft.Extensions.DependencyInjection;
using AvisoHtml = Clinica.Ui.Blazor.Moldura.AvisoDaSessao;
using PedidoHtml = Clinica.Ui.Blazor.Moldura.PedidoNoSino;
using RespostaHtml = Clinica.Ui.Blazor.Moldura.RespostaNoSino;

namespace Clinica.Desktop.Shell;

/// <summary>
/// A ponte entre o <see cref="ShellViewModel"/> e a moldura Razor (out/2026). O ViewModel
/// continua sendo quem decide — o menu filtrado pela permissão, a navegação com histórico, as
/// abas, a busca, o sino —, e esta classe só o TRADUZ para o que a moldura desenha, além de
/// pôr à vista no palco do navegador a tela que o ViewModel escolheu.
///
/// Uma tela de <see cref="IModuloApp.CriarTela"/> é um DESCRITOR (<see cref="ITelaDoNavegador"/>):
/// o componente Razor que o palco ativa. Trocar de tela, trocar de aba e voltar no histórico
/// passam todos por <see cref="Sincronizar"/>.
/// </summary>
public sealed class MolduraDaSuite : IMolduraDoApp, IDisposable
{
    private readonly ShellViewModel _vm;
    private readonly SnackbarService? _snackbar;
    private readonly DialogoService? _dialogos;
    private readonly Window _janela;
    private Notificacoes? _notificacoes;
    private TelaComAbas? _abasEscutadas;
    private object? _visivel;

    public MolduraDaSuite(ShellViewModel vm, IServiceProvider servicos, Window janela)
    {
        _vm = vm;
        _janela = janela;
        _snackbar = servicos.GetService<SnackbarService>();
        _dialogos = servicos.GetService<IDialogoService>() as DialogoService;

        _vm.PropertyChanged += MudouNoViewModel;
        _vm.ResultadosPesquisa.CollectionChanged += MudouColecao;
        _vm.PedidosNoSino.CollectionChanged += MudouColecao;
        _vm.RespostasNoSino.CollectionChanged += MudouColecao;
        foreach (var item in _vm.Itens) item.PropertyChanged += MudouNoViewModel;
        if (_snackbar is not null)
        {
            _snackbar.Historico.CollectionChanged += NovoAviso;
            _snackbar.PropertyChanged += MudouNoViewModel;
        }
        Sincronizar();
    }

    public Palco Palco { get; } = new();

    public event Action? Mudou;

    // ==================== o que a moldura desenha ====================

    public string NomeDoApp => _vm.NomeDoApp;
    public string UsuarioRotulo => _vm.UsuarioRotulo;
    public string DataHoje => _vm.DataHoje;
    public string TituloTela => _vm.TituloTela;
    public bool AtualizandoTela => _vm.AtualizandoTela;

    public IReadOnlyList<GrupoDaBarra> Barra => _vm.Superiores
        .Select(g => new GrupoDaBarra(g.NomeNavegacao, g.Direto is { } d ? Item(d) : null,
            g.Itens.Select(Item).ToList()))
        .ToList();

    private static ItemDaBarra Item(ItemMenuModulo i)
        => new(i.Chave, i.Rotulo, string.IsNullOrWhiteSpace(i.DescricaoMenu) ? null : i.DescricaoMenu, i.EstaAtivo);

    public IReadOnlyList<AbaDaTela> Abas => _vm.TelaAtual is TelaComAbas abas
        ? abas.Rotulos.Select((r, i) => new AbaDaTela(r, i == abas.Selecionada)).ToList()
        : [];

    public string? FalhaDaTela => _visivel is null or ITelaDoNavegador
        ? null
        : "Não foi possível abrir esta tela. Tente de novo; se continuar, avise o suporte com a data e a hora.";

    // ----- busca -----
    public string TextoPesquisa
    {
        get => _vm.TextoPesquisa;
        set => _vm.TextoPesquisa = value;
    }
    public bool PesquisaAberta => _vm.PesquisaAberta;
    public IReadOnlyList<ResultadoDaPesquisa> ResultadosPesquisa => _vm.ResultadosPesquisa
        .Select(r => new ResultadoDaPesquisa(r.Rotulo, r.Caminho, r)).ToList();
    public void AbrirResultado(ResultadoDaPesquisa? resultado)
        => _vm.NavegarResultadoCommand.Execute(resultado?.Alvo as ResultadoPesquisa);
    public void FecharPesquisa() => _vm.FecharPesquisaCommand.Execute(null);

    // ----- navegação -----
    public void Navegar(string chave)
    {
        var item = _vm.Itens.FirstOrDefault(i => i.Chave == chave);
        if (item is not null) _vm.Navegar(item);
    }

    public void SelecionarAba(int indice) => (_vm.TelaAtual as TelaComAbas)?.Selecionar(indice);

    public void AbrirTreinamento() => _vm.AbrirTreinamentoCommand.Execute(null);

    // ----- infusão -----
    public int AssinaturasPendentes => _vm.TotalAssinaturasInfusao;
    public string ResumoAssinaturas => _vm.ResumoAssinaturasInfusao;
    public void AbrirFilaDeInfusao() => _vm.AbrirFilaInfusaoCommand.Execute(null);

    // ----- sino -----
    public bool SinoAberto => _vm.AvisosAbertos;
    public string ContadorDoSino => _vm.TemAvisos ? _vm.ContadorAvisos : "";
    public string ResumoDoSino => _vm.ResumoPedidos;

    public SecaoDoSino<PedidoHtml> Pedidos => _vm.PodeVerPedidos
        ? new(true, _vm.ContadorPedidos,
            _vm.PedidosNoSino.Select(p => new PedidoHtml(p.Id, p.Titulo, p.Quando, p.Origem, p.Novo)).ToList(),
            _vm.SituacaoSino)
        : SecaoDoSino<PedidoHtml>.Oculta;

    public SecaoDoSino<RespostaHtml> Respostas => _vm.PodeVerConfirmacoes
        ? new(true, _vm.ContadorRespostas,
            _vm.RespostasNoSino.Select(r => new RespostaHtml(r, r.Titulo, r.Sessao, r.Detalhe, Tom(r.Tipo))).ToList(),
            _vm.SituacaoRespostas)
        : SecaoDoSino<RespostaHtml>.Oculta;

    private static string Tom(Clinica.Application.Modelos.TipoAvisoConfirmacao tipo) => tipo switch
    {
        Clinica.Application.Modelos.TipoAvisoConfirmacao.NaoVem => "nao-vem",
        Clinica.Application.Modelos.TipoAvisoConfirmacao.SemResposta => "sem-resposta",
        _ => "confirmou"
    };

    public IReadOnlyList<AvisoHtml> Historico => _snackbar?.Historico
        .Select(a => new AvisoHtml(a.Mensagem, TomDe(a.Tipo), a.HoraTexto)).ToList() ?? [];

    public void AlternarSino() => _vm.AbrirAvisosCommand.Execute(null);

    public void AbrirPedido(PedidoHtml pedido)
    {
        if (_vm.PedidosNoSino.FirstOrDefault(p => p.Id == pedido.Id) is { } original)
            _vm.AbrirPedidoDoSinoCommand.Execute(original);
    }

    public void AbrirTodosOsPedidos() => _vm.AbrirPedidosEnfermagemCommand.Execute(null);

    public void AbrirResposta(RespostaHtml resposta)
    {
        if (resposta.Alvo is RespostaNoSino original) _vm.AbrirRespostaDoSinoCommand.Execute(original);
    }

    public void AbrirConfirmacoes() => _vm.AbrirConfirmacoesCommand.Execute(null);

    public void FecharSobreposicoes() => _vm.FecharSobreposicoesCommand.Execute(null);

    public void TrocarUsuario() => _vm.ReabrirParaTrocarUsuario();

    public void Ligar(Dialogos dialogos, Notificacoes notificacoes)
    {
        _notificacoes = notificacoes;
        _dialogos?.LigarMoldura(dialogos, _janela);
    }

    // ==================== o palco segue o ViewModel ====================

    /// <summary>
    /// Põe à vista a tela que o ViewModel escolheu: o item simples, ou a aba selecionada do
    /// item composto. Uma instância NOVA do mesmo componente (a tela do paciente aberta para
    /// outra pessoa, a volta pelo histórico) desativa e reativa — é o "voltou à tela" que faz
    /// ela reler o pedido e os dados, como a troca de conteúdo do WPF fazia.
    /// </summary>
    private void Sincronizar()
    {
        var abas = _vm.TelaAtual as TelaComAbas;
        if (!ReferenceEquals(abas, _abasEscutadas))
        {
            if (_abasEscutadas is not null) _abasEscutadas.Mudou -= MudouAba;
            _abasEscutadas = abas;
            if (abas is not null) abas.Mudou += MudouAba;
        }

        var visivel = abas is not null ? abas.ConteudoVisivel : _vm.TelaAtual;
        if (ReferenceEquals(visivel, _visivel)) return;
        _visivel = visivel;

        if (visivel is ITelaDoNavegador tela)
        {
            var atual = Palco.Telas.FirstOrDefault(e => e.Id == Palco.Atual);
            if (atual?.Tela == tela.Componente) Palco.Desativar();
            Palco.Ativar(tela.Componente);
        }
        else Palco.Desativar();
    }

    private void MudouAba()
    {
        Sincronizar();
        Mudou?.Invoke();
    }

    private void MudouNoViewModel(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.TelaAtual)) Sincronizar();
        Mudou?.Invoke();
    }

    private void MudouColecao(object? sender, NotifyCollectionChangedEventArgs e) => Mudou?.Invoke();

    /// <summary>
    /// O snackbar das telas WPF (e dos avisos do shell) passa a sair nas notificações do
    /// navegador: cada aviso novo entra no topo do histórico — é ele que vira a notificação.
    /// </summary>
    private void NovoAviso(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == 0 && _notificacoes is { } n)
        {
            foreach (AvisoRegistrado a in e.NewItems!)
            {
                switch (a.Tipo)
                {
                    case TipoSnackbar.Sucesso: n.Sucesso(a.Mensagem); break;
                    case TipoSnackbar.Erro: n.Erro(a.Mensagem); break;
                    default: n.Info(a.Mensagem); break;
                }
            }
        }
        Mudou?.Invoke();
    }

    private static TomNotificacao TomDe(TipoSnackbar tipo) => tipo switch
    {
        TipoSnackbar.Sucesso => TomNotificacao.Sucesso,
        TipoSnackbar.Erro => TomNotificacao.Erro,
        _ => TomNotificacao.Info
    };

    public void Dispose()
    {
        _vm.PropertyChanged -= MudouNoViewModel;
        _vm.ResultadosPesquisa.CollectionChanged -= MudouColecao;
        _vm.PedidosNoSino.CollectionChanged -= MudouColecao;
        _vm.RespostasNoSino.CollectionChanged -= MudouColecao;
        foreach (var item in _vm.Itens) item.PropertyChanged -= MudouNoViewModel;
        if (_snackbar is not null)
        {
            _snackbar.Historico.CollectionChanged -= NovoAviso;
            _snackbar.PropertyChanged -= MudouNoViewModel;
        }
        if (_abasEscutadas is not null) _abasEscutadas.Mudou -= MudouAba;
        _dialogos?.Desligar();
    }
}
