using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Um monitor ligado na máquina, como a clínica o escolhe em Configurações.</summary>
/// <param name="Id">
/// O nome do dispositivo (<c>\\.\DISPLAY2</c>). É o que fica GRAVADO — o índice na lista
/// muda quando alguém desliga um cabo, e a tela do paciente passaria a ser a da
/// recepcionista sem ninguém mexer em nada.
/// </param>
/// <param name="Principal">É a tela onde o Windows põe a barra de tarefas.</param>
public sealed record TelaDoSistema(
    string Id, string Rotulo, bool Principal, int X, int Y, int Largura, int Altura);

/// <summary>
/// Os monitores da máquina, para o sistema saber ONDE abrir a tela que o paciente vê
/// (parcela 66, 5ª rodada).
///
/// Por que P/Invoke e não <c>System.Windows.Forms.Screen</c>: ligar o WinForms num projeto
/// WPF só para listar monitor traz um framework inteiro de UI para dentro do processo. São
/// duas chamadas do Win32 e elas não mudam desde o Windows 2000.
///
/// ⚠️ As coordenadas aqui são PIXELS FÍSICOS, e é de propósito. WPF trabalha em unidades
/// independentes de DPI, e numa clínica com um monitor 4K de recepção e um touch 1080p de
/// balcão os dois sistemas discordam — posicionar a janela por unidade WPF a joga no lugar
/// errado, às vezes fora de qualquer tela. Quem posiciona é o <c>MoveWindow</c>, que fala
/// pixel físico.
/// </summary>
public static class TelasDoSistema
{
    /// <summary>Todos os monitores, a principal primeiro.</summary>
    public static IReadOnlyList<TelaDoSistema> Listar()
    {
        var telas = new List<TelaDoSistema>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var principal = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
                var largura = info.rcMonitor.Right - info.rcMonitor.Left;
                var altura = info.rcMonitor.Bottom - info.rcMonitor.Top;

                telas.Add(new TelaDoSistema(
                    info.szDevice,
                    // O rótulo diz o TAMANHO, não o número: "Tela 2" não ajuda ninguém a
                    // saber qual das duas é a do balcão, e a resolução costuma bastar
                    // (o touch pequeno é sempre o de menor resolução).
                    $"{(principal ? "Principal" : "Secundária")} — {largura}×{altura}",
                    principal, info.rcMonitor.Left, info.rcMonitor.Top, largura, altura));
            }

            return true;
        }, IntPtr.Zero);

        return telas.OrderByDescending(t => t.Principal).ToList();
    }

    /// <summary>
    /// A tela gravada, se ela ainda existir E não for a principal.
    ///
    /// ⚠️ As duas recusas importam. Cabo desligado é rotina numa clínica, e abrir o painel
    /// do paciente numa tela que não existe deixaria a janela invisível — a recepcionista
    /// veria o sistema travar esperando uma assinatura que ninguém pode dar. E apontar para
    /// a PRINCIPAL cobriria a tela dela com o painel em tela cheia, que é o mesmo desastre
    /// pelo avesso. Nos dois casos o sistema volta ao modo de uma tela só, que funciona.
    /// </summary>
    public static TelaDoSistema? Resolver(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var tela = Listar().FirstOrDefault(
            t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        return tela is null || tela.Principal ? null : tela;
    }

    /// <summary>
    /// Joga a janela para a tela indicada, em tela cheia e sem bordas.
    ///
    /// Precisa ser chamado DEPOIS de a janela estar visível: antes disso ela não tem HWND, e
    /// o <c>MoveWindow</c> não teria o que mover.
    /// </summary>
    public static void Posicionar(Window janela, TelaDoSistema tela)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(tela);

        var hwnd = new WindowInteropHelper(janela).Handle;
        if (hwnd == IntPtr.Zero) return;

        // Sem `WindowState.Maximized`: maximizar leva a janela para o monitor que o WINDOWS
        // acha que é o dela, e é justamente essa conta que estamos corrigindo. Ocupar o
        // retângulo exato do monitor dá o mesmo resultado visual e não depende de palpite.
        MoveWindow(hwnd, tela.X, tela.Y, tela.Largura, tela.Altura, true);
    }

    /// <summary>
    /// Impede o monitor de dormir enquanto a coleta está aberta.
    ///
    /// A tela do paciente fica parada boa parte do dia — é justamente o que faz o Windows
    /// desligá-la. Sem isto, a hora de assinar seria a hora de descobrir que a tela apagou,
    /// e alguém teria de sacudir um mouse que está do outro lado do balcão.
    /// </summary>
    public static void ManterAcordado(bool manter)
        => SetThreadExecutionState(manter
            ? ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED
            : ES_CONTINUOUS);

    // ==================== Win32 ====================

    private const int MONITORINFOF_PRIMARY = 1;

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr dados);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr rect, MonitorEnumProc callback, IntPtr dados);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(
        IntPtr janela, int x, int y, int largura, int altura, bool redesenhar);

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
