using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Application.Assinatura;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// A JANELA DE TRANSIÇÃO de um formulário Blazor (out/2026 — a conversão da área do
/// SafeID/assinatura). As telas Blazor abrem os formulários na PRÓPRIA página
/// (<see cref="JanelasNaTela"/>), sem janela nenhuma. Esta janela existe só para o código WPF que
/// ainda pede um deles e não tem página onde abri-lo — a ficha do balcão (Recepção) assinando um
/// documento, colhendo um termo ou anotando a enfermagem, e a ponte WPF dos Prontuários. Quando
/// essas telas virarem Blazor, ela fica sem chamador e sai.
///
/// ⚠️ Ela cria um navegador próprio (o custo que o <see cref="NavegadorDaJanela"/> existe para
/// evitar nas telas): por isso o navegador nasce ESCONDIDO e só aparece depois da primeira
/// renderização — o fundo da janela é o da página, e o instante preto da janela nativa do WebView2
/// não chega à vista. É modal, como as janelas WPF que substitui.
/// </summary>
public sealed class JanelaDeFormularioBlazor : Window
{
    private readonly VisorDoFormulario _visor;
    private readonly DispatcherTimer _rede;
    private bool _terminou;
    private Exception? _falha;
    private Action? _desistir;

    private JanelaDeFormularioBlazor(IServiceProvider servicos, string titulo, Func<IServiceProvider, Task> acao)
    {
        Title = titulo;
        Width = 1040;
        Height = 700;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Brush.Fundo");

        _visor = new VisorDoFormulario
        {
            HostPage = Path.Combine("ativos", "index.html"),
            Services = servicos,
            Visibility = Visibility.Hidden
        };
        _visor.SetResourceReference(Control.BackgroundProperty, "Brush.Fundo");
        _visor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Clinica.Ui.Blazor.Componentes.FormularioAvulso),
            Parameters = new Dictionary<string, object?>
            {
                ["Acao"] = acao,
                ["AoMontar"] = (Action)(() => Dispatcher.BeginInvoke(Mostrar, DispatcherPriority.Background)),
                ["AoTerminar"] = (Action<Exception?>)(falha => Dispatcher.BeginInvoke(() => Terminar(falha))),
                ["AoPedirDesistencia"] = (Action<Action>)(desistir => _desistir = desistir)
            }
        });
        _visor.BlazorWebViewInitializing += (_, e) =>
        {
            // O MESMO perfil do navegador das telas (fora da pasta da instalação).
            e.UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClinicaSemDor", "Blazor");
        };
        _visor.BlazorWebViewInitialized += (_, e) =>
        {
            e.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xF8, 0xFA, 0xFC);
            var config = e.WebView.CoreWebView2.Settings;
            config.AreDefaultContextMenusEnabled = false;
            config.IsStatusBarEnabled = false;
            // A senha do A1 é digitada aqui: o navegador não a oferece para guardar.
            config.IsPasswordAutosaveEnabled = false;
            config.IsGeneralAutofillEnabled = false;
#if !DEBUG
            config.AreDevToolsEnabled = false;
            config.AreBrowserAcceleratorKeysEnabled = false;
#endif
        };
        _visor.UrlLoading += (_, e) =>
        {
            if (e.UrlLoadingStrategy == UrlLoadingStrategy.OpenExternally)
                e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        };
        Content = _visor;

        // Rede: se a primeira renderização não avisar, mostra assim mesmo — navegador escondido
        // para sempre seria pior.
        _rede = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _rede.Tick += (_, _) => Mostrar();
        _rede.Start();

        // O X da janela é "desisti": os formulários abertos desistem e a ação termina (fechar no
        // meio nunca grava — a porta "desisti" nunca é a que efetiva o ato).
        Closing += (_, e) =>
        {
            if (_terminou) return;
            e.Cancel = true;
            _desistir?.Invoke();
        };
        Closed += (_, _) => _rede.Stop();
    }

    private void Mostrar()
    {
        _rede.Stop();
        _visor.Visibility = Visibility.Visible;
    }

    private void Terminar(Exception? falha)
    {
        _falha = falha;
        _terminou = true;
        Close();
    }

    /// <summary>
    /// Abre o formulário (pela <paramref name="acao"/>, com os serviços do navegador desta janela)
    /// e espera ele terminar. A falha da ação é relançada para quem chamou mostrar a frase.
    /// </summary>
    public static T? Executar<T>(IServiceProvider servicos, string titulo, Func<IServiceProvider, Task<T>> acao)
    {
        T? resultado = default;
        var janela = new JanelaDeFormularioBlazor(servicos, titulo, async sp => resultado = await acao(sp))
        {
            Owner = JanelaDona.Atual()
        };
        janela.ShowDialog();
        if (janela._falha is { } falha) throw new InvalidOperationException(falha.Message, falha);
        return resultado;
    }

    private sealed class VisorDoFormulario : BlazorWebView
    {
        public override IFileProvider CreateFileProvider(string contentRootDir) =>
            new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos");
    }
}

/// <summary>
/// Os formulários Blazor que o código WPF ainda pede (out/2026) — a ponte de TRANSIÇÃO do
/// <see cref="IFormulariosBlazorNoWpf"/> do shell: cada um abre a <see cref="JanelaDeFormularioBlazor"/>
/// com a MESMA porta que as telas Blazor usam (<see cref="IAssinaturaNaTela"/>,
/// <see cref="ColetaDeTermoNaPagina"/>, o formulário da evolução de enfermagem).
/// </summary>
public sealed class FormulariosBlazorNoWpf(IServiceProvider servicos) : IFormulariosBlazorNoWpf
{
    public Task<CertificadoAssinatura?> EscolherCertificadoAsync(string assunto) => NaInterface(() =>
        JanelaDeFormularioBlazor.Executar(servicos, "Assinar com certificado A1",
            sp => sp.GetRequiredService<IAssinaturaNaTela>().EscolherCertificadoAsync(assunto)));

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId,
        int? profissionalId, int? agendamentoId) => NaInterface(() =>
        JanelaDeFormularioBlazor.Executar(servicos, "Termo do procedimento",
            sp => sp.GetRequiredService<ColetaDeTermoNaPagina>()
                .AbrirAsync(pacienteId, paciente, modeloId, documentoId, profissionalId, agendamentoId)));

    public Task AnotarEnfermagemAsync(int pacienteId, string paciente) => NaInterface(() =>
        JanelaDeFormularioBlazor.Executar(servicos, "Evolução de enfermagem",
            sp => sp.GetRequiredService<JanelasNaTela>()
                .AbrirAsync<Clinica.Ui.Blazor.Formularios.Infusao.EvolucaoDaFolhaForm, bool>(new Dictionary<string, object?>
                {
                    ["PacienteId"] = pacienteId,
                    ["Paciente"] = paciente
                })));

    private static Task<T> NaInterface<T>(Func<T> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return Task.FromResult(acao());
        return despachante.InvokeAsync(acao).Task;
    }
}
