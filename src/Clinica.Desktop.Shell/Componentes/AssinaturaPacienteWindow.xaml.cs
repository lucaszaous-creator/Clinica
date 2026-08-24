using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Colher a assinatura do paciente. Ver <see cref="AssinaturaPacienteViewModel"/> para as
/// decisões do ato; aqui ficam as duas coisas que dependem do controle visual — o
/// <c>InkCanvas</c> (que guarda traços e não se amarra a propriedade) e a SEGUNDA TELA.
///
/// Duas telas, o modelo da maquininha (parcela 66, 5ª rodada)
/// ----------------------------------------------------------
/// Quando a clínica configurou um monitor virado para o paciente, a coleta acontece em
/// DUAS janelas: esta fica com quem conduz (declarações, documento conferido, confirmar) e
/// a <see cref="PainelDoPacienteWindow"/> abre na tela dele com o termo grande e a área de
/// assinar. É o desenho que a cliente pediu — "o dispositivo ligar na hora aparecendo o
/// lugar certo para o paciente assinar".
///
/// ⚠️ Sem segunda tela configurada, TUDO continua acontecendo aqui, como antes. Não é um
/// modo degradado: é o modo de quem tem um monitor só, e ele precisa funcionar por inteiro
/// — a clínica pode ficar meses sem comprar o touch, e a feature não pode esperar por isso.
/// </summary>
public partial class AssinaturaPacienteWindow : Window
{
    private readonly AssinaturaPacienteViewModel _vm;

    /// <summary>A tela do paciente, quando há uma. Null = coleta numa janela só.</summary>
    private PainelDoPacienteWindow? _painel;

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
        vm.TermoCarregado += AoCarregarTermo;
        vm.DeclaracaoRespondida += AoResponderDeclaracao;
        vm.PropertyChanged += AoMudarPropriedadeDoVm;

        Closed += (_, _) =>
        {
            vm.Fechou -= AoFechar;
            vm.TermoCarregado -= AoCarregarTermo;
            vm.DeclaracaoRespondida -= AoResponderDeclaracao;
            vm.PropertyChanged -= AoMudarPropriedadeDoVm;
            // Vigia órfã seguiria consultando o balde com a janela fechada.
            vm.PararVigia();
            FecharPainelDoPaciente();
        };

        Loaded += async (_, _) => await vm.CarregarAsync();
    }

    // ==================== A tela do paciente ====================

    /// <summary>
    /// Abre a tela do paciente quando o termo terminou de carregar — e não antes: ela
    /// mostra o TEXTO, e uma tela em branco virada para quem vai assinar é pior do que
    /// tela nenhuma.
    /// </summary>
    private void AoCarregarTermo()
    {
        if (_vm.TelaDoPaciente is not { } tela) return;

        try
        {
            var painel = new PainelDoPacienteWindow(new PainelDoPacienteViewModel
            {
                Titulo = _vm.Titulo,
                PacienteNome = _vm.PacienteNome,
                Corpo = _vm.Corpo
            })
            {
                // Dona é ESTA janela: minimizar a coleta leva a tela do paciente junto, e
                // fechar aqui fecha lá. Painel órfão no balcão é um convite para alguém
                // assinar o termo de outra pessoa.
                Owner = this
            };

            foreach (var d in _vm.Declaracoes)
                painel.DataContextComoPainel().Declaracoes.Add(new DeclaracaoNaTelaDoPaciente
                {
                    Ordem = d.Ordem,
                    Descricao = d.Descricao,
                    Resposta = string.IsNullOrWhiteSpace(d.Resposta) ? "—" : d.Resposta!
                });

            painel.TracoMudou += AoMudarTracoDoPaciente;

            // Mostrar ANTES de posicionar: sem HWND não há o que mover.
            painel.Show();
            TelasDoSistema.Posicionar(painel, tela);
            TelasDoSistema.ManterAcordado(true);

            _painel = painel;

            // A área desta janela some: com duas telas, quem assina é o paciente na dele.
            // Deixar as duas ativas permitiria a recepcionista assinar pelo paciente sem
            // querer — e o termo diria que ele assinou.
            AtualizarAreaLocal();
            AvisoTelaDoPaciente.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            // Falhar aqui NÃO derruba a coleta: sem a segunda tela, ela continua nesta
            // janela, que é o modo de sempre. Mas não passa calado.
            Clinica.Application.Diagnostico.Registrar(
                "Assinatura do paciente — tela do paciente não pôde ser aberta", ex);

            _painel = null;
            AtualizarAreaLocal();
            AvisoTelaDoPaciente.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// A resposta marcada aqui aparece na tela dele NO MESMO INSTANTE.
    ///
    /// Não é conforto: o selo do termo é o SHA-256 do que o paciente tinha na frente, e uma
    /// tela que mostra o texto antigo faria a evidência afirmar algo que não é verdade.
    /// </summary>
    private void AoResponderDeclaracao(int ordem, string? resposta)
    {
        if (_painel?.DataContext is not PainelDoPacienteViewModel vm) return;

        var linha = vm.Declaracoes.FirstOrDefault(d => d.Ordem == ordem);
        if (linha is not null)
            linha.Resposta = string.IsNullOrWhiteSpace(resposta) ? "—" : resposta!;
    }

    private void AoMudarTracoDoPaciente(bool temTraco) => _vm.TemTraco = temTraco;

    private void FecharPainelDoPaciente()
    {
        TelasDoSistema.ManterAcordado(false);

        if (_painel is null) return;

        _painel.TracoMudou -= AoMudarTracoDoPaciente;
        _painel.Close();
        _painel = null;
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
    /// no primeiro valor local). Ela some em DOIS casos: há um monitor virado para o
    /// paciente, ou o traço chegou do CELULAR — nos dois, deixar a área ativa permitiria
    /// rabiscar por cima do que o paciente assinou.
    /// </summary>
    private void AtualizarAreaLocal()
        => AreaDeAssinaturaLocal.Visibility =
            _painel is null && !_vm.TemTracoRemoto ? Visibility.Visible : Visibility.Collapsed;

    private void AoMudarPropriedadeDoVm(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssinaturaPacienteViewModel.TemTracoRemoto))
            AtualizarAreaLocal();
    }

    private void AoLimparTraco(object sender, RoutedEventArgs e)
    {
        // Com duas telas, quem tem o traço é a tela dele — apagar daqui apaga lá.
        if (_painel is not null)
        {
            _painel.Limpar();
            return;
        }

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

        var assinouNoPainel = _painel is { } painel && painel.TemTraco;
        var assinouAqui = _painel is null && AreaAssinatura.Strokes.Count > 0;

        // Guarda que FALA: o botão já está apagado sem traço, mas o atalho de teclado passa
        // direto — e voltar calada é botão que não faz nada.
        if (!assinouNoPainel && !assinouAqui)
        {
            _vm.MensagemEhErro = true;
            _vm.Mensagem = _painel is null
                ? "Peça ao paciente para assinar na área indicada."
                : "O paciente ainda não assinou na tela dele.";
            return;
        }

        var (png, largura, altura) = _painel is { } p
            ? (p.ExportarPng(), p.LarguraDaArea, p.AlturaDaArea)
            : (ExportarPng(), (int)AreaAssinatura.ActualWidth, (int)AreaAssinatura.ActualHeight);

        await _vm.ConfirmarAsync(png, largura, altura);
    }

    /// <summary>
    /// Rasteriza a área de assinatura desta janela em PNG com fundo transparente. Só usada
    /// no modo de UMA tela — com duas, quem exporta é a tela do paciente.
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

/// <summary>Atalho tipado para o <c>DataContext</c> da tela do paciente.</summary>
internal static class PainelDoPacienteExtensoes
{
    public static PainelDoPacienteViewModel DataContextComoPainel(this PainelDoPacienteWindow janela)
        => (PainelDoPacienteViewModel)janela.DataContext;
}
