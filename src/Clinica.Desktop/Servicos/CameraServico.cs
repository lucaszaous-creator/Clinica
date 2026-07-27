using System.Drawing.Imaging;
using System.IO;
using AForge.Video;
using AForge.Video.DirectShow;

namespace Clinica.Desktop.Servicos;

/// <summary>Webcam reconhecida pelo Windows (dispositivo DirectShow).</summary>
public sealed record DispositivoCamera(string Nome, string Moniker)
{
    public override string ToString() => Nome;
}

/// <summary>
/// Acesso à webcam da recepção via DirectShow. Cada quadro sai daqui já codificado em
/// JPEG: assim o System.Drawing (Bitmap) fica confinado nesta classe e a UI trabalha
/// só com bytes, que ela transforma em BitmapImage.
/// Os eventos chegam numa thread própria do AForge — quem escuta precisa voltar para o
/// Dispatcher antes de tocar na tela.
/// </summary>
public sealed class CameraServico : IDisposable
{
    private readonly object _trava = new();
    private VideoCaptureDevice? _dispositivo;
    private byte[]? _ultimoQuadro;

    /// <summary>Quadro recém-chegado da câmera, em JPEG. Disparado fora da thread de UI.</summary>
    public event Action<byte[]>? QuadroRecebido;

    /// <summary>Falha na captura (câmera ocupada por outro app, desconectada etc.).</summary>
    public event Action<string>? Falhou;

    public bool Ativa => _dispositivo?.IsRunning == true;

    /// <summary>Webcams instaladas. Lista vazia = nenhuma câmera conectada.</summary>
    public static IReadOnlyList<DispositivoCamera> Listar()
    {
        var lista = new List<DispositivoCamera>();
        try
        {
            foreach (FilterInfo filtro in new FilterInfoCollection(FilterCategory.VideoInputDevice))
                lista.Add(new DispositivoCamera(filtro.Name, filtro.MonikerString));
        }
        catch (Exception ex)
        {
            // Sem DirectShow disponível: a tela cai no modo "escolher arquivo".
            Configuracao.LogErros.Registrar("Câmera — enumeração de dispositivos falhou", ex);
        }
        return lista;
    }

    /// <summary>Liga a câmera e começa a emitir quadros. Troca de dispositivo é permitida.</summary>
    public void Iniciar(DispositivoCamera camera)
    {
        Parar();

        try
        {
            var dispositivo = new VideoCaptureDevice(camera.Moniker);

            // Melhor resolução até 1280px de largura: acima disso o preview pesa sem
            // ganho nenhum para um retrato de 640px. Se a câmera só oferece resoluções
            // maiores, fica com a menor delas.
            var capacidades = dispositivo.VideoCapabilities;
            if (capacidades is { Length: > 0 })
            {
                var candidatas = capacidades.Where(c => c.FrameSize.Width <= 1280).ToArray();
                dispositivo.VideoResolution = candidatas.Length > 0
                    ? candidatas.OrderByDescending(c => c.FrameSize.Width * c.FrameSize.Height).First()
                    : capacidades.OrderBy(c => c.FrameSize.Width * c.FrameSize.Height).First();
            }

            dispositivo.NewFrame += AoReceberQuadro;
            dispositivo.VideoSourceError += AoFalhar;
            _dispositivo = dispositivo;
            dispositivo.Start();
        }
        catch (Exception ex)
        {
            Parar();
            Configuracao.LogErros.Registrar("Câmera — falha ao abrir o dispositivo", ex);
            Falhou?.Invoke($"Não foi possível abrir a câmera: {ex.Message}");
        }
    }

    /// <summary>Desliga a câmera. Seguro de chamar mais de uma vez.</summary>
    public void Parar()
    {
        var dispositivo = _dispositivo;
        _dispositivo = null;
        if (dispositivo is null) return;

        dispositivo.NewFrame -= AoReceberQuadro;
        dispositivo.VideoSourceError -= AoFalhar;
        try
        {
            if (!dispositivo.IsRunning) return;

            dispositivo.SignalToStop();

            // WaitForStop bloqueia até a thread do driver encerrar — num driver ruim isso
            // congelaria a janela. A espera fica fora da UI; o dispositivo já foi solto aqui.
            Task.Run(() =>
            {
                try { dispositivo.WaitForStop(); }
                catch { /* encerramento best-effort */ }
            });
        }
        catch (Exception ex)
        {
            // Driver travando no encerramento não pode derrubar a tela.
            Configuracao.LogErros.Registrar("Câmera — falha ao encerrar o dispositivo", ex);
        }
    }

    /// <summary>Último quadro recebido (JPEG) — é o que a tela "congela" ao capturar.</summary>
    public byte[]? UltimoQuadro()
    {
        lock (_trava) return _ultimoQuadro;
    }

    private void AoReceberQuadro(object sender, NewFrameEventArgs e)
    {
        try
        {
            // O Bitmap pertence ao AForge e é reciclado: copiamos para JPEG aqui mesmo.
            using var memoria = new MemoryStream();
            e.Frame.Save(memoria, ImageFormat.Jpeg);
            var jpeg = memoria.ToArray();

            lock (_trava) _ultimoQuadro = jpeg;
            QuadroRecebido?.Invoke(jpeg);
        }
        catch
        {
            // Quadro corrompido: ignora e espera o próximo.
        }
    }

    private void AoFalhar(object sender, VideoSourceErrorEventArgs e)
        => Falhou?.Invoke(e.Description);

    public void Dispose() => Parar();
}
