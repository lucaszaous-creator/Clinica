using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Desktop.Shell.Controls;

/// <summary>
/// Captura do retrato do paciente pela webcam da recepção. O visor mostra o recorte
/// quadrado central — exatamente o que será gravado — e o botão Capturar congela o
/// último quadro. Sempre há a saída alternativa "Escolher arquivo…", para o caso de
/// não haver câmera ou de ela estar ocupada por outro programa.
///
/// Cópia da janela do faturamento: a webcam fica no balcão, então quem precisa dela é
/// a Recepção, e o shell não pode referenciar o executável congelado.
/// </summary>
public partial class CapturaFotoWindow : Window
{
    private readonly CameraServico _camera = new();

    /// <summary>
    /// Quando a câmera é bloqueada pela privacidade do Windows (ou está presa em outro
    /// programa) ela costuma abrir sem erro e simplesmente não entregar quadro nenhum.
    /// Este relógio transforma esse silêncio numa orientação clara.
    /// </summary>
    private readonly DispatcherTimer _vigiaPrimeiroQuadro = new() { Interval = TimeSpan.FromSeconds(6) };

    private bool _capturado;
    private bool _recebeuQuadro;

    /// <summary>Foto cheia em JPEG (só preenchida quando o diálogo confirma).</summary>
    public byte[]? Conteudo { get; private set; }

    /// <summary>Miniatura em JPEG para os avatares (só preenchida quando o diálogo confirma).</summary>
    public byte[]? Miniatura { get; private set; }

    public CapturaFotoWindow(string nomePaciente)
    {
        InitializeComponent();
        TxtPaciente.Text = string.IsNullOrWhiteSpace(nomePaciente)
            ? "Novo cadastro"
            : nomePaciente;

        _camera.QuadroRecebido += AoReceberQuadro;
        _camera.Falhou += AoFalhar;
        _vigiaPrimeiroQuadro.Tick += AoDemorarPrimeiroQuadro;

        Loaded += (_, _) => CarregarCameras();
        Closed += (_, _) =>
        {
            _vigiaPrimeiroQuadro.Stop();
            _camera.Dispose();
        };
    }

    private void AoDemorarPrimeiroQuadro(object? sender, EventArgs e)
    {
        _vigiaPrimeiroQuadro.Stop();
        if (_recebeuQuadro || _capturado) return;

        TxtEstado.Text = "A câmera não está enviando imagem.";
        TxtEstado.Visibility = Visibility.Visible;
        Erro("Verifique se outro programa está usando a webcam e se o acesso está liberado em "
             + "Configurações do Windows → Privacidade e segurança → Câmera "
             + "(inclusive “Permitir que aplicativos da área de trabalho acessem sua câmera”). "
             + "Enquanto isso, dá para usar “Escolher arquivo…”.");
    }

    private void CarregarCameras()
    {
        // A enumeração passa pelo DirectShow: se ela falhar (driver, política de
        // privacidade da câmera, biblioteca ausente), a janela ainda serve para
        // escolher um arquivo — nada de erro na cara do usuário.
        IReadOnlyList<DispositivoCamera> cameras;
        try
        {
            cameras = CameraServico.Listar();
        }
        catch (Exception ex)
        {
            cameras = Array.Empty<DispositivoCamera>();
            Erro($"Não foi possível acessar as câmeras deste computador: {ex.Message}");
        }

        if (cameras.Count == 0)
        {
            PainelCamera.Visibility = Visibility.Collapsed;
            BtnCapturar.IsEnabled = false;
            Guia.Visibility = Visibility.Collapsed;
            TxtEstado.Text = "Nenhuma webcam encontrada.\nConecte a câmera e reabra esta janela, "
                             + "ou use “Escolher arquivo…”.";
            return;
        }

        ComboCameras.ItemsSource = cameras;
        ComboCameras.SelectedIndex = 0; // dispara o SelectionChanged, que liga a câmera
    }

    private void ComboCameras_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboCameras.SelectedItem is not DispositivoCamera camera) return;
        _capturado = false;
        _recebeuQuadro = false;
        AtualizarAcoes();
        TxtEstado.Text = "Ligando a câmera…";
        TxtEstado.Visibility = Visibility.Visible;
        Erro(null);
        _camera.Iniciar(camera);

        _vigiaPrimeiroQuadro.Stop();
        _vigiaPrimeiroQuadro.Start();
    }

    /// <summary>Chega na thread do AForge: a imagem é montada aqui (congelada) e só a troca vai para a UI.</summary>
    private void AoReceberQuadro(byte[] jpeg)
    {
        if (_capturado) return;
        var imagem = Retrato.Carregar(jpeg);
        if (imagem is null) return;

        Dispatcher.InvokeAsync(() =>
        {
            if (_capturado) return;
            _recebeuQuadro = true;
            _vigiaPrimeiroQuadro.Stop();
            Visor.Source = imagem;
            TxtEstado.Visibility = Visibility.Collapsed;
        });
    }

    private void AoFalhar(string mensagem)
        => Dispatcher.InvokeAsync(() =>
        {
            _vigiaPrimeiroQuadro.Stop();
            Erro($"Problema com a câmera: {mensagem}. Você ainda pode usar “Escolher arquivo…”.");
        });

    private void Capturar_Click(object sender, RoutedEventArgs e)
    {
        var quadro = _camera.UltimoQuadro();
        if (quadro is null)
        {
            Erro("Aguarde a imagem da câmera aparecer no visor e tente de novo.");
            return;
        }
        Congelar(quadro);
    }

    private void Arquivo_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Escolher a foto do paciente",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };
        if (dialogo.ShowDialog(this) != true) return;

        try
        {
            Congelar(File.ReadAllBytes(dialogo.FileName));
        }
        catch (Exception ex)
        {
            Erro($"Não foi possível ler o arquivo: {ex.Message}");
        }
    }

    /// <summary>Recorta e guarda a imagem escolhida, deixando o visor congelado nela.</summary>
    private void Congelar(byte[] imagem)
    {
        try
        {
            var (cheia, miniatura) = Retrato.Preparar(imagem);
            Conteudo = cheia;
            Miniatura = miniatura;
            _capturado = true;
            _vigiaPrimeiroQuadro.Stop();
            Visor.Source = Retrato.Carregar(cheia);
            TxtEstado.Visibility = Visibility.Collapsed;
            Erro(null);
            AtualizarAcoes();
        }
        catch (Exception ex)
        {
            Erro($"Não foi possível preparar a foto: {ex.Message}");
        }
    }

    private void Repetir_Click(object sender, RoutedEventArgs e)
    {
        Conteudo = null;
        Miniatura = null;
        _capturado = false;
        Erro(null);
        AtualizarAcoes();

        if (!_camera.Ativa && ComboCameras.SelectedItem is DispositivoCamera camera)
            _camera.Iniciar(camera);
    }

    private void Usar_Click(object sender, RoutedEventArgs e)
    {
        if (Conteudo is null || Miniatura is null)
        {
            Erro("Capture a foto antes de confirmar.");
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    private void AtualizarAcoes()
    {
        var temCameras = ComboCameras.Items.Count > 0;
        BtnCapturar.Visibility = _capturado ? Visibility.Collapsed : Visibility.Visible;
        BtnCapturar.IsEnabled = temCameras;
        BtnRepetir.Visibility = _capturado ? Visibility.Visible : Visibility.Collapsed;
        BtnUsar.Visibility = _capturado ? Visibility.Visible : Visibility.Collapsed;
        BtnUsar.IsDefault = _capturado;
        BtnCapturar.IsDefault = !_capturado;
        Guia.Visibility = _capturado || !temCameras ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Erro(string? mensagem)
    {
        TxtErro.Text = mensagem ?? string.Empty;
        TxtErro.Visibility = string.IsNullOrEmpty(mensagem) ? Visibility.Collapsed : Visibility.Visible;
    }
}
