using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Colher a assinatura do paciente. Ver <see cref="AssinaturaPacienteViewModel"/> para as
/// decisões do ato; aqui fica o que depende do controle visual — o <c>InkCanvas</c>, que
/// guarda traços e não se amarra a propriedade.
///
/// A coleta é SEMPRE nesta janela. O modo "duas telas" (um monitor virado para o paciente,
/// parcela 66, 5ª rodada) foi REMOVIDO em out/2026 por decisão da direção — junto com a
/// escolha do monitor nas Configurações e a janela da tela do paciente. A única outra
/// origem do traço continua sendo o celular (termo pelo WhatsApp, parcela 81).
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
        vm.PropertyChanged += AoMudarPropriedadeDoVm;

        Closed += (_, _) =>
        {
            vm.Fechou -= AoFechar;
            vm.PropertyChanged -= AoMudarPropriedadeDoVm;
            // Vigia órfã seguiria consultando o balde com a janela fechada.
            vm.PararVigia();
        };

        Loaded += async (_, _) => await vm.CarregarAsync();
    }

    // ==================== A janela de quem conduz ====================

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

    /// <summary>
    /// O DONO ÚNICO da visibilidade da área local (parcela 58: estilo com gatilho morre
    /// no primeiro valor local). Ela só some quando o traço chegou do CELULAR — deixar a
    /// área ativa permitiria rabiscar por cima do que o paciente assinou.
    /// </summary>
    private void AtualizarAreaLocal()
        => AreaDeAssinaturaLocal.Visibility =
            _vm.TemTracoRemoto ? Visibility.Collapsed : Visibility.Visible;

    private void AoMudarPropriedadeDoVm(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssinaturaPacienteViewModel.TemTracoRemoto))
            AtualizarAreaLocal();
    }

    private void AoLimparTraco(object sender, RoutedEventArgs e)
    {
        AreaAssinatura.Strokes.Clear();
        _vm.TemTraco = false;
        DicaAssine.Visibility = Visibility.Visible;
    }

    private async void AoConfirmar(object sender, RoutedEventArgs e)
    {
        // O traço do CELULAR tem precedência (parcela 81): ele já existe em bytes, a área
        // local está oculta, e exportar o InkCanvas vazio no lugar dele confirmaria um
        // termo em branco por cima do que o paciente assinou.
        if (_vm.TemTracoRemoto)
        {
            await _vm.ConfirmarAsync(
                _vm.TracoRemotoPng!, _vm.TracoRemotoLargura, _vm.TracoRemotoAltura);
            return;
        }

        // Guarda que FALA: o botão já está apagado sem traço, mas o atalho de teclado passa
        // direto — e voltar calada é botão que não faz nada.
        if (AreaAssinatura.Strokes.Count == 0)
        {
            _vm.MensagemEhErro = true;
            _vm.Mensagem = "Peça ao paciente para assinar na área indicada.";
            return;
        }

        await _vm.ConfirmarAsync(
            ExportarPng(), (int)AreaAssinatura.ActualWidth, (int)AreaAssinatura.ActualHeight);
    }

    /// <summary>
    /// Rasteriza a área de assinatura desta janela em PNG com fundo transparente.
    ///
    /// A DPI é 192 (o dobro dos 96 padrão) porque o PDF encolhe a imagem para uma faixa de
    /// ~38 pontos de altura: na resolução de tela o traço sai serrilhado no papel.
    /// Rasteriza o <c>InkCanvas</c>, e não a moldura em volta — incluir o <c>Border</c>
    /// traria o fundo e a borda para dentro do PDF, e o traço apareceria dentro de uma
    /// caixinha cinza em cima da linha de assinatura.
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
