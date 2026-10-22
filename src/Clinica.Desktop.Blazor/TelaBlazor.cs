using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// Uma tela Blazor devolvida por <c>IModuloApp.CriarTela</c>.
///
/// ⚠️ Desde out/2026 a moldura dos apps é Blazor (<c>JanelaDoApp</c>): a janela principal tem
/// UM navegador ocupando tudo, e esta tela é só o DESCRITOR (<see cref="ITelaDoNavegador"/>)
/// que diz ao palco daquele navegador QUAL componente mostrar — ela nunca entra numa árvore
/// WPF ali. Ela continua sendo um <c>UserControl</c> por um caso só: posta dentro de outra
/// janela WPF, ela cria o navegador DAQUELA janela (<see cref="NavegadorDaJanela"/>) e se
/// mostra nele — um por janela, nunca um por tela.
///
/// ⚠️ A primeira versão (out/2026) criava um BlazorWebView por tela, com o fundo pintado em
/// três camadas para "não haver instante preto" — e havia: ~100 ms pretos a cada tela
/// aberta, que a clínica mostrou em vídeo. A janela nativa do WebView2 aparece antes de o
/// navegador existir dentro dela, e nenhuma cor configurada vale nesse instante. Não se
/// esconde esse instante com cor; deixa-se de criar navegador a cada tela.
/// </summary>
public sealed class TelaBlazor : UserControl, ITelaDoNavegador
{
    private readonly Type _tela;
    private readonly IServiceProvider _servicos;
    private NavegadorDaJanela? _navegador;

    /// <summary>O componente Razor que esta tela hospeda — é por ele que o palco da janela a
    /// põe à vista, e que o validador de leiaute confere que uma rota abriu a tela certa.</summary>
    public Type Componente => _tela;

    public TelaBlazor(Type componente, IServiceProvider servicos)
    {
        _tela = componente;
        _servicos = servicos;
        SetResourceReference(BackgroundProperty, "Brush.Fundo");
        Focusable = false;

        // Fora da janela do app (uma tela posta noutra janela WPF): o navegador acompanha a
        // VISIBILIDADE desta tela, não a criação.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) Assumir();
            else _navegador?.Soltar(this);
        };
        LayoutUpdated += (_, _) => _navegador?.Posicionar(this);
    }

    private void Assumir()
    {
        if (Content is not Grid camada) Content = camada = new Grid();
        _navegador = NavegadorDaJanela.Da(camada, _servicos);
        _navegador.Mostrar(this, _tela);
    }
}

/// <summary>
/// O navegador de uma janela WPF que hospede <see cref="TelaBlazor"/> (fora da janela do app,
/// que tem o dela): um BlazorWebView com o <c>PalcoDeTelas</c> como raiz, criado na primeira
/// tela e nunca destruído enquanto a janela existir, posicionado sobre a área da tela à vista.
/// </summary>
internal sealed class NavegadorDaJanela
{
    private static readonly ConditionalWeakTable<Panel, NavegadorDaJanela> PorCamada = new();

    private readonly BlazorWebView _visor;
    private readonly Palco _palco = new();
    private readonly DispatcherTimer _rede;
    private readonly Panel _camada;
    private TelaBlazor? _dono;
    private bool _pronto;

    public static NavegadorDaJanela Da(Panel camada, IServiceProvider servicos)
        => PorCamada.GetValue(camada, c => new NavegadorDaJanela(c, servicos));

    private NavegadorDaJanela(Panel camada, IServiceProvider servicos)
    {
        _camada = camada;
        // As regras do navegador (perfil, fundo, menus) moram num lugar só, no shell.
        _visor = NavegadorEmbutido.Criar(servicos, typeof(Clinica.Ui.Blazor.Componentes.PalcoDeTelas),
            new Dictionary<string, object?> { ["Palco"] = _palco });
        _visor.HorizontalAlignment = HorizontalAlignment.Left;
        _visor.VerticalAlignment = VerticalAlignment.Top;

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
}

/// <summary>
/// O que a tela Blazor pede ao WPF. Reusa as rotinas do shell — CSV no padrão do Excel em
/// português (ponto e vírgula + BOM), a janela "Salvar como", a área de transferência e a
/// navegação da suíte — para não existir uma segunda definição de nenhuma delas.
///
/// O BlazorWebView roda os componentes no Dispatcher do WPF, então estas chamadas já
/// chegam na thread da interface; o `InvokeAsync` é a garantia, não a regra.
/// </summary>
public sealed class HostDaTelaWpf(IServiceProvider servicos) : IHostDaTela
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

    public Task<string?> AbrirPreviaAsync(byte[] pdf, string nomeSugerido)
        => NaInterface(() => ImpressaoPdf.AbrirPreviaAsync(pdf, nomeSugerido));

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

    public Task<bool> VoltarAsync()
        => NaInterface(() => Task.FromResult(Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Voltar()));

    /// <summary>
    /// A ficha do paciente é a TELA DO PACIENTE (Blazor) aberta na seção Ficha — no MESMO
    /// navegador da janela, nunca numa janela própria (out/2026: a ficha WPF por cima abria um
    /// segundo navegador e trazia de volta o flash preto). Fixa o paciente no posto e navega;
    /// o "voltar" da tela do paciente volta para onde se estava.
    ///
    /// ⚠️ A tela do paciente pede <c>VerProntuario</c>, e o balcão não o tem (o corte da parcela
    /// 49): para quem não a alcança, a ficha é a FICHA DO BALCÃO (Blazor, out/2026 — a
    /// administrativa, publicada pela Recepção). Sem esse caminho, a recepcionista clicava no
    /// nome do paciente no Início e lia "não está disponível neste aplicativo".
    /// </summary>
    public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId)
        => NaInterface(async () =>
        {
            const string chaveDaFicha = "consultorio-paciente";
            var foco = servicos.GetService<Clinica.Desktop.Shell.Modulos.PacienteEmFoco>();
            var telaDoPaciente = foco is not null && Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Existe(chaveDaFicha);
            var fichaDoBalcao = Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Existe(PedidoDaFichaDoBalcao.Chave);
            if (!telaDoPaciente && !fichaDoBalcao) return false;
            using var escopo = servicos.CreateScope();
            var paciente = await escopo.ServiceProvider
                .GetRequiredService<Clinica.Application.Servicos.PacienteService>().ObterAsync(pacienteId);
            if (paciente is null) return false;
            if (telaDoPaciente)
            {
                foco!.Definir(paciente.Id, paciente.Nome);
                return Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Ir(chaveDaFicha);
            }
            servicos.GetRequiredService<PedidoDaFichaDoBalcao>().Definir(paciente.Id, paciente.Nome);
            return Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Ir(PedidoDaFichaDoBalcao.Chave);
        });

    public Task<string?> PerguntarOndeSalvarAsync(string nomeSugerido, string filtro, string extensao)
        => NaInterface(() =>
        {
            var dialogo = new Microsoft.Win32.SaveFileDialog
            {
                FileName = ImpressaoPdf.NomeSeguro(nomeSugerido), Filter = filtro, DefaultExt = extensao
            };
            return Task.FromResult(dialogo.ShowDialog() == true ? dialogo.FileName : null);
        });

    public Task<string?> PerguntarPastaAsync(string titulo)
        => NaInterface(() => Task.FromResult(ImpressaoPdf.EscolherPasta(titulo)));

    public async Task<string?> GravarArquivoAsync(string caminho, byte[] conteudo)
    {
        try
        {
            // A pasta pode ainda não existir: a exportação do prontuário (Guarda) grava os CSV
            // numa SUBPASTA datada dentro da que a pessoa escolheu, como o WPF fazia.
            if (Path.GetDirectoryName(caminho) is { Length: > 0 } pasta) Directory.CreateDirectory(pasta);
            await File.WriteAllBytesAsync(caminho, conteudo);
            return null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — arquivo não pôde ser gravado", ex);
            return $"Não foi possível gravar {Path.GetFileName(caminho)}: {ex.Message}";
        }
    }

    public Task ChamarAtencaoAsync()
        => NaInterface(() =>
        {
            var janela = System.Windows.Application.Current?.MainWindow;
            if (janela is null || janela.IsActive) return Task.FromResult(false);
            var mao = new System.Windows.Interop.WindowInteropHelper(janela).Handle;
            if (mao == IntPtr.Zero) return Task.FromResult(false);
            // Pisca a janela e o botão da barra de tarefas até ela voltar para a frente
            // (FLASHW_ALL | FLASHW_TIMERNOFG) — o mesmo aviso que o Teams e o WhatsApp dão.
            var info = new PiscarJanela.Info
            {
                Tamanho = (uint)System.Runtime.InteropServices.Marshal.SizeOf<PiscarJanela.Info>(),
                Janela = mao, Modo = 0x3 | 0xC, Vezes = 0, Intervalo = 0,
            };
            return Task.FromResult(PiscarJanela.FlashWindowEx(ref info));
        });

    private static class PiscarJanela
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Info
        {
            public uint Tamanho;
            public IntPtr Janela;
            public uint Modo;
            public uint Vezes;
            public uint Intervalo;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool FlashWindowEx(ref Info info);
    }

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        return d is null || d.CheckAccess() ? acao() : d.InvokeAsync(acao).Task.Unwrap();
    }
}
