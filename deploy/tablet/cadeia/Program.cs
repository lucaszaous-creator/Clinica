using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

if (args.Length != 2 || args[0] is not ("validar" or "instalar" or "verificar"))
    throw new InvalidOperationException("Informe validar, instalar ou verificar e a pasta dos certificados publicos.");

// Certificados públicos obtidos por HTTPS dos repositórios oficiais do ITI e Safeweb.
// A raiz também coincide com a já confiada pelo Windows usado na clínica.
var arquivos = new[] {
    (Nome: "icp-brasil-v5.cer", Hash: "CAA53FC6091C6951887C976E378F6EF89AA6377C55D97B6475422B71ED7E9B17"),
    (Nome: "ac-rfb-v4.cer", Hash: "2C7A8AD75458F7E2FE8D7F7884A585127E4F242BD5C3EBEA499DACB630315E21"),
    (Nome: "ac-safeweb-rfb-v5.cer", Hash: "7B3A4BE434DCCFB19009D4835104A052A8AB64850145A48C625784E821E053EA")
};
var certificados = arquivos.Select(a => {
    var bytes = File.ReadAllBytes(Path.Combine(args[1], a.Nome));
    if (Convert.ToHexString(SHA256.HashData(bytes)) != a.Hash)
        throw new InvalidOperationException("Certificado publico diferente do pacote revisado.");
    var c = new X509Certificate2(bytes);
    if (c.HasPrivateKey || c.NotAfter.ToUniversalTime() <= DateTime.UtcNow
        || c.NotBefore.ToUniversalTime() > DateTime.UtcNow
        || !c.Extensions.OfType<X509BasicConstraintsExtension>().Any(x => x.CertificateAuthority))
        throw new InvalidOperationException("O pacote deve conter apenas autoridades publicas vigentes.");
    return c;
}).ToArray();

foreach (var certificado in certificados)
{
    using var cadeia = new X509Chain();
    cadeia.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    cadeia.ChainPolicy.CustomTrustStore.Add(certificados[0]);
    cadeia.ChainPolicy.ExtraStore.AddRange(certificados[1..]);
    cadeia.ChainPolicy.DisableCertificateDownloads = true;
    cadeia.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    cadeia.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
    if (!cadeia.Build(certificado)) throw new InvalidOperationException("Cadeia oficial nao conferiu criptograficamente.");
}
if (args[0] == "validar") { Console.WriteLine("PACOTE_OFICIAL_VALIDADO"); return; }
if (!OperatingSystem.IsLinux() || Environment.UserName is not ("clinica-posto-hml" or "clinica-tablet"))
    throw new InvalidOperationException("Execute somente como usuario dedicado do portal na VPS.");

var adicionados = new List<(StoreName Nome, X509Certificate2 Certificado)>();
try
{
    if (args[0] == "instalar")
        for (var i = 0; i < certificados.Length; i++)
        {
            var nome = i == 0 ? StoreName.Root : StoreName.CertificateAuthority;
            using var store = new X509Store(nome, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            if (store.Certificates.Find(X509FindType.FindByThumbprint, certificados[i].Thumbprint, false).Count == 0)
            {
                store.Add(certificados[i]); adicionados.Add((nome, certificados[i]));
            }
        }
    // Mesmo mecanismo padrão do assinador clínico, sem raiz customizada ou ExtraStore.
    using var conferir = new X509Chain();
    conferir.ChainPolicy.DisableCertificateDownloads = true;
    conferir.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
    conferir.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
    if (!conferir.Build(certificados[^1]))
        throw new InvalidOperationException("Repositorio do servico ainda nao reconhece a cadeia.");
    Console.WriteLine("CADEIA_RECONHECIDA_PELO_SERVICO; perfil=" + Environment.UserName
        + "; certificados_adicionados=" + adicionados.Count);
}
catch
{
    foreach (var adicionado in adicionados)
    {
        using var store = new X509Store(adicionado.Nome, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite); store.Remove(adicionado.Certificado);
    }
    throw;
}
