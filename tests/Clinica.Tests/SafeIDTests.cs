using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
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

        Assert.Equal(comBarra, lido!.RedirectUris!.Single().ToString());
    }

    [Fact]
    public void VariasUrisDeRetornoNaOrdemCadastrada()
    {
        // As três portas registradas no portal: a queda para a seguinte é o que impede uma
        // porta ocupada de travar a assinatura com o paciente na sala.
        var lido = ConfiguracaoSafeID.DoAmbiente(chave => chave switch
        {
            ConfiguracaoSafeID.ChaveClientId => "id",
            ConfiguracaoSafeID.ChaveClientSecret => "segredo",
            ConfiguracaoSafeID.ChaveRedirectUri =>
                "http://127.0.0.1:8123/safeid/retorno; http://127.0.0.1:8124/safeid/retorno",
            _ => null
        });

        Assert.Collection(lido!.RedirectUris!,
            primeira => Assert.Equal(8123, primeira.Port),
            segunda => Assert.Equal(8124, segunda.Port));
    }

    [Fact]
    public void EscutaRecusaListaVazia()
    {
        // Sem URL configurada a mensagem tem de dizer ONDE configurar — "nenhuma porta
        // livre" mandaria procurar o problema no lugar errado.
        var erro = Assert.Throws<InvalidOperationException>(() => EscutaLoopback.Abrir([]));
        Assert.Contains(ConfiguracaoSafeID.ChaveRedirectUri, erro.Message);
    }

    [Fact]
    public void EscutaCaiParaAPortaSeguinteQuandoAPrimeiraEstaOcupada()
    {
        using var primeira = EscutaLoopback.Abrir([Loopback(18123), Loopback(18124)]);

        // A segunda escuta encontra 18123 ocupada pela primeira e assume 18124 sozinha.
        using var segunda = EscutaLoopback.Abrir([Loopback(18123), Loopback(18124)]);

        Assert.Equal(18123, primeira.Endereco.Port);
        Assert.Equal(18124, segunda.Endereco.Port);
    }

    private static Uri Loopback(int porta) => new($"http://127.0.0.1:{porta}/safeid/retorno");

    [Fact]
    public void SemConfiguracaoDeRetornoUsaOCadastroPadrao()
    {
        // As URIs são do cadastro da aplicação no PSC, não escolha de quem instala: sem nada
        // configurado a clínica tem de sair funcionando, senão a instalação numa máquina nova
        // volta a depender de ritual.
        var opcoes = new OpcoesSafeID("id", "segredo");

        Assert.Equal(OpcoesSafeID.RetornosPadrao, opcoes.Retornos);
        Assert.All(opcoes.Retornos, uri => Assert.True(uri.IsLoopback));
    }

    [Fact]
    public void RetornoConfiguradoVenceOPadrao()
    {
        var opcoes = new OpcoesSafeID("id", "segredo", RedirectUris: [Loopback(9999)]);

        Assert.Equal(9999, opcoes.Retornos.Single().Port);
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
        RedirectUris: [new Uri("http://127.0.0.1:8123/retorno")]);

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

    // ---- O "base64" do PSC, e as formas em que ele chega ----
    //
    // O cliente da clínica levou "O SafeID devolveu uma assinatura que não pôde ser
    // decodificada" no primeiro documento assinado em nuvem. `Convert.FromBase64String` é
    // estrito exatamente onde um serviço REST costuma ser frouxo — alfabeto, enchimento e
    // armadura —, e as três falham com a MESMA exceção, sem dizer qual foi. Estes testes
    // fixam as três formas como aceitas, e a quarta (lixo de verdade) como recusada COM a
    // evidência: sem ela a próxima ocorrência recomeça do zero.

    [Fact]
    public async Task Assinatura_em_base64url_e_aceita()
    {
        // É a forma que o resto desta integração já usa — o PKCE desta mesma classe a produz.
        var pkcs7 = new byte[] { 0xFB, 0xFF, 0xBF, 0x01, 0x02 };
        var base64url = Convert.ToBase64String(pkcs7)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // O teste só vale se o valor de fato tiver os caracteres que o padrão recusa.
        Assert.True(base64url.Contains('-') || base64url.Contains('_'));

        var cliente = new ClienteSafeID(
            new HttpClient(HandlerQueResponde(EnvelopeBruto(base64url))), Opcoes);

        Assert.Equal(pkcs7, await cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));
    }

    [Fact]
    public async Task Assinatura_sem_o_enchimento_do_fim_e_aceita()
    {
        // Dois bytes pedem "==" no fim. Sem eles o comprimento não fecha em múltiplo de 4 e
        // o .NET recusa um valor que é perfeitamente decodificável.
        var pkcs7 = new byte[] { 0x30, 0x82 };
        var semEnchimento = Convert.ToBase64String(pkcs7).TrimEnd('=');

        var cliente = new ClienteSafeID(
            new HttpClient(HandlerQueResponde(EnvelopeBruto(semEnchimento))), Opcoes);

        Assert.Equal(pkcs7, await cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));
    }

    [Fact]
    public async Task Assinatura_com_armadura_PEM_e_aceita()
    {
        var pkcs7 = new byte[] { 0x30, 0x82, 0x04, 0x11 };
        var armada = "-----BEGIN PKCS7-----\n"
            + Convert.ToBase64String(pkcs7) + "\n-----END PKCS7-----";

        var cliente = new ClienteSafeID(
            new HttpClient(HandlerQueResponde(EnvelopeBruto(armada))), Opcoes);

        Assert.Equal(pkcs7, await cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));
    }

    /// <summary>
    /// O hexa tem de ser tentado ANTES do base64: o alfabeto dele cabe dentro do do base64,
    /// então <c>FromBase64String</c> aceitaria este mesmo valor e devolveria lixo — que
    /// entraria calado no <c>/Contents</c> do PDF. Este teste falha se alguém inverter a
    /// ordem, porque aí voltariam bytes diferentes em vez de uma exceção.
    /// </summary>
    [Fact]
    public async Task Assinatura_em_hexadecimal_e_aceita()
    {
        // Começa em 0x30 — o SEQUENCE que abre todo DER, e é isso que o desempata do base64.
        var pkcs7 = new byte[] { 0x30, 0x82, 0x04, 0x11, 0xAB };

        var cliente = new ClienteSafeID(
            new HttpClient(HandlerQueResponde(EnvelopeBruto(Convert.ToHexString(pkcs7)))), Opcoes);

        Assert.Equal(pkcs7, await cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));
    }

    /// <summary>
    /// O que não é assinatura continua sendo recusado — e a mensagem NOMEIA o caractere que
    /// ofendeu. A frase do .NET ("contains a non-base 64 character") não diz qual é, e foi
    /// por isso que a primeira ocorrência na clínica não deu para diagnosticar pelo log.
    /// </summary>
    [Fact]
    public async Task Assinatura_ilegivel_e_recusada_dizendo_o_que_veio_no_lugar()
    {
        var cliente = new ClienteSafeID(
            new HttpClient(HandlerQueResponde(EnvelopeBruto("erro: certificado inválido!"))),
            Opcoes);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));

        Assert.Contains("NÃO foi assinada", erro.Message);
        Assert.Contains("erro: certificado", erro.Message);   // o começo do que veio
        Assert.Contains("!", erro.Message);                   // e o caractere que ofendeu
    }

    /// <summary>
    /// O PSC responde 200 com o motivo no corpo em alguns casos. "Não devolveu a assinatura"
    /// sozinho manda a clínica procurar defeito no celular da médica.
    /// </summary>
    [Fact]
    public async Task Resposta_sem_assinatura_leva_junto_o_que_o_PSC_disse()
    {
        var handler = HandlerQueResponde(
            """{"status":"N","message":"Certificado revogado","signatures":[]}""");

        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.AssinarHashAsync("t", new byte[32], "req-1", "Receita"));

        Assert.Contains("Certificado revogado", erro.Message);
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

        var url = cliente
            .UrlDeAutorizacao(desafio, Opcoes.RedirectUris!.Single(), cpf: "12345678909", estado: "abc")
            .ToString();

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
        // CMS de verdade: o assinador confere o que voltou antes de embutir no PDF.
        var pkcs7 = Pkcs7DeTeste();
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

    /// <summary>
    /// A segunda rodada de diagnóstico às cegas, virada em teste.
    ///
    /// Decodificar o <c>raw_signature</c> prova que veio base64 — não prova que veio uma
    /// ASSINATURA. Sem esta conferência, bytes que não são PKCS#7 iam calados para o
    /// <c>/Contents</c> do PDF e o erro só aparecia na conferência, como "ASN1 corrupted
    /// data": uma frase que não distingue "o PSC devolveu outra coisa" de "o arquivo foi
    /// adulterado" nem de "nós lemos errado de volta".
    /// </summary>
    [Fact]
    public async Task Resposta_que_decodifica_mas_nao_e_PKCS7_e_recusada_ANTES_de_virar_PDF()
    {
        // Base64 impecável de bytes que não são um CMS — o caso que passava batido.
        var handler = HandlerQueResponde(Envelope("nao sou um pkcs7"u8.ToArray(), id: null));
        var cliente = new ClienteSafeID(new HttpClient(handler), Opcoes);

        var certificado = new CertificadoDeNuvem(
            "A3:1", CertificadoIcpBrasil.Ler(
                X509Certificate2.CreateFromPem(PemDeTeste("Dra. Ana Souza", "12345678909"))));

        var assinador = new AssinadorSafeID(cliente, "t", certificado, "Receita");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => assinador.GetSignatureAsync(new MemoryStream([1, 2, 3])));

        Assert.Contains("não são um PKCS#7", erro.Message);
        Assert.Contains("NÃO foi assinada", erro.Message);

        // E diz com o que os bytes se parecem: um CMS abre em 30 82, e é o começo que
        // nomeia a causa quando o PSC responde noutro formato.
        Assert.Contains(Convert.ToHexString("nao sou "u8.ToArray()), erro.Message);
    }

    /// <summary>
    /// ⚠️ O SafeID devolve o CMS em BER com comprimento INDEFINIDO (<c>30 80 … 00 00</c>), e
    /// <b>PDF assinado exige DER</b> (ISO 32000-1 / PAdES). Aceitar o BER só no nosso
    /// <c>Conferir</c> produziria um arquivo que a casa aprova e que o Adobe e o validador do
    /// ITI podem recusar — e quem descobre isso é o farmacêutico, com a receita na mão.
    ///
    /// A conversão não toca na assinatura: ela cobre os atributos assinados, não a
    /// codificação de fora. Este teste prova as duas metades — sai DER, e ainda confere.
    /// </summary>
    [Fact]
    public async Task CMS_em_BER_do_PSC_entra_no_PDF_normalizado_em_DER()
    {
        var conteudo = "os bytes cobertos"u8.ToArray();
        var (berDoPsc, derEsperado) = Pkcs7EmBerEEmDer(conteudo);

        Assert.Equal(0x80, berDoPsc[1]);          // é mesmo o indefinido que o PSC manda

        var cliente = new ClienteSafeID(
            new HttpClient(HandlerQueResponde(Envelope(berDoPsc, id: null))), Opcoes);

        var certificado = new CertificadoDeNuvem(
            "A3:1", CertificadoIcpBrasil.Ler(
                X509Certificate2.CreateFromPem(PemDeTeste("Dra. Ana Souza", "12345678909"))));

        var voltou = await new AssinadorSafeID(cliente, "t", certificado, "Receita")
            .GetSignatureAsync(new MemoryStream([1, 2, 3]));

        Assert.NotEqual(0x80, voltou[1]);         // saiu DER, comprimento definido
        Assert.Equal(derEsperado, voltou);

        // E a assinatura sobrevive à conversão — é o que a torna segura.
        var confere = new SignedCms(new ContentInfo(conteudo), detached: true);
        confere.Decode(voltou);
        confere.CheckSignature(verifySignatureOnly: true);
    }

    /// <summary>O mesmo CMS nas duas codificações: a do PSC (BER indefinido) e a do PDF (DER).</summary>
    private static (byte[] Ber, byte[] Der) Pkcs7EmBerEEmDer(byte[] conteudo)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            "CN=Dra. Ana Souza", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        var cms = new SignedCms(new ContentInfo(conteudo), detached: true);
        cms.ComputeSignature(new CmsSigner(certificado));
        var der = cms.Encode();

        AsnDecoder.ReadEncodedValue(
            der, AsnEncodingRules.DER, out var inicio, out var tamanho, out _);

        return ([0x30, 0x80, .. der.AsSpan(inicio, tamanho).ToArray(), 0x00, 0x00], der);
    }

    /// <summary>Um CMS destacado de verdade, como o PSC devolve com `signature_format: CMS`.</summary>
    private static byte[] Pkcs7DeTeste()
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            "CN=Dra. Ana Souza", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        var cms = new SignedCms(new ContentInfo("conteúdo assinado"u8.ToArray()), detached: true);
        cms.ComputeSignature(new CmsSigner(certificado));
        return cms.Encode();
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

    /// <summary>O mesmo envelope, com o <c>raw_signature</c> exatamente como o PSC o mandou.</summary>
    private static string EnvelopeBruto(string rawSignature)
        => $$"""
            {"certificate_alias":"A3:1","signatures":[
              {"id":"req-1","raw_signature":{{JsonSerializer.Serialize(rawSignature)}}}]}
            """;

    // ---- O passo a passo da autorização ----

    [Fact]
    public async Task Sem_configuracao_a_autorizacao_diz_ONDE_configurar()
    {
        // "SafeID não configurado" mandaria procurar no portal da Safeweb. A frase tem de
        // apontar a tela, senão o suporte da clínica passa a tarde no lugar errado.
        var servico = new AutorizacaoSafeIDService(
            _ => Task.FromResult<OpcoesSafeID?>(null),
            _ => new ClienteSafeID(new HttpClient(HandlerQueResponde("{}")), Opcoes),
            _ => throw new Xunit.Sdk.XunitException("não devia abrir o navegador"));

        Assert.False(await servico.ConfiguradoAsync());

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.AutorizarAsync());

        Assert.Contains("Configurações", erro.Message);
    }

    [Fact]
    public async Task Autorizacao_completa_troca_o_codigo_e_devolve_o_certificado()
    {
        // O fluxo inteiro, com escuta de verdade: o "navegador" é um HttpClient que bate na
        // URI de retorno levando o code e o state, como o PSC faria.
        var handler = new HandlerPorCaminho
        {
            ["token"] = """{"access_token":"tk-1","expires_in":300}""",
            ["certificate-discovery"] =
                $$"""{"status":"S","certificates":[{"alias":"slot-1","certificate":{{JsonSerializer.Serialize(PemDeTeste("Dra. Ana Souza", "12345678909"))}}}]}"""
        };

        var opcoes = Opcoes with { RedirectUris = [Loopback(18200)] };

        var servico = new AutorizacaoSafeIDService(
            _ => Task.FromResult<OpcoesSafeID?>(opcoes),
            o => new ClienteSafeID(new HttpClient(handler), o),
            url => _ = SimularNavegador(url));

        var sessao = await servico.AutorizarAsync(cpf: "12345678909", espera: TimeSpan.FromSeconds(20));

        Assert.Equal("tk-1", sessao.Token.AccessToken);
        Assert.Equal("slot-1", sessao.Certificados.Single().Alias);

        // A regra que faz a assinatura qualificada valer atravessa a nuvem intacta: o CPF sai
        // de DENTRO do certificado que o PSC devolveu, não de um campo informado.
        Assert.Equal("12345678909", sessao.Certificados.Single().Certificado.Cpf);

        // O mesmo redirect_uri nas duas chamadas — divergir aqui é a recusa que o PSC não
        // explica.
        Assert.Contains("redirect_uri=" + Uri.EscapeDataString(Loopback(18200).ToString()),
            handler.CorpoDe("token"));
    }

    [Fact]
    public async Task Credencial_recusada_falha_ANTES_de_abrir_o_navegador()
    {
        // O primeiro teste na clínica caiu aqui: a aplicação estava cadastrada em produção e
        // o sistema chamou homologação. O PSC respondeu com uma PÁGINA de erro no navegador,
        // que nunca chega ao endereço de retorno — a escuta esperaria os três minutos em
        // silêncio enquanto o usuário já via o erro na outra janela.
        var abriu = false;

        var servico = new AutorizacaoSafeIDService(
            _ => Task.FromResult<OpcoesSafeID?>(
                Opcoes with { BaseUrl = OpcoesSafeID.BaseHomologacao }),
            o => new ClienteSafeID(
                new HttpClient(HandlerQueResponde(
                    """{"message":"Não foi possível encontrar a aplicação com base no client_id."}""",
                    HttpStatusCode.BadRequest)),
                o),
            _ => abriu = true);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.AutorizarAsync());

        Assert.False(abriu);                                // não incomodou a médica
        Assert.Contains("homologação", erro.Message);       // nomeia o ambiente chamado
        Assert.Contains("Configurações", erro.Message);     // e diz onde corrigir
    }

    [Fact]
    public async Task Autorizacao_que_ninguem_confirma_expira_com_frase_propria()
    {
        var opcoes = Opcoes with { RedirectUris = [Loopback(18201)] };

        // A credencial precisa PASSAR na conferência para o teste chegar à espera — é o que
        // separa "credencial errada" de "ninguém confirmou", que são falhas diferentes.
        var servico = new AutorizacaoSafeIDService(
            _ => Task.FromResult<OpcoesSafeID?>(opcoes),
            o => new ClienteSafeID(
                new HttpClient(HandlerQueResponde("""{"access_token":"tk","expires_in":300}""")), o),
            _ => { /* a médica não pegou o celular */ });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.AutorizarAsync(espera: TimeSpan.FromMilliseconds(300)));

        Assert.Contains("expirou", erro.Message);
    }

    /// <summary>Faz o papel do navegador: volta na URI de retorno com o code e o state.</summary>
    private static async Task SimularNavegador(Uri autorizacao)
    {
        var consulta = System.Web.HttpUtility.ParseQueryString(autorizacao.Query);
        var retorno = new Uri(consulta["redirect_uri"]!);

        using var navegador = new HttpClient();

        // A escuta pode não ter terminado de subir no instante em que a URL é "aberta".
        for (var tentativa = 0; tentativa < 40; tentativa++)
        {
            try
            {
                await navegador.GetAsync(
                    $"{retorno}?code=cod-1&state={consulta["state"]}");
                return;
            }
            catch (HttpRequestException) { await Task.Delay(50); }
        }
    }

    private sealed class HandlerPorCaminho : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _respostas = [];
        private readonly Dictionary<string, string> _corpos = [];

        public string this[string caminho] { set => _respostas[caminho] = value; }

        public string CorpoDe(string caminho) => _corpos.GetValueOrDefault(caminho, string.Empty);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage requisicao, CancellationToken ct)
        {
            var caminho = _respostas.Keys.FirstOrDefault(
                c => requisicao.RequestUri!.ToString().Contains(c)) ?? string.Empty;

            if (requisicao.Content is not null)
                _corpos[caminho] = await requisicao.Content.ReadAsStringAsync(ct);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _respostas.GetValueOrDefault(caminho, "{}"),
                    Encoding.UTF8, "application/json")
            };
        }
    }

    // ---- O certificado carrega o próprio assinador ----

    [Fact]
    public async Task Certificado_em_nuvem_assina_SEM_chave_privada_nesta_maquina()
    {
        // A regra central da parcela 44. Sem ela, `Criticar` recusaria todo certificado do
        // PSC com "não tem chave privada nesta máquina" — que é verdade e é irrelevante,
        // porque quem faz a conta é o PSC.
        var pkcs7 = Encoding.ASCII.GetBytes("PKCS7-DE-MENTIRA");

        var publico = X509Certificate2.CreateFromPem(PemDeTeste("Dra. Ana Souza", "12345678909"));
        Assert.False(publico.HasPrivateKey);   // é o caso que interessa

        var certificado = CertificadoIcpBrasil.Ler(publico) with
        {
            AssinadorRemoto = new AssinadorDeMentira(pkcs7)
        };

        Assert.True(certificado.EmNuvem);
        Assert.Equal("SafeID — nuvem", certificado.Procedencia);

        var assinado = await new AssinaturaDigitalService(exigirCadeiaConfiavel: false)
            .AssinarAsync(PdfDeUmaPagina(), certificado, PedidoSimples());

        // O que o "PSC" devolveu foi para dentro do /Contents do PDF.
        Assert.Contains(
            Convert.ToHexString(pkcs7),
            Encoding.Latin1.GetString(assinado.Pdf).ToUpperInvariant());
    }

    [Fact]
    public void Certificado_da_maquina_continua_dizendo_que_eh_da_maquina()
    {
        var certificado = CertificadoIcpBrasil.Ler(
            X509Certificate2.CreateFromPem(PemDeTeste("Dra. Ana Souza", "12345678909")));

        Assert.False(certificado.EmNuvem);
        Assert.Equal("nesta máquina", certificado.Procedencia);
    }

    private sealed class AssinadorDeMentira(byte[] pkcs7)
        : PdfSharp.Pdf.Signatures.IDigitalSigner
    {
        public string CertificateName => "assinador de teste";
        public Task<int> GetSignatureSizeAsync() => Task.FromResult(2048);
        public Task<byte[]> GetSignatureAsync(Stream conteudo) => Task.FromResult(pkcs7);
    }

    private static PedidoAssinatura PedidoSimples() => new(
        Motivo: "Teste",
        NomeExibido: "Dra. Ana Souza",
        RegistroConselho: "CRM 12345",
        Area: new AreaAssinatura(0, 40, 40, 220, 60));

    private static byte[] PdfDeUmaPagina()
    {
        var documento = new PdfSharp.Pdf.PdfDocument();
        documento.AddPage();
        using var fluxo = new MemoryStream();
        documento.Save(fluxo, false);
        return fluxo.ToArray();
    }

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
