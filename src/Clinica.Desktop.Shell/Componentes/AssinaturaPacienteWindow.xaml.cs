using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Colher a assinatura do paciente no tablet. Ver
/// <see cref="AssinaturaPacienteViewModel"/> para as decisões.
///
/// O que mora AQUI e não no ViewModel é só o que depende do controle visual: o
/// <c>InkCanvas</c> guarda traços (<see cref="StrokeCollection"/>) e não se amarra a uma
/// propriedade, e transformar esses traços em PNG exige rasterizar o próprio elemento.
/// </summary>
public partial class AssinaturaPacienteWindow : Window
{
    private readonly AssinaturaPacienteViewModel _vm;

    public AssinaturaPacienteWindow(AssinaturaPacienteViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = vm;

        // A caneta é fina e escura: o traço vai para um PDF em preto e branco, e uma
        // caneta grossa vira borrão quando a imagem é reduzida à faixa da assinatura.
        AreaAssinatura.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.Black,
            Width = 2.2,
            Height = 2.2,
            FitToCurve = true
        };

        vm.Fechou += AoFechar;
        Closed += (_, _) => vm.Fechou -= AoFechar;

        Loaded += async (_, _) => await vm.CarregarAsync();
    }

    private void AoFechar()
    {
        DialogResult = true;
        Close();
    }

    private void AoDesenhar(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        _vm.TemTraco = AreaAssinatura.Strokes.Count > 0;
        DicaAssine.Visibility = Visibility.Collapsed;
    }

    private void AoLimparTraco(object sender, RoutedEventArgs e)
    {
        AreaAssinatura.Strokes.Clear();
        _vm.TemTraco = false;
        DicaAssine.Visibility = Visibility.Visible;
    }

    private async void AoConfirmar(object sender, RoutedEventArgs e)
    {
        // Guarda sobre o CONTROLE, e ela fala: o botão já está apagado sem traço, mas o
        // atalho de teclado passa direto — e voltar calada é botão que não faz nada.
        if (AreaAssinatura.Strokes.Count == 0)
        {
            _vm.MensagemEhErro = true;
            _vm.Mensagem = "Peça ao paciente para assinar na área indicada.";
            return;
        }

        var png = ExportarPng();
        await _vm.ConfirmarAsync(png, (int)AreaAssinatura.ActualWidth, (int)AreaAssinatura.ActualHeight);
    }

    /// <summary>
    /// Rasteriza a área de assinatura em PNG com fundo transparente.
    ///
    /// ⚠️ Rasteriza o <c>InkCanvas</c>, e não a moldura em volta: incluir o
    /// <c>Border</c> traria o fundo e a borda para dentro do PDF, e o traço apareceria
    /// dentro de uma caixinha cinza em cima da linha de assinatura.
    ///
    /// A DPI é 192 (o dobro dos 96 padrão) porque o PDF encolhe a imagem para uma faixa de
    /// ~46 pontos de altura: na resolução de tela o traço sai serrilhado no papel.
    /// </summary>
    private byte[] ExportarPng()
    {
        const double dpi = 192;
        var escala = dpi / 96.0;

        var largura = (int)Math.Ceiling(AreaAssinatura.ActualWidth * escala);
        var altura = (int)Math.Ceiling(AreaAssinatura.ActualHeight * escala);

        var alvo = new RenderTargetBitmap(largura, altura, dpi, dpi, PixelFormats.Pbgra32);
        alvo.Render(AreaAssinatura);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(alvo));

        using var memoria = new MemoryStream();
        encoder.Save(memoria);
        return memoria.ToArray();
    }
}
