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
/// fundo pintado antes da página e nada de barra de status nem de ferramentas de desenvolvedor no
/// build de produção. O menu de contexto do navegador só aparece em CAMPO EDITÁVEL, e só com o
/// corretor ortográfico em pt-BR, recortar, copiar e colar (<see cref="MenuDoCorretor"/>, 24/11/2026).
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

        visor.BlazorWebViewInitializing += (_, e) => PrepararAmbiente(e);
        visor.BlazorWebViewInitialized += (_, e) =>
        {
            e.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xF8, 0xFA, 0xFC);
            ConfigurarNavegador(e.WebView.CoreWebView2);
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

    /// <summary>
    /// O ambiente do navegador, IGUAL em toda janela da suíte (a do app e a de formulário avulso,
    /// <c>JanelaDeFormularioBlazor</c>): as duas usam a mesma pasta de perfil, e o WebView2 recusa
    /// abrir um segundo ambiente na mesma pasta com opções diferentes.
    /// <list type="bullet">
    /// <item>O perfil mora no LocalAppData, fora da pasta da instalação: a pasta do Velopack é
    /// substituída a cada atualização.</item>
    /// <item>O idioma é o pt-BR (<see cref="MenuDoCorretor.Idioma"/>): os menus do navegador, o
    /// <c>accept-language</c> e o dicionário do corretor ortográfico — sem ele, o corretor seguiria o
    /// idioma do Windows da máquina do balcão.</item>
    /// </list>
    /// </summary>
    public static void PrepararAmbiente(BlazorWebViewInitializingEventArgs e)
    {
        e.UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClinicaSemDor", "Blazor");
        e.EnvironmentOptions = new CoreWebView2EnvironmentOptions { Language = MenuDoCorretor.Idioma };
    }

    /// <summary>
    /// As regras do navegador, iguais em toda janela da suíte: sem barra de status, sem guardar senha
    /// nem preencher formulário, sem ferramentas de desenvolvedor no build de produção, e o menu de
    /// contexto só em campo editável, só com o corretor, recortar, copiar e colar.
    /// </summary>
    public static void ConfigurarNavegador(CoreWebView2 navegador)
    {
        var config = navegador.Settings;
        // O menu precisa estar LIGADO para o corretor aparecer; quem o recorta é o ContextMenuRequested.
        config.AreDefaultContextMenusEnabled = true;
        config.IsStatusBarEnabled = false;
        config.IsPasswordAutosaveEnabled = false;
        config.IsGeneralAutofillEnabled = false;
#if !DEBUG
        config.AreDevToolsEnabled = false;
        config.AreBrowserAcceleratorKeysEnabled = false;
#endif
        navegador.ContextMenuRequested += (_, e) => RecortarMenu(e);
    }

    /// <summary>Aplica <see cref="MenuDoCorretor.Manter"/>: sem índice nenhum, não há menu (Handled sem menu próprio).</summary>
    private static void RecortarMenu(CoreWebView2ContextMenuRequestedEventArgs e)
    {
        try
        {
            var itens = e.MenuItems;
            var manter = MenuDoCorretor.Manter(e.ContextMenuTarget.IsEditable,
                itens.Select(i => new ItemDoMenuDoNavegador(i.Name ?? "", Tipo(i.Kind))).ToList());
            if (manter is null) { e.Handled = true; return; }
            var ficam = manter.ToHashSet();
            for (var i = itens.Count - 1; i >= 0; i--)
                if (!ficam.Contains(i)) itens.RemoveAt(i);
        }
        catch (Exception ex)
        {
            // Na dúvida, nenhum menu: o do Edge inteiro (Inspecionar, Salvar como) é o que não pode aparecer.
            e.Handled = true;
            Clinica.Application.Diagnostico.Registrar("Navegador da suíte — menu do corretor", ex);
        }
    }

    private static TipoDoItemDoNavegador Tipo(CoreWebView2ContextMenuItemKind tipo) => tipo switch
    {
        CoreWebView2ContextMenuItemKind.Command => TipoDoItemDoNavegador.Comando,
        CoreWebView2ContextMenuItemKind.Separator => TipoDoItemDoNavegador.Separador,
        CoreWebView2ContextMenuItemKind.Submenu => TipoDoItemDoNavegador.Submenu,
        _ => TipoDoItemDoNavegador.Outro
    };

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
