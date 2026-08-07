using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Clinica.Application.Assinatura;
using Clinica.Application.Assinatura.SafeID;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A integração com o PSC Safeweb (SafeID) — o certificado em nuvem da médica.
///
/// O que estes testes protegem, e por que não é cerimônia
/// -----------------------------------------------------
/// Nenhum deles fala com a Safeweb: o que se está testando é a MONTAGEM da requisição e a
/// LEITURA da resposta, contra um <c>HttpMessageHandler</c> de mentira. Isso cobre a classe
/// de erro que mais custa aqui — mandar o campo errado e descobrir na homologação, ou pior,
/// mandar o campo CERTO com o conteúdo errado e assinar coisa nenhuma.
///
/// O teste mais importante do arquivo é <see cref="Somente_o_hash_sobe_para_o_PSC"/>. A API
/// tem três modos de assinar e dois deles sobem o PDF inteiro; escolhemos o que sobe 32
/// bytes. Essa escolha é fácil de desfazer sem querer numa refatoração, e o dia em que ela
/// se desfizer o prontuário do paciente passa a sair da clínica sem ninguém notar — não há
/// erro de compilação nem tela vermelha para avisar.
/// </summary>
public class SafeIDTests
{
    // ---- Configuração vinda do ambiente ----
    //
    // O teste que importa aqui é o da AUSÊNCIA. `AddClinica` é chamado pelo faturamento
    // CONGELADO, que não assina nada: se a leitura da configuração lançasse por falta de
    // variável de ambiente, a abertura de um app em produção quebraria por causa de uma
    // funcionalidade que ele nunca usou.

    [Fact]
    public void ConfiguracaoAusenteNaoLancaEDevolveNulo()
    {
        Assert.Null(ConfiguracaoSafeID.DoAmbiente(_ => null));
    }

    [Theory]
    [InlineData("id", null)]
    [InlineData(null, "segredo")]
    [InlineData("id", "   ")]
    public void MeiaConfiguracaoEhTratadaComoAusente(string? id, string? segredo)
    {
        // Meia configuração é pior que nenhuma: a tela apareceria e falharia no clique.
        var lido = ConfiguracaoSafeID.DoAmbiente(chave => chave switch
        {
            ConfiguracaoSafeID.ChaveClientId => id,
            ConfiguracaoSafeID.ChaveClientSecret => segredo,
            _ => null
        });

        Assert.Null(lido);
    }

    [Fact]
    public void AmbientePadraoEhProducao()
    {
        // Apontar para homologação sem perceber faria meses de documentos serem assinados
        // com certificado de teste. O engano inverso falha no primeiro clique.
        var lido = Configuracao(ambiente: null);

        Assert.NotNull(lido);
        Assert.Equal(OpcoesSafeID.BasePadrao, lido!.Base);
    }

    [Theory]
    [InlineData("homologacao")]
    [InlineData("HOMOLOGAÇÃO")]
    [InlineData("hml")]
    public void HomologacaoSoQuandoPedidaPorExtenso(string ambiente)
    {
        Assert.Equal(OpcoesSafeID.BaseHomologacao, Configuracao(ambiente)!.Base);
    }

    [Fact]
    public void UriDeRetornoEhLidaLiteralmente()
    {
        // O redirect_uri é comparado como TEXTO EXATO pelo OAuth. Normalizar (tirar barra
        // do fim, baixar caixa) produziria recusa do PSC com mensagem que não explica.
        const string comBarra = "http://127.0.0.1:8123/safeid/retorno/";

        var lido = ConfiguracaoSafeID.DoAmbiente(chave => chave switch
        {
            ConfiguracaoSafeID.ChaveClientId => "id",
            ConfiguracaoSafeID.ChaveClientSecret => "segredo",
            ConfiguracaoSafeID.ChaveRedirectUri => comBarra,
            _ => null
        });

        Assert.Equal(comBarra, lido!.RedirectUri!.ToString());
    }

    private static OpcoesSafeID? Configuracao(string? ambiente) =>
        ConfiguracaoSafeID.DoAmbiente(chave => chave switch
        {
            ConfiguracaoSafeID.ChaveClientId => "id",
            ConfiguracaoSafeID.ChaveClientSecret => "segredo",
            ConfiguracaoSafeID.ChaveAmbiente => ambiente,
            _ => null
        });

    private static readonly OpcoesSafeID Opcoes = new(
        ClientId: "clinica-teste",
        ClientSecret: "segredo",
        RedirectUri: new Uri("http://127.0.0.1:8123/retorno"));

    // ---- Assinatura ----

    [Fact]
    public async Task Assina_hash_manda_sha256_em_base64_e_pede_CMS()
    {
        var pkcs7 = new byte[] { 1, 2, 3, 4, 5 };
        var handler = HandlerQueResponde(Envelope(pkcs7, "req-1"));
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var hash = SHA256.HashData("conteúdo da folha"u8.ToArray());
        var voltou = await cliente.AssinarHashAsync("token-1", hash, "req-1", "Prescrição 2026/0001");

        Assert.Equal(pkcs7, voltou);

        var enviado = JsonDocument.Parse(handler.Corpos.Single()).RootElement;
        var item = enviado.GetProperty("hashes")[0];

        Assert.Equal(Convert.ToBase64String(hash), item.GetProperty("hash").GetString());
        Assert.Equal(ClienteSafeID.OidSha256, item.GetProperty("hash_algorithm").GetString());
        Assert.Equal("CMS", item.GetProperty("signature_format").GetString());
        Assert.Equal("Prescrição 2026/0001", item.GetProperty("alias").GetString());
    }

    /// <summary>
    /// A regra de proteção de dados desta integração, escrita como teste: o documento NÃO
    /// sai da clínica. Sobe o hash e nada mais — nem o PDF, nem nome, nem CID.
    /// </summary>
    [Fact]
    public async Task Somente_o_hash_sobe_para_o_PSC()
    {
        var handler = HandlerQueResponde(Envelope([9, 9], "req-1"));
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var pdf = Encoding.UTF8.GetBytes(
            "%PDF-1.7 Maria da Silva CPF 12345678909 CID F41.1 Sertralina 50mg");

        await cliente.AssinarHashAsync("token-1", SHA256.HashData(pdf), "req-1", "Receita");

        var corpo = handler.Corpos.Single();

        Assert.DoesNotContain("Maria da Silva", corpo);
        Assert.DoesNotContain("F41.1", corpo);
        Assert.DoesNotContain("Sertralina", corpo);
        Assert.DoesNotContain(Convert.ToBase64String(pdf), corpo);

        // E o endpoint é o do hash, nunca o que recebe documento.
        Assert.EndsWith("/oauth/signature", handler.Urls.Single());
    }

    [Fact]
    public async Task Resposta_sem_assinatura_nao_passa_por_sucesso()
    {
        var handler = HandlerQueResponde("""{"certificate_alias":"x","signatures":[]}""");
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));

        Assert.Contains("NÃO foi assinada", erro.Message);
    }

    [Fact]
    public async Task Erro_do_PSC_vira_mensagem_que_diz_o_que_aconteceu()
    {
        var handler = HandlerQueResponde(
            """{"error":"invalid_token"}""", HttpStatusCode.Unauthorized);
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));

        // O corpo entra na mensagem: "401" sozinho mandaria a médica reinstalar o app à toa.
        Assert.Contains("401", erro.Message);
        Assert.Contains("invalid_token", erro.Message);
    }

    // ---- Certificado ----

    /// <summary>
    /// A regra que faz a assinatura qualificada valer atravessa intacta para a nuvem: o CPF
    /// sai de DENTRO do certificado (OID 2.16.76.1.3.1), não do que o servidor afirma ao lado.
    /// </summary>
    [Fact]
    public async Task Certificado_do_PSC_entrega_o_CPF_de_dentro_do_certificado()
    {
        var pem = PemDeTeste("Dra. Ana Souza", "12345678909");
        var handler = HandlerQueResponde($$"""
            {"status":"S","certificates":[{"alias":"A3 PESSOAL:123","certificate":{{JsonSerializer.Serialize(pem)}}}]}
            """);

        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);
        var certificados = await cliente.CertificadosAsync("token-1");

        var unico = Assert.Single(certificados);
        Assert.Equal("A3 PESSOAL:123", unico.Alias);
        Assert.Equal("12345678909", unico.Certificado.Cpf);
        Assert.Contains("Ana Souza", unico.Certificado.Titular);

        // E ele serve para a regra de titularidade, que é o ponto de tudo isto.
        TitularDoCertificado.Exigir(unico.Certificado, "123.456.789-09", "Dra. Ana Souza");
    }

    [Fact]
    public async Task Titular_sem_certificado_no_PSC_e_dito_em_vez_de_lista_vazia()
    {
        var handler = HandlerQueResponde("""{"status":"N","certificates":[]}""");
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.CertificadosAsync("token-1"));

        Assert.Contains("aplicativo SafeID", erro.Message);
    }

    // ---- PKCE ----

    [Fact]
    public void Desafio_pkce_respeita_a_rfc_7636()
    {
        var desafio = DesafioPkce.Gerar();

        Assert.InRange(desafio.Verificador.Length, 43, 128);
        Assert.DoesNotContain('=', desafio.Verificador);
        Assert.DoesNotContain('+', desafio.Verificador);
        Assert.DoesNotContain('/', desafio.Verificador);

        // O desafio é o S256 do verificador — é isso que o PSC recalcula do outro lado.
        var esperado = Convert.ToBase64String(
                SHA256.HashData(Encoding.ASCII.GetBytes(desafio.Verificador)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Equal(esperado, desafio.Desafio);
        Assert.NotEqual(DesafioPkce.Gerar().Verificador, desafio.Verificador);
    }

    [Fact]
    public void Url_de_autorizacao_leva_o_desafio_e_o_escopo_de_sessao()
    {
        var cliente = new ClienteSafeID(new HttpClient(HandlerQueResponde("{}")), Opcoes);
        var desafio = DesafioPkce.Gerar();

        var url = cliente.UrlDeAutorizacao(desafio, cpf: "12345678909", estado: "abc").ToString();

        Assert.Contains("response_type=code", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("code_challenge=" + Uri.EscapeDataString(desafio.Desafio), url);
        Assert.Contains("scope=signature_session", url);
        Assert.Contains("login_hint=12345678909", url);

        // O verificador é segredo: ele fica nesta máquina até a troca do código.
        Assert.DoesNotContain(desafio.Verificador, url);
    }

    // ---- O assinador que o PDFsharp usa ----

    [Fact]
    public async Task Assinador_calcula_o_hash_do_conteudo_coberto()
    {
        var pkcs7 = new byte[] { 7, 7, 7 };
        var handler = HandlerQueResponde(Envelope(pkcs7, id: null));
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var certificado = new CertificadoDeNuvem(
            "A3:1", CertificadoIcpBrasil.Ler(
                X509Certificate2.CreateFromPem(PemDeTeste("Dra. Ana Souza", "12345678909"))));

        var assinador = new AssinadorSafeID(cliente, "token-1", certificado, "Receita 2026/0001");

        var conteudo = "os bytes cobertos pelo ByteRange"u8.ToArray();
        var voltou = await assinador.GetSignatureAsync(new MemoryStream(conteudo));

        Assert.Equal(pkcs7, voltou);
        Assert.Equal("Dra. Ana Souza", assinador.CertificateName);

        var enviado = JsonDocument.Parse(handler.Corpos.Single()).RootElement;
        Assert.Equal(
            Convert.ToBase64String(SHA256.HashData(conteudo)),
            enviado.GetProperty("hashes")[0].GetProperty("hash").GetString());
        Assert.Equal("A3:1", enviado.GetProperty("certificate_alias").GetString());
    }

    /// <summary>
    /// O PDFsharp reserva o espaço do <c>/Contents</c> ANTES de assinar. Se o PKCS#7 voltar
    /// maior, o encaixe falharia — e falhar dizendo o quê é a diferença entre corrigir uma
    /// constante e caçar um PDF corrompido.
    /// </summary>
    [Fact]
    public async Task Assinatura_maior_que_o_espaco_reservado_e_recusada_com_o_numero()
    {
        var enorme = new byte[AssinadorSafeID.TamanhoReservado + 1];
        var handler = HandlerQueResponde(Envelope(enorme, id: null));
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var certificado = new CertificadoDeNuvem(
            string.Empty, CertificadoIcpBrasil.Ler(
                X509Certificate2.CreateFromPem(PemDeTeste("Dra. Ana Souza", "12345678909"))));

        var assinador = new AssinadorSafeID(cliente, "t", certificado, "Receita");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => assinador.GetSignatureAsync(new MemoryStream([1, 2, 3])));

        Assert.Contains("NÃO foi assinada", erro.Message);
        Assert.Contains(AssinadorSafeID.TamanhoReservado.ToString(), erro.Message);
    }

    [Fact]
    public async Task Token_da_aplicacao_vai_como_formulario_e_traz_a_validade()
    {
        var handler = HandlerQueResponde(
            """{"access_token":"abc","expires_in":7200,"token_type":"Bearer"}""");
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var token = await cliente.TokenDaAplicacaoAsync();

        Assert.Equal("abc", token.AccessToken);
        Assert.True(token.Vigente);
        Assert.Contains("grant_type=client_credentials", handler.Corpos.Single());
        Assert.Contains("client_id=clinica-teste", handler.Corpos.Single());
    }

    // ---- Apoio ----

    private static string Envelope(byte[] pkcs7, string? id)
        => $$"""
            {"certificate_alias":"A3:1","signatures":[
              {"id":{{JsonSerializer.Serialize(id ?? "qualquer")}},
               "raw_signature":"{{Convert.ToBase64String(pkcs7)}}"}]}
            """;

    private static HandlerFalso HandlerQueResponde(
        string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(json, status);

    /// <summary>Grava o que foi enviado e devolve sempre a mesma resposta.</summary>
    private sealed class HandlerFalso(string json, HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Corpos { get; } = [];
        public List<string> Urls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage requisicao, CancellationToken ct)
        {
            Urls.Add(requisicao.RequestUri!.AbsoluteUri);
            Corpos.Add(requisicao.Content is null
                ? string.Empty
                : await requisicao.Content.ReadAsStringAsync(ct));

            return new HttpResponseMessage(status) { Content = new StringContent(json) };
        }
    }

    /// <summary>Um e-CPF de mentira em PEM, como o <c>certificate-discovery</c> devolve.</summary>
    private static string PemDeTeste(string nome, string cpf)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var conteudo = Encoding.ASCII.GetBytes(
            "14031978" + cpf + "12345678901" + "123456".PadLeft(15, '0') + "SSPSP ");

        var escritor = new AsnWriter(AsnEncodingRules.DER);
        using (escritor.PushSequence())
        using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            escritor.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                escritor.WriteOctetString(conteudo);
        }

        pedido.CertificateExtensions.Add(
            new X509Extension("2.5.29.17", escritor.Encode(), critical: false));

        var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        return new string(PemEncoding.Write("CERTIFICATE", certificado.RawData));
    }
}
