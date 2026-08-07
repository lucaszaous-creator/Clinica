using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Clinica.Domain;

namespace Clinica.Application.Assinatura;

/// <summary>
/// Um certificado que pode assinar, já lido: quem é o titular, até quando vale, e — o que
/// nenhum outro campo dá — o <b>CPF que está DENTRO do certificado</b>.
/// </summary>
/// <param name="Certificado">O objeto do sistema, com a chave privada. Não é serializado.</param>
/// <param name="Cpf">Só dígitos, extraído da extensão ICP-Brasil. Null se não for um e-CPF.</param>
/// <param name="Provedor">
/// Nome do provedor de chave do Windows (CSP/KSP), quando dá para saber. É ele que
/// distingue um certificado em NUVEM de um que está mesmo na máquina — e é a única fonte
/// honesta disso: o emissor não serve, porque a mesma AC emite os dois.
/// </param>
public sealed record CertificadoAssinatura(
    X509Certificate2 Certificado,
    string Titular,
    string Emissor,
    string NumeroSerie,
    DateTime ValidoDe,
    DateTime ValidoAte,
    string? Cpf,
    string? Provedor = null)
{
    /// <summary>
    /// Quem faz a conta da assinatura quando a chave NÃO está nesta máquina — o SafeID
    /// (parcela 44). Nulo é o caminho de sempre: token ou arquivo, com a chave em mãos.
    ///
    /// Mora aqui, e não como parâmetro de quem manda assinar, porque é propriedade DESTE
    /// certificado: um certificado em nuvem não vira local dependendo de quem o usa. A
    /// alternativa era um parâmetro opcional atravessando dois serviços e quatro telas,
    /// quase sempre nulo — e bastaria uma delas esquecer de repassá-lo para a assinatura
    /// cair em silêncio no caminho local e morrer com "certificado sem chave privada", que
    /// é a mensagem que menos ajuda a achar o defeito.
    /// </summary>
    public PdfSharp.Pdf.Signatures.IDigitalSigner? AssinadorRemoto { get; init; }

    public bool Vigente => DateTime.Now >= ValidoDe && DateTime.Now <= ValidoAte;

    /// <summary>É um e-CPF ICP-Brasil (traz o CPF do titular dentro de si).</summary>
    public bool EhECpf => Cpf is not null;

    /// <summary>
    /// A chave vive em NUVEM (SafeID, Bird ID, VIDaaS) e a assinatura vai pedir
    /// autorização no celular do titular.
    ///
    /// Três estados, não dois: <c>true</c> é nuvem, <c>false</c> é máquina e
    /// <c>null</c> é <b>não sei</b> — o provedor não foi identificado (Linux, ou um
    /// CSP que não se apresenta). Chutar "máquina" faria a tela prometer uma assinatura
    /// instantânea que vai ficar meio minuto esperando um PIN no telefone.
    /// </summary>
    /// <remarks>
    /// <b>Duas coisas diferentes desembocam aqui</b>, e a fusão é deliberada (parcela 45).
    /// Um certificado é "em nuvem" quando assinamos por ele via API do PSC
    /// (<see cref="AssinadorRemoto"/>, parcela 44) <i>ou</i> quando a chave está num
    /// provedor de nuvem instalado nesta máquina (o driver da Safeweb, do Bird ID…). São
    /// caminhos técnicos distintos e a consequência para quem usa é a MESMA: a assinatura
    /// para e espera uma autorização no celular. Manter dois conceitos separados obrigaria
    /// cada tela a perguntar as duas coisas, e a que esquecesse metade deixaria a janela
    /// parecendo travada.
    /// </remarks>
    public bool? EmNuvem => AssinadorRemoto is not null
        ? true
        : Provedor is null
            ? null
            : ProvedoresEmNuvem.Any(p => Provedor.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>De onde ele veio, curto — o seletor escreve isto na linha.</summary>
    public string Procedencia => AssinadorRemoto is not null
        ? "SafeID — nuvem"
        : EmNuvem switch
        {
            true => "nuvem (nesta máquina)",
            false => "nesta máquina",
            null => "origem não identificada"
        };

    /// <summary>
    /// Pedaços de nome de provedor que denunciam chave em nuvem. Lista curta e explícita
    /// de propósito: o que não está aqui devolve "máquina", e errar para "máquina" é o
    /// erro barato (a tela deixa de avisar sobre o celular; não promete nada falso).
    /// </summary>
    private static readonly string[] ProvedoresEmNuvem =
        ["safeweb", "safeid", "bird", "soluti", "vidaas", "valid", "cloud", "nuvem", "remot"];

    /// <summary>Onde a chave está, na frase que a tela mostra.</summary>
    public string OndeEstaAChave => AssinadorRemoto is not null
        ? "Em nuvem pelo SafeID — a autorização vai para o celular do titular"
        : EmNuvem switch
        {
            true => "Em nuvem — a autorização vai para o celular do titular",
            false => "Nesta máquina (arquivo A1 ou token A3)",
            null => "Origem da chave não identificada"
        };

    /// <summary>Como o certificado aparece no seletor.</summary>
    public string Rotulo
    {
        get
        {
            var validade = $"vence em {ValidoAte:dd/MM/yyyy}";
            var documento = Cpf is null ? "sem CPF no certificado" : Domain.Cpf.Formatar(Cpf);
            return $"{Titular} — {documento} — {validade}";
        }
    }
}

/// <summary>Por que um certificado não serve. Vazio = serve.</summary>
public sealed record ImpedimentoCertificado(string Motivo);

/// <summary>
/// Leitura de certificado ICP-Brasil (parcela 42).
///
/// O que aqui existe e não existe em lugar nenhum do .NET
/// ------------------------------------------------------
/// O <b>CPF do titular</b>. Um e-CPF carrega o CPF numa extensão própria da ICP-Brasil
/// (OID <c>2.16.76.1.3.1</c>), dentro do <i>subjectAltName</i> como um <c>otherName</c> —
/// e o .NET não expõe <c>otherName</c> em nenhuma API pronta (a
/// <c>X509SubjectAlternativeNameExtension</c> só entrega DNS, IP e e-mail). Então se lê o
/// ASN.1 na mão.
///
/// Isso não é preciosismo: <b>é a metade que faz a assinatura qualificada valer alguma
/// coisa.</b> Sem comparar o CPF do certificado com o CPF do profissional que está
/// prescrevendo, o sistema provaria apenas que <i>alguém</i> com <i>algum</i> token
/// assinou — e o token da recepcionista assinaria a prescrição da médica sem um alerta.
///
/// O leiaute do campo (norma ICP-Brasil DOC-ICP-04) é uma cadeia de caracteres colada:
/// <c>DDMMAAAA</c> (nascimento) + <c>CPF</c> (11) + <c>NIS</c> (11) + <c>RG</c> (15) +
/// órgão expedidor (6). O que interessa aqui são os 11 do meio.
/// </summary>
public static class CertificadoIcpBrasil
{
    /// <summary>Pessoa física — o e-CPF. É o que um profissional de saúde usa para assinar.</summary>
    public const string OidPessoaFisica = "2.16.76.1.3.1";

    /// <summary>Responsável por pessoa jurídica (e-CNPJ). Também carrega o CPF de quem responde.</summary>
    public const string OidResponsavelPj = "2.16.76.1.3.4";

    private const string OidSubjectAltName = "2.5.29.17";

    /// <summary>
    /// Certificados de assinatura instalados para o usuário do Windows — inclui o A1
    /// (arquivo importado) e o A3 (token/cartão), porque o Windows expõe os dois pelo
    /// mesmo repositório.
    ///
    /// Em Linux o repositório vem vazio, e isso é correto: os testes assinam com um
    /// certificado construído na memória, e a suíte roda em Windows.
    /// </summary>
    public static IReadOnlyList<CertificadoAssinatura> DoRepositorioDoUsuario()
    {
        try
        {
            using var repositorio = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            repositorio.Open(OpenFlags.ReadOnly);

            return repositorio.Certificates
                .Where(PodeAssinar)
                .Select(Ler)
                .OrderByDescending(c => c.Vigente)
                .ThenBy(c => c.Titular)
                .ToList();
        }
        catch (Exception ex)
        {
            // Degradar é certo (a máquina pode não ter repositório), sumir não é: sem o
            // registro, "nenhum certificado encontrado" seria indistinguível de
            // "nenhum certificado instalado", e o profissional procuraria o token na
            // gaveta enquanto o problema é outro.
            Diagnostico.Registrar("CertificadoIcpBrasil.DoRepositorioDoUsuario", ex);
            return [];
        }
    }

    /// <summary>Certificado A1 vindo de arquivo <c>.pfx</c>/<c>.p12</c>.</summary>
    public static CertificadoAssinatura DeArquivo(string caminho, string senha)
    {
        var certificado = new X509Certificate2(
            caminho, senha,
            // Exportável não: a chave não precisa sair daqui. PersistKeySet também não —
            // importar um A1 para assinar não pode deixar a chave instalada na máquina
            // depois que o programa fecha.
            X509KeyStorageFlags.EphemeralKeySet);

        return Ler(certificado);
    }

    /// <summary>Serve para assinar: tem chave privada e o uso de chave permite assinatura.</summary>
    public static bool PodeAssinar(X509Certificate2 certificado)
    {
        if (!certificado.HasPrivateKey) return false;

        var uso = certificado.Extensions
            .OfType<X509KeyUsageExtension>()
            .FirstOrDefault();

        // Sem a extensão, o certificado não restringe o uso — não é motivo para excluí-lo.
        if (uso is null) return true;

        return uso.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)
            || uso.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);
    }

    /// <summary>Lê os campos que interessam de um certificado já carregado.</summary>
    public static CertificadoAssinatura Ler(X509Certificate2 certificado)
        => new(
            certificado,
            Titular: NomeSimples(certificado),
            Emissor: certificado.GetNameInfo(X509NameType.SimpleName, forIssuer: true),
            NumeroSerie: certificado.SerialNumber,
            ValidoDe: certificado.NotBefore,
            ValidoAte: certificado.NotAfter,
            Cpf: CpfDoTitular(certificado),
            Provedor: ProvedorDaChave(certificado));

    /// <summary>
    /// O nome do provedor de chave (CSP/KSP) do Windows, quando existe.
    ///
    /// É assim que o sistema sabe que a chave está em NUVEM: o SafeID Desktop, o Bird ID e
    /// o VIDaaS instalam um provedor próprio e o certificado aparece no repositório do
    /// Windows apontando para ele. Ler o EMISSOR não resolveria — a mesma autoridade emite
    /// o certificado em nuvem e o de token.
    ///
    /// Devolve <c>null</c> quando não dá para saber (Linux, provedor que não se apresenta,
    /// ou qualquer falha): a tela então diz "não identificada" em vez de inventar.
    /// Obter o handle da chave NÃO dispara pedido de autorização no celular — isso só
    /// acontece na hora de assinar.
    /// </summary>
    public static string? ProvedorDaChave(X509Certificate2 certificado)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            using var chave = certificado.GetRSAPrivateKey();

            return chave switch
            {
                System.Security.Cryptography.RSACng cng => cng.Key.Provider?.Provider,
                System.Security.Cryptography.RSACryptoServiceProvider csp
                    => csp.CspKeyContainerInfo.ProviderName,
                _ => null
            };
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("CertificadoIcpBrasil.ProvedorDaChave", ex);
            return null;
        }
    }

    /// <summary>
    /// O CPF gravado no certificado, só dígitos, ou <c>null</c> quando não é um e-CPF.
    ///
    /// Tenta o OID de pessoa física e, na falta, o de responsável por PJ — uma clínica que
    /// compra e-CNPJ para a titular ainda assim tem o CPF dela lá dentro, e recusar esse
    /// caso obrigaria a comprar um segundo certificado sem nenhum ganho de garantia.
    /// </summary>
    public static string? CpfDoTitular(X509Certificate2 certificado)
    {
        var extensao = certificado.Extensions[OidSubjectAltName];
        if (extensao is null) return null;

        try
        {
            foreach (var oid in new[] { OidPessoaFisica, OidResponsavelPj })
            {
                var campo = LerOtherName(extensao.RawData, oid);
                if (campo is null || campo.Length < 19) continue;

                // DDMMAAAA(8) + CPF(11) + ...
                var cpf = Domain.Cpf.Normalizar(campo.Substring(8, 11));
                if (Domain.Cpf.Valido(cpf)) return cpf;
            }
        }
        catch (Exception ex)
        {
            // ASN.1 de emissor exótico não pode derrubar o seletor de certificados. Fica
            // sem CPF — e ficar sem CPF tem consequência VISÍVEL (a conferência recusa a
            // assinatura), então o silêncio aqui não vira aprovação lá na frente.
            Diagnostico.Registrar("CertificadoIcpBrasil.CpfDoTitular", ex);
        }

        return null;
    }

    /// <summary>
    /// Confere se o certificado encadeia até uma autoridade em que a máquina confia.
    ///
    /// Vale a pena entender o que isto pega: uma assinatura feita com certificado cuja
    /// cadeia não valida <b>abre como inválida em qualquer leitor de PDF</b>. Sem esta
    /// checagem, o profissional assinaria tranquilo e só descobriria numa auditoria, com
    /// meses de folhas assinadas para refazer — que é o pior momento possível.
    ///
    /// Em máquina que ainda não tem a cadeia da ICP-Brasil instalada, a resposta traz o
    /// motivo do próprio Windows, que é o que diz à clínica o que baixar.
    /// </summary>
    public static ImpedimentoCertificado? ConferirCadeia(X509Certificate2 certificado)
    {
        try
        {
            using var cadeia = new X509Chain();
            cadeia.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            cadeia.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            if (cadeia.Build(certificado)) return null;

            var motivos = cadeia.ChainStatus
                .Where(s => s.Status != X509ChainStatusFlags.NoError)
                .Select(s => s.StatusInformation.Trim())
                .Where(m => m.Length > 0)
                .Distinct()
                .ToList();

            return new ImpedimentoCertificado(motivos.Count > 0
                ? string.Join("; ", motivos)
                : "a cadeia do certificado não pôde ser validada nesta máquina");
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("CertificadoIcpBrasil.ConferirCadeia", ex);
            return new ImpedimentoCertificado(
                "não foi possível validar a cadeia do certificado: " + ex.Message);
        }
    }

    /// <summary>Nome do titular sem o ruído do DN (e sem o ":CPF" que algumas ACs colam no CN).</summary>
    private static string NomeSimples(X509Certificate2 certificado)
    {
        var nome = certificado.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (string.IsNullOrWhiteSpace(nome)) return certificado.Subject;

        // Várias ACs emitem CN = "FULANO DE TAL:12345678909". O número não é nome.
        var doisPontos = nome.IndexOf(':');
        return doisPontos > 0 ? nome[..doisPontos].Trim() : nome.Trim();
    }

    /// <summary>
    /// Percorre o <c>GeneralNames</c> do subjectAltName procurando o <c>otherName</c> do
    /// OID pedido, e devolve o conteúdo como texto.
    ///
    /// <code>
    /// GeneralNames ::= SEQUENCE OF GeneralName
    /// GeneralName  ::= CHOICE { otherName [0] IMPLICIT OtherName, ... }
    /// OtherName    ::= SEQUENCE { type-id OBJECT IDENTIFIER, value [0] EXPLICIT ANY }
    /// </code>
    ///
    /// O conteúdo vem como OCTET STRING na maioria das ACs, mas há emissor que usa
    /// PrintableString ou UTF8String — aceitar os três custa três linhas e evita que o
    /// sistema recuse o certificado de uma AC inteira.
    /// </summary>
    private static string? LerOtherName(byte[] extensaoSan, string oidAlvo)
    {
        var nomes = new AsnReader(extensaoSan, AsnEncodingRules.DER).ReadSequence();

        while (nomes.HasData)
        {
            var etiqueta = nomes.PeekTag();
            if (etiqueta.TagClass != TagClass.ContextSpecific || etiqueta.TagValue != 0)
            {
                nomes.ReadEncodedValue();   // é dNSName, rfc822Name…: não interessa
                continue;
            }

            var outroNome = nomes.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0));
            var oid = outroNome.ReadObjectIdentifier();
            var valor = outroNome.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0));

            if (oid != oidAlvo) continue;

            return valor.PeekTag().TagValue switch
            {
                (int)UniversalTagNumber.OctetString =>
                    Encoding.ASCII.GetString(valor.ReadOctetString()),
                (int)UniversalTagNumber.PrintableString =>
                    valor.ReadCharacterString(UniversalTagNumber.PrintableString),
                (int)UniversalTagNumber.UTF8String =>
                    valor.ReadCharacterString(UniversalTagNumber.UTF8String),
                (int)UniversalTagNumber.IA5String =>
                    valor.ReadCharacterString(UniversalTagNumber.IA5String),
                _ => null
            };
        }

        return null;
    }
}
