using System.Reflection;

namespace Clinica.Application.Servicos;

/// <summary>
/// Acesso à identidade visual da Clínica SemDor embarcada no assembly (Recursos/Marca),
/// para desenhar a logo no cabeçalho dos PDFs. Carrega uma única vez e nunca lança —
/// se o recurso não existir, retorna null e o documento é gerado sem a logo.
/// </summary>
public static class MarcaSemDor
{
    // Cores da marca (azul-royal do símbolo/wordmark e acentos claros da "gota").
    public const string Navy = "#07329A";
    public const string Azul = "#123A9E";
    public const string AzulMedio = "#5E87D9";
    public const string AzulClaro = "#B0D3F3";

    private static readonly Lazy<byte[]?> _logo = new(() => Carregar("logo-cor.png"));
    private static readonly Lazy<byte[]?> _simbolo = new(() => Carregar("simbolo-cor.png"));

    /// <summary>Logo horizontal (símbolo + "Clínica SemDor"), ou null se o recurso faltar.</summary>
    public static byte[]? Logo => _logo.Value;

    /// <summary>Somente o símbolo, ou null se o recurso faltar.</summary>
    public static byte[]? Simbolo => _simbolo.Value;

    private static byte[]? Carregar(string arquivo)
    {
        try
        {
            var asm = typeof(MarcaSemDor).Assembly;
            // Nome do recurso embarcado: <RootNamespace>.Recursos.Marca.<arquivo>
            var nome = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(".Marca." + arquivo, StringComparison.OrdinalIgnoreCase)
                                     || n.EndsWith("." + arquivo, StringComparison.OrdinalIgnoreCase));
            if (nome is null) return null;

            using var stream = asm.GetManifestResourceStream(nome);
            if (stream is null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
