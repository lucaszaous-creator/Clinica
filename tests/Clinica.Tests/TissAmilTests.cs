using System.Net;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A Amil no conector TISS (nov/2026): a elegibilidade sai pelo catálogo de operadoras
/// (<see cref="OperadorasTiss"/>), com o registro ANS dela, o login como código do prestador e o
/// endereço APIM — que o cadastro do convênio pode trocar sem novo deploy. A rede é DE MENTIRA:
/// nada sai da máquina. Login, senha, carteira e CNPJ são fictícios.
/// </summary>
public sealed class TissAmilTests : IDisposable
{
    private const string Amil = "Amil";
    private const string Login = "000123";
    private const string Senha = "senha-ficticia-de-teste";
    private static readonly string Md5 = TissElegibilidadeWs.SenhaEmMd5(Senha);
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly TissBancoDeTeste _b = new();
    private readonly Rede _rede = new();

    public void Dispose() => _b.Dispose();

    // ---------- o catálogo ----------

    [Fact]
    public void O_catalogo_traz_a_Amil_com_o_registro_ANS_e_os_enderecos_APIM_por_servico()
    {
        var amil = OperadorasTiss.Da(Convenio.Amil)!;

        amil.RegistroAns.Should().Be("326305");
        amil.CodigoDoPrestador.Should().Be(CodigoDoPrestadorTiss.LoginDoWebService);
        amil.EnvioDeGuiaNasTelas.Should().BeFalse("a guia da Amil ainda não passa pela conferência ANS na baixa");
        amil.Endereco(TipoTransacaoTiss.Elegibilidade).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-verifica-elegibilidade/v4.01.00"));
        // Resposta do Núcleo TISS (nov/2026): lote, status e demonstrativo em 4.03.00.
        amil.Endereco(TipoTransacaoTiss.EnvioGuia).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-lote-guias/v4.03.00"));
        amil.Endereco(TipoTransacaoTiss.StatusProtocolo).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-status-protocolo/v4.03.00"));
        amil.EnderecoDemonstrativo.Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-demonstrativo-retorno/v4.03.00"));
        // Manual do Credenciado (jul/2026, p. 53): cancelamento e autorização em 4.03.00.
        amil.Endereco(TipoTransacaoTiss.CancelamentoGuia).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-cancela-guia/v4.03.00"));
        amil.Endereco(TipoTransacaoTiss.CancelaAutorizacao).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-cancela-guia/v4.03.00"));
        amil.Endereco(TipoTransacaoTiss.Autorizacao).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-procedimento/v4.03.00"));
        amil.Endereco(TipoTransacaoTiss.StatusAutorizacao).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-status-autorizacao/v4.03.00"));
        amil.VersaoTiss(TipoTransacaoTiss.Autorizacao).Should().Be("4.03.00");
        amil.VersaoTiss(TipoTransacaoTiss.EnvioGuia).Should().Be("4.03.00");
        amil.VersaoTiss(TipoTransacaoTiss.StatusProtocolo).Should().Be("4.03.00");
        amil.VersaoTiss(TipoTransacaoTiss.Elegibilidade).Should().Be("4.01.00", "funcionou em produção assim");
        amil.VersaoTiss(TipoTransacaoTiss.CancelamentoGuia).Should().Be("4.03.00");
        // A Saúde Petrobras recusa a 4.01.00 desde 01/08/2026 (canal oficial, post 499): tudo em 4.03.00.
        foreach (var tipo in new[] { TipoTransacaoTiss.Elegibilidade, TipoTransacaoTiss.EnvioGuia, TipoTransacaoTiss.StatusProtocolo, TipoTransacaoTiss.CancelamentoGuia })
            OperadorasTiss.SaudePetrobras.VersaoTiss(tipo).Should().Be("4.03.00");
    }

    [Fact]
    public void A_Saude_Petrobras_continua_com_o_mesmo_registro_codigo_e_enderecos()
    {
        var petrobras = OperadorasTiss.Da(Convenio.Petrobras)!;

        petrobras.RegistroAns.Should().Be(TissWebserviceCliente.RegistroAnsSaudePetrobras).And.Be("422631");
        petrobras.CodigoDoPrestador.Should().Be(CodigoDoPrestadorTiss.CnpjDaClinica);
        petrobras.EnvioDeGuiaNasTelas.Should().BeTrue();
        // Os endereços 4.03.00, escritos por extenso (base tiss40300/ws/40300, não o soap:address do WSDL).
        petrobras.Endereco(TipoTransacaoTiss.Elegibilidade).Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissVerificaElegibilidade"));
        petrobras.Endereco(TipoTransacaoTiss.EnvioGuia).Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissLoteGuias"));
        petrobras.Endereco(TipoTransacaoTiss.StatusProtocolo).Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissSolicitacaoStatusProtocolo"));
        petrobras.Endereco(TipoTransacaoTiss.CancelamentoGuia).Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissCancelaGuia"));
        petrobras.Codificacao.Should().Be(CodificacaoTissWs.Iso88591, "o próprio demonstrativo 4.03.00 dela declara ISO-8859-1");
        petrobras.EnderecoHomologacao.Should().BeNull("a Saúde Petrobras não tem homologação");
        petrobras.DiasDeEnvioNoMes.Should().Be(new DiasDeEnvioNoMesTiss(1, 20));
        petrobras.JanelaDeEnvio.Should().BeNull("os dias 1 a 20 são aviso: a fila não segura o lote da Petrobras");
    }

    [Fact]
    public void Todo_endereco_do_catalogo_e_HTTPS_e_as_familias_nao_se_repetem()
    {
        OperadorasTiss.Todas.Select(o => o.Familia).Should().OnlyHaveUniqueItems();
        OperadorasTiss.Todas.SelectMany(o => o.Enderecos.Values).Should().OnlyContain(u => u.Scheme == Uri.UriSchemeHttps);
        OperadorasTiss.Todas.Should().OnlyContain(o => o.RegistroAns.Length == 6 && o.RegistroAns.All(char.IsAsciiDigit));
    }

    // ---------- a elegibilidade, ponta a ponta ----------

    [Fact]
    public async Task Elegibilidade_da_Amil_vai_ao_endereco_dela_com_o_registro_dela_e_o_login_como_codigo()
    {
        // A clínica tem UM registro ANS nos dados do prestador (o da Petrobras, nos testes): a mensagem
        // da Amil não pode levá-lo. Vale o do catálogo.
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        var pedido = await PedirAsync();

        (await Conector().ProcessarProximaAsync()).Should().BeTrue();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        t.Resultado.Should().StartWith("Elegível.");
        var ida = _rede.Idas.Should().ContainSingle().Subject;
        ida.Endereco.Should().Be(OperadorasTiss.Amil.Endereco(TipoTransacaoTiss.Elegibilidade));
        var mensagem = Corpo(ida.Corpo);
        mensagem.Descendants(Ans + "registroANS").Single().Value.Should().Be("326305");
        mensagem.Descendants(Ans + "codigoPrestadorNaOperadora").Select(e => e.Value).Should().Equal(Login, Login);
        mensagem.Descendants(Ans + "loginPrestador").Single().Value.Should().Be(Login);
        mensagem.Descendants(Ans + "senhaPrestador").Single().Value.Should().Be(Md5);
        ida.Corpo.Should().NotContain(Senha).And.NotContain("00000000000191", "o CNPJ não vai como código na Amil");
    }

    [Fact]
    public async Task A_mensagem_de_elegibilidade_da_Amil_e_valida_no_XSD_4_01_00()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        await PedirAsync();

        await Conector().ProcessarProximaAsync();

        var mensagem = Corpo(_rede.Idas.Single().Corpo);
        mensagem.Name.Should().Be(Ans + "pedidoElegibilidadeWS");
        TissWsDeTeste.ValidarNoXsd(mensagem).Should().BeEmpty();
        // E o XSD reprova de verdade: sem a carteira, a mesma mensagem não passa.
        mensagem.Descendants(Ans + "numeroCarteira").Single().Remove();
        TissWsDeTeste.ValidarNoXsd(mensagem).Should().NotBeEmpty();
    }

    [Fact]
    public async Task O_endereco_cadastrado_vence_o_do_catalogo_e_todos_grava_o_link_unico_nos_quatro_servicos()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);

        (await ComandosDoConector.ExecutarAsync(["endereco", Amil, "todos", "https://exemplo.invalid/tiss/link-unico", "--operador", "operador.servidor"],
            Servicos(), saida, erro)).Should().Be(0, erro.ToString());

        var credenciais = _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto()));
        foreach (var tipo in new[] { TipoTransacaoTiss.Elegibilidade, TipoTransacaoTiss.EnvioGuia, TipoTransacaoTiss.StatusProtocolo, TipoTransacaoTiss.CancelamentoGuia,
                     TipoTransacaoTiss.Autorizacao, TipoTransacaoTiss.StatusAutorizacao, TipoTransacaoTiss.CancelaAutorizacao })
            (await credenciais.EnderecoAsync(Amil, tipo)).Should().Be(new Uri("https://exemplo.invalid/tiss/link-unico"));

        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        await PedirAsync();
        await Conector().ProcessarProximaAsync();
        _rede.Idas.Single().Endereco.Should().Be(new Uri("https://exemplo.invalid/tiss/link-unico"));
    }

    [Fact]
    public async Task O_registro_ANS_cadastrado_no_convenio_vence_o_do_catalogo()
    {
        _b.Db.Convenios.Add(new ConvenioCadastro { Codigo = "AmilEmpresas", Nome = "Amil Empresas", Familia = Convenio.Amil, RegistroAnsOperadora = "999981" });
        await _b.Db.SaveChangesAsync();
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync("AmilEmpresas", Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        await _b.Fila().EnfileirarElegibilidadeAsync("AmilEmpresas", TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);

        await Conector().ProcessarProximaAsync();

        var ida = _rede.Idas.Single();
        // A família é Amil: herda o endereço do catálogo; o registro é o cadastrado.
        ida.Endereco.Should().Be(OperadorasTiss.Amil.Endereco(TipoTransacaoTiss.Elegibilidade));
        Corpo(ida.Corpo).Descendants(Ans + "registroANS").Single().Value.Should().Be("999981");
    }

    [Fact]
    public async Task O_codigo_do_prestador_pode_ser_o_cnpj_um_codigo_cadastrado_ou_voltar_ao_padrao()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);

        (await ComandosDoConector.ExecutarAsync(["codigo", Amil, "987654", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        await PedirAsync();
        await Conector().ProcessarProximaAsync();
        Corpo(_rede.Idas[^1].Corpo).Descendants(Ans + "codigoPrestadorNaOperadora").Select(e => e.Value).Should().Equal("987654", "987654");

        (await ComandosDoConector.ExecutarAsync(["codigo", Amil, "cnpj", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        await PedirAsync(consultarDeNovo: true);
        await Conector().ProcessarProximaAsync();
        Corpo(_rede.Idas[^1].Corpo).Descendants(Ans + "codigoPrestadorNaOperadora").First().Value.Should().Be("00000000000191");

        (await ComandosDoConector.ExecutarAsync(["codigo", Amil, "padrao", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0);
        (await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).CodigoDoPrestadorAsync(Amil)).Regra
            .Should().Be(CodigoDoPrestadorTiss.LoginDoWebService);

        // A trilha diz QUE mudou e quem mudou; o código literal não vai para ela.
        var trilha = await _b.NovoContexto().Auditoria.AsNoTracking().Where(a => a.Acao == "CodigoPrestadorTissAlterado").ToListAsync();
        trilha.Should().HaveCount(3).And.OnlyContain(a => a.Operador == "operador.servidor");
        trilha.Should().NotContain(a => a.Detalhe!.Contains("987654"));
        (saida.ToString() + erro).Should().NotContain("987654");
    }

    [Fact]
    public async Task Codigo_maior_que_o_campo_do_padrao_e_recusado_e_login_longo_nao_vai_para_a_rede()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());
        (await ComandosDoConector.ExecutarAsync(["codigo", Amil, new string('9', 15), "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(1);
        erro.ToString().Should().Contain("até 14 caracteres");

        // Login de 15+ caracteres como código: recusado antes da rede, dizendo o que fazer.
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, "LOGINCOMQUINZECAR", Senha, TissBancoDeTeste.Operador);
        var pedido = await PedirAsync();
        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Contain("mais de 14 caracteres").And.Contain("comando codigo");
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task Convenio_fora_do_catalogo_sem_registro_ANS_em_lugar_nenhum_e_recusado_antes_da_rede()
    {
        const string fora = "UnimedPadrao";
        await _b.Credenciais().SalvarAsync(fora, Login, Senha, TissBancoDeTeste.Operador);
        await _b.Credenciais().DefinirEnderecoAsync(fora, TipoTransacaoTiss.Elegibilidade, "https://exemplo.invalid/tiss/elegibilidade", TissBancoDeTeste.Operador);
        var pedido = await _b.Fila().EnfileirarElegibilidadeAsync(fora, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Contain("registro ANS");
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sem_credencial_da_Amil_nada_sai()
    {
        await _b.PrestadorAsync();
        var pedido = await PedirAsync();

        await Conector().ProcessarProximaAsync();

        (await GravadaAsync(pedido.Id)).Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        _rede.Idas.Should().BeEmpty();
    }

    // ---------- os comandos ----------

    [Fact]
    public async Task Credencial_e_situacao_da_Amil_funcionam_como_os_da_Petrobras_sem_mostrar_login_nem_senha()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await ComandosDoConector.ExecutarAsync(["credencial", Amil, "--operador", "operador.servidor"], Servicos(), saida, erro,
            ambiente: v => v == ComandosDoConector.VariavelLogin ? Login : v == ComandosDoConector.VariavelSenha ? Senha : null)).Should().Be(0);
        (await ComandosDoConector.ExecutarAsync(["situacao", Amil], Servicos(), saida, erro)).Should().Be(0, erro.ToString());

        var texto = saida.ToString();
        texto.Should().Contain("Amil: login cadastrado, senha cadastrada, envio desligado")
            .And.Contain("Amil, registro ANS 326305 no catálogo")
            .And.Contain("código do prestador: o login do web service")
            .And.Contain("api-tiss-verifica-elegibilidade/v4.01.00 (padrão do catálogo)");
        texto.Should().NotContain(Senha).And.NotContain(Md5);
        texto.Split('\n').Where(l => l.Contains("operadora ")).Should().NotContain(l => l.Contains(Login), "a linha de situação não repete o login");
    }

    // ---------- o envio de guias: pronto, desligado ----------

    [Fact]
    public async Task O_envio_de_guias_da_Amil_tem_endereco_mas_continua_desligado_e_fora_das_telas()
    {
        var credenciais = _b.Credenciais();

        (await credenciais.EnderecoAsync(Amil, TipoTransacaoTiss.EnvioGuia)).Should().Be(OperadorasTiss.Amil.Endereco(TipoTransacaoTiss.EnvioGuia));
        (await credenciais.EnvioLigadoAsync(Amil)).Should().BeFalse("o interruptor do convênio nasce desligado");
        EnvioTissNaTela.TemEnvioDeGuia(Amil, Convenio.Amil).Should().BeFalse();

        // E a guia da Amil, sem a conferência ANS, é recusada pela própria fila.
        var (_, guia) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        var conferencia = await _b.Fila().ConferirEnvioAsync(Amil, [guia.Id]);
        conferencia.PodeEnviar.Should().BeFalse();
        conferencia.PorGuia[guia.Id].Should().Contain("padrão ANS");
    }

    // ---------- a codificação da mensagem (nov/2026: o HTTP 415 do APIM) ----------

    [Fact]
    public async Task A_mensagem_da_Amil_sai_em_UTF_8_declarada_nos_bytes_e_no_cabecalho_e_valida_no_XSD()
    {
        OperadorasTiss.Amil.Codificacao.Should().Be(CodificacaoTissWs.Utf8);
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        var pedido = await PedirAsync();

        await Conector().ProcessarProximaAsync();

        var ida = _rede.Idas.Single();
        ida.ContentType.Should().Be("text/xml; charset=utf-8");
        ida.SoapAction.Should().Be("\"\"", "SOAP 1.1 do WSDL da Amil: soapAction vazio, entre aspas");
        var texto = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(ida.Bytes);
        texto.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        ida.Bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, "sem BOM: a declaração já diz a codificação");
        // Os bytes, lidos como o servidor os lê (pela declaração), dão a mensagem válida no XSD.
        using var leitor = System.Xml.XmlReader.Create(new MemoryStream(ida.Bytes));
        var mensagem = XDocument.Load(leitor).Root!.Element(Soap + "Body")!.Elements().Single();
        TissWsDeTeste.ValidarNoXsd(mensagem).Should().BeEmpty();
        // O XML guardado na fila diz a MESMA codificação que foi pela rede.
        (await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(t => t.Id == pedido.Id)).XmlEnviado
            .Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    }

    [Fact]
    public async Task A_mensagem_da_Petrobras_sai_byte_a_byte_como_antes_em_ISO_8859_1()
    {
        OperadorasTiss.SaudePetrobras.Codificacao.Should().Be(CodificacaoTissWs.Iso88591);
        // O de antes: Latin1.GetBytes(envelope) com charset ISO-8859-1. O Codificar devolve o mesmo.
        var envelope = TissElegibilidadeWs.MontarEnvelope(new PedidoElegibilidadeTiss
        {
            RegistroAnsOperadora = "422631", CodigoPrestadorNaOperadora = "00000000000191", NumeroCarteira = TissBancoDeTeste.CarteiraFicticia,
            Login = "login-ficticio", SenhaMd5 = Md5, Sequencial = "000000001001", Momento = new DateTime(2026, 11, 10, 9, 0, 0)
        });
        var (bytes, charset) = TissWebserviceCliente.Codificar(envelope, CodificacaoTissWs.Iso88591);
        bytes.Should().Equal(Encoding.Latin1.GetBytes(envelope));
        charset.Should().Be("ISO-8859-1");

        // E pela fila, de ponta a ponta: o cabeçalho e a declaração continuam os de antes.
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        await _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);
        await Conector().ProcessarProximaAsync();
        var ida = _rede.Idas.Single();
        ida.ContentType.Should().Be("text/xml; charset=ISO-8859-1");
        ida.Corpo.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
        ida.Bytes.Should().Equal(Encoding.Latin1.GetBytes(ida.Corpo));
    }

    /// <summary>
    /// O hash é do TEXTO das tags, em ISO-8859-1 (Componente de Comunicação do padrão), e não dos
    /// bytes que vão pela rede: a mesma mensagem, mandada em ISO-8859-1 ou em UTF-8, leva o MESMO
    /// hash, e quem a relê (pela declaração) recalcula e confere. Com acento a diferença aparece:
    /// o MD5 dos bytes UTF-8 seria outro.
    /// </summary>
    [Theory]
    [InlineData("0001234567")]
    [InlineData("Clínica São João")]
    public void O_hash_e_do_texto_e_vale_igual_nas_duas_codificacoes(string conteudo)
    {
        var ans = TissMensagemWs.Ans;
        var mensagem = TissMensagemWs.Fechar("pedidoElegibilidadeWS",
            new XElement(ans + "cabecalho", new XElement(ans + "Padrao", "4.01.00")),
            new XElement(ans + "pedidoElegibilidade", new XElement(ans + "numeroCarteira", conteudo)));
        var envelope = TissMensagemWs.Envelope(mensagem);
        var hash = mensagem.Element(ans + "hash")!.Value;

        foreach (var codificacao in new[] { CodificacaoTissWs.Iso88591, CodificacaoTissWs.Utf8 })
        {
            var (bytes, _) = TissWebserviceCliente.Codificar(envelope, codificacao);
            using var leitor = System.Xml.XmlReader.Create(new MemoryStream(bytes));
            var relida = XDocument.Load(leitor).Root!.Element(Soap + "Body")!.Elements().Single();
            relida.Element(ans + "hash")!.Value.Should().Be(hash, $"o hash não depende da codificação ({codificacao})");
            relida.Descendants(ans + "numeroCarteira").Single().Value.Should().Be(conteudo);
            var semHash = new XElement(relida);
            semHash.Element(ans + "hash")!.Remove();
            TissElegibilidadeWs.CalcularHash(semHash).Should().Be(hash);
        }
        var textoDasTags = "4.01.00" + conteudo;
        hash.Should().Be(Convert.ToHexString(System.Security.Cryptography.MD5.HashData(Encoding.Latin1.GetBytes(textoDasTags))).ToLowerInvariant());
        var md5DoUtf8 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(textoDasTags))).ToLowerInvariant();
        if (conteudo.Any(c => c > 127)) md5DoUtf8.Should().NotBe(hash);
        else md5DoUtf8.Should().Be(hash);
    }

    // ---------- HTTP de erro sem mensagem SOAP ----------

    [Fact]
    public async Task HTTP_415_sem_corpo_vira_nao_foi_possivel_sem_repetir_e_o_motivo_diz_o_status()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.UnsupportedMediaType, "");
        var pedido = await PedirAsync();

        await Conector().ProcessarProximaAsync();
        _b.Agora = _b.Agora.AddHours(2);
        await Conector().ProcessarProximaAsync();

        _rede.Idas.Should().ContainSingle("um 415 não melhora tentando de novo, e cada ida gasta a cota do dia");
        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        t.Tentativas.Should().Be(1);
        t.Erro.Should().Contain("HTTP 415").And.Contain(FilaTiss.FraseRecusaDoHttp);
    }

    [Fact]
    public async Task HTTP_de_erro_com_pagina_leva_o_trecho_do_corpo_sem_a_carteira_e_sem_a_senha()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        var pagina = $"<html><body><h1>415 Unsupported Media Type</h1><p>Esperado text/xml; charset=utf-8. "
                     + $"Pedido {TissBancoDeTeste.CarteiraFicticia} {Md5}</p>{new string('x', 600)}</body></html>";
        _rede.Responder(HttpStatusCode.UnsupportedMediaType, pagina);
        var pedido = await PedirAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        t.Erro.Should().Contain("HTTP 415").And.Contain("Unsupported Media Type").And.Contain("charset=utf-8");
        t.Erro.Should().NotContain(TissBancoDeTeste.CarteiraFicticia).And.NotContain(Md5);
        t.Erro.Should().NotContain(new string('x', 301), "só um trecho curto do corpo");
    }

    [Fact]
    public async Task HTTP_503_sem_corpo_continua_tentando_de_novo()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.ServiceUnavailable, "");
        var pedido = await PedirAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        t.Erro.Should().Contain("HTTP 503");
    }

    [Theory]
    [InlineData(415, true)] [InlineData(404, true)] [InlineData(403, true)] [InlineData(401, true)]
    [InlineData(408, false)] [InlineData(429, false)] [InlineData(500, false)] [InlineData(503, false)] [InlineData(null, false)]
    public void Recusa_definitiva_do_HTTP_e_so_o_4xx_que_nao_passa_sozinho(int? status, bool definitiva)
        => FilaTiss.RecusaDefinitivaDoHttp(status).Should().Be(definitiva);

    [Fact]
    public async Task O_comando_cancelar_tira_da_fila_a_transacao_pendente_e_so_ela()
    {
        var pedido = await PedirAsync();
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await ComandosDoConector.ExecutarAsync(["cancelar", pedido.Id.ToString(), "--operador", "operador.servidor"], Servicos(), saida, erro))
            .Should().Be(0, erro.ToString());
        saida.ToString().Should().Contain($"Transação {pedido.Id} cancelada");
        (await GravadaAsync(pedido.Id)).Estado.Should().Be(EstadoTransacaoTiss.Cancelada);

        // De novo: já não está Pendente — nada muda, e o código de saída diz isso.
        (await ComandosDoConector.ExecutarAsync(["cancelar", pedido.Id.ToString(), "--operador", "operador.servidor"], Servicos(), saida, erro))
            .Should().Be(1);
        // Sem operador, não cancela.
        (await ComandosDoConector.ExecutarAsync(["cancelar", pedido.Id.ToString()], Servicos(), saida, erro)).Should().Be(2);
    }

    // ---------- montagem ----------

    private ExecutorDeTransacoesTiss Conector()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores =
        [
            new ProcessadorDeElegibilidade(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeEnvioDeGuia(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeStatusDeProtocolo(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeCancelamentoDeGuia(repo, parametros, credenciais, () => _b.Agora)
        ];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores,
            new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = false, Consumidor = "conector-de-teste" },
            NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    private IServiceProvider Servicos()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new ServiceCollection().AddSingleton(_b.Credenciais(repo)).AddSingleton(_b.Fila(repo)).BuildServiceProvider();
    }

    private Task<TransacaoTiss> PedirAsync(bool consultarDeNovo = false)
        => _b.Fila().EnfileirarElegibilidadeAsync(Amil, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador, consultarDeNovo);

    private Task<TransacaoTiss> GravadaAsync(int id) => _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(t => t.Id == id);

    /// <summary>O elemento do web service dentro do envelope SOAP que saiu.</summary>
    private static XElement Corpo(string envelope) => XDocument.Parse(envelope).Root!.Element(Soap + "Body")!.Elements().Single();

    private static string Recibo(string resposta) => $"""
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body>
            <ans:respostaElegibilidadeWS>
              <ans:cabecalho><ans:Padrao>4.01.00</ans:Padrao></ans:cabecalho>
              <ans:respostaElegibilidade><ans:reciboElegibilidade><ans:registroANS>326305</ans:registroANS>
                <ans:numeroCarteira>{TissBancoDeTeste.CarteiraFicticia}</ans:numeroCarteira><ans:nomeBeneficiario>PACIENTE DE TESTE</ans:nomeBeneficiario>
                <ans:respostaSolicitacao>{resposta}</ans:respostaSolicitacao></ans:reciboElegibilidade></ans:respostaElegibilidade>
              <ans:hash>0</ans:hash>
            </ans:respostaElegibilidadeWS>
          </soapenv:Body>
        </soapenv:Envelope>
        """.Trim();

    /// <summary>A "operadora": responde o combinado, na ordem, e anota cada ida (os bytes e os cabeçalhos também).</summary>
    private sealed class Rede : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, string Corpo, byte[] Bytes, string? ContentType, string? SoapAction);

        private readonly Queue<HttpResponseMessage> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            Idas.Add(new Ida(request.RequestUri!, Encoding.Latin1.GetString(bytes), bytes,
                request.Content.Headers.ContentType?.ToString(),
                request.Headers.TryGetValues("SOAPAction", out var acao) ? acao.Single() : null));
            return _respostas.Count > 0 ? _respostas.Dequeue() : throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
        }
    }
}
