using System.Net;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Abstracoes;
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
/// A HOMOLOGAÇÃO DA AMIL (nov/2026): o ambiente por convênio e os comandos <c>testar-*</c> do servidor,
/// o único caminho de um lote nesta fase. A rede é DE MENTIRA — nada sai da máquina, nem para a
/// homologação. O que se prova: o teste só roda em homologação, vai ao ponto único de homologação,
/// funciona com o envio desligado e não grava nada nas guias nem na fila (só a linha da trilha).
/// Login, senha, carteira e CNPJ são fictícios.
/// </summary>
public sealed class TissAmilHomologacaoTests : IDisposable
{
    private const string Amil = "Amil";
    private const string Login = "000123";
    private const string Senha = "senha-ficticia-de-teste";
    private const string Op = "operador.servidor";
    private static readonly Uri Homologacao = new("https://api-dev.servicos.grupoamil.com.br/api-tiss-servico-test");
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly TissBancoDeTeste _b = new();
    private readonly Rede _rede = new();

    public void Dispose() => _b.Dispose();

    private async Task ProntoAsync(bool homologacao = true)
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        if (homologacao) await _b.Credenciais().DefinirAmbienteAsync(Amil, true, Op);
    }

    // ==================== o ambiente ====================

    [Fact]
    public async Task Em_homologacao_todos_os_servicos_da_Amil_vao_ao_ponto_unico_e_a_troca_vai_para_a_trilha()
    {
        await ProntoAsync(homologacao: false);
        var credenciais = _b.Credenciais();
        (await credenciais.EnderecoAsync(Amil, TipoTransacaoTiss.EnvioGuia)).Should().Be(OperadorasTiss.Amil.Endereco(TipoTransacaoTiss.EnvioGuia));

        var (saida, erro) = (new StringWriter(), new StringWriter());
        (await ComandosDoConector.ExecutarAsync(["ambiente", Amil, "homologacao", "--operador", Op], Servicos(), saida, erro)).Should().Be(0, erro.ToString());
        // Até o endereço cadastrado perde para a homologação: teste nunca cai em produção.
        credenciais = _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto()));
        await credenciais.DefinirEnderecoAsync(Amil, TipoTransacaoTiss.Elegibilidade, "https://exemplo.invalid/eleg", Op);
        foreach (var tipo in Enum.GetValues<TipoTransacaoTiss>())
            (await credenciais.EnderecoAsync(Amil, tipo)).Should().Be(Homologacao);

        (await ComandosDoConector.ExecutarAsync(["situacao", Amil], Servicos(), saida, erro)).Should().Be(0);
        saida.ToString().Should().Contain("AMBIENTE DE HOMOLOGAÇÃO");

        (await ComandosDoConector.ExecutarAsync(["ambiente", Amil, "producao", "--operador", Op], Servicos(), saida, erro)).Should().Be(0);
        // Uma leitura nova (o repositório memoriza a configuração por alguns segundos, por escopo).
        (await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).EnderecoAsync(Amil, TipoTransacaoTiss.EnvioGuia))
            .Should().Be(OperadorasTiss.Amil.Endereco(TipoTransacaoTiss.EnvioGuia));
        var trilha = await _b.NovoContexto().Auditoria.AsNoTracking().Select(a => a.Acao).ToListAsync();
        trilha.Should().Contain(["AmbienteTissHomologacao", "AmbienteTissProducao"]);

        // Operadora sem homologação conhecida: recusa com a frase.
        (await ComandosDoConector.ExecutarAsync(["ambiente", TissBancoDeTeste.Petrobras, "homologacao", "--operador", Op], Servicos(), saida, erro))
            .Should().Be(1);
        erro.ToString().Should().Contain("homologação");
    }

    [Fact]
    public async Task Com_a_Amil_em_homologacao_a_fila_de_producao_recusa_os_pedidos_das_telas_e_nada_sai()
    {
        await ProntoAsync();
        var pedido = await _b.Fila().EnfileirarElegibilidadeAsync(Amil, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);

        await Conector().ProcessarProximaAsync();

        _rede.Idas.Should().BeEmpty();
        var t = await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(x => x.Id == pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Contain("HOMOLOGAÇÃO");
    }

    // ==================== os testes ====================

    [Fact]
    public async Task Em_producao_os_testes_recusam_e_nada_vai_a_rede()
    {
        await ProntoAsync(homologacao: false);
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await Executar(saida, erro, "testar-lote", Amil, "--exemplo", "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op)).Should().Be(1);
        (await Executar(saida, erro, "testar-elegibilidade", Amil, "--carteira", TissBancoDeTeste.CarteiraFicticia)).Should().Be(1);
        (await Executar(saida, erro, "testar-protocolo", Amil, "--protocolo", "123", "--operador", Op)).Should().Be(1);

        _rede.Idas.Should().BeEmpty();
        erro.ToString().Should().Contain("PRODUÇÃO").And.Contain("ambiente Amil homologacao");
    }

    [Fact]
    public async Task O_lote_de_exemplo_vai_a_homologacao_em_4_03_00_UTF_8_com_o_envio_desligado_mostra_o_protocolo_e_nao_grava_nada()
    {
        await ProntoAsync();
        (await _b.Credenciais().EnvioLigadoAsync(Amil)).Should().BeFalse("o teste funciona com o envio desligado");
        _rede.Responder(HttpStatusCode.OK, ProtocoloRecebido("777001"));
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await Executar(saida, erro, "testar-lote", Amil, "--exemplo", "--carteira", TissBancoDeTeste.CarteiraFicticia,
            "--senha", "SENHA0001", "--token", "123456", "--operador", Op)).Should().Be(0, erro.ToString());

        var ida = _rede.Idas.Should().ContainSingle().Subject;
        ida.Endereco.Should().Be(Homologacao);
        ida.ContentType.Should().Be("text/xml; charset=utf-8");
        ida.SoapAction.Should().Be("\"\"");
        using var leitor = System.Xml.XmlReader.Create(new MemoryStream(ida.Bytes));
        var mensagem = XDocument.Load(leitor).Root!.Element(Soap + "Body")!.Elements().Single();
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        mensagem.Descendants(Ans + "guiaSP-SADT").Should().ContainSingle();
        mensagem.Descendants(Ans + "codValidacao").Single().Value.Should().Be("123456");
        mensagem.Descendants(Ans + "numeroCarteira").Single().Value.Should().Be(TissBancoDeTeste.CarteiraFicticia);
        Encoding.UTF8.GetString(ida.Bytes).Should().NotContain(Senha);

        saida.ToString().Should().Contain("PROTOCOLO: 777001").And.Contain("testar-protocolo Amil --protocolo 777001");
        var db = _b.NovoContexto();
        (await db.TransacoesTiss.CountAsync()).Should().Be(0, "o teste não entra na fila");
        (await db.TransacoesTissGuias.CountAsync()).Should().Be(0);
        (await db.Auditoria.AsNoTracking().Where(a => a.Acao == "TesteTissHomologacao").Select(a => a.Detalhe).SingleAsync())
            .Should().Contain("protocolo 777001").And.NotContain(TissBancoDeTeste.CarteiraFicticia);
    }

    [Fact]
    public async Task O_lote_com_guias_conferidas_le_as_guias_sem_altera_las_e_usa_numero_proprio_do_teste()
    {
        await ProntoAsync();
        var (_, guia) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        var db = _b.NovoContexto();
        var codigo = await db.Codigos.SingleAsync(c => c.Id == guia.Id);
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil(), TissBancoDeTeste.Operador, _b.Agora);
        await db.SaveChangesAsync();
        _rede.Responder(HttpStatusCode.UnsupportedMediaType, "<html>Unsupported Media Type</html>");
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await Executar(saida, erro, "testar-lote", Amil, "--guias", guia.Id.ToString(), "--operador", Op)).Should().Be(1);

        var mensagem = XDocument.Parse(Encoding.UTF8.GetString(_rede.Idas.Single().Bytes));
        mensagem.Descendants(Ans + "numeroGuiaPrestador").Single().Value.Should().StartWith($"H{guia.Id}T");
        erro.ToString().Should().Contain("HTTP 415");
        var depois = await _b.NovoContexto().Codigos.AsNoTracking().SingleAsync(c => c.Id == guia.Id);
        depois.Baixado.Should().BeFalse();
        depois.NumeroGuiaReal.Should().BeNull();
        (await _b.NovoContexto().TransacoesTiss.CountAsync()).Should().Be(0);

        // Guia de outra operadora não vai num lote da Amil.
        var petrobras = (await _b.GuiaAsync()).Guia.Id;
        (await Executar(saida, erro, "testar-lote", Amil, "--guias", petrobras.ToString(), "--operador", Op)).Should().Be(1);
        erro.ToString().Should().Contain("não é da Amil");
        _rede.Idas.Should().ContainSingle();
    }

    [Fact]
    public async Task Elegibilidade_e_protocolo_de_teste_vao_a_homologacao()
    {
        await ProntoAsync();
        _rede.Responder(HttpStatusCode.InternalServerError, "");
        _rede.Responder(HttpStatusCode.InternalServerError, "");
        var (saida, erro) = (new StringWriter(), new StringWriter());

        await Executar(saida, erro, "testar-elegibilidade", Amil, "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op);
        await Executar(saida, erro, "testar-protocolo", Amil, "--protocolo", "777001", "--operador", Op, "--soap-action", "tissSolicitacaoStatusProtocolo");

        _rede.Idas.Select(i => i.Endereco).Should().AllBeEquivalentTo(Homologacao);
        _rede.Idas[1].SoapAction.Should().Be("\"tissSolicitacaoStatusProtocolo\"");
        var status = XDocument.Parse(Encoding.UTF8.GetString(_rede.Idas[1].Bytes));
        status.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        var elegibilidade = XDocument.Parse(Encoding.UTF8.GetString(_rede.Idas[0].Bytes));
        elegibilidade.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.01.00", "a elegibilidade da Amil continua em 4.01.00");
        (await _b.NovoContexto().TransacoesTiss.CountAsync()).Should().Be(0);
    }

    // ==================== o lembrete depois do envio ====================

    [Fact]
    public void Depois_do_envio_a_Amil_pede_a_confirmacao_no_portal_e_a_Petrobras_nao()
    {
        var concluido = new TransacaoTiss { Tipo = TipoTransacaoTiss.EnvioGuia, Estado = EstadoTransacaoTiss.Concluida, Protocolo = "777001" };
        EnvioTissNaTela.LembreteDeEntrega(OperadorasTiss.Amil, concluido)
            .Should().Be("Enviado. Confirme a entrega no portal da Amil (protocolo 777001): enviado não é entregue.");
        EnvioTissNaTela.LembreteDeEntrega(OperadorasTiss.SaudePetrobras, concluido).Should().BeNull();
        EnvioTissNaTela.LembreteDeEntrega(OperadorasTiss.Amil, new TransacaoTiss { Tipo = TipoTransacaoTiss.EnvioGuia, Estado = EstadoTransacaoTiss.Pendente })
            .Should().BeNull("antes do protocolo não há o que confirmar");
    }

    // ==================== peças ====================

    private static DadosGuiaConsultaAns GuiaAmil() => new()
    {
        Modelo = ModeloDaGuiaTiss.SpSadt,
        RegistroAns = "326305", NumeroCarteira = TissBancoDeTeste.CarteiraFicticia, NomeBeneficiario = "Paciente de Teste",
        CodigoPrestadorNaOperadora = Login, NomeContratado = "Clínica de Teste Ltda", Cnes = "9999999",
        NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33", Cbos = "225105",
        IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = new DateOnly(2026, 11, 10), CodigoTabela = "22",
        TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139.00m,
        Senha = "SENHA0001", DataAutorizacao = new DateOnly(2026, 11, 10), CodigoValidacao = "123456",
        TipoAtendimento = "04", CaraterAtendimento = "1", DescricaoProcedimento = "Consulta em consultório"
    };

    private static string ProtocoloRecebido(string numero) => TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO", $"""
        <ans:recebimentoLote><ans:protocoloRecebimento>
          <ans:registroANS>326305</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>{Login}</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:numeroLote>8</ans:numeroLote>
          <ans:dataEnvioLote>2026-11-10</ans:dataEnvioLote>
          <ans:detalheProtocolo>
            <ans:numeroProtocolo>{numero}</ans:numeroProtocolo>
            <ans:valorTotalProtocolo>1.00</ans:valorTotalProtocolo>
            <ans:dadosGuiasProtocolo><ans:dadosGuias>{TissWsDeTeste.GuiaDevolvida("HX090000")}</ans:dadosGuias></ans:dadosGuiasProtocolo>
          </ans:detalheProtocolo>
        </ans:protocoloRecebimento></ans:recebimentoLote>
        """);

    private Task<int> Executar(TextWriter saida, TextWriter erro, params string[] args)
        => ComandosDoConector.ExecutarAsync(args, Servicos(), saida, erro);

    private IServiceProvider Servicos()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new ServiceCollection()
            .AddSingleton<IClinicaRepositorio>(repo)
            .AddSingleton(_b.Credenciais(repo)).AddSingleton(_b.Fila(repo))
            .AddSingleton(new ParametrosService(repo, _b.Protecao))
            .AddSingleton(new TissWebserviceCliente(new HttpClient(_rede)))
            .BuildServiceProvider();
    }

    private ExecutorDeTransacoesTiss Conector()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores = [new ProcessadorDeElegibilidade(repo, parametros, credenciais, () => _b.Agora)];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores, new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = false, Consumidor = "conector-de-teste" }, NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    /// <summary>A "operadora": responde o combinado, na ordem, e anota cada ida (bytes e cabeçalhos).</summary>
    private sealed class Rede : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, byte[] Bytes, string? ContentType, string? SoapAction);
        private readonly Queue<HttpResponseMessage> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Idas.Add(new Ida(request.RequestUri!, await request.Content!.ReadAsByteArrayAsync(ct), request.Content.Headers.ContentType?.ToString(),
                request.Headers.TryGetValues("SOAPAction", out var a) ? a.Single() : null));
            return _respostas.Count > 0 ? _respostas.Dequeue() : throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
        }
    }
}
