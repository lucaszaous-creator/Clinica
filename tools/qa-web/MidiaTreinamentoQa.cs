using System.IO;
using System.Security.Cryptography;
using Clinica.Desktop.Shell.Treinamento;

/// <summary>Mídias públicas do catálogo, fixadas no repositório para QA sem rede.</summary>
internal static class MidiaTreinamentoQa
{
    internal static void Preparar(string raiz, AulaTreinamento aula)
    {
        var origem = Path.Combine(AppContext.BaseDirectory, "midia-qa", aula.Id + ".mp4");
        using var arquivo = File.OpenRead(origem);
        var hash = Convert.ToHexString(SHA256.HashData(arquivo));
        if (!hash.Equals(aula.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mídia do QA diverge do catálogo publicado: " + aula.Id);
        var pasta = Path.Combine(raiz, "videos");
        Directory.CreateDirectory(pasta);
        File.Copy(origem, Path.Combine(pasta, aula.Id + "-" + aula.Sha256.ToLowerInvariant() + ".mp4"));
    }
}
