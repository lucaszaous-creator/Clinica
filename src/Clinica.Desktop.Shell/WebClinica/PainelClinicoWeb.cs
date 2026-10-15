using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Clinica.Domain.Entities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Clinica.Desktop.Shell.WebClinica;

/// <summary>Apresentação local. O adaptador fornece somente dados e comandos explicitamente autorizados.</summary>
public sealed class PainelClinicoWeb : UserControl, IDisposable
{
    private const string Origem = "https://interface.clinica.local/index.html";
    private readonly WebView2 _browser = new() { DefaultBackgroundColor = System.Drawing.Color.White };
    private Window? _janela;
    private readonly Func<object> _estado;
    private readonly Func<JsonElement, Task> _executar;
    private readonly Action _acesso;
    private readonly string _tela;
    private readonly string _contexto = Guid.NewGuid().ToString("N");
    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private readonly Permissao _permissoes = SessaoUsuario.Atual.Efetivas;
    private readonly DispatcherTimer _timer;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private bool _iniciou, _pronto, _ocupado, _descartado;
    private string? _ultimo, _erro;
    public bool PodeReutilizar => !_descartado && ReferenceEquals(Content, _raiz);

    // ===== A tela preta (out/2026) =====
    // O WebView2 é uma janela NATIVA: até o navegador pintar a primeira página, a área dele
    // fica PRETA — e o WPF não consegue desenhar nada por cima dela. Era o retângulo preto de
    // 1–5 s ao abrir Receitas, Infusão, Pedidos e Ocupação. Agora o navegador nasce com 1×1 px
    // (vivo, carregando e pintando) e a área mostra o fundo claro da suíte com "Abrindo…"; ele
    // só ganha o tamanho da tela quando a página avisa que DESENHOU o primeiro estado.
    private readonly Grid _raiz = new();
    private readonly Border _espera;
    private readonly DispatcherTimer _limiteEspera;
    private bool _exibido;

    public PainelClinicoWeb(string tela, Func<object> obterEstado, Func<JsonElement, Task> executar, Action exigirAcesso)
    {
        _tela = tela; _estado = obterEstado; _executar = executar; _acesso = exigirAcesso;
        _browser.Width = 1; _browser.Height = 1;
        _browser.HorizontalAlignment = HorizontalAlignment.Left;
        _browser.VerticalAlignment = VerticalAlignment.Top;
        var aviso = new TextBlock { Text = "Abrindo…", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        aviso.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Texto.Secundario");
        _espera = new Border { Child = aviso, Background = Brushes.White };
        _raiz.Children.Add(_browser);
        _raiz.Children.Add(_espera);
        Content = _raiz;
        // Rede: página antiga (sem o aviso "exibido") ou pintura que não avisou — nunca fica escondida.
        _limiteEspera = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _limiteEspera.Tick += (_, _) => Exibir();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Enviar();
        Loaded += Carregar;
        Unloaded += (_, _) => _timer.Stop();
    }

    private void ExigirAcesso()
    {
        if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuario || SessaoUsuario.Atual.Efetivas != _permissoes)
            throw new InvalidOperationException("Sua sessão mudou. Abra a tela novamente.");
        _acesso();
    }

    private async void Carregar(object sender, RoutedEventArgs e)
    {
        if (_descartado) return;
        if (_janela is null && Window.GetWindow(this) is { } janela)
        {
            _janela = janela;
            WeakEventManager<Window, EventArgs>.AddHandler(janela, nameof(Window.Closed), AoFecharJanela);
        }
        _timer.Start();
        if (_iniciou) { Enviar(); return; }
        _iniciou = true;
        _limiteEspera.Start();
        try
        {
            ExigirAcesso();
            var pasta = Path.Combine(AppContext.BaseDirectory, "WebClinica", "wwwroot");
            if (!File.Exists(Path.Combine(pasta, "index.html"))) throw new FileNotFoundException("Interface local ausente. Reinstale esta versão do aplicativo.");
            var ambiente = await Ambiente();
            if (_descartado) return;
            await _browser.EnsureCoreWebView2Async(ambiente);
            if (_descartado) return;
            var core = _browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.SetVirtualHostNameToFolderMapping("interface.clinica.local", pasta, CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += (_, a) => { a.Cancel = a.Uri != Origem; if (!a.Cancel) _pronto = false; };
            core.FrameNavigationStarting += (_, a) => a.Cancel = true;
            core.NewWindowRequested += (_, a) => a.Handled = true;
            core.DownloadStarting += (_, a) => a.Cancel = true;
            core.PermissionRequested += (_, a) => { a.State = CoreWebView2PermissionState.Deny; a.Handled = true; };
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, a) =>
            {
                if (!Uri.TryCreate(a.Request.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "interface.clinica.local" || uri.Port != 443 || uri.UserInfo.Length > 0)
                    a.Response = core.Environment.CreateWebResourceResponse(Stream.Null, 403, "Bloqueado", "Content-Type: text/plain");
            };
            core.ProcessFailed += (_, _) => Falhar("A interface foi interrompida. Reabra a tela.");
            core.WebMessageReceived += Receber;
            core.Navigate(Origem);
        }
        catch (Exception ex) { Falhar(ex.Message); }
    }

    private async void Receber(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_descartado || !IsLoaded || e.Source != Origem || _browser.CoreWebView2.Source != Origem || e.WebMessageAsJson.Length > 65536) return;
        var executando = false;
        try
        {
            ExigirAcesso();
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var m = doc.RootElement;
            if (m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("acao", out var a)) return;
            var acao = a.GetString();
            if (acao == "exibido") { Exibir(); return; }
            if (acao == "pronto") { _pronto = true; _ultimo = null; Enviar(); return; }
            if (!_pronto || _ocupado || !m.TryGetProperty("contextoHost", out var contexto) || contexto.GetString() != _contexto) return;
            if (acao == "dimensionar" && _tela == "filtros-agenda")
            {
                if (m.TryGetProperty("altura", out var altura) && altura.TryGetDouble(out var valor) && double.IsFinite(valor))
                    Height = Math.Clamp(Math.Ceiling(valor), 100, 360);
                return;
            }
            executando = true;
            _erro = null;
            // Campos são aplicados sincronamente pelo adaptador, sem bloquear a digitação.
            _ocupado = acao is not ("campo" or "filtrar");
            if (_ocupado)
            {
                Enviar();
                // ShowDialog pode abrir outro WebView. Sair da pilha do evento nativo
                // antes do comando evita bloquear a inicialização desse navegador.
                await Dispatcher.Yield(DispatcherPriority.Background);
                if (_descartado || !IsLoaded) return;
                ExigirAcesso();
            }
            await _executar(m.Clone());
        }
        catch (Exception ex) { _erro = ex is JsonException ? "Comando inválido." : ex.Message; }
        finally { if (executando) { _ocupado = false; Enviar(); } }
    }

    private void Enviar()
    {
        if (!_pronto || _descartado || !IsLoaded) return;
        try
        {
            ExigirAcesso();
            var json = JsonSerializer.Serialize(new { tela = _tela, estado = _estado(), ocupado = _ocupado, erro = _erro, contextoHost = _contexto }, Json);
            if (json == _ultimo) return;
            _browser.CoreWebView2.PostWebMessageAsJson(json); _ultimo = json;
        }
        catch (Exception ex) { Falhar(ex.Message); }
    }

    /// <summary>A página pintou: o navegador ganha a área inteira e o "Abrindo…" sai.</summary>
    private void Exibir()
    {
        _limiteEspera.Stop();
        if (_exibido || _descartado) return;
        _exibido = true;
        _browser.Width = double.NaN; _browser.Height = double.NaN;
        _browser.HorizontalAlignment = HorizontalAlignment.Stretch;
        _browser.VerticalAlignment = VerticalAlignment.Stretch;
        _espera.Visibility = Visibility.Collapsed;
    }

    // ===== Um navegador para o app inteiro =====
    // Cada tela criava o seu ambiente do WebView2; agora todas dividem um, e o app o aquece
    // (processo do navegador no ar, página no cache) logo depois de abrir — a primeira tela
    // web deixa de pagar a partida do navegador.
    private static readonly object TravaAmbiente = new();
    private static Task<CoreWebView2Environment>? _ambiente;
    private static CoreWebView2Controller? _aquecido;

    private static string Perfil => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClinicaSemDor", "Interface", "WebView2");

    private static Task<CoreWebView2Environment> Ambiente()
    {
        lock (TravaAmbiente)
        {
            if (_ambiente is null || _ambiente.IsFaulted || _ambiente.IsCanceled)
                _ambiente = CoreWebView2Environment.CreateAsync(userDataFolder: Perfil);
            return _ambiente;
        }
    }

    /// <summary>
    /// Sobe o navegador escondido, com a interface carregada, para a primeira tela web abrir
    /// rápido. Falhar aqui não muda nada: a tela inicializa do jeito de sempre.
    /// </summary>
    public static async void Preaquecer(Window janela)
    {
        try
        {
            if (_aquecido is not null) return;
            var pasta = Path.Combine(AppContext.BaseDirectory, "WebClinica", "wwwroot");
            if (!File.Exists(Path.Combine(pasta, "index.html"))) return;
            var hwnd = new WindowInteropHelper(janela).Handle;
            if (hwnd == IntPtr.Zero) return;
            var ambiente = await Ambiente();
            var controle = await ambiente.CreateCoreWebView2ControllerAsync(hwnd);
            controle.IsVisible = false;
            controle.Bounds = new System.Drawing.Rectangle(0, 0, 0, 0);
            var core = controle.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.SetVirtualHostNameToFolderMapping("interface.clinica.local", pasta, CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += (_, a) => a.Cancel = a.Uri != Origem;
            core.NewWindowRequested += (_, a) => a.Handled = true;
            core.DownloadStarting += (_, a) => a.Cancel = true;
            core.PermissionRequested += (_, a) => { a.State = CoreWebView2PermissionState.Deny; a.Handled = true; };
            core.Navigate(Origem);
            _aquecido = controle;
            janela.Closed += (_, _) => { _aquecido?.Close(); _aquecido = null; };
        }
        catch (Exception ex) { Configuracao.LogSuite.Registrar("Pré-aquecimento da interface web", ex); }
    }

    /// <summary>Publica mudanças já carregadas, sem esperar a próxima batida do timer.</summary>
    public void AtualizarEstado() => Enviar();

    private void Falhar(string mensagem)
    {
        _pronto = false; _timer.Stop(); _limiteEspera.Stop();
        if (!_descartado) Content = new TextBlock { Text = mensagem, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
    }

    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true; _timer.Stop(); _limiteEspera.Stop(); _browser.Dispose();
        if (_janela is not null)
            WeakEventManager<Window, EventArgs>.RemoveHandler(_janela, nameof(Window.Closed), AoFecharJanela);
        _janela = null;
    }
    private void AoFecharJanela(object? sender, EventArgs e) => Dispose();
}
