using System.Net;
using System.Text;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O conector do web service TISS (nov/2026), com a REDE DE MENTIRA: nenhuma chamada sai da
/// máquina. A volta inteira é a de produção — fila, credencial cifrada, processador, transporte
/// HTTP e gravação do desfecho; só o último elo (o servidor da operadora) é um manipulador
/// de teste. Login, senha, carteira e CNPJ são fictícios.
/// </summary>
public sealed class ConectorTissTests : IDisposable
{
    private const string Login = "LOGINTESTE";
    private const string Senha = "senha-ficticia-de-teste";
    private static readonly string Md5 = TissElegibilidadeWs.SenhaEmMd5(Senha);

    private readonly TissBancoDeTeste _b = new();
    private readonly RedeDeMentira _rede = new();
    private readonly RegistroDeLog _log = new();

    public void Dispose() => _b.Dispose();

    // ---------- montagem ----------

    private ExecutorDeTransacoesTiss Conector(bool envioNoServidor = false, IProcessadorTransacaoTiss? processadorExtra = null)
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var processadores = new List<IProcessadorTransacaoTiss>
        {
            new ProcessadorDeElegibilidade(repo, new ParametrosService(repo, _b.Protecao), credenciais, () => _b.Agora),
            new ProcessadorDeEnvioDeGuia(), new ProcessadorDeStatusDeProtocolo(), new ProcessadorDeCancelamentoDeGuia()
        };
        if (processadorExtra is not null) processadores.Insert(0, processadorExtra);
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores,
            new TransporteTissHttp(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = envioNoServidor, Consumidor = "conector-de-teste" }, _log.Para<ExecutorDeTransacoesTiss>());
    }

    private async Task ProntoParaElegibilidadeAsync()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);
    }

    private Task<TransacaoTiss> PedirElegibilidadeAsync()
        => _b.Fila().EnfileirarElegibilidadeAsync(TissBancoDeTeste.Petrobras, TissBancoDeTeste.CarteiraFicticia, new DateOnly(2027, 12, 31), null, TissBancoDeTeste.Operador);

    private Task<TransacaoTiss> GravadaAsync(int id) => _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(t => t.Id == id);

    private static string Resposta(string miolo) => $"""
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body>
            <ans:respostaElegibilidadeWS>
              <ans:cabecalho><ans:Padrao>4.01.00</ans:Padrao></ans:cabecalho>
              <ans:respostaElegibilidade>{miolo}</ans:respostaElegibilidade>
              <ans:hash>0</ans:hash>
            </ans:respostaElegibilidadeWS>
          </soapenv:Body>
        </soapenv:Envelope>
        """.Trim();

    private static string Recibo(string resposta) => Resposta($"""
        <ans:reciboElegibilidade><ans:registroANS>422631</ans:registroANS><ans:numeroCarteira>{TissBancoDeTeste.CarteiraFicticia}</ans:numeroCarteira>
        <ans:nomeBeneficiario>JOSÉ DE TESTE</ans:nomeBeneficiario><ans:validadeCarteira>2027-12-31</ans:validadeCarteira>
        <ans:respostaSolicitacao>{resposta}</ans:respostaSolicitacao></ans:reciboElegibilidade>
        """);

    private static string FalhaTiss(string falha) => $"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body><soapenv:Fault><faultcode>soapenv:Server</faultcode><faultstring>erro</faultstring>
            <detail><ans:tissFaultWS><ans:tissFault>{falha}</ans:tissFault></ans:tissFaultWS></detail>
          </soapenv:Fault></soapenv:Body>
        </soapenv:Envelope>
        """;

    // ---------- elegibilidade, ponta a ponta ----------

    [Fact]
    public async Task Elegibilidade_ponta_a_ponta_enfileira_processa_e_grava_o_resultado()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        var pedido = await PedirElegibilidadeAsync();

        (await Conector().ProcessarProximaAsync()).Should().BeTrue();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        t.Resultado.Should().Be("Elegível. Validade da carteira: 31/12/2027.");
        t.Tentativas.Should().Be(1);
        t.EnvioIniciadoEm.Should().NotBeNull();
        t.ProcessadaEm.Should().NotBeNull();
        t.Erro.Should().BeNull();
        // O nome do beneficiário fica só na resposta guardada, não na frase da tela.
        t.Resultado.Should().NotContain("JOSÉ");
        t.XmlRecebido.Should().Contain("JOSÉ DE TESTE");

        // O que saiu para a "operadora": o endereço do manual, por HTTPS, em ISO-8859-1, com o MD5.
        var ida = _rede.Idas.Should().ContainSingle().Subject;
        ida.Endereco.Should().Be(TissWebserviceCliente.ElegibilidadeSaudePetrobras);
        ida.TipoDeConteudo.Should().Be("text/xml; charset=ISO-8859-1");
        ida.Corpo.Should().Contain("<ans:tipoTransacao>VERIFICA_ELEGIBILIDADE</ans:tipoTransacao>")
            .And.Contain($"<ans:numeroCarteira>{TissBancoDeTeste.CarteiraFicticia}</ans:numeroCarteira>")
            .And.Contain("<ans:codigoPrestadorNaOperadora>00000000000191</ans:codigoPrestadorNaOperadora>")
            .And.Contain("<ans:registroANS>422631</ans:registroANS>")
            .And.Contain($"<ans:senhaPrestador>{Md5}</ans:senhaPrestador>");
        // E o que ficou guardado: a mesma mensagem, sem a senha.
        t.XmlEnviado.Should().Contain("VERIFICA_ELEGIBILIDADE").And.Contain(Login).And.NotContain(Md5).And.NotContain(Senha);
    }

    [Fact]
    public async Task Carteira_negada_pela_operadora_e_resultado_e_nao_falha()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Responder(HttpStatusCode.OK, Recibo("N"));
        var pedido = await PedirElegibilidadeAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        t.Resultado.Should().StartWith("Não elegível");
    }

    [Fact]
    public async Task Login_recusado_pela_operadora_falha_de_vez_sem_nova_tentativa()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Responder(HttpStatusCode.InternalServerError, FalhaTiss("LoginInvalido"));
        var pedido = await PedirElegibilidadeAsync();
        var conector = Conector();

        await conector.ProcessarProximaAsync();
        _b.Agora = _b.Agora.AddHours(1);
        (await conector.ProcessarProximaAsync()).Should().BeFalse("não há o que repetir");

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        t.Erro.Should().Contain("LoginInvalido");
        t.XmlRecebido.Should().Contain("LoginInvalido");
        _rede.Idas.Should().HaveCount(1, "login recusado repetido pode bloquear o acesso da clínica");
    }

    [Fact]
    public async Task Rede_fora_do_ar_tenta_de_novo_depois_da_espera_e_conclui_quando_a_operadora_volta()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Cair();
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        var pedido = await PedirElegibilidadeAsync();
        var conector = Conector();

        await conector.ProcessarProximaAsync();
        (await GravadaAsync(pedido.Id)).Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        (await conector.ProcessarProximaAsync()).Should().BeFalse("ainda dentro da espera");

        _b.Agora = _b.Agora.AddMinutes(1);
        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        t.Tentativas.Should().Be(2);
        _rede.Idas.Should().HaveCount(2);
        // Cada tentativa leva um sequencial próprio.
        _rede.Idas.Select(i => i.Sequencial).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Rede_fora_do_ar_tres_vezes_desiste_e_pede_que_alguem_olhe()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Cair(); _rede.Cair(); _rede.Cair();
        var pedido = await PedirElegibilidadeAsync();

        for (var i = 0; i < 3; i++)
        {
            await Conector().ProcessarProximaAsync();
            _b.Agora = _b.Agora.AddMinutes(10);
        }

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        t.Tentativas.Should().Be(3);
        (await Conector().ProcessarProximaAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Resposta_vazia_da_operadora_conta_como_sem_resposta()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Responder(HttpStatusCode.BadGateway, "");
        var pedido = await PedirElegibilidadeAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        t.Erro.Should().Contain("HTTP 502");
    }

    [Fact]
    public async Task Sem_credencial_cadastrada_o_conector_recusa_e_nada_sai()
    {
        await _b.PrestadorAsync();
        var pedido = await PedirElegibilidadeAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Contain("Não há login");
        t.EnvioIniciadoEm.Should().BeNull();
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sem_cnpj_da_clinica_o_conector_recusa_e_diz_onde_conferir()
    {
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);
        var pedido = await PedirElegibilidadeAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Contain("registro ANS");
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task Convenio_sem_endereco_cadastrado_nao_vai_a_lugar_nenhum()
    {
        // Só a elegibilidade da Petrobras tem endereço conhecido pelo código (o do manual dela).
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync("Amil", Login, Senha, TissBancoDeTeste.Operador);
        var pedido = await _b.Fila().EnfileirarElegibilidadeAsync("Amil", TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Contain("endereço");
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task Fila_vazia_devolve_falso_e_nao_toca_a_rede()
    {
        (await Conector().ProcessarProximaAsync()).Should().BeFalse();
        _rede.Idas.Should().BeEmpty();
    }

    // ---------- as duas chaves do envio ----------

    [Theory]
    [InlineData(TipoTransacaoTiss.EnvioGuia)]
    [InlineData(TipoTransacaoTiss.CancelamentoGuia)]
    public async Task Com_a_chave_do_servidor_desligada_o_envio_e_recusado_com_frase_clara(TipoTransacaoTiss tipo)
    {
        await ProntoParaElegibilidadeAsync();
        await _b.Credenciais().DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        var pedido = await PedirEscritaAsync(tipo);

        await Conector(envioNoServidor: false).ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Be(ExecutorDeTransacoesTiss.FraseEnvioDesligadoNoServidor);
        t.EnvioIniciadoEm.Should().BeNull();
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task Com_a_chave_do_convenio_desligada_que_e_o_padrao_o_envio_e_recusado()
    {
        await ProntoParaElegibilidadeAsync();
        var pedido = await PedirEscritaAsync(TipoTransacaoTiss.EnvioGuia);

        await Conector(envioNoServidor: true).ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        t.Resultado.Should().Be(ExecutorDeTransacoesTiss.FraseEnvioDesligadoNoConvenio(TissBancoDeTeste.Petrobras));
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task A_chave_de_envio_nao_atrapalha_quem_so_pergunta()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        var pedido = await PedirElegibilidadeAsync();

        await Conector(envioNoServidor: false).ProcessarProximaAsync();

        (await GravadaAsync(pedido.Id)).Estado.Should().Be(EstadoTransacaoTiss.Concluida);
    }

    // ---------- o que ainda não foi ligado ----------

    [Theory]
    [InlineData(TipoTransacaoTiss.EnvioGuia)]
    [InlineData(TipoTransacaoTiss.CancelamentoGuia)]
    [InlineData(TipoTransacaoTiss.StatusProtocolo)]
    public async Task Envio_status_e_cancelamento_respondem_ainda_nao_ligado_e_nada_sai(TipoTransacaoTiss tipo)
    {
        // TODO(tiss-b-mensagens): quando os processadores de verdade entrarem, este teste dá lugar
        // aos de ponta a ponta de cada tipo. Até lá ele fixa que NADA sai para a operadora.
        await ProntoParaElegibilidadeAsync();
        await _b.Credenciais().DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        var pedido = tipo == TipoTransacaoTiss.StatusProtocolo
            ? await _b.Fila().EnfileirarStatusProtocoloAsync(TissBancoDeTeste.Petrobras, "987654", null, TissBancoDeTeste.Operador)
            : await PedirEscritaAsync(tipo);

        await Conector(envioNoServidor: true).ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        t.Erro.Should().Be(ProcessadorAindaNaoLigado.Frase(tipo)).And.Contain("ainda não foi ligado");
        t.EnvioIniciadoEm.Should().BeNull();
        _rede.Idas.Should().BeEmpty();
        _b.Agora = _b.Agora.AddHours(1);
        (await Conector(envioNoServidor: true).ProcessarProximaAsync()).Should().BeFalse("não volta para a fila");
    }

    private async Task<TransacaoTiss> PedirEscritaAsync(TipoTransacaoTiss tipo)
    {
        var (paciente, guia) = await _b.GuiaAsync();
        return tipo == TipoTransacaoTiss.EnvioGuia
            ? await _b.Fila().EnfileirarEnvioGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, paciente.Id, TissBancoDeTeste.Operador)
            : await _b.Fila().EnfileirarCancelamentoGuiaAsync(TissBancoDeTeste.Petrobras, guia.Id, "G123", "987654", TissBancoDeTeste.Operador);
    }

    // ---------- a regra dura, pelo conector: envio de guia não se repete ----------

    [Fact]
    public async Task Envio_de_guia_que_ficou_sem_resposta_da_rede_nao_e_reenviado_pelo_conector()
    {
        await ProntoParaElegibilidadeAsync();
        await _b.Credenciais().DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        var pedido = await PedirEscritaAsync(TipoTransacaoTiss.EnvioGuia);
        var envio = new ProcessadorDeMentira(TipoTransacaoTiss.EnvioGuia, Md5);
        _rede.Cair();
        _rede.Responder(HttpStatusCode.OK, "<recebido/>"); // se houvesse segunda ida, ela "daria certo"

        await Conector(envioNoServidor: true, envio).ProcessarProximaAsync();
        _b.Agora = _b.Agora.AddDays(1);
        var deNovo = await Conector(envioNoServidor: true, envio).ProcessarProximaAsync();

        deNovo.Should().BeFalse();
        _rede.Idas.Should().HaveCount(1, "a mensagem pode ter chegado: reenviar é arriscar a guia em duplicidade");
        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        t.Erro.Should().Contain("não é reenviada sozinha");
    }

    [Fact]
    public async Task Envio_de_guia_recebido_grava_protocolo_e_numero_e_nao_da_baixa_na_guia()
    {
        await ProntoParaElegibilidadeAsync();
        await _b.Credenciais().DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        var pedido = await PedirEscritaAsync(TipoTransacaoTiss.EnvioGuia);
        _rede.Responder(HttpStatusCode.OK, "<recebido/>");

        await Conector(envioNoServidor: true, new ProcessadorDeMentira(TipoTransacaoTiss.EnvioGuia, Md5)).ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        t.Protocolo.Should().Be("987654");
        t.NumeroGuiaOperadora.Should().Be("OP123");
        t.XmlRecebido.Should().Be("<recebido/>");
        t.XmlEnviado.Should().NotContain(Md5);
        var guia = await _b.NovoContexto().Codigos.AsNoTracking().SingleAsync();
        guia.Status.Should().Be(Clinica.Domain.StatusCodigo.Aberto, "ligar o protocolo à baixa é da tela, não do conector");
        guia.NumeroGuiaReal.Should().BeNull();
    }

    // ---------- a senha nunca vaza ----------

    [Fact]
    public async Task Nem_o_log_nem_a_linha_da_fila_levam_senha_login_ou_carteira_em_nenhum_desfecho()
    {
        await ProntoParaElegibilidadeAsync();
        _rede.Responder(HttpStatusCode.OK, Recibo("S"));
        _rede.Responder(HttpStatusCode.InternalServerError, FalhaTiss("LoginInvalido"));
        _rede.Cair();
        for (var i = 0; i < 3; i++)
        {
            await PedirElegibilidadeAsync();
            _b.Agora = _b.Agora.AddSeconds(1);
        }

        for (var i = 0; i < 3; i++) await Conector().ProcessarProximaAsync();

        _log.Linhas.Should().NotBeEmpty();
        foreach (var linha in _log.Linhas)
            linha.Should().NotContain(Md5).And.NotContain(Senha).And.NotContain(Login).And.NotContain(TissBancoDeTeste.CarteiraFicticia).And.NotContain("JOSÉ");
        foreach (var t in await _b.NovoContexto().TransacoesTiss.AsNoTracking().ToListAsync())
        {
            (t.Erro ?? "").Should().NotContain(Md5).And.NotContain(Senha);
            (t.Resultado ?? "").Should().NotContain(Md5).And.NotContain(Senha);
            (t.XmlEnviado ?? "").Should().NotContain(Md5).And.NotContain(Senha);
            (t.XmlRecebido ?? "").Should().NotContain(Md5).And.NotContain(Senha);
        }
        var trilha = await _b.NovoContexto().Auditoria.AsNoTracking().ToListAsync();
        trilha.Should().OnlyContain(a => !(a.Detalhe ?? "").Contains(Md5) && !(a.Detalhe ?? "").Contains(Senha));
    }

    [Fact]
    public async Task Excecao_de_rede_que_repete_o_pedido_na_mensagem_nao_leva_a_senha_para_a_fila_nem_para_o_log()
    {
        await ProntoParaElegibilidadeAsync();
        // O pior caso: a biblioteca de rede devolve o corpo do pedido (com o MD5) na mensagem do erro.
        _rede.Cair(corpoDoPedidoNaMensagem: true);
        var pedido = await PedirElegibilidadeAsync();

        await Conector().ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Erro.Should().Contain("sem resposta da operadora").And.NotContain(Md5);
        _log.Linhas.Should().OnlyContain(l => !l.Contains(Md5));
    }

    [Fact]
    public async Task Processador_que_estoura_vira_falha_com_o_tipo_da_excecao_e_sem_a_mensagem_dela()
    {
        await ProntoParaElegibilidadeAsync();
        var pedido = await PedirElegibilidadeAsync();
        var estoura = new ProcessadorDeMentira(TipoTransacaoTiss.Elegibilidade, Md5) { EstourarCom = new FormatException($"valor inesperado: {Md5}") };

        await Conector(processadorExtra: estoura).ProcessarProximaAsync();

        var t = await GravadaAsync(pedido.Id);
        t.Erro.Should().Contain("FormatException").And.NotContain(Md5);
        _log.Linhas.Should().OnlyContain(l => !l.Contains(Md5));
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public void O_executor_troca_por_asteriscos_o_segredo_que_escapar_para_um_texto()
        => ExecutorDeTransacoesTiss.Ocultar($"erro com {Md5.ToUpperInvariant()} no meio", [Md5])
            .Should().Be("erro com ******** no meio");

    [Fact]
    public async Task O_transporte_recusa_endereco_que_nao_e_https()
    {
        var enviar = () => new TransporteTissHttp(new HttpClient(_rede)).EnviarAsync(new Uri("http://exemplo.invalid/tiss"), "<a/>", default);

        await enviar.Should().ThrowAsync<InvalidOperationException>();
        _rede.Idas.Should().BeEmpty();
    }

    // ---------- os comandos de quem opera o servidor ----------

    [Fact]
    public async Task O_comando_de_credencial_grava_cifrado_e_nao_mostra_a_senha()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());

        var codigo = await ComandosDoConector.ExecutarAsync(["credencial", TissBancoDeTeste.Petrobras, "--operador", "operador.servidor"],
            Servicos(), saida, erro, ambiente: v => v == ComandosDoConector.VariavelLogin ? Login : v == ComandosDoConector.VariavelSenha ? Senha : null);

        codigo.Should().Be(0);
        (saida.ToString() + erro).Should().NotContain(Senha).And.NotContain(Md5);
        (await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).AbrirParaEnvioAsync(TissBancoDeTeste.Petrobras))!.SenhaMd5.Should().Be(Md5);
        (await _b.NovoContexto().Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "CredencialTissAlterada")).Operador.Should().Be("operador.servidor");
    }

    [Fact]
    public async Task O_comando_de_credencial_le_a_senha_da_entrada_e_recusa_quando_falta_o_operador()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());
        string? Ambiente(string v) => v == ComandosDoConector.VariavelLogin ? Login : null;

        var semOperador = await ComandosDoConector.ExecutarAsync(["credencial", TissBancoDeTeste.Petrobras, "--senha-na-entrada"],
            Servicos(), saida, erro, new StringReader(Senha + "\n"), Ambiente);
        var comOperador = await ComandosDoConector.ExecutarAsync(["credencial", TissBancoDeTeste.Petrobras, "--senha-na-entrada", "--operador", "operador.servidor"],
            Servicos(), saida, erro, new StringReader(Senha + "\n"), Ambiente);

        semOperador.Should().Be(1);
        comOperador.Should().Be(0);
        (saida.ToString() + erro).Should().NotContain(Senha).And.NotContain(Md5);
    }

    [Fact]
    public async Task Os_comandos_ligam_o_envio_gravam_o_endereco_e_mostram_a_situacao_sem_dado_de_paciente()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());
        await ProntoParaElegibilidadeAsync();
        await PedirElegibilidadeAsync();

        (await ComandosDoConector.ExecutarAsync(["envio", TissBancoDeTeste.Petrobras, "ligar", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0);
        (await ComandosDoConector.ExecutarAsync(["endereco", TissBancoDeTeste.Petrobras, "envio", "https://exemplo.invalid/tiss/lote", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0);
        (await ComandosDoConector.ExecutarAsync(["endereco", TissBancoDeTeste.Petrobras, "envio", "http://exemplo.invalid", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(1);
        (await ComandosDoConector.ExecutarAsync(["situacao", TissBancoDeTeste.Petrobras], Servicos(), saida, erro)).Should().Be(0);
        (await ComandosDoConector.ExecutarAsync(["comando-que-nao-existe"], Servicos(), saida, erro)).Should().Be(2);

        (await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).EnvioLigadoAsync(TissBancoDeTeste.Petrobras)).Should().BeTrue();
        var texto = saida.ToString();
        texto.Should().Contain("envio LIGADO").And.Contain("Elegibilidade").And.Contain("Pendente");
        texto.Should().NotContain(TissBancoDeTeste.CarteiraFicticia).And.NotContain(Login).And.NotContain(Md5);
    }

    private IServiceProvider Servicos()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new ServiceCollection()
            .AddSingleton(_b.Credenciais(repo))
            .AddSingleton(_b.Fila(repo))
            .BuildServiceProvider();
    }

    [Fact]
    public void O_registro_do_conector_tem_um_processador_para_cada_tipo_de_transacao()
    {
        var servicos = new ServiceCollection();
        servicos.AddScoped<Clinica.Application.Abstracoes.IClinicaRepositorio>(_ => new ClinicaRepositorio(_b.NovoContexto()));
        servicos.AddScoped(sp => new ParametrosService(sp.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>(), _b.Protecao));
        servicos.AddScoped(sp => new CredenciaisTissService(sp.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>(), _b.Protecao));
        servicos.AddScoped(sp => new FilaTissService(sp.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>()));
        servicos.AddSingleton<ITransporteTiss>(new TransporteTissHttp(new HttpClient(_rede)));
        servicos.AddLogging();
        servicos.AddConectorTiss(new OpcoesDoConector());

        using var provedor = servicos.BuildServiceProvider(validateScopes: true);
        using var escopo = provedor.CreateScope();

        escopo.ServiceProvider.GetServices<IProcessadorTransacaoTiss>().Select(p => p.Tipo)
            .Should().BeEquivalentTo(Enum.GetValues<TipoTransacaoTiss>());
        escopo.ServiceProvider.GetRequiredService<ExecutorDeTransacoesTiss>().Should().NotBeNull();
        provedor.GetRequiredService<OpcoesDoConector>().EnvioHabilitado.Should().BeFalse("o envio nasce desligado no servidor");
    }

    // ---------- as peças de mentira ----------

    /// <summary>A "operadora": responde o que foi combinado, na ordem, e anota tudo o que recebeu.</summary>
    private sealed class RedeDeMentira : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, string Corpo, string? TipoDeConteudo)
        {
            public string Sequencial => System.Text.RegularExpressions.Regex.Match(Corpo, "<ans:sequencialTransacao>(.*?)<").Groups[1].Value;
        }

        private readonly Queue<Func<string, HttpResponseMessage>> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(_ => new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        public void Cair(bool corpoDoPedidoNaMensagem = false)
            => _respostas.Enqueue(pedido => throw new HttpRequestException("conexão recusada" + (corpoDoPedidoNaMensagem ? ": " + pedido : "")));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var corpo = Encoding.Latin1.GetString(await request.Content!.ReadAsByteArrayAsync(ct));
            Idas.Add(new Ida(request.RequestUri!, corpo, request.Content.Headers.ContentType?.ToString()));
            if (_respostas.Count == 0) throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
            return _respostas.Dequeue()(corpo);
        }
    }

    /// <summary>Um processador que monta uma mensagem com a senha e lê qualquer resposta como "recebida".</summary>
    private sealed class ProcessadorDeMentira(TipoTransacaoTiss tipo, string segredo) : IProcessadorTransacaoTiss
    {
        public Exception? EstourarCom { get; init; }
        public TipoTransacaoTiss Tipo => tipo;

        public Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
            => EstourarCom is not null
                ? throw EstourarCom
                : Task.FromResult(PreparoTiss.Enviar(new Uri("https://exemplo.invalid/tiss/lote"),
                    $"<lote><senhaPrestador>{segredo}</senhaPrestador><guia>{transacao.CodigoId}</guia></lote>", segredo));

        public DesfechoTiss Ler(TransacaoTiss transacao, string xmlDaResposta) => DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
        {
            Resultado = "Recebida pela operadora.", Protocolo = "987654", NumeroGuiaOperadora = "OP123", XmlRecebido = xmlDaResposta
        });
    }

    /// <summary>Guarda tudo o que o conector escreveria no journal, já com os valores no lugar.</summary>
    private sealed class RegistroDeLog
    {
        public List<string> Linhas { get; } = [];
        public ILogger<T> Para<T>() => new Escritor<T>(Linhas);

        private sealed class Escritor<T>(List<string> linhas) : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? excecao, Func<TState, Exception?, string> formatar)
                => linhas.Add(formatar(estado, excecao) + (excecao is null ? "" : " " + excecao));
        }
    }
}
