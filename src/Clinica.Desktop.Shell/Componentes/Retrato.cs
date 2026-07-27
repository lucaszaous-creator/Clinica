using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Desktop.Shell.Configuracao;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Preparo do retrato do paciente: recorte quadrado centralizado (é assim que ele
/// aparece nos avatares) e duas saídas em JPEG — a foto cheia, guardada na tabela
/// própria, e a miniatura, que viaja junto com a linha do paciente.
///
/// Cópia deliberada de <c>Clinica.Desktop/Servicos/Retrato.cs</c>: a webcam é da
/// RECEPÇÃO, e o shell não pode referenciar o executável congelado do faturamento.
/// Mesmo débito de duplicação do design system, que a Fase 4 cancelada tornou
/// permanente — ver <c>docs/arquitetura-multi-exe.md</c>. As constantes de tamanho
/// precisam continuar iguais às de lá: os dois gravam na MESMA tabela.
/// </summary>
public static class Retrato
{
    /// <summary>Lado da foto cheia gravada no banco.</summary>
    public const int LadoCheio = 640;

    /// <summary>Lado da miniatura usada nos avatares da lista.</summary>
    public const int LadoMiniatura = 160;

    /// <summary>Transforma bytes de imagem num BitmapImage congelado (seguro entre threads).</summary>
    public static BitmapImage? Carregar(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.StreamSource = new MemoryStream(bytes);
            imagem.EndInit();
            imagem.Freeze();
            return imagem;
        }
        catch (Exception ex)
        {
            // Bytes inválidos (arquivo corrompido): a UI cai no avatar de iniciais.
            LogSuite.Registrar("Retrato — imagem do paciente não pôde ser lida", ex);
            return null;
        }
    }

    /// <summary>
    /// Recorta o quadro no quadrado central e devolve o par (foto cheia, miniatura).
    /// Aceita qualquer formato que o WPF saiba abrir (JPEG, PNG, BMP…).
    /// </summary>
    public static (byte[] Cheia, byte[] Miniatura) Preparar(byte[] imagem)
    {
        var origem = Carregar(imagem)
            ?? throw new InvalidOperationException("Não foi possível ler a imagem.");

        var quadrada = RecortarQuadrado(origem);
        return (Codificar(quadrada, LadoCheio, 88), Codificar(quadrada, LadoMiniatura, 82));
    }

    /// <summary>Recorte quadrado centralizado — o mesmo enquadramento mostrado na captura.</summary>
    public static BitmapSource RecortarQuadrado(BitmapSource origem)
    {
        var lado = Math.Min(origem.PixelWidth, origem.PixelHeight);
        if (lado <= 0) return origem;
        if (origem.PixelWidth == origem.PixelHeight) return origem;

        var recorte = new CroppedBitmap(origem, new Int32Rect(
            (origem.PixelWidth - lado) / 2,
            (origem.PixelHeight - lado) / 2,
            lado, lado));
        recorte.Freeze();
        return recorte;
    }

    /// <summary>Reduz para o lado pedido (nunca amplia) e codifica em JPEG.</summary>
    private static byte[] Codificar(BitmapSource fonte, int lado, int qualidade)
    {
        var maior = Math.Max(fonte.PixelWidth, fonte.PixelHeight);
        BitmapSource saida = fonte;

        if (maior > lado && maior > 0)
        {
            var escala = lado / (double)maior;
            var reduzida = new TransformedBitmap(fonte, new ScaleTransform(escala, escala));
            reduzida.Freeze();
            saida = reduzida;
        }

        var codificador = new JpegBitmapEncoder { QualityLevel = qualidade };
        codificador.Frames.Add(BitmapFrame.Create(saida));
        using var memoria = new MemoryStream();
        codificador.Save(memoria);
        return memoria.ToArray();
    }
}
