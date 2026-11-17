using System.Net;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O ENVIO DE GUIAS DA AMIL, pronto e desligado (nov/2026). A Amil recebe a consulta e a sessão em
/// GUIA SP/SADT, com a senha de autorização e o token do beneficiário (Manual do Credenciado jul/2026;
/// Manual de Faturamento Eletrônico v3.1; lista de glosas de bloqueio e alerta da prévia). Aqui: a
/// regra da conferência, o XML no XSD 4.01.00, os prazos como aviso e o lote pelo conector — sobre
/// rede DE MENTIRA. Login, senha, token, carteira e CNPJ são fictícios.
/// </summary>
public sealed class TissAmilGuiaSpSadtTests : IDisposable
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly DateOnly Dia = new(2026, 11, 10);
    private const string Login = "000123";
    private const string Senha = "senha-ficticia-de-teste";

    private readonly TissBancoDeTeste _b = new();

    public void Dispose() => _b.Dispose();

    /// <summary>Uma guia SP/SADT completa da Amil, como o formulário a conferiria (tudo fictício).</summary>
    private static DadosGuiaConsultaAns GuiaAmil() => new()
    {
        Modelo = ModeloDaGuiaTiss.SpSadt,
        RegistroAns = "326305", NumeroCarteira = "000123456789", NomeBeneficiario = "Paciente de Teste",
        CodigoPrestadorNaOperadora = Login, NomeContratado = "Clínica de Teste Ltda", Cnes = "9999999",
        NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33", Cbos = "225105",
        IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = Dia, CodigoTabela = "22",
        TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139.00m,
        Senha = "SENHA0001", DataAutorizacao = Dia, ValidadeSenha = Dia.AddDays(60), CodigoValidacao = "123456",
        TipoAtendimento = DominiosTiss.TipoAtendimentoConsulta, CaraterAtendimento = "1", DescricaoProcedimento = "Consulta em consultório"
    };

    // ==================== o catálogo ====================

    [Fact]
    public void A_Amil_recebe_SP_SADT_com_prazo_de_90_dias_e_janela_do_1o_ao_5o_dia_util_e_fica_desligada()
    {
        OperadorasTiss.Amil.ModeloDaGuia.Should().Be(ModeloDaGuiaTiss.SpSadt);
        OperadorasTiss.Amil.PrazoDeCobrancaDias.Should().Be(90);
        OperadorasTiss.Amil.JanelaDeEnvio.Should().Be(new JanelaDeEnvioTiss(1, 5, new TimeOnly(7, 0), new TimeOnly(19, 0)));
        // Pronta e DESLIGADA: fora das telas de envio e da conferência na baixa.
        OperadorasTiss.Amil.EnvioDeGuiaNasTelas.Should().BeFalse();
        OperadorasTiss.TemEnvioDeGuia(Convenio.Amil).Should().BeFalse();
        GuiaAnsNaBaixa.Exige(TipoCodigo.Consulta, null, Convenio.Amil).Should().BeFalse();
        GuiaAnsNaBaixa.Exige(OperadorasTiss.Amil with { EnvioDeGuiaNasTelas = true }).Should().BeTrue("ligar é uma linha no catálogo");
        // A Saúde Petrobras não muda.
        OperadorasTiss.SaudePetrobras.ModeloDaGuia.Should().Be(ModeloDaGuiaTiss.Consulta);
        OperadorasTiss.SaudePetrobras.PrazoDeCobrancaDias.Should().BeNull();
        GuiaAnsNaBaixa.Exige(TipoCodigo.Consulta, null, Convenio.Petrobras).Should().BeTrue();
    }

    // ==================== a conferência (glosas de bloqueio) ====================

    [Fact]
    public void A_guia_SP_SADT_completa_passa_e_a_consulta_pede_o_tipo_de_consulta()
    {
        GuiaAnsNaBaixa.Validar(GuiaAmil()).Should().BeEmpty();
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { TipoConsulta = "" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.TipoConsulta));
        // Sessão (outras terapias): o tipo de consulta não existe nela.
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { TipoAtendimento = "03", TipoConsulta = "", DescricaoProcedimento = "Sessão de acupuntura" })
            .Should().BeEmpty();
    }

    [Fact]
    public void Sem_senha_sem_confirmacao_ou_com_autorizacao_posterior_a_guia_nao_passa_e_a_frase_diz_a_glosa()
    {
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { Senha = " " })[nameof(DadosGuiaConsultaAns.Senha)].Should().Contain("1403");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CodigoValidacao = null })[nameof(DadosGuiaConsultaAns.FormaConfirmacao)].Should().Contain("3096");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CodigoValidacao = null, FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoToken })
            [nameof(DadosGuiaConsultaAns.CodigoValidacao)].Should().Contain("3096");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CodigoValidacao = "12345" })[nameof(DadosGuiaConsultaAns.CodigoValidacao)].Should().Contain("6 dígitos");
        // Por token, o motivo de ausência digitado por engano não vai (a forma escolhida manda).
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { AusenciaCodigoValidacao = "04" }).Should().BeEmpty();
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { DataAutorizacao = Dia.AddDays(1) })[nameof(DadosGuiaConsultaAns.DataAutorizacao)].Should().Contain("1814");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { DataAutorizacao = null }).Should().ContainKey(nameof(DadosGuiaConsultaAns.DataAutorizacao));
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { TipoAtendimento = "05" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.TipoAtendimento), "05 foi inativado na 4.00");
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { CaraterAtendimento = null }).Should().ContainKey(nameof(DadosGuiaConsultaAns.CaraterAtendimento));
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { DescricaoProcedimento = "" }).Should().ContainKey(nameof(DadosGuiaConsultaAns.DescricaoProcedimento));
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { NomeContratado = null }).Should().ContainKey(nameof(DadosGuiaConsultaAns.NomeContratado));
        // Sem confirmação: o motivo E a declaração dos anexos no portal (a 991 é BLOQUEIO, não alerta).
        var semToken = GuiaAmil() with { CodigoValidacao = null, AusenciaCodigoValidacao = "07", FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoNenhuma };
        GuiaAnsNaBaixa.Validar(semToken)[nameof(DadosGuiaConsultaAns.AnexosNoPortal)].Should().Contain("991");
        GuiaAnsNaBaixa.Validar(semToken with { AnexosNoPortal = true }).Should().BeEmpty();
        GuiaAnsNaBaixa.Alertas(GuiaAmil() with { ValidadeSenha = Dia.AddDays(-1) }).Should().ContainSingle(a => a.Contains("1405"));
        GuiaAnsNaBaixa.Alertas(GuiaAmil()).Should().BeEmpty();
        // A guia de consulta (Petrobras) não ganha nenhuma dessas exigências.
        GuiaAnsNaBaixa.Validar(GuiaAmil() with { Modelo = ModeloDaGuiaTiss.Consulta, Senha = null, CodigoValidacao = null, DataAutorizacao = null })
            .Should().BeEmpty();
    }

    [Fact]
    public void Gravar_e_reler_preserva_os_campos_da_SP_SADT_e_a_de_consulta_os_deixa_nulos()
    {
        var codigo = new CodigoFaturamento { Id = 7 };
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil(), "faturista.teste", new DateTime(2026, 11, 10, 9, 0, 0));
        codigo.GuiaAns!.ModeloGuia.Should().Be(GuiaAnsNaBaixa.ModeloGuiaSpSadt);
        GuiaAnsNaBaixa.DaGuia(codigo.GuiaAns).Should().BeEquivalentTo(GuiaAmil() with { FormaConfirmacao = GuiaAnsNaBaixa.ConfirmacaoToken }, o => o
            .Excluding(d => d.ValidadeCarteira).Excluding(d => d.NomeSocial).Excluding(d => d.Observacao));

        // Refeita como consulta (catálogo mudou): os campos da SP/SADT saem, nada fica pela metade.
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil() with { Modelo = ModeloDaGuiaTiss.Consulta }, "faturista.teste", DateTime.Now);
        codigo.GuiaAns.ModeloGuia.Should().BeNull();
        codigo.GuiaAns.Senha.Should().BeNull();
        codigo.GuiaAns.CodigoValidacao.Should().BeNull();
    }

    [Fact]
    public void O_formulario_mostra_o_tipo_de_consulta_so_quando_o_atendimento_e_consulta()
    {
        var edicao = GuiaAnsEmEdicao.De(GuiaAmil());
        edicao.EhSpSadt.Should().BeTrue();
        edicao.PedeTipoConsulta.Should().BeTrue();
        edicao.TipoAtendimento = "03";
        edicao.PedeTipoConsulta.Should().BeFalse();
        edicao.CodigoValidacao = "";
        edicao.Erros().Should().ContainKey(nameof(DadosGuiaConsultaAns.CodigoValidacao));
        GuiaAnsEmEdicao.De(GuiaAmil() with { Modelo = ModeloDaGuiaTiss.Consulta }).PedeTipoConsulta.Should().BeTrue();
    }

    // ==================== o XML (XSD 4.01.00) ====================

    [Theory]
    [InlineData("123456", null, "04")]
    [InlineData(null, "07", "03")]
    public void A_guia_SP_SADT_conferida_sai_no_lote_valida_no_XSD_com_senha_e_token_no_lugar_do_padrao(
        string? token, string? ausencia, string tipoAtendimento)
    {
        var codigo = new CodigoFaturamento { Id = 99 };
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil() with
        {
            CodigoValidacao = token, AusenciaCodigoValidacao = ausencia, TipoAtendimento = tipoAtendimento,
            TipoConsulta = tipoAtendimento == "04" ? "1" : ""
        }, "faturista.teste", DateTime.Now);

        var guia = TissExportService.GuiaConferida(codigo, NumeroGuiaPrestadorTiss.Gerar(99));
        guia.Name.Should().Be(Ans + "guiaSP-SADT");
        var mensagem = TissLoteGuiasWs.MontarPedido(new PedidoLoteGuiasTiss
        {
            Remetente = new RemetenteTissWs { RegistroAnsOperadora = "326305", CodigoPrestadorNaOperadora = Login, Login = Login, SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5(Senha), VersaoTiss = "4.03.00" },
            Sequencial = "000000001001", Momento = new DateTime(2026, 11, 10, 9, 0, 0), NumeroLote = "900000000099", Guias = [guia]
        });
        // A Amil recebe o lote em 4.03.00 (o XSD oficial dela); a guia é a mesma estrutura da 4.01.00.
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");

        var autorizacao = guia.Element(Ans + "dadosAutorizacao")!;
        autorizacao.Element(Ans + "senha")!.Value.Should().Be("SENHA0001");
        if (token is not null) autorizacao.Element(Ans + "codValidacao")!.Value.Should().Be(token);
        else autorizacao.Element(Ans + "ausenciaCodValidacao")!.Value.Should().Be(ausencia);
        guia.Descendants(Ans + "tipoConsulta").Any().Should().Be(tipoAtendimento == "04");
        guia.Descendants(Ans + "valorTotalGeral").Single().Value.Should().Be("139.00");
    }

    [Fact]
    public void A_guia_de_consulta_da_Petrobras_continua_saindo_igual()
    {
        var guia = TissWsDeTeste.GuiaConsulta();
        guia.Name.Should().Be(Ans + "guiaConsulta");
        TissExportService.GuiaConferida(TissWsDeTeste.CodigoConferido(), NumeroGuiaPrestadorTiss.Gerar(1234)).ToString()
            .Should().Be(guia.ToString());
    }

    [Fact]
    public void O_arquivo_de_lote_para_subir_no_SisAmil_leva_a_guia_SP_SADT_conferida()
    {
        var codigo = new CodigoFaturamento { Id = 5, AtendimentoId = 1, Status = StatusCodigo.Baixado, DataBaixa = Dia, Tipo = TipoCodigo.Consulta };
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil(), "faturista.teste", DateTime.Now);
        var xml = new TissExportService().GerarLoteXml([codigo], new DadosPrestador { RegistroAnsOperadora = "326305", CodigoNaOperadora = Login }, "1");
        var doc = XDocument.Parse(xml);
        doc.Descendants(Ans + "guiaSP-SADT").Should().ContainSingle();
        doc.Descendants(Ans + "guiaConsulta").Should().BeEmpty();
        doc.Descendants(Ans + "codValidacao").Single().Value.Should().Be("123456");
    }

    // ==================== os prazos: aviso, nunca bloqueio ====================

    [Theory]
    [InlineData(2026, 11, 10, null)]          // atendido hoje: longe do prazo
    [InlineData(2026, 8, 25, "Faltam 13")]    // 77 dias: aviso (prazo em 23/11)
    [InlineData(2026, 8, 10, "Passou o prazo")] // 98 dias: venceu (glosa 1701)
    public void O_prazo_de_90_dias_avisa_perto_do_fim_e_depois_dele(int ano, int mes, int dia, string? trecho)
    {
        var aviso = PrazosTiss.PrazoDeCobranca(OperadorasTiss.Amil, new DateOnly(ano, mes, dia), Dia);
        if (trecho is null) aviso.Should().BeNull();
        else aviso.Should().Contain(trecho).And.Contain("Amil");
        PrazosTiss.PrazoDeCobranca(OperadorasTiss.SaudePetrobras, new DateOnly(2026, 1, 1), Dia).Should().BeNull("a Petrobras não tem prazo no catálogo");
    }

    [Fact]
    public void A_janela_do_1o_ao_5o_dia_util_das_7h_as_19h()
    {
        // Nov/2026: dia 2 é segunda-feira (1º dia útil); dia 6 é o 5º; dia 9 é o 6º.
        // Nov/2026: dia 2 (segunda) é Finados — o 1º dia útil é o 3, e o 5º é o 9.
        PrazosTiss.DiaUtilDoMes(new DateOnly(2026, 11, 3)).Should().Be(1);
        PrazosTiss.DiaUtilDoMes(new DateOnly(2026, 11, 9)).Should().Be(5);
        PrazosTiss.DiaUtilDoMes(new DateOnly(2026, 11, 1)).Should().BeNull("domingo");
        PrazosTiss.JanelaDeEnvio(OperadorasTiss.Amil, new DateTime(2026, 11, 3, 10, 0, 0)).Should().BeNull();
        PrazosTiss.JanelaDeEnvio(OperadorasTiss.Amil, new DateTime(2026, 11, 3, 19, 30, 0)).Should().Contain("das 07:00 às 19:00");
        PrazosTiss.JanelaDeEnvio(OperadorasTiss.Amil, new DateTime(2026, 11, 10, 10, 0, 0)).Should().Contain("6º dia útil");
        PrazosTiss.JanelaDeEnvio(OperadorasTiss.SaudePetrobras, new DateTime(2026, 11, 9, 23, 0, 0)).Should().BeNull();
    }

    // ==================== o lote da Amil pela fila e pelo conector ====================

    [Fact]
    public async Task A_conferencia_do_lote_da_Amil_avisa_a_janela_e_o_prazo_sem_impedir_o_envio()
    {
        _b.Agora = new DateTime(2026, 11, 10, 10, 0, 0); // 6º dia útil (dia 2 é Finados): fora da janela
        var (_, guia) = await GuiaAmilNoBancoAsync(atendidoEm: new DateOnly(2026, 8, 20));

        var conferencia = await _b.Fila().ConferirEnvioAsync("Amil", [guia.Id]);

        conferencia.PodeEnviar.Should().BeTrue("prazo é aviso, não bloqueio");
        conferencia.Avisos.Should().Contain(a => a.Contains("1º ao 5º dia útil"));
        // Atendida em 20/08: vence em 18/11, ANTES da próxima janela (01/12) — aviso GRAVE (vermelho).
        conferencia.AvisosPorGuia[guia.Id].Should().Contain("vence em 18/11/2026").And.Contain("ANTES da próxima janela");
        conferencia.PrazoGrave.Should().Contain(guia.Id);
        (await _b.Fila().ConferirEnvioAsync(TissBancoDeTeste.Petrobras, [(await _b.GuiaAsync()).Guia.Id])).Avisos.Should().BeEmpty();
    }

    [Fact]
    public async Task O_lote_da_Amil_vai_em_SP_SADT_UTF_8_ao_endereco_APIM_e_nao_sai_com_o_envio_do_convenio_desligado()
    {
        _b.Agora = new DateTime(2026, 11, 3, 10, 0, 0); // dentro da janela da Amil (2º dia útil)
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync("Amil", Login, Senha, TissBancoDeTeste.Operador);
        var (_, guia) = await GuiaAmilNoBancoAsync(atendidoEm: DateOnly.FromDateTime(_b.Agora));
        var rede = new Rede();

        // Desligado (o estado de hoje): nada sai.
        await _b.Fila().EnfileirarEnvioAsync("Amil", [guia.Id], TissBancoDeTeste.Operador);
        await Conector(rede, envioNoServidor: true).ProcessarProximaAsync();
        rede.Idas.Should().BeEmpty();

        // Ligado só NO TESTE, contra a rede de mentira: a mensagem que sairia.
        await _b.Credenciais().DefinirEnvioAsync("Amil", true, TissBancoDeTeste.Operador);
        await _b.Fila().EnfileirarEnvioAsync("Amil", [guia.Id], TissBancoDeTeste.Operador);
        await Conector(rede, envioNoServidor: true).ProcessarProximaAsync();

        var ida = rede.Idas.Should().ContainSingle().Subject;
        ida.Endereco.Should().Be(OperadorasTiss.Amil.Endereco(TipoTransacaoTiss.EnvioGuia));
        ida.ContentType.Should().Be("text/xml; charset=utf-8");
        using var leitor = System.Xml.XmlReader.Create(new MemoryStream(ida.Bytes));
        var mensagem = XDocument.Load(leitor).Root!.Element(Soap + "Body")!.Elements().Single();
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        mensagem.Descendants(Ans + "guiaSP-SADT").Should().ContainSingle();
        mensagem.Descendants(Ans + "registroANS").First().Value.Should().Be("326305");
    }

    // ==================== a versão do padrão por operadora ====================

    [Fact]
    public void O_lote_da_Petrobras_continua_em_4_01_00_byte_a_byte_e_versao_desconhecida_e_recusada()
    {
        var pedido = new PedidoLoteGuiasTiss
        {
            Remetente = TissWsDeTeste.Remetente(), Sequencial = TissWsDeTeste.Sequencial, Momento = TissWsDeTeste.Momento,
            NumeroLote = "42", Guias = [TissWsDeTeste.GuiaConsulta()]
        };
        var padrao = TissLoteGuiasWs.MontarEnvelope(pedido);
        // Dizer 4.01.00 explicitamente dá EXATAMENTE a mensagem de antes (sem versão dita).
        TissLoteGuiasWs.MontarEnvelope(pedido with { Remetente = TissWsDeTeste.Remetente() with { VersaoTiss = "4.01.00" } })
            .Should().Be(padrao);
        XDocument.Parse(padrao).Descendants(Ans + "Padrao").Single().Value.Should().Be("4.01.00");
        TissWsDeTeste.ValidarNoXsd(TissLoteGuiasWs.MontarPedido(pedido)).Should().BeEmpty();

        var desconhecida = () => TissLoteGuiasWs.MontarPedido(pedido with { Remetente = TissWsDeTeste.Remetente() with { VersaoTiss = "4.02.00" } });
        desconhecida.Should().Throw<ArgumentException>().WithMessage("*4.02.00*");
    }

    [Fact]
    public void A_mensagem_da_Amil_em_4_03_00_nao_passaria_no_XSD_4_01_00()
    {
        // Prova de que a validação 4.03.00 não é de enfeite: o XSD 4.01.00 recusa "4.03.00" no Padrao.
        var codigo = new CodigoFaturamento { Id = 98 };
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil(), "faturista.teste", DateTime.Now);
        var mensagem = TissLoteGuiasWs.MontarPedido(new PedidoLoteGuiasTiss
        {
            Remetente = new RemetenteTissWs { RegistroAnsOperadora = "326305", CodigoPrestadorNaOperadora = Login, Login = Login, SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5(Senha), VersaoTiss = "4.03.00" },
            Sequencial = "000000001001", Momento = new DateTime(2026, 11, 10, 9, 0, 0), NumeroLote = "900000000098",
            Guias = [TissExportService.GuiaConferida(codigo, NumeroGuiaPrestadorTiss.Gerar(98))]
        });
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.01.00").Should().NotBeEmpty();
    }

    // ==================== peças ====================

    private async Task<(Paciente, CodigoFaturamento)> GuiaAmilNoBancoAsync(DateOnly atendidoEm)
    {
        var (paciente, guia) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        var db = _b.NovoContexto();
        var codigo = await db.Codigos.SingleAsync(c => c.Id == guia.Id);
        GuiaAnsNaBaixa.Aplicar(codigo, GuiaAmil() with { DataAtendimento = atendidoEm, DataAutorizacao = atendidoEm, ValidadeSenha = null },
            TissBancoDeTeste.Operador, _b.Agora);
        await db.SaveChangesAsync();
        return (paciente, codigo);
    }

    private ExecutorDeTransacoesTiss Conector(Rede rede, bool envioNoServidor)
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores = [new ProcessadorDeEnvioDeGuia(repo, parametros, credenciais, () => _b.Agora)];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores,
            new TissWebserviceCliente(new HttpClient(rede)),
            new OpcoesDoConector { EnvioHabilitado = envioNoServidor, Consumidor = "conector-de-teste" },
            NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    /// <summary>A "operadora": responde 500 sem corpo (o desfecho não importa aqui) e anota cada ida.</summary>
    private sealed class Rede : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, byte[] Bytes, string? ContentType);
        public List<Ida> Idas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Idas.Add(new Ida(request.RequestUri!, await request.Content!.ReadAsByteArrayAsync(ct), request.Content.Headers.ContentType?.ToString()));
            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("")) };
        }
    }
}
