using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Clinica.Application.Assinatura;

/// <summary>Importação temporária: nunca instala a chave, nem conserva a senha.</summary>
public static class CertificadoA1
{
    public const int LimiteBytes = 128 * 1024;
    public static CertificadoAssinatura Abrir(byte[] arquivo, string senha, string? cpf)
    {
        if (arquivo.Length is 0 or > LimiteBytes || string.IsNullOrEmpty(senha) || senha.Length > 200)
            throw new InvalidOperationException("Escolha um arquivo A1 de até 128 KB e informe sua senha.");
        X509Certificate2 certificado;
        try
        {
            if (X509Certificate2.GetCertContentType(arquivo) != X509ContentType.Pfx)
                throw new CryptographicException();
            certificado = new X509Certificate2(arquivo, senha, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException)
        { throw new InvalidOperationException("Não foi possível abrir o A1. Confira o arquivo e a senha."); }
        try
        {
            var abreSemSenha = false;
            try { using var semSenha = new X509Certificate2(arquivo, "", X509KeyStorageFlags.EphemeralKeySet); abreSemSenha = true; }
            catch (CryptographicException) { }
            if (abreSemSenha) throw new InvalidOperationException("O arquivo A1 precisa estar protegido por senha. Exporte uma cópia protegida antes de continuar.");
            var lido = CertificadoIcpBrasil.Ler(certificado);
            if (!CertificadoIcpBrasil.PodeAssinar(certificado) || !PoliticaA1(certificado)
                || certificado.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority)
                || !TemIdentificacaoPessoaFisica(certificado))
                throw new InvalidOperationException("Use um certificado e-CPF A1 ICP-Brasil com chave de assinatura.");
            if (!lido.Vigente) throw new InvalidOperationException("O certificado A1 está fora da validade. Cadastre um certificado vigente.");
            TitularDoCertificado.Exigir(lido, cpf, "profissional conectado");
            return lido;
        }
        catch { certificado.Dispose(); throw; }
    }

    private static bool TemIdentificacaoPessoaFisica(X509Certificate2 certificado)
    {
        try
        {
            return certificado.Extensions["2.5.29.17"] is { } san
                && CertificadoIcpBrasil.LerOtherName(san.RawData, CertificadoIcpBrasil.OidPessoaFisica) is not null;
        }
        catch (AsnContentException) { return false; }
    }

    private static bool PoliticaA1(X509Certificate2 certificado)
    {
        try
        {
            var extensao = certificado.Extensions["2.5.29.32"];
            if (extensao is null) return false;
            var leitor = new AsnReader(extensao.RawData, AsnEncodingRules.DER);
            var politicas = leitor.ReadSequence();
            while (politicas.HasData)
            {
                var oid = politicas.ReadSequence().ReadObjectIdentifier();
                if (oid.StartsWith("2.16.76.1.2.1.", StringComparison.Ordinal)
                    || oid.StartsWith("2.16.76.1.2.101.", StringComparison.Ordinal)) return true;
            }
        }
        catch (AsnContentException) { }
        return false;
    }
}

public interface IConfiancaCertificadoA1
{
    void Exigir(X509Certificate2 certificado);
}

/// <summary>Raiz oficial v5; outras raízes oficiais devem ser homologadas antes de ampliar a lista.</summary>
public sealed class ConfiancaCertificadoA1 : IConfiancaCertificadoA1
{
    public void Exigir(X509Certificate2 certificado)
    {
        using var cadeia = new X509Chain();
        cadeia.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        cadeia.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        cadeia.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10);
        if (!cadeia.Build(certificado) || cadeia.ChainElements.Count < 2
            || cadeia.ChainElements[^1].Certificate.GetCertHashString(HashAlgorithmName.SHA256)
                != "CAA53FC6091C6951887C976E378F6EF89AA6377C55D97B6475422B71ED7E9B17")
            throw new InvalidOperationException("Não foi possível confirmar a cadeia ICP-Brasil e a situação de revogação do A1. Confira a conexão e a cadeia instalada.");
    }
}
