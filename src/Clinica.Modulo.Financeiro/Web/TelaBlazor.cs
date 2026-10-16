using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.FileProviders;

namespace Clinica.Financeiro.Web;

/// <summary>
/// O lugar de uma tela Blazor na área de trabalho do WPF. Ela NÃO tem navegador: o
/// navegador é UM por janela (<see cref="NavegadorDaJanela"/>), mora na camada persistente
/// do shell, e esta tela só diz a ele QUAL componente mostrar e ONDE (a área dela).
///
/// ⚠️ A primeira versão (out/2026) criava um BlazorWebView por tela, com o fundo pintado em
/// três camadas para "não haver instante preto" — e havia: ~100 ms pretos a cada tela
/// aberta, que a clínica mostrou em vídeo. A janela nativa do WebView2 aparece antes de o
/// navegador existir dentro dela, e nenhuma cor configurada vale nesse instante. Não se
/// esconde esse instante com cor; deixa-se de criar navegador a cada tela.
/// </summary>
public sealed class TelaBlazor : UserControl
{
    private readonly Type _tela;
    private readonly IServiceProvider _servicos;
    private NavegadorDaJanela? _navegador;

    public TelaBlazor(Type componente, IServiceProvider servicos)
    {
        _tela = componente;
        _servicos = servicos;
        SetResourceReference(BackgroundProperty, "Brush.Fundo");
        Focusable = false;

        // O shell monta, esconde (aba trocada) e descarta (item simples) esta tela; o
        // navegador acompanha a VISIBILIDADE dela, não a criação.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) Assumir();
            else _navegador?.Soltar(this);
        };
        // Redimensionar a janela, dobrar o cabeçalho ou recolher a lateral mexe na área da
        // tela sem mudar a visibilidade: o navegador segue o retângulo.
        LayoutUpdated += (_, _) => _navegador?.Posicionar(this);
    }

    private void Assumir()
    {
        // O navegador mora na camada da janela do shell. Fora dela (uma tela aberta noutra
        // janela, que hoje não existe) a própria tela vira a camada — continua funcionando,
        // só não compartilha.
        Panel camada;
        if (Window.GetWindow(this) is Clinica.Desktop.Shell.ShellWindow janela) camada = janela.CamadaDaJanela;
        else
        {
            if (Content is not Grid propria) Content = propria = new Grid();
            camada = propria;
        }
        _navegador = NavegadorDaJanela.Da(camada, _servicos);
        _navegador.Mostrar(this, _tela);
    }
}

/// <summary>
/// O navegador ÚNICO de uma janela: um BlazorWebView com o <c>PalcoDeTelas</c> como raiz,
/// criado na primeira tela Blazor e nunca destruído enquanto a janela existir. Ele fica na
/// camada persistente, posicionado sobre a área da tela que está à vista, e ESCONDIDO quando
/// a tela à vista é WPF.
/// </summary>
internal sealed class NavegadorDaJanela
{
    /// <summary>O fundo da página (`--fundo`, Cor.Cinza.50) — o mesmo da index.html.</summary>
    private const string FundoArgb = "FFF8FAFC";

    private static readonly ConditionalWeakTable<Panel, NavegadorDaJanela> PorCamada = new();

    private readonly VisorEmbutido _visor;
    private readonly Palco _palco = new();
    private readonly DispatcherTimer _rede;
    private readonly Panel _camada;
    private TelaBlazor? _dono;
    private bool _pronto;

    static NavegadorDaJanela()
    {
        // A cor que o controlador do WebView2 pinta ANTES da página. Vale para o processo.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", FundoArgb);
    }

    public static NavegadorDaJanela Da(Panel camada, IServiceProvider servicos)
        => PorCamada.GetValue(camada, c => new NavegadorDaJanela(c, servicos));

    private NavegadorDaJanela(Panel camada, IServiceProvider servicos)
    {
        _camada = camada;
        _visor = new VisorEmbutido
        {
            HostPage = Path.Combine("ativos", "index.html"),
            Services = servicos,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            // HIDDEN, nunca Collapsed: o controle fica no leiaute (a janela nativa nasce, o
            // navegador inicializa, a página carrega) e só aparece depois de DESENHAR. A
            // janela nativa recém-criada é preta, e é esse o único instante em que poderia
            // haver preto — agora uma vez por janela, e escondido.
            Visibility = Visibility.Hidden
        };
        _visor.SetResourceReference(Control.BackgroundProperty, "Brush.Fundo");
        _visor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Clinica.Ui.Blazor.Componentes.PalcoDeTelas),
            Parameters = new Dictionary<string, object?> { ["Palco"] = _palco }
        });

        _visor.BlazorWebViewInitializing += (_, e) =>
        {
            // O perfil do navegador mora no LocalAppData, fora da pasta da instalação: a
            // pasta do Velopack é substituída a cada atualização.
            e.UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaSemDor", "Blazor");
        };
        _visor.BlazorWebViewInitialized += (_, e) =>
        {
            // A mesma cor, direto no controlador — a variável de ambiente só vale se for lida
            // antes de o processo criar o primeiro navegador.
            e.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xF8, 0xFA, 0xFC);
            var config = e.WebView.CoreWebView2.Settings;
            config.AreDefaultContextMenusEnabled = false;
            config.IsStatusBarEnabled = false;
            config.IsPasswordAutosaveEnabled = false;
            config.IsGeneralAutofillEnabled = false;
#if !DEBUG
            config.AreDevToolsEnabled = false;
            config.AreBrowserAcceleratorKeysEnabled = false;
#endif
        };
        // A tela não navega para fora: link externo não abre dentro do app.
        _visor.UrlLoading += (_, e) =>
        {
            if (e.UrlLoadingStrategy == UrlLoadingStrategy.OpenExternally)
                e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        };

        // A primeira renderização com tela libera a janela. O BeginInvoke em Background deixa
        // o lote de renderização seguir para a página antes de a janela aparecer.
        _palco.Montou += () => _visor.Dispatcher.BeginInvoke(Liberar, DispatcherPriority.Background);
        // Rede: se a primeira renderização não avisar (runtime do WebView2 ausente, página que
        // não carregou), mostra assim mesmo — navegador escondido para sempre seria pior.
        _rede = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _rede.Tick += (_, _) => Liberar();
        _rede.Start();

        camada.Children.Add(_visor);
    }

    private void Liberar()
    {
        _rede.Stop();
        _pronto = true;
        Atualizar();
    }

    /// <summary>A tela <paramref name="dono"/> ficou à vista: o navegador vai para cima dela.</summary>
    public void Mostrar(TelaBlazor dono, Type tela)
    {
        _dono = dono;
        Posicionar(dono);
        _palco.Ativar(tela);
        Atualizar();
    }

    /// <summary>
    /// A tela saiu de vista. Só esconde se ela ainda for a dona — na troca de aba, a nova
    /// pode ter assumido antes de a antiga avisar que sumiu.
    /// </summary>
    public void Soltar(TelaBlazor dono)
    {
        if (!ReferenceEquals(_dono, dono)) return;
        _dono = null;
        _palco.Desativar();
        Atualizar();
    }

    /// <summary>Encaixa o navegador exatamente na área da tela dona.</summary>
    public void Posicionar(TelaBlazor dono)
    {
        if (!ReferenceEquals(_dono, dono) || !dono.IsVisible) return;
        Rect area;
        try
        {
            area = dono.TransformToVisual(_camada).TransformBounds(new Rect(dono.RenderSize));
        }
        catch (InvalidOperationException)
        {
            return; // a tela ainda não está na mesma árvore da camada
        }
        var margem = new Thickness(area.Left, area.Top, 0, 0);
        if (_visor.Margin != margem) _visor.Margin = margem;
        if (!_visor.Width.Equals(area.Width)) _visor.Width = area.Width;
        if (!_visor.Height.Equals(area.Height)) _visor.Height = area.Height;
    }

    private void Atualizar()
    {
        var visivel = _pronto && _dono is not null ? Visibility.Visible : Visibility.Hidden;
        if (_visor.Visibility != visivel) _visor.Visibility = visivel;
    }

    /// <summary>
    /// Serve a página e o CSS de DENTRO da DLL do Clinica.Ui.Blazor (manifesto de recursos)
    /// em vez de uma pasta ao lado do .exe — não há arquivo que o pacote possa esquecer.
    /// </summary>
    private sealed class VisorEmbutido : BlazorWebView
    {
        public override IFileProvider CreateFileProvider(string contentRootDir) =>
            new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos");
    }
}

/// <summary>
/// O que a tela Blazor pede ao WPF. Reusa as rotinas do shell — CSV no padrão do Excel em
/// português (ponto e vírgula + BOM), a janela "Salvar como", a área de transferência e a
/// navegação da suíte — para não existir uma segunda definição de nenhuma delas.
///
/// O BlazorWebView roda os componentes no Dispatcher do WPF, então estas chamadas já
/// chegam na thread da interface; o `InvokeAsync` é a garantia, não a regra.
/// </summary>
public sealed class HostDaTelaWpf : IHostDaTela
{
    public Task<string?> ExportarCsvAsync(
        IReadOnlyList<string> cabecalho,
        IReadOnlyList<IReadOnlyList<string>> linhas,
        string nomeArquivo)
    {
        var csv = ExportacaoCsv.Montar(cabecalho, linhas);
        return NaInterface(() => ImpressaoPdf.SalvarEAbrirAsync(csv, ImpressaoPdf.NomeSeguro(nomeArquivo), "CSV (*.csv)|*.csv", ".csv"));
    }

    public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao)
        => NaInterface(() => ImpressaoPdf.SalvarEAbrirAsync(conteudo, ImpressaoPdf.NomeSeguro(nomeSugerido), filtro, extensao));

    public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo)
        => NaInterface(async () =>
        {
            var caminho = ImpressaoPdf.Escolher(filtro, titulo);
            if (caminho is null) return null;
            return new ArquivoEscolhido(Path.GetFileName(caminho), await File.ReadAllBytesAsync(caminho));
        });

    public Task<bool> CopiarAsync(string texto)
        => NaInterface(() =>
        {
            // A área de transferência é recurso ÚNICO da máquina e fica presa enquanto outro
            // programa a segura: três tentativas, e falha nunca vira "copiado".
            for (var i = 0; i < 3; i++)
            {
                try { Clipboard.SetText(texto); return Task.FromResult(true); }
                catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(60); }
            }
            return Task.FromResult(false);
        });

    public Task<bool> NavegarAsync(string chave)
        => NaInterface(() => Task.FromResult(Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Ir(chave)));

    public Task<bool> AbrirEnderecoAsync(string url)
    {
        // Só http(s): o endereço vem montado pela tela, mas nada que não seja web sai daqui
        // como "abrir" — um file:// ou um executável pela mesma porta seria outra coisa.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return Task.FromResult(false);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — endereço externo não pôde ser aberto", ex);
            return Task.FromResult(false);
        }
    }

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        return d is null || d.CheckAccess() ? acao() : d.InvokeAsync(acao).Task.Unwrap();
    }
}
