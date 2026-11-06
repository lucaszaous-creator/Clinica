using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Moldura;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell;

/// <summary>
/// A janela NATIVA de um app da suíte (out/2026). Ela só existe porque o Windows exige uma:
/// o conteúdo inteiro é UM BlazorWebView, e o que se vê — a entrada, a configuração do banco,
/// o cabeçalho, o menu, as abas, o sino e as telas — é Razor (<c>Clinica.Ui.Blazor.Moldura</c>).
///
/// Uma janela só para a vida inteira do app: a entrada e depois a moldura trocam DENTRO do
/// mesmo navegador (<see cref="EstadoDaJanela"/>). Criar um segundo navegador é o instante
/// preto que a clínica filmou em out/2026 — e é por isso que o login deixou de ser uma janela
/// à parte. A configuração do banco é a exceção: ela acontece ANTES de o host existir (não há
/// serviços da clínica para dar ao navegador), uma vez por máquina, e usa uma janela própria.
///
/// As janelas WPF que ainda abrem por cima (as da área congelada, as da abertura do
/// faturamento) usam ESTA como dona — ela é a <c>MainWindow</c> do app.
/// </summary>
public sealed class JanelaDoApp : Window
{
    private readonly EstadoDaJanela _estado = new();
    private readonly BlazorWebView _visor;
    private readonly DispatcherTimer _rede;
    private readonly IServiceProvider _servicos;
    private MolduraDaSuite? _moldura;
    private ShellViewModel? _shell;
    private Action? _aoFecharNaEntrada;

    /// <param name="servicos">Os serviços do app (ou um provedor mínimo, na configuração do banco).</param>
    /// <param name="titulo">"Recepção — Clínica SemDor": o título da janela na barra de tarefas.</param>
    public JanelaDoApp(IServiceProvider servicos, string titulo)
    {
        _servicos = servicos;
        Title = titulo;
        Width = 1280;
        Height = 690;
        MinWidth = 880;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "Brush.Fundo");
        try
        {
            Icon = BitmapFrame.Create(new Uri(
                "pack://application:,,,/Clinica.Desktop.Shell;component/Assets/Marca/icone.png"));
        }
        catch (Exception ex) { LogSuite.Registrar("Janela do app — ícone não carregou", ex); }

        _visor = NavegadorEmbutido.Criar(servicos, typeof(RaizDoApp),
            new Dictionary<string, object?> { ["Estado"] = _estado },
            core => servicos.GetService<Treinamento.TreinamentoDaSuite>()?.MapearPastas(core));
        Content = _visor;

        // A primeira renderização libera o navegador; o BeginInvoke em Background deixa o lote
        // de renderização seguir para a página antes de ela aparecer.
        _estado.Desenhou += () => Dispatcher.BeginInvoke(Liberar, DispatcherPriority.Background);
        // Rede: se a raiz não avisar (runtime do WebView2 ausente, página que não carregou),
        // mostra assim mesmo — navegador escondido para sempre seria pior.
        _rede = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _rede.Tick += (_, _) => Liberar();
        _rede.Start();

        Closed += (_, _) =>
        {
            _rede.Stop();
            _aoFecharNaEntrada?.Invoke();
            if (_shell is not null)
            {
                _shell.PararAvisosGlobais();
                _shell.LiberarTelas();
            }
            _moldura?.Dispose();
            SairDoCrm();
        };
    }

    /// <summary>
    /// Tira a pessoa do CRM quando o app fecha (PR 5 do WhatsApp nativo). É o ponto ÚNICO de
    /// saída: "Sair" fecha esta janela, e "Trocar usuário" reabre o processo (parcela 45) —
    /// nenhum dos dois troca a sessão em memória. Quem desistiu na entrada não tem sessão, e
    /// aí o host pode já ter sido descartado: por isso a sessão é conferida ANTES do provedor.
    /// </summary>
    private void SairDoCrm()
    {
        var sessao = SessaoUsuario.Atual;
        if (!sessao.Autenticado) return;
        try
        {
            Clinica.Infrastructure.Crm.SaidaDoCrm.AoFecharOApp(
                _servicos.GetService<Clinica.Application.Abstracoes.IClienteDoCrm>(), sessao.UsuarioId,
                Clinica.Infrastructure.Crm.SaidaDoCrm.Espera);
        }
        catch (Exception ex) { LogSuite.Registrar("Janela do app — saída do CRM no fechamento", ex); }
    }

    private void Liberar()
    {
        _rede.Stop();
        if (_visor.Visibility == Visibility.Visible) return;
        _visor.Visibility = Visibility.Visible;
        // ⚠️ Mostrar não basta (out/2026, a clínica filmou: a entrada só aparecia depois de
        // MAXIMIZAR a janela, que já abre maximizada). O WebView2 nasce escondido e, ao ficar
        // visível, a janela nativa dele só repinta quando os LIMITES mudam — o que acontece
        // num redimensionamento e não na troca de visibilidade. A cutucada muda o tamanho do
        // visor em 1 px e volta: o HwndHost reposiciona a janela nativa duas vezes e o
        // navegador desenha a página que já estava pronta.
        Dispatcher.BeginInvoke(Cutucar, DispatcherPriority.Loaded);
    }

    private void Cutucar()
    {
        _visor.Margin = new Thickness(0, 0, 0, 1);
        _visor.UpdateLayout();
        _visor.Margin = new Thickness(0);
    }

    /// <summary>
    /// Mostra a ENTRADA e devolve quem entrou — ou nulo quando a pessoa desistiu ("Sair" ou
    /// fechou a janela). A janela é mostrada aqui, se ainda não estava.
    /// </summary>
    /// <param name="recusar">
    /// Recusa quem não tem o que fazer no app (o Faturamento exige "Ver faturamento"): a frase
    /// aparece na própria entrada, que volta a pedir usuário e senha.
    /// </param>
    public Task<UsuarioSistema?> EntrarAsync(string nomeApp, bool primeiroAcesso,
        Func<UsuarioSistema, string?>? recusar = null)
    {
        var resposta = new TaskCompletionSource<UsuarioSistema?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _aoFecharNaEntrada = () => resposta.TrySetResult(null);
        _estado.MostrarEntrada(new EntradaDaSuite(nomeApp, primeiroAcesso, recusar,
            usuario => { _aoFecharNaEntrada = null; resposta.TrySetResult(usuario); },
            () => { resposta.TrySetResult(null); Close(); }));
        if (!IsVisible) Show();
        return resposta.Task;
    }

    /// <summary>
    /// Depois da entrada: a moldura com o menu e as telas, no MESMO navegador. A sessão já
    /// tem de estar gravada — o <see cref="ShellViewModel"/> lê a permissão ao ser construído.
    /// </summary>
    public void MostrarMoldura(ShellViewModel shell)
    {
        _shell = shell;
        _moldura = new MolduraDaSuite(shell, _servicos, this);
        _estado.MostrarMoldura(_moldura);
        // O ciclo dos avisos pertence à janela inteira: trocar de tela não o interrompe.
        shell.IniciarAvisosGlobais();
    }

    /// <summary>
    /// A CONFIGURAÇÃO DO BANCO no primeiro acesso da máquina, numa janela própria (o host da
    /// clínica ainda não existe — não há serviços para dar ao navegador da janela principal).
    /// Devolve verdadeiro quando a conexão foi testada e salva.
    /// </summary>
    public static async Task<bool> ConfigurarBancoAsync(string nomeApp)
    {
        var colecao = new ServiceCollection();
        colecao.AddWpfBlazorWebView();
        await using var servicos = colecao.BuildServiceProvider();

        var janela = new JanelaDoApp(servicos, $"Configurar o banco de dados — {nomeApp}")
        {
            Width = 780,
            Height = 600,
            MinWidth = 560,
            MinHeight = 440
        };
        var resposta = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        janela._estado.MostrarConfiguracao(new ConfiguracaoDaSuite(nomeApp, salvou =>
        {
            resposta.TrySetResult(salvou);
            janela.Close();
        }));
        janela.Closed += (_, _) => resposta.TrySetResult(false);
        janela.Show();
        var salvou = await resposta.Task;
        try { await janela._visor.DisposeAsync(); }
        catch (Exception ex) { LogSuite.Registrar("Configuração do banco — navegador não foi descartado", ex); }
        return salvou;
    }

    /// <summary>A porta de entrada: grava nada; quem grava a sessão é quem abriu a janela.</summary>
    private sealed class EntradaDaSuite(
        string nomeApp, bool primeiroAcesso, Func<UsuarioSistema, string?>? recusar,
        Action<UsuarioSistema> entrou, Action desistiu) : IEntradaDoApp
    {
        public string NomeDoApp => nomeApp;
        public bool PrimeiroAcesso => primeiroAcesso;
        public string? Recusar(UsuarioSistema usuario) => recusar?.Invoke(usuario);
        public void Entrou(UsuarioSistema usuario) => entrou(usuario);
        public void Desistiu() => desistiu();
    }

    /// <summary>A configuração do banco sobre o <see cref="ConexaoStore"/> (DPAPI, pasta comum da suíte).</summary>
    private sealed class ConfiguracaoDaSuite(string nomeApp, Action<bool> concluir) : IConfiguracaoDoBanco
    {
        public string NomeDoApp => nomeApp;
        public string Normalizar(string entrada) => ConexaoStore.Normalizar(entrada);
        public Task<(bool Ok, string Mensagem)> TestarAsync(string conexao) => ConexaoStore.TestarAsync(conexao);
        public void Salvar(string conexao) => ConexaoStore.Salvar(conexao);
        public void Concluir(bool salvou) => concluir(salvou);
    }
}
