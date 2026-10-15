using System.IO;
using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.FileProviders;

namespace Clinica.Financeiro.Web;

/// <summary>
/// Hospeda uma tela Blazor (projeto Clinica.Ui.Blazor) dentro do WPF — o piloto de
/// out/2026, ver docs/piloto-blazor.md.
///
/// Por que isto NÃO é o WebView2 que saiu em out/2026: aquele era HTML+JavaScript escrito
/// à mão, conversando com as ViewModels por mensagens, e cada tela partia um navegador
/// novo sem cor de fundo — a tela PRETA de 1 a 5 s a cada troca. Aqui:
/// 1. a tela é C# (Razor) chamando os MESMOS serviços da Application, sem ponte de
///    mensagens nem segunda cópia de regra;
/// 2. a cor de fundo é pintada nas TRÊS camadas que aparecem antes do conteúdo (o
///    controle WPF, o WebView2 e a primeira linha da página), então não existe instante
///    em que algo preto apareça;
/// 3. o controle é criado UMA vez por aba e guardado (o `TelaComAbas` monta cada aba uma
///    vez), então voltar à aba não parte navegador nenhum.
/// </summary>
public sealed class TelaBlazor : UserControl
{
    /// <summary>O fundo da página (`--fundo`, Cor.Cinza.50) — o mesmo da index.html.</summary>
    private const string FundoArgb = "FFF8FAFC";

    static TelaBlazor()
    {
        // Lida pelo WebView2 ao criar o controlador: é a cor do instante ANTES da página
        // existir, que é exatamente onde a tela preta aparecia. Vale para o processo.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", FundoArgb);
    }

    private readonly SinalDeVolta _sinal = new();

    public TelaBlazor(Type componente, IServiceProvider servicos)
    {
        SetResourceReference(BackgroundProperty, "Brush.Fundo");

        var visor = new VisorEmbutido
        {
            HostPage = Path.Combine("ativos", "index.html"),
            Services = servicos
        };
        visor.SetResourceReference(BackgroundProperty, "Brush.Fundo");
        // A tela entra pela RAIZ, que desenha junto a camada de diálogos e as notificações —
        // toda tela tem os mesmos vizinhos (ver Clinica.Ui.Blazor/Componentes/Raiz.razor).
        visor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Clinica.Ui.Blazor.Componentes.Raiz),
            Parameters = new Dictionary<string, object?> { ["Tela"] = componente, ["Sinal"] = _sinal }
        });

        // O shell reconhece telas que sabem se reler ao voltar pelo DataContext
        // (IAtualizarAoVoltar). É ele que faz a aba guardada voltar ATUALIZADA.
        DataContext = new AvisoDeVolta(_sinal);

        visor.BlazorWebViewInitializing += (_, e) =>
        {
            // O perfil do navegador mora no LocalAppData, fora da pasta da instalação: a
            // pasta do Velopack é substituída a cada atualização.
            e.UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaSemDor", "Blazor");
        };
        visor.BlazorWebViewInitialized += (_, e) =>
        {
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
        visor.UrlLoading += (_, e) =>
        {
            if (e.UrlLoadingStrategy == UrlLoadingStrategy.OpenExternally)
                e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        };

        Content = visor;
    }

    /// <summary>
    /// Serve a página e o CSS de DENTRO da DLL do Clinica.Ui.Blazor (manifesto de recursos)
    /// em vez de uma pasta ao lado do .exe — não há arquivo que o pacote possa esquecer.
    /// </summary>
    private sealed class AvisoDeVolta(SinalDeVolta sinal) : Clinica.Desktop.Shell.Modulos.IAtualizarAoVoltar
    {
        public void AtualizarAoVoltar() => sinal.Avisar();
    }

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
