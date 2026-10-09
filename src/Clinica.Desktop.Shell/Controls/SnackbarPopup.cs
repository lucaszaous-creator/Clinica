using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Janela transitória da notificação: o HWND do WebView2 fica acima de qualquer Border
/// WPF, mesmo com ZIndex. O popup vence esse airspace sem alterar o navegador.
/// Só acompanha sua janela ativa; não captura foco, mouse nem permanece sobre outros apps.
/// </summary>
public sealed class SnackbarPopup : Popup
{
    public static readonly DependencyProperty EstaVisivelProperty = DependencyProperty.Register(
        nameof(EstaVisivel), typeof(bool), typeof(SnackbarPopup),
        new PropertyMetadata(false, (d, _) => ((SnackbarPopup)d).Sincronizar()));

    public bool EstaVisivel
    {
        get => (bool)GetValue(EstaVisivelProperty);
        set => SetValue(EstaVisivelProperty, value);
    }

    private Window? _janela;

    public SnackbarPopup()
    {
        AllowsTransparency = true;
        Focusable = false;
        IsHitTestVisible = false;
        StaysOpen = true;
        PopupAnimation = PopupAnimation.None;
        Placement = PlacementMode.Custom;
        CustomPopupPlacementCallback = (tamanho, alvo, _) =>
            [new CustomPopupPlacement(new Point(Math.Max(8, (alvo.Width - tamanho.Width) / 2),
                Math.Max(8, alvo.Height - tamanho.Height - 24)), PopupPrimaryAxis.None)];
        Loaded += AoCarregar;
        Unloaded += AoDescarregar;
        Opened += AoAbrir;
    }

    private void AoCarregar(object sender, RoutedEventArgs e)
    {
        DesligarJanela();
        _janela = Window.GetWindow(PlacementTarget ?? this);
        if (_janela is null) return;
        _janela.Activated += AoMudarEstado;
        _janela.Deactivated += AoMudarEstado;
        _janela.StateChanged += AoMudarEstado;
        _janela.LocationChanged += AoMudarEstado;
        _janela.SizeChanged += AoMudarTamanho;
        _janela.IsVisibleChanged += AoMudarVisibilidade;
        _janela.Closed += AoFecharJanela;
        Sincronizar();
    }

    private void AoDescarregar(object sender, RoutedEventArgs e) => DesligarJanela();
    private void AoFecharJanela(object? sender, EventArgs e) => DesligarJanela();
    private void AoMudarEstado(object? sender, EventArgs e) => Sincronizar();
    private void AoMudarTamanho(object sender, SizeChangedEventArgs e) => Sincronizar();
    private void AoMudarVisibilidade(object sender, DependencyPropertyChangedEventArgs e) => Sincronizar();

    private void Sincronizar()
    {
        var mostrar = EstaVisivel && IsLoaded && _janela is { IsActive: true, IsVisible: true }
            && _janela.WindowState != WindowState.Minimized
            && GetForegroundWindow() == new WindowInteropHelper(_janela).Handle;
        if (!mostrar) { IsOpen = false; return; }
        if (PlacementTarget is FrameworkElement alvo)
            PlacementRectangle = new Rect(0, 0, alvo.ActualWidth, alvo.ActualHeight);
        if (!IsOpen) IsOpen = true;
        else
        {
            // Popup não segue LocationChanged sozinho. Invalidar um offset reposiciona
            // pelo alvo sem fechar/reabrir nem gerar ativação; a diferença é subpixel.
            HorizontalOffset = HorizontalOffset == 0 ? 0.01 : 0;
        }
    }

    private void AoAbrir(object? sender, EventArgs e)
    {
        if (Child is null || PresentationSource.FromVisual(Child) is not HwndSource fonte) return;
        // Popup WPF é topmost por padrão. Remover essa característica é essencial:
        // nem uma mudança de foco entre eventos pode deixá-lo sobre outro aplicativo.
        if (!SetWindowPos(fonte.Handle, new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010))
            IsOpen = false; // O aviso continua no histórico; não conservar um popup topmost.
    }

    private void DesligarJanela()
    {
        IsOpen = false;
        if (_janela is null) return;
        _janela.Activated -= AoMudarEstado;
        _janela.Deactivated -= AoMudarEstado;
        _janela.StateChanged -= AoMudarEstado;
        _janela.LocationChanged -= AoMudarEstado;
        _janela.SizeChanged -= AoMudarTamanho;
        _janela.IsVisibleChanged -= AoMudarVisibilidade;
        _janela.Closed -= AoFecharJanela;
        _janela = null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr janela, IntPtr depois, int x, int y, int largura, int altura, uint flags);
}
