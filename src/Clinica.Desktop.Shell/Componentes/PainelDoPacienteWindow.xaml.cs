using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A tela virada para o PACIENTE (parcela 66, 5ª rodada) — o modelo da maquininha: quem
/// conduz fica com os controles, quem assina vê só o que precisa ler e onde assinar.
///
/// Ela é aberta e fechada pela <see cref="AssinaturaPacienteWindow"/>, e não vive sozinha:
/// sem a janela da recepcionista não há o que confirmar, e uma tela de assinatura órfã no
/// balcão é um convite para alguém assinar o termo de outra pessoa.
///
/// ⚠️ <c>Topmost</c> e sem barra de título: o paciente não pode fechar, mover nem alcançar
/// nada por trás. O que fecha é o fim da coleta, do outro lado do balcão.
/// </summary>
public partial class PainelDoPacienteWindow : Window
{
    /// <summary>Avisa a janela da recepcionista que o traço mudou (assinou ou apagou).</summary>
    public event Action<bool>? TracoMudou;

    public PainelDoPacienteWindow(PainelDoPacienteViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // A mesma caneta da janela de uma tela só: fina e escura, porque o destino é um PDF
        // em preto e branco onde a imagem é reduzida à faixa da assinatura.
        AreaAssinatura.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.Black,
            Width = 2.2,
            Height = 2.2,
            FitToCurve = true
        };
    }

    /// <summary>Há traço na área.</summary>
    public bool TemTraco => AreaAssinatura.Strokes.Count > 0;

    /// <summary>Largura da área de coleta, para o PDF manter a proporção.</summary>
    public int LarguraDaArea => (int)AreaAssinatura.ActualWidth;

    public int AlturaDaArea => (int)AreaAssinatura.ActualHeight;

    /// <summary>
    /// Rasteriza o traço em PNG com fundo transparente.
    ///
    /// É a MESMA rotina da janela de uma tela só, e por isso mora nas duas: 192 dpi (o dobro
    /// dos 96 padrão) porque o PDF encolhe a imagem para uma faixa de ~38 pontos de altura,
    /// e na resolução de tela o traço sai serrilhado no papel. Rasteriza o `InkCanvas` e não
    /// a moldura em volta — incluir o `Border` traria o fundo e a borda para dentro do PDF,
    /// e o traço apareceria dentro de uma caixinha cinza em cima da linha de assinatura.
    /// </summary>
    public byte[] ExportarPng()
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

    private void AoDesenhar(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        DicaAssine.Visibility = Visibility.Collapsed;
        TracoMudou?.Invoke(true);
    }

    private void AoLimpar(object sender, RoutedEventArgs e)
    {
        AreaAssinatura.Strokes.Clear();
        DicaAssine.Visibility = Visibility.Visible;
        TracoMudou?.Invoke(false);
    }

    /// <summary>Apaga o traço de fora — a recepcionista também pode recomeçar.</summary>
    public void Limpar() => AoLimpar(this, new RoutedEventArgs());

    /// <summary>
    /// Abre um EXEMPLO na tela escolhida, para a direção conferir que é a certa antes de
    /// haver um paciente esperando (Configurações → "Testar").
    ///
    /// As telas do Windows se chamam <c>\\.\DISPLAY2</c> e ninguém sabe qual é qual olhando
    /// o nome. Sem este teste, o primeiro a descobrir que o termo abre no monitor errado
    /// seria o paciente — vendo o próprio nome e o procedimento dele numa tela virada para a
    /// sala de espera.
    ///
    /// ⚠️ O exemplo NÃO usa dado de paciente nenhum, pela mesma razão do arquivo de teste da
    /// publicação (parcela 53): publicar documento real para conferir infraestrutura seria
    /// expor dado de saúde para testar posicionamento de janela.
    /// </summary>
    public static void MostrarExemplo(TelaDoSistema tela)
    {
        ArgumentNullException.ThrowIfNull(tela);

        var vm = new PainelDoPacienteViewModel
        {
            Titulo = "Exemplo — tela do paciente",
            PacienteNome = "É esta a tela virada para o paciente?",
            Corpo = "Se você está lendo isto no monitor virado para quem vai assinar, a "
                    + "configuração está certa. É aqui que o termo do procedimento vai "
                    + "aparecer, com as declarações e o espaço para a assinatura.\n\n"
                    + "Feche esta janela pelo botão abaixo."
        };

        vm.Declaracoes.Add(new DeclaracaoNaTelaDoPaciente
        {
            Ordem = 1,
            Descricao = "Exemplo de declaração — a resposta aparece aqui do lado",
            Resposta = "Sim"
        });

        var janela = new PainelDoPacienteWindow(vm);

        // O botão de apagar o traço vira o de FECHAR: no exemplo não há o que apagar, e uma
        // janela sem barra de título e sem saída obrigaria a direção a matar o app.
        janela.BotaoApagar.Content = "Fechar";
        janela.BotaoApagar.Click -= janela.AoLimpar;
        janela.BotaoApagar.Click += (_, _) => janela.Close();

        // Mostrar ANTES de posicionar: sem HWND não há o que mover.
        janela.Show();
        TelasDoSistema.Posicionar(janela, tela);
    }
}
