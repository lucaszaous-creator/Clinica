using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Domain.Entities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Clinica.Desktop.Shell.WebClinica;

/// <summary>Apresentação local. O adaptador fornece somente dados e comandos explicitamente autorizados.</summary>
public sealed class PainelClinicoWeb : UserControl, IDisposable
{
    private const string Origem = "https://interface.clinica.local/index.html";
    private readonly WebView2 _browser = new();
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

    public PainelClinicoWeb(string tela, Func<object> obterEstado, Func<JsonElement, Task> executar, Action exigirAcesso)
    {
        _tela = tela; _estado = obterEstado; _executar = executar; _acesso = exigirAcesso;
        Content = _browser;
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
        _timer.Start();
        if (_iniciou) return;
        _iniciou = true;
        try
        {
            ExigirAcesso();
            var pasta = Path.Combine(AppContext.BaseDirectory, "WebClinica", "wwwroot");
            if (!File.Exists(Path.Combine(pasta, "index.html"))) throw new FileNotFoundException("Interface local ausente. Reinstale esta versão do aplicativo.");
            var perfil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicaSemDor", "Interface", "WebView2");
            var ambiente = await CoreWebView2Environment.CreateAsync(userDataFolder: perfil);
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
            if (acao == "pronto") { _pronto = true; _ultimo = null; Enviar(); return; }
            if (!_pronto || _ocupado || !m.TryGetProperty("contextoHost", out var contexto) || contexto.GetString() != _contexto) return;
            executando = true;
            _erro = null;
            // Campos são aplicados sincronamente pelo adaptador, sem bloquear a digitação.
            _ocupado = acao != "campo";
            if (_ocupado) Enviar();
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

    private void Falhar(string mensagem)
    {
        _pronto = false; _timer.Stop();
        if (!_descartado) Content = new TextBlock { Text = mensagem, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
    }

    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true; _timer.Stop(); _browser.Dispose();
    }
}
