using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Ui.Blazor.Moldura;

// A MOLDURA DOS APPS EM BLAZOR (out/2026).
//
// Até aqui a janela principal, o menu, o login, a configuração do banco e a troca de senha eram
// WPF, e o Blazor rodava DENTRO deles (um navegador por janela, posicionado sobre a área da
// tela). Agora a janela nativa só existe porque o Windows exige uma: ela tem UM BlazorWebView
// que ocupa tudo, e o que se vê — entrada, configuração, cabeçalho, menu, abas, sino, telas — é
// Razor deste projeto.
//
// Este projeto é `net8.0` puro e não conhece o shell WPF (módulos, ShellViewModel, ConexaoStore).
// Por isso a moldura conversa com o hospedeiro por estes contratos: quem os implementa é o
// `Clinica.Desktop.Shell` (MolduraDaSuite, EntradaDaSuite, ConfiguracaoDaSuite), e os testes
// e a prévia implementam versões de mentira. As REGRAS continuam onde estavam: a navegação, o
// filtro de permissões e o sino no ShellViewModel; a senha e o travamento no AcessoService.

/// <summary>Um item do menu do topo (uma tela, ou um item com abas).</summary>
public sealed record ItemDaBarra(string Chave, string Rotulo, string? Descricao, bool Ativo);

/// <summary>
/// Uma entrada da barra do topo: ou um item DIRETO (a Recepção, com os principais à mostra),
/// ou uma categoria com submenu.
/// </summary>
public sealed record GrupoDaBarra(string Rotulo, ItemDaBarra? Direto, IReadOnlyList<ItemDaBarra> Itens)
{
    public bool Ativo => Direto?.Ativo ?? Itens.Any(i => i.Ativo);
}

/// <summary>Uma aba da tela composta à vista ("Caixa" → Movimento, Fechamento…).</summary>
public sealed record AbaDaTela(string Rotulo, bool Ativa);

/// <summary>Uma linha da paleta de busca (Ctrl+F). <paramref name="Caminho"/> é o item pai quando o achado é uma aba.</summary>
public sealed record ResultadoDaPesquisa(string Rotulo, string? Caminho, object Alvo);

/// <summary>Um aviso já exibido nesta sessão — a segunda via que o sino guarda.</summary>
public sealed record AvisoDaSessao(string Texto, TomNotificacao Tom, string Hora);

/// <summary>Um pedido da enfermagem no sino.</summary>
public sealed record PedidoNoSino(int Id, string Titulo, string Quando, string Origem, bool Novo);

/// <summary>Uma resposta de paciente no sino. <paramref name="Tom"/>: "nao-vem", "sem-resposta" ou "confirmou".</summary>
public sealed record RespostaNoSino(object Alvo, string Titulo, string Sessao, string Detalhe, string Tom);

/// <summary>Uma seção do sino: só aparece para quem a tela que resolve existe.</summary>
public sealed record SecaoDoSino<T>(bool Visivel, string Contador, IReadOnlyList<T> Linhas, string Situacao)
{
    public static SecaoDoSino<T> Oculta { get; } = new(false, "", [], "");
}

/// <summary>
/// A janela principal de um app depois da entrada: o que o cabeçalho mostra e o que os
/// cliques pedem. Implementada pelo shell sobre o ShellViewModel — a moldura só desenha.
/// </summary>
public interface IMolduraDoApp
{
    /// <summary>"Recepção", "Consultório"… — ao lado da logo.</summary>
    string NomeDoApp { get; }
    string UsuarioRotulo { get; }
    string DataHoje { get; }

    /// <summary>As telas abertas, montadas no navegador desta janela.</summary>
    Palco Palco { get; }

    IReadOnlyList<GrupoDaBarra> Barra { get; }
    string TituloTela { get; }

    /// <summary>As abas da tela à vista; vazio quando ela é uma tela só.</summary>
    IReadOnlyList<AbaDaTela> Abas { get; }

    /// <summary>
    /// A tela à vista não é uma tela Blazor (uma aba que falhou ao montar): a moldura diz
    /// isso no lugar dela, em vez de deixar a área em branco.
    /// </summary>
    string? FalhaDaTela { get; }

    /// <summary>A tela voltou do histórico com o que mostrava e relê por trás.</summary>
    bool AtualizandoTela { get; }

    // ----- busca (Ctrl+F) -----
    string TextoPesquisa { get; set; }
    bool PesquisaAberta { get; }
    IReadOnlyList<ResultadoDaPesquisa> ResultadosPesquisa { get; }
    /// <summary>Abre o resultado; nulo abre o primeiro (Enter na busca).</summary>
    void AbrirResultado(ResultadoDaPesquisa? resultado);
    void FecharPesquisa();

    // ----- navegação -----
    void Navegar(string chave);
    void SelecionarAba(int indice);
    void AbrirTreinamento();

    // ----- assinaturas de infusão pendentes -----
    int AssinaturasPendentes { get; }
    string ResumoAssinaturas { get; }
    void AbrirFilaDeInfusao();

    // ----- sino -----
    bool SinoAberto { get; }
    /// <summary>"" sem nada a ver; um número; "!" quando a leitura falhou.</summary>
    string ContadorDoSino { get; }
    string ResumoDoSino { get; }
    SecaoDoSino<PedidoNoSino> Pedidos { get; }
    SecaoDoSino<RespostaNoSino> Respostas { get; }
    IReadOnlyList<AvisoDaSessao> Historico { get; }
    void AlternarSino();
    void AbrirPedido(PedidoNoSino pedido);
    void AbrirTodosOsPedidos();
    void AbrirResposta(RespostaNoSino resposta);
    void AbrirConfirmacoes();

    /// <summary>Esc: fecha o sino e a paleta da busca.</summary>
    void FecharSobreposicoes();

    /// <summary>Reabre o app na tela de entrada (a confirmação é da moldura, antes).</summary>
    void TrocarUsuario();

    /// <summary>
    /// A moldura entrega ao hospedeiro os diálogos e as notificações DESTE navegador: os avisos
    /// das telas WPF que ainda abrem por cima (o snackbar) e as perguntas do <c>IDialogoService</c>
    /// passam a sair em HTML, aqui.
    /// </summary>
    void Ligar(Dialogos dialogos, Notificacoes notificacoes);

    /// <summary>Algo do cabeçalho mudou — a moldura redesenha.</summary>
    event Action? Mudou;
}

/// <summary>A porta de entrada de um app: quem pode entrar e o que acontece depois.</summary>
public interface IEntradaDoApp
{
    string NomeDoApp { get; }

    /// <summary>Base sem nenhum usuário: a entrada oferece criar o primeiro (Gerente).</summary>
    bool PrimeiroAcesso { get; }

    /// <summary>
    /// Recusa quem entrou mas não tem o que fazer neste app (o Faturamento exige "Ver
    /// faturamento"). Devolve a frase da recusa, ou nulo para deixar entrar.
    /// </summary>
    string? Recusar(UsuarioSistema usuario);

    /// <summary>Entrou (senha conferida e, se provisória, já trocada).</summary>
    void Entrou(UsuarioSistema usuario);

    /// <summary>A pessoa desistiu ("Sair"): o app fecha.</summary>
    void Desistiu();
}

/// <summary>A configuração do banco no primeiro acesso de uma máquina.</summary>
public interface IConfiguracaoDoBanco
{
    string NomeDoApp { get; }

    /// <summary>Converte o que foi colado (URL da Neon ou connection string). Lança quando não entende.</summary>
    string Normalizar(string entrada);

    Task<(bool Ok, string Mensagem)> TestarAsync(string conexao);

    /// <summary>Grava a conexão já testada. Lança quando o disco recusa.</summary>
    void Salvar(string conexao);

    /// <summary>Fim da configuração: <paramref name="salvou"/> falso quando a pessoa desistiu.</summary>
    void Concluir(bool salvou);
}

/// <summary>
/// O que a janela nativa está mostrando agora. Uma janela só para a vida inteira do app: a
/// entrada e depois a moldura trocam DENTRO do mesmo navegador — criar um segundo navegador é
/// o instante preto que a clínica filmou em out/2026.
/// </summary>
public sealed class EstadoDaJanela
{
    public IConfiguracaoDoBanco? Configuracao { get; private set; }
    public IEntradaDoApp? Entrada { get; private set; }
    public IMolduraDoApp? Moldura { get; private set; }

    public event Action? Mudou;

    /// <summary>A raiz desenhou pela primeira vez: o hospedeiro pode mostrar o navegador.</summary>
    public event Action? Desenhou;

    public void MostrarConfiguracao(IConfiguracaoDoBanco configuracao)
    {
        Configuracao = configuracao;
        Mudou?.Invoke();
    }

    public void MostrarEntrada(IEntradaDoApp entrada)
    {
        Entrada = entrada;
        Mudou?.Invoke();
    }

    public void MostrarMoldura(IMolduraDoApp moldura)
    {
        Moldura = moldura;
        Mudou?.Invoke();
    }

    private bool _desenhou;

    public void AvisarDesenho()
    {
        if (_desenhou) return;
        _desenhou = true;
        Desenhou?.Invoke();
    }
}
