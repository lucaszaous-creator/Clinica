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
        amil.Endereco(TipoTransacaoTiss.EnvioGuia).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-lote-guias/v4.01.00"));
        amil.Endereco(TipoTransacaoTiss.StatusProtocolo).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-status-protocolo/v4.01.00"));
        amil.Endereco(TipoTransacaoTiss.CancelamentoGuia).Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-cancela-guia/v4.01.00"));
    }

    [Fact]
    public void A_Saude_Petrobras_continua_com_o_mesmo_registro_codigo_e_enderecos()
    {
        var petrobras = OperadorasTiss.Da(Convenio.Petrobras)!;

        petrobras.RegistroAns.Should().Be(TissWebserviceCliente.RegistroAnsSaudePetrobras).And.Be("422631");
        petrobras.CodigoDoPrestador.Should().Be(CodigoDoPrestadorTiss.CnpjDaClinica);
        petrobras.EnvioDeGuiaNasTelas.Should().BeTrue();
        // Os mesmos endereços de antes do catálogo, escritos por extenso.
        petrobras.Endereco(TipoTransacaoTiss.Elegibilidade).Should().Be(new Uri("https://webservices.crc.com.br/tiss/ws/40100/tissVerificaElegibilidade"));
        petrobras.Endereco(TipoTransacaoTiss.EnvioGuia).Should().Be(new Uri("https://webservices.crc.com.br/tiss/ws/40100/tissLoteGuias"));
        petrobras.Endereco(TipoTransacaoTiss.StatusProtocolo).Should().Be(new Uri("https://webservices.crc.com.br/tiss/ws/40100/tissSolicitacaoStatusProtocolo"));
        petrobras.Endereco(TipoTransacaoTiss.CancelamentoGuia).Should().Be(new Uri("https://webservices.crc.com.br/tiss/ws/40100/tissCancelaGuia"));
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
        foreach (var tipo in new[] { TipoTransacaoTiss.Elegibilidade, TipoTransacaoTiss.EnvioGuia, TipoTransacaoTiss.StatusProtocolo, TipoTransacaoTiss.CancelamentoGuia })
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

    /// <summary>A "operadora": responde o combinado, na ordem, e anota cada ida.</summary>
    private sealed class Rede : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, string Corpo);

        private readonly Queue<HttpResponseMessage> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Idas.Add(new Ida(request.RequestUri!, Encoding.Latin1.GetString(await request.Content!.ReadAsByteArrayAsync(ct))));
            return _respostas.Count > 0 ? _respostas.Dequeue() : throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
        }
    }
}
