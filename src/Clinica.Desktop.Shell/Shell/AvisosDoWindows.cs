using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Clinica.Application.Servicos;

namespace Clinica.Desktop.Shell;

/// <summary>
/// A NOTIFICAÇÃO DO WINDOWS E O NÚMERO NO ÍCONE DA BARRA DE TAREFAS (24/11/2026, F1b do "CRM com cara de WhatsApp"):
/// com o app em segundo plano, a mensagem nova de paciente vira notificação do Windows; o botão do app na barra de
/// tarefas leva o número de conversas não lidas, o mesmo do item "WhatsApp" do menu. O texto é do
/// <see cref="NotificacaoDoWhatsApp"/> (Application, com teste); aqui só o Windows.
/// <list type="bullet">
/// <item><b>Notificação</b>: <c>Shell_NotifyIcon</c> com balão (no Windows 10/11 ele vira a notificação nativa, com o
/// nome do app e o som do sistema). É Win32 puro, de propósito: o "toast" do Windows App SDK pediria trocar o
/// <c>TargetFramework</c> dos apps para <c>net8.0-windows10.0.x</c> e registrar a identidade do app, e o
/// <c>NotifyIcon</c> do WinForms traria o <c>System.Windows.Forms</c> para dentro do shell WPF (nomes ambíguos e o
/// <c>compilar-sombra</c> sem chão). O ícone da área de notificação nasce na PRIMEIRA notificação e sai quando a
/// janela fecha; clicar na notificação (ou no ícone) traz a janela e abre a conversa.</item>
/// <item><b>Número no ícone</b>: o <see cref="TaskbarItemInfo.Overlay"/> do próprio WPF, uma bolinha com a cor de erro
/// do design system (<c>Brush.Erro</c>) — o selo vermelho de "não lido".</item>
/// </list>
/// Tudo protegido: falha do Windows vai ao log e nunca derruba a janela (notificação é cortesia, não regra).
/// </summary>
public sealed class AvisosDoWindows : IDisposable
{
    private const int WmDoIcone = 0x8000 + 0x51;          // WM_APP + 0x51: a mensagem que o ícone manda à janela
    private const int NinBalaoClicado = 0x405;            // NIN_BALLOONUSERCLICK
    private const int WmBotaoEsquerdoSolto = 0x202;       // WM_LBUTTONUP (clique no ícone)
    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2, NimSetVersion = 4;
    private const uint NifMessage = 0x1, NifIcon = 0x2, NifTip = 0x4, NifInfo = 0x10;
    private const uint NiifUser = 0x4, NiifLargeIcon = 0x20;
    private const uint NotifyIconVersion4 = 4;
    private const uint IdDoIcone = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOuVersao;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool ShellNotifyIcon(uint mensagem, ref NotifyIconData dados);

    private readonly Window _janela;
    private readonly Action<string?> _aoClicar;
    private HwndSource? _fonte;
    private System.Drawing.Icon? _icone;
    private bool _iconeNaArea;
    private string? _conversaDoUltimo;
    private string? _numero;

    /// <param name="janela">A janela do app (a dona do botão na barra de tarefas).</param>
    /// <param name="aoClicar">O clique na notificação: a conversa dela (nula = a lista da aba).</param>
    public AvisosDoWindows(Window janela, Action<string?> aoClicar)
    {
        _janela = janela;
        _aoClicar = aoClicar;
    }

    /// <summary>O app está em segundo plano: a janela não é a ativa, ou está minimizada.</summary>
    public bool EmSegundoPlano => !_janela.IsActive || _janela.WindowState == WindowState.Minimized;

    /// <summary>O número de não lidas no ícone da barra de tarefas (zero tira a bolinha).</summary>
    public void Contar(int naoLidas)
    {
        var numero = NotificacaoDoWhatsApp.NoIcone(naoLidas);
        if (numero == _numero) return;
        try
        {
            _numero = numero;
            var barra = _janela.TaskbarItemInfo ??= new TaskbarItemInfo();
            barra.Overlay = numero is null ? null : Bolinha(numero);
            barra.Description = NotificacaoDoWhatsApp.DicaDoIcone(naoLidas) ?? "";
        }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("WhatsApp — número no ícone da barra de tarefas", ex); }
    }

    /// <summary>A notificação do Windows (só chame com o app em segundo plano: quem olha a tela já vê a lista).</summary>
    public void Notificar(AvisoDoWindows aviso)
    {
        try
        {
            if (!GarantirIcone()) return;
            _conversaDoUltimo = aviso.ConversaId;
            var dados = Dados(NifInfo);
            dados.szInfoTitle = Cortar(aviso.Titulo, 63);
            dados.szInfo = Cortar(aviso.Texto, 255);
            dados.dwInfoFlags = NiifUser | NiifLargeIcon;
            dados.hBalloonIcon = _icone?.Handle ?? IntPtr.Zero;
            ShellNotifyIcon(NimModify, ref dados);
        }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("WhatsApp — notificação do Windows", ex); }
    }

    /// <summary>Tira o número do ícone e o ícone da área de notificação (fim da sessão, fechamento da janela).</summary>
    public void Limpar()
    {
        Contar(0);
        if (!_iconeNaArea) return;
        try
        {
            var dados = Dados(0);
            ShellNotifyIcon(NimDelete, ref dados);
        }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("WhatsApp — tirar o ícone da área de notificação", ex); }
        _iconeNaArea = false;
    }

    public void Dispose()
    {
        Limpar();
        _fonte?.RemoveHook(Gancho);
        _fonte = null;
        _icone?.Dispose();
        _icone = null;
    }

    private bool GarantirIcone()
    {
        if (_iconeNaArea) return true;
        var hwnd = new WindowInteropHelper(_janela).Handle;
        if (hwnd == IntPtr.Zero) return false;
        if (_fonte is null)
        {
            _fonte = HwndSource.FromHwnd(hwnd);
            _fonte?.AddHook(Gancho);
        }
        _icone ??= Environment.ProcessPath is { } exe ? System.Drawing.Icon.ExtractAssociatedIcon(exe) : null;
        var dados = Dados(NifMessage | NifIcon | NifTip);
        dados.uCallbackMessage = WmDoIcone;
        dados.hIcon = _icone?.Handle ?? IntPtr.Zero;
        dados.szTip = Cortar(string.IsNullOrWhiteSpace(_janela.Title) ? NotificacaoDoWhatsApp.Titulo : _janela.Title, 127);
        if (!ShellNotifyIcon(NimAdd, ref dados)) return false;
        dados.uTimeoutOuVersao = NotifyIconVersion4;
        ShellNotifyIcon(NimSetVersion, ref dados);
        _iconeNaArea = true;
        return true;
    }

    private NotifyIconData Dados(uint bandeiras) => new()
    {
        cbSize = Marshal.SizeOf<NotifyIconData>(),
        hWnd = new WindowInteropHelper(_janela).Handle,
        uID = IdDoIcone,
        uFlags = bandeiras,
        szTip = "", szInfo = "", szInfoTitle = ""
    };

    /// <summary>NOTIFYICON_VERSION_4: o evento vem na palavra baixa do lParam.</summary>
    private IntPtr Gancho(IntPtr hwnd, int mensagem, IntPtr wParam, IntPtr lParam, ref bool tratada)
    {
        if (mensagem != WmDoIcone) return IntPtr.Zero;
        var evento = (int)((long)lParam & 0xFFFF);
        if (evento is NinBalaoClicado or WmBotaoEsquerdoSolto)
        {
            tratada = true;
            var conversa = evento == NinBalaoClicado ? _conversaDoUltimo : null;
            try
            {
                if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
                _janela.Activate();
                _aoClicar(conversa);
            }
            catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("WhatsApp — clique na notificação do Windows", ex); }
        }
        return IntPtr.Zero;
    }

    /// <summary>A bolinha do número (32 px, o tamanho que o Windows reduz para o canto do botão), na cor de erro do design system.</summary>
    private ImageSource Bolinha(string numero)
    {
        const double lado = 32;
        var fundo = _janela.TryFindResource("Brush.Erro") as Brush ?? Brushes.Red;
        var letra = _janela.TryFindResource("Brush.Superficie") as Brush ?? Brushes.White;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawEllipse(fundo, null, new Point(lado / 2, lado / 2), lado / 2, lado / 2);
            var texto = new FormattedText(numero, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                numero.Length > 1 ? 15 : 20, letra, 1.0);
            dc.DrawText(texto, new Point((lado - texto.Width) / 2, (lado - texto.Height) / 2));
        }
        var imagem = new RenderTargetBitmap((int)lado, (int)lado, 96, 96, PixelFormats.Pbgra32);
        imagem.Render(visual);
        imagem.Freeze();
        return imagem;
    }

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
