using System.Net;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
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
/// A SAÚDE PETROBRAS EM TISS 4.03.00 (nov/2026). Canal oficial da operadora (post 499, 06/07/2026):
/// "A partir de 01/08/2026, não aceitaremos o envio da versão 4.01.00". Os serviços 4.03.00 ficam em
/// <c>https://webservices.crc.com.br/tiss40300/ws/40300/&lt;serviço&gt;</c>. Aqui: a elegibilidade e o
/// arquivo saem em 4.03.00 válidos no XSD, a consulta de teste roda em produção só para elegibilidade, o
/// endereço antigo cadastrado pode voltar ao catálogo e os dias 1 a 20 são aviso. Rede de mentira; dados fictícios.
/// </summary>
public sealed class TissPetrobras403Tests : IDisposable
{
    private const string Petrobras = TissBancoDeTeste.Petrobras;
    private const string Amil = "Amil";
    private const string Op = "operador.servidor";
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;

    private readonly TissBancoDeTeste _b = new();
    private readonly Rede _rede = new();

    public void Dispose() => _b.Dispose();

    // ==================== elegibilidade ====================

    [Fact]
    public async Task A_elegibilidade_da_Petrobras_sai_em_4_03_00_no_endereco_novo_em_ISO_8859_1_e_a_da_Amil_continua_4_01_00()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Petrobras, "000111", "senha-ficticia", TissBancoDeTeste.Operador);
        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia", TissBancoDeTeste.Operador);

        _rede.Responder(HttpStatusCode.OK, RespostaElegibilidade("S"));
        await _b.Fila().EnfileirarElegibilidadeAsync(Petrobras, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);
        (await Conector().ProcessarProximaAsync()).Should().BeTrue();

        var ida = _rede.Idas.Single();
        ida.Endereco.Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissVerificaElegibilidade"));
        ida.ContentType.Should().Contain("ISO-8859-1");
        var mensagem = Corpo(ida.Corpo);
        mensagem.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        TissWsDeTeste.ValidarNoXsd(mensagem).Should().NotBeEmpty("a mesma mensagem não passaria no XSD 4.01.00: é a versão que mudou");
        (await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync()).Estado.Should().Be(EstadoTransacaoTiss.Concluida);

        _rede.Responder(HttpStatusCode.OK, RespostaElegibilidade("S"));
        await _b.Fila().EnfileirarElegibilidadeAsync(Amil, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);
        await Conector().ProcessarProximaAsync();
        Corpo(_rede.Idas[^1].Corpo).Descendants(Ans + "Padrao").Single().Value.Should().Be("4.01.00", "a elegibilidade da Amil funcionou em 4.01.00");
    }

    [Fact]
    public void Versao_desconhecida_e_recusada_antes_de_montar_a_mensagem()
    {
        var pedido = new PedidoElegibilidadeTiss
        {
            RegistroAnsOperadora = "422631", CodigoPrestadorNaOperadora = "00000000000191", NumeroCarteira = TissBancoDeTeste.CarteiraFicticia,
            Login = "000111", SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5("senha-ficticia"), Sequencial = "1", Momento = new DateTime(2026, 11, 18, 9, 0, 0),
            VersaoTiss = "4.02.00"
        };
        var acao = () => TissElegibilidadeWs.MontarPedido(pedido);
        acao.Should().Throw<ArgumentException>().WithMessage("*4.02.00*");
    }

    // ==================== o arquivo de lote ====================

    [Fact]
    public async Task O_arquivo_de_lote_sai_na_versao_da_operadora_Petrobras_4_03_00_e_as_outras_4_01_00()
    {
        TissExportService.VersaoDoArquivo("422631").Should().Be("4.03.00");
        TissExportService.VersaoDoArquivo("326305").Should().Be("4.01.00", "o arquivo da Amil (segurada) não muda neste PR");
        TissExportService.VersaoDoArquivo("999999").Should().Be("4.01.00");
        TissExportService.VersaoDoArquivo(null).Should().Be("4.01.00");

        var (_, guia) = await _b.GuiaAsync();
        guia.DarBaixa(DateOnly.FromDateTime(_b.Agora), "PB1649", "secretaria", null);
        await _b.Db.SaveChangesAsync();
        var codigo = await new ClinicaRepositorio(_b.NovoContexto()).ObterCodigoAsync(guia.Id);
        var xml = new TissExportService().GerarLoteXml([codigo!],
            new DadosPrestador { CodigoNaOperadora = "00000000000191", RegistroAnsOperadora = "422631", Cnpj = "00000000000191" }, "9");
        var doc = XDocument.Parse(xml);
        doc.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        doc.Descendants(Ans + "guiaConsulta").Should().ContainSingle();
        TissWsDeTeste.ValidarNoXsd(doc.Root!, "4.03.00").Should().BeEmpty();
    }

    // ==================== o teste em produção ====================

    [Fact]
    public async Task testar_elegibilidade_da_Petrobras_roda_em_producao_com_uma_ida_e_so_a_linha_da_trilha()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Petrobras, "000111", "senha-ficticia", TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, RespostaElegibilidade("S"));
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await ComandosDoConector.ExecutarAsync(["testar-elegibilidade", Petrobras, "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op],
            Servicos(), saida, erro)).Should().Be(0, erro.ToString());

        var ida = _rede.Idas.Single();
        ida.Endereco.Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissVerificaElegibilidade"));
        Corpo(ida.Corpo).Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        saida.ToString().Should().Contain("PRODUÇÃO Saúde Petrobras").And.Contain("TISS 4.03.00").And.Contain("Elegível")
            .And.NotContain("senha-ficticia");
        await using var db = _b.NovoContexto();
        (await db.TransacoesTiss.CountAsync()).Should().Be(0, "o teste não passa pela fila");
        var trilha = await db.Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "TesteTissHomologacao");
        trilha.Detalhe.Should().Contain("PRODUÇÃO").And.NotContain(TissBancoDeTeste.CarteiraFicticia);
    }

    [Fact]
    public async Task Em_producao_so_a_elegibilidade_da_operadora_sem_homologacao_roda_lote_protocolo_e_Amil_nao()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Petrobras, "000111", "senha-ficticia", TissBancoDeTeste.Operador);
        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia", TissBancoDeTeste.Operador);
        string[][] recusados =
        [
            ["testar-protocolo", Petrobras, "--protocolo", "13963964", "--operador", Op],
            ["testar-lote", Petrobras, "--exemplo", "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op],
            ["testar-elegibilidade", Amil, "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op],
        ];
        foreach (var args in recusados)
        {
            var (saida, erro) = (new StringWriter(), new StringWriter());
            (await ComandosDoConector.ExecutarAsync(args, Servicos(), saida, erro)).Should().NotBe(0, string.Join(' ', args));
            erro.ToString().Should().Contain("Nada foi enviado", string.Join(' ', args));
        }
        _rede.Idas.Should().BeEmpty();
    }

    // ==================== endereço antigo e situação ====================

    [Fact]
    public async Task Endereco_antigo_cadastrado_vence_o_catalogo_aparece_na_situacao_e_padrao_o_apaga()
    {
        await _b.Credenciais().SalvarAsync(Petrobras, "000111", "senha-ficticia", TissBancoDeTeste.Operador);
        var (saida, erro) = (new StringWriter(), new StringWriter());
        (await ComandosDoConector.ExecutarAsync(["endereco", Petrobras, "elegibilidade", "https://webservices.crc.com.br/tiss/ws/40100/tissVerificaElegibilidade", "--operador", Op],
            Servicos(), saida, erro)).Should().Be(0, erro.ToString());

        (await ComandosDoConector.ExecutarAsync(["situacao", Petrobras], Servicos(), saida, erro)).Should().Be(0);
        saida.ToString().Should().Contain("Elegibilidade: TISS 4.03.00 em https://webservices.crc.com.br/tiss/ws/40100/tissVerificaElegibilidade (CADASTRADO")
            .And.Contain("EnvioGuia: TISS 4.03.00 em https://webservices.crc.com.br/tiss40300/ws/40300/tissLoteGuias (padrão do catálogo)");

        saida = new StringWriter();
        (await ComandosDoConector.ExecutarAsync(["endereco", Petrobras, "todos", "padrao", "--operador", Op], Servicos(), saida, erro)).Should().Be(0, erro.ToString());
        saida.ToString().Should().Contain("voltaram aos endereços do catálogo");
        var credenciais = _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto()));
        (await credenciais.EnderecoCadastradoAsync(Petrobras, TipoTransacaoTiss.Elegibilidade)).Should().BeNull();
        (await credenciais.EnderecoAsync(Petrobras, TipoTransacaoTiss.Elegibilidade))
            .Should().Be(new Uri("https://webservices.crc.com.br/tiss40300/ws/40300/tissVerificaElegibilidade"));
        (await _b.NovoContexto().Auditoria.AsNoTracking().CountAsync(a => a.Acao == "EnderecoTissAlterado" && a.Detalhe!.Contains("padrão do catálogo")))
            .Should().BeGreaterThan(0);
    }

    // ==================== dias 1 a 20: aviso ====================

    [Theory]
    [InlineData(1, null)]
    [InlineData(20, null)]
    [InlineData(21, "01/12")]
    [InlineData(30, "01/12")]
    public void Os_dias_1_a_20_da_Petrobras_sao_aviso(int dia, string? proximo)
    {
        var aviso = PrazosTiss.JanelaDeEnvio(OperadorasTiss.SaudePetrobras, new DateTime(2026, 11, dia, 10, 0, 0));
        if (proximo is null) aviso.Should().BeNull();
        else aviso.Should().Contain("do dia 1 ao dia 20").And.Contain(proximo).And.Contain("não é bloqueado");
    }

    [Fact]
    public async Task Fora_dos_dias_o_lote_da_Petrobras_vai_para_a_fila_na_hora_e_a_conferencia_avisa()
    {
        _b.Agora = new DateTime(2026, 11, 25, 9, 0, 0);
        var (_, guia) = await _b.GuiaAsync();
        var envio = await _b.Fila().EnfileirarEnvioAsync(Petrobras, [guia.Id], TissBancoDeTeste.Operador);
        envio.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        envio.ProximaTentativaEm.Should().BeNull("a fila não segura o lote da Petrobras: é só aviso");
    }

    // ==================== peças ====================

    private ExecutorDeTransacoesTiss Conector()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores = [new ProcessadorDeElegibilidade(repo, parametros, credenciais, () => _b.Agora)];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores, new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = false, Consumidor = "conector-de-teste" }, NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    private IServiceProvider Servicos()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new ServiceCollection()
            .AddSingleton<Clinica.Application.Abstracoes.IClinicaRepositorio>(repo).AddSingleton(new ParametrosService(repo, _b.Protecao))
            .AddSingleton(_b.Credenciais(repo)).AddSingleton(_b.Fila(repo))
            .AddSingleton(new TissWebserviceCliente(new HttpClient(_rede)))
            .AddSingleton(new RelogioDoConector(() => _b.Agora))
            .BuildServiceProvider();
    }

    private static XElement Corpo(string envelope) => XDocument.Parse(envelope).Root!.Element(TissWsDeTeste.Soap + "Body")!.Elements().Single();

    private static string RespostaElegibilidade(string resposta) => $"""
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body>
            <ans:respostaElegibilidadeWS>
              <ans:cabecalho><ans:Padrao>4.03.00</ans:Padrao></ans:cabecalho>
              <ans:respostaElegibilidade><ans:reciboElegibilidade><ans:registroANS>422631</ans:registroANS>
                <ans:numeroCarteira>{TissBancoDeTeste.CarteiraFicticia}</ans:numeroCarteira><ans:nomeBeneficiario>PACIENTE DE TESTE</ans:nomeBeneficiario>
                <ans:respostaSolicitacao>{resposta}</ans:respostaSolicitacao></ans:reciboElegibilidade></ans:respostaElegibilidade>
              <ans:hash>0</ans:hash>
            </ans:respostaElegibilidadeWS>
          </soapenv:Body>
        </soapenv:Envelope>
        """.Trim();

    /// <summary>A "operadora": responde o combinado, na ordem, e anota cada ida (o corpo e o Content-Type).</summary>
    private sealed class Rede : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, string Corpo, string? ContentType);

        private readonly Queue<HttpResponseMessage> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            Idas.Add(new Ida(request.RequestUri!, Encoding.Latin1.GetString(bytes), request.Content.Headers.ContentType?.ToString()));
            return _respostas.Count > 0 ? _respostas.Dequeue() : throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
        }
    }
}
