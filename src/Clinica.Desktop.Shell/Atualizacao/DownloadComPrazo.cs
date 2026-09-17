namespace Clinica.Atualizacao;

/// <summary>O limite de abertura não abandona o download nem deixa uma exceção sem observar.</summary>
public static class DownloadComPrazo
{
    public static async Task<T?> AguardarAsync<T>(Task<T?> download, TimeSpan limite,
        Action<T> aplicarAoFechar, Action<Exception> registrarFalha) where T : class
    {
        if (await Task.WhenAny(download, Task.Delay(limite)) == download)
            return await download;
        _ = CompletarAsync();
        return null;

        async Task CompletarAsync()
        {
            try
            {
                if (await download.ConfigureAwait(false) is { } pronto) aplicarAoFechar(pronto);
            }
            catch (Exception ex) { registrarFalha(ex); }
        }
    }
}
