using System.IO;
using System.Windows;
using System.Windows.Controls;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.FileProviders;
using Microsoft.Web.WebView2.Core;

namespace Clinica.Desktop.Shell;

/// <summary>
/// O BlazorWebView da suíte, configurado num lugar só (out/2026): a página e o CSS servidos de
/// DENTRO da DLL do Clinica.Ui.Blazor, o perfil do navegador fora da pasta da instalação, o
/// fundo pintado antes da página e nada de menu de contexto, barra de status ou ferramentas de
/// desenvolvedor no build de produção.
///
/// Nasceu dentro da <c>NavegadorDaJanela</c>; subiu para cá quando a moldura dos apps passou a
/// ser Blazor (<see cref="JanelaDoApp"/>) — duas cópias destas regras divergiriam na primeira
/// correção, e a que ficasse para trás abriria o navegador com o menu de contexto do Edge.
/// </summary>
public static class NavegadorEmbutido
{
    /// <summary>O fundo da página (`--fundo`, Cor.Cinza.50) — o mesmo da index.html.</summary>
    public const string FundoArgb = "FFF8FAFC";

    static NavegadorEmbutido()
    {
        // A cor que o controlador do WebView2 pinta ANTES da página. Vale para o processo, e só
        // se for lida antes de o primeiro navegador nascer — por isso no construtor estático.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", FundoArgb);
    }

    /// <summary>
    /// Cria o navegador com <paramref name="raiz"/> como componente-raiz. Ele nasce
    /// <see cref="Visibility.Hidden"/> (nunca Collapsed: precisa estar no leiaute para a janela
    /// nativa nascer e a página carregar) — quem o hospeda o mostra depois de a raiz desenhar,
    /// porque a janela nativa recém-criada do WebView2 é preta.
    /// </summary>
    public static BlazorWebView Criar(IServiceProvider servicos, Type raiz,
        IDictionary<string, object?> parametros, Action<CoreWebView2>? aoIniciar = null)
    {
        var visor = new VisorEmbutido
        {
            HostPage = Path.Combine("ativos", "index.html"),
            Services = servicos,
            Visibility = Visibility.Hidden
        };
        visor.SetResourceReference(Control.BackgroundProperty, "Brush.Fundo");
        visor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = raiz,
            Parameters = parametros
        });

        visor.BlazorWebViewInitializing += (_, e) =>
        {
            // O perfil do navegador mora no LocalAppData, fora da pasta da instalação: a pasta
            // do Velopack é substituída a cada atualização.
            e.UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaSemDor", "Blazor");
        };
        visor.BlazorWebViewInitialized += (_, e) =>
        {
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
            // A foto do paciente (CapturaFotoForm) usa a webcam do balcão pelo navegador, e o áudio
            // do WhatsApp (GravadorDeAudio, 24/10/2026) o microfone. Sem isto o WebView2 pergunta com
            // um balão do próprio navegador a cada abertura — e a recepcionista não tem como saber
            // que é o sistema pedindo. Libera SÓ a câmera e o microfone e SÓ para a origem do
            // próprio app; o resto segue a regra padrão do WebView2. (O Windows ainda pode bloquear
            // o dispositivo na privacidade: a tela diz onde liberar.)
            e.WebView.CoreWebView2.PermissionRequested += (_, pedido) =>
            {
                if (pedido.PermissionKind is CoreWebView2PermissionKind.Camera or CoreWebView2PermissionKind.Microphone
                    && EhDaPropriaApp(pedido.Uri))
                    pedido.State = CoreWebView2PermissionState.Allow;
            };
            try { aoIniciar?.Invoke(e.WebView.CoreWebView2); }
            catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("Navegador da suíte — configuração extra", ex); }
        };
        // A tela não navega para fora: link externo não abre dentro do app (quem abre endereço
        // externo é o IHostDaTela.AbrirEnderecoAsync, no navegador padrão do Windows).
        visor.UrlLoading += (_, e) =>
        {
            if (e.UrlLoadingStrategy == UrlLoadingStrategy.OpenExternally)
                e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        };
        return visor;
    }

    /// <summary>A página pertence ao próprio app (a origem em que o BlazorWebView serve as telas)?</summary>
    public static bool EhDaPropriaApp(string? endereco)
        => Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
           && uri.Host == "0.0.0.0";

    /// <summary>
    /// Serve a página e o CSS de DENTRO da DLL do Clinica.Ui.Blazor (manifesto de recursos) em
    /// vez de uma pasta ao lado do .exe — não há arquivo que o pacote possa esquecer.
    /// </summary>
    private sealed class VisorEmbutido : BlazorWebView
    {
        public override IFileProvider CreateFileProvider(string contentRootDir) =>
            new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos");
    }
}
