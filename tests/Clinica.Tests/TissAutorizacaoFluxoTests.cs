using System.Net;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
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
/// A AUTORIZAÇÃO PELO SISTEMA (nov/2026, Amil): o Chegou pede pela fila, o conector leva o pedido e
/// guarda a senha, pergunta a situação com intervalo e teto quando em análise, não confunde falta de
/// resposta com negativa, cancela a guia de solicitação quando o horário é desmarcado; a recepção
/// digita o token com o paciente presente e a baixa abre com tudo travado. A rede é de MENTIRA e
/// todos os dados são fictícios.
/// </summary>
public sealed class TissAutorizacaoFluxoTests : IDisposable
{
    private const string Amil = "Amil";
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;
    private static readonly XNamespace Soap = TissWsDeTeste.Soap;

    private readonly TissBancoDeTeste _b = new();
    private readonly Rede _rede = new();

    public TissAutorizacaoFluxoTests() => _b.Agora = new DateTime(2026, 11, 17, 9, 0, 0);

    public void Dispose() => _b.Dispose();

    // ==================== o pedido ====================

    [Fact]
    public async Task Chegou_pede_a_autorizacao_so_com_a_chave_ligada_e_nunca_duas()
    {
        var ag = await HorarioAmilAsync();
        await Agenda().RegistrarChegadaAsync(ag.Id, TissBancoDeTeste.Operador);
        (await Banco().AutorizacoesTiss.CountAsync()).Should().Be(0, "a chave nasce desligada");

        var outro = await HorarioAmilAsync();
        await LigarAsync();
        await Agenda().RegistrarChegadaAsync(outro.Id, TissBancoDeTeste.Operador);
        var a = await Banco().AutorizacoesTiss.SingleAsync();
        a.AgendamentoId.Should().Be(outro.Id);
        a.Situacao.Should().Be(SituacaoAutorizacaoTiss.Pedida);
        a.NumeroGuiaPrestador.Should().Be("A" + a.Id);
        a.Cbos.Should().Be("225105");
        a.CodigoProcedimento.Should().Be("10101012");
        var t = await Banco().TransacoesTiss.SingleAsync();
        (t.Tipo, t.AutorizacaoId, t.Estado).Should().Be((TipoTransacaoTiss.Autorizacao, a.Id, EstadoTransacaoTiss.Pendente));

        // Pedir de novo pelo botão devolve a mesma: um horário, uma autorização valendo.
        var denovo = await Servico().PedirAsync(outro.Id, TissBancoDeTeste.Operador);
        denovo.Nova.Should().BeFalse();
        denovo.Autorizacao!.Id.Should().Be(a.Id);
        (await Banco().TransacoesTiss.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Sem_CBO_ou_particular_o_pedido_e_recusado_com_a_frase_e_nada_vai_a_fila()
    {
        await LigarAsync();
        var semCbo = await HorarioAmilAsync(cbos: null);
        (await Servico().PedirAsync(semCbo.Id, TissBancoDeTeste.Operador)).Impedimento.Should().Contain("CBO");
        var particular = await HorarioAmilAsync(particular: true);
        (await Servico().PedirAsync(particular.Id, TissBancoDeTeste.Operador)).Impedimento.Should().Contain("particular");
        (await Banco().TransacoesTiss.CountAsync()).Should().Be(0);
    }

    // ==================== o conector ====================

    [Fact]
    public async Task Autorizado_guarda_senha_data_e_validade_e_a_mensagem_sai_valida_no_XSD_4_03_00()
    {
        var a = await PedidaAsync();
        _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("1"));

        (await Conector().ProcessarProximaAsync()).Should().BeTrue();

        var ida = _rede.Idas.Single();
        ida.Endereco.Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-procedimento/v4.03.00"));
        var mensagem = Corpo(ida.Corpo);
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "CBOS").Single().Value.Should().Be("225105");

        var gravada = await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id);
        gravada.Situacao.Should().Be(SituacaoAutorizacaoTiss.Autorizada);
        (gravada.Senha, gravada.DataAutorizacao, gravada.ValidadeSenha).Should().Be(("SENHA0001", new DateOnly(2026, 11, 17), new DateOnly(2027, 1, 16)));
        AutorizacaoTissService.Selo(gravada, new DateOnly(2026, 11, 17)).Rotulo.Should().Be("Autorizado · peça o token ao paciente");
        // A senha fica na autorização: nem a linha da fila nem a trilha a repetem.
        (await Banco().TransacoesTiss.SingleAsync()).Resultado.Should().NotContain("SENHA0001");
        (await Banco().Auditoria.AsNoTracking().ToListAsync()).Should().NotContain(e => (e.Detalhe ?? "").Contains("SENHA0001"));
    }

    [Fact]
    public async Task Em_analise_pergunta_a_situacao_com_intervalo_e_para_no_teto()
    {
        var a = await PedidaAsync();
        _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("2"));
        await Conector().ProcessarProximaAsync();

        var t = await Banco().TransacoesTiss.SingleAsync();
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        t.ProximaTentativaEm.Should().Be(_b.Agora + FilaTiss.IntervaloDaSituacaoDaAutorizacao);
        (await Conector().ProcessarProximaAsync()).Should().BeFalse("antes do intervalo nada sai — sem laço");

        // As perguntas da situação vão ao endpoint DELA, até o teto.
        for (var i = 0; i < FilaTiss.TetoDeConsultasDaAutorizacao; i++)
        {
            _b.Agora += FilaTiss.IntervaloDaSituacaoDaAutorizacao;
            _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("2"));
            (await Conector().ProcessarProximaAsync()).Should().BeTrue();
        }
        _rede.Idas.Skip(1).Should().OnlyContain(i => i.Endereco == new Uri("https://api.servicos.grupoamil.com.br/api-tiss-solicitacao-status-autorizacao/v4.03.00"));
        TissWsDeTeste.ValidarNoXsd(Corpo(_rede.Idas[1].Corpo), "4.03.00").Should().BeEmpty();

        _b.Agora += FilaTiss.IntervaloDaSituacaoDaAutorizacao;
        (await Conector().ProcessarProximaAsync()).Should().BeFalse("passado o teto, a transação terminou");
        (await Banco().TransacoesTiss.SingleAsync()).Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        var gravada = await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id);
        gravada.Situacao.Should().Be(SituacaoAutorizacaoTiss.EmAnalise);
        gravada.Motivo.Should().Contain("confira no portal");
    }

    [Fact]
    public async Task Negado_mostra_o_motivo_e_sem_resposta_e_nao_sei_nunca_negado()
    {
        var negada = await PedidaAsync();
        _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("3"));
        await Conector().ProcessarProximaAsync();
        var n = await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == negada.Id);
        n.Situacao.Should().Be(SituacaoAutorizacaoTiss.Negada);
        n.Motivo.Should().Contain("1801");
        AutorizacaoTissService.Selo(n, new DateOnly(2026, 11, 17)).PodePedir.Should().BeTrue();

        var semResposta = await PedidaAsync();
        _rede.Responder(HttpStatusCode.BadGateway, "<html>gateway</html>");
        await Conector().ProcessarProximaAsync();
        var s = await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == semResposta.Id);
        s.Situacao.Should().Be(SituacaoAutorizacaoTiss.NaoSeSabe, "falha de rede é 'não sei', nunca 'negado'");
        var t = await Banco().TransacoesTiss.SingleAsync(x => x.AutorizacaoId == semResposta.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Falhou, "o pedido que saiu não é repetido sozinho");
        _b.Agora = _b.Agora.AddHours(2);
        (await Conector().ProcessarProximaAsync()).Should().BeFalse();
        AutorizacaoTissService.Selo(s, new DateOnly(2026, 11, 17)).Rotulo.Should().Be("Autorização sem resposta");
    }

    [Fact]
    public async Task Com_a_chave_desligada_o_conector_recusa_e_nada_vai_a_rede()
    {
        var a = await PedidaAsync();
        await _b.Credenciais().DefinirAutorizacaoAsync(Amil, false, TissBancoDeTeste.Operador);
        await Conector().ProcessarProximaAsync();
        _rede.Idas.Should().BeEmpty();
        (await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id)).Situacao.Should().Be(SituacaoAutorizacaoTiss.Cancelada);
    }

    [Fact]
    public async Task Horario_desmarcado_com_senha_emitida_cancela_a_guia_de_solicitacao_na_operadora()
    {
        var a = await PedidaAsync();
        _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("1"));
        await Conector().ProcessarProximaAsync();

        await Agenda().CancelarAsync(a.AgendamentoId, TissBancoDeTeste.Operador);
        var pedido = await Banco().TransacoesTiss.SingleAsync(t => t.Tipo == TipoTransacaoTiss.CancelaAutorizacao);
        pedido.NumeroGuiaPrestador.Should().Be(a.NumeroGuiaPrestador);
        (await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id)).Situacao.Should().Be(SituacaoAutorizacaoTiss.CancelamentoPedido);

        _rede.Responder(HttpStatusCode.OK, ReciboDoCancelamento(a.NumeroGuiaPrestador));
        await Conector().ProcessarProximaAsync();
        var ida = _rede.Idas[^1];
        ida.Endereco.Should().Be(new Uri("https://api.servicos.grupoamil.com.br/api-tiss-cancela-guia/v4.03.00"));
        var mensagem = Corpo(ida.Corpo);
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "tipoGuia").Single().Value.Should().Be("1", "cancela a guia de SOLICITAÇÃO: o padrão não cancela pela senha");
        (await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id)).Situacao.Should().Be(SituacaoAutorizacaoTiss.Cancelada);
    }

    [Fact]
    public async Task Horario_desmarcado_com_o_pedido_ainda_na_fila_nada_sai()
    {
        var a = await PedidaAsync();
        await Agenda().MarcarFaltaAsync(a.AgendamentoId, TissBancoDeTeste.Operador);
        (await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id)).Situacao.Should().Be(SituacaoAutorizacaoTiss.Cancelada);
        await Conector().ProcessarProximaAsync();
        _rede.Idas.Should().BeEmpty();
        (await Banco().TransacoesTiss.CountAsync(t => t.Tipo == TipoTransacaoTiss.CancelaAutorizacao)).Should().Be(0);
    }

    // ==================== o token na recepção e a baixa ====================

    [Fact]
    public async Task O_token_e_colhido_na_recepcao_mascarado_fora_da_baixa_e_fora_da_trilha()
    {
        var a = await AutorizadaAsync();
        var hoje = new DateOnly(2026, 11, 17);
        AutorizacaoTissService.AvisoDeTokenFaltando([a], hoje).Should().Be(
            "Faltou o token de 1 paciente Amil hoje. Peça ao paciente enquanto ele está na clínica: na baixa ele já foi embora.");

        (await Servico().RegistrarTokenAsync(a.Id, "12a", TissBancoDeTeste.Operador)).Should().Contain("6 dígitos");
        (await Servico().RegistrarTokenAsync(a.Id, "482913", TissBancoDeTeste.Operador)).Should().BeNull();

        var gravada = await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == a.Id);
        (gravada.Token, gravada.TokenRegistradoPor, gravada.TokenRegistradoEm).Should().Be(("482913", TissBancoDeTeste.Operador, _b.Agora));
        var selo = AutorizacaoTissService.Selo(gravada, hoje);
        selo.Rotulo.Should().Be("Autorizado · token ok");
        selo.Dica.Should().Contain("••••13").And.NotContain("482913");
        AutorizacaoTissService.AvisoDeTokenFaltando([gravada], hoje).Should().BeNull();
        (await Banco().Auditoria.AsNoTracking().ToListAsync()).Should().Contain(e => e.Acao == "TokenBeneficiarioRegistrado")
            .And.NotContain(e => (e.Detalhe ?? "").Contains("482913"));

        // Sem token: o motivo da tabela, ali mesmo.
        var outra = await AutorizadaAsync();
        (await Servico().RegistrarSemTokenAsync(outra.Id, "07", TissBancoDeTeste.Operador)).Should().BeNull();
        AutorizacaoTissService.Selo(await Banco().AutorizacoesTiss.SingleAsync(x => x.Id == outra.Id), hoje).Rotulo.Should().Be("Não confirmou");
    }

    [Fact]
    public async Task A_baixa_abre_com_senha_data_validade_e_token_travados_e_a_correcao_vai_a_trilha()
    {
        var a = await AutorizadaAsync();
        await Servico().RegistrarTokenAsync(a.Id, "482913", TissBancoDeTeste.Operador);
        var guia = await GuiaDoHorarioAsync(a.AgendamentoId);

        var proposta = await new GuiaAnsNaBaixaService(_b.Repo, Parametros(), new PrecoConvenioService(_b.Repo), _b.Credenciais(),
            new EnvioTissLigadoNaPrevia(new HashSet<Convenio> { Convenio.Amil })).PrepararAsync(guia.Id);
        var d = proposta.Dados!;
        (d.Senha, d.DataAutorizacao, d.ValidadeSenha, d.CodigoValidacao).Should().Be(("SENHA0001", new DateOnly(2026, 11, 17), new DateOnly(2027, 1, 16), "482913"));
        var edicao = GuiaAnsEmEdicao.De(d);
        edicao.SenhaTravada.Should().BeTrue();
        edicao.ConfirmacaoTravada.Should().BeTrue();
        edicao.RotuloDaAutorizacao.Should().Be("autorizado pela Amil em 17/11");

        // Corrigir o token na baixa é exceção: a trilha diz que mudou — sem o token.
        AutorizacaoTissService.DivergenciaNaBaixa(await Banco().AutorizacoesTiss.AsNoTracking().SingleAsync(x => x.Id == a.Id), edicao.ParaDados()).Should().BeNull("conferida como veio, nada a registrar");
        edicao.CorrigindoConfirmacao = true;
        edicao.ConfirmacaoTravada.Should().BeFalse();
        edicao.CodigoValidacao = "111111";
        AutorizacaoTissService.DivergenciaNaBaixa(await Banco().AutorizacoesTiss.AsNoTracking().SingleAsync(x => x.Id == a.Id), edicao.ParaDados()).Should().Contain("o token da recepção")
            .And.NotContain("111111").And.NotContain("482913");
    }

    // ==================== comandos ====================

    [Fact]
    public async Task O_comando_autorizacao_liga_a_chave_propria_e_testar_autorizacao_so_roda_na_homologacao()
    {
        var (saida, erro) = (new StringWriter(), new StringWriter());
        (await ComandosDoConector.ExecutarAsync(["autorizacao", Amil, "ligar", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0, erro.ToString());
        (await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).AutorizacaoLigadaAsync(Amil)).Should().BeTrue();
        (await _b.Credenciais(new ClinicaRepositorio(_b.NovoContexto())).EnvioLigadoAsync(Amil)).Should().BeFalse("a autorização não liga o envio de guias");

        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia-de-teste", TissBancoDeTeste.Operador);
        (await ComandosDoConector.ExecutarAsync(["testar-autorizacao", Amil, "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", "operador.servidor"],
            Servicos(), saida, erro)).Should().Be(1);
        erro.ToString().Should().Contain("só rodam na homologação");
        _rede.Idas.Should().BeEmpty();
    }

    [Fact]
    public async Task O_teste_de_autorizacao_na_homologacao_faz_uma_ida_e_so_grava_a_trilha()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia-de-teste", TissBancoDeTeste.Operador);
        await _b.Credenciais().DefinirAmbienteAsync(Amil, true, TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("1"));
        var (saida, erro) = (new StringWriter(), new StringWriter());

        (await ComandosDoConector.ExecutarAsync(["testar-autorizacao", Amil, "--carteira", TissBancoDeTeste.CarteiraFicticia, "--procedimento", "10101012",
            "--cpf-profissional", "52998224725", "--operador", "operador.servidor"], Servicos(), saida, erro)).Should().Be(0, erro.ToString());

        var ida = _rede.Idas.Single();
        ida.Endereco.Should().Be(OperadorasTiss.Amil.EnderecoHomologacao);
        TissWsDeTeste.ValidarNoXsd(Corpo(ida.Corpo), "4.03.00").Should().BeEmpty();
        saida.ToString().Should().Contain("Autorizado").And.Contain("CPF do profissional não vai");
        (await Banco().AutorizacoesTiss.CountAsync()).Should().Be(0);
        (await Banco().TransacoesTiss.CountAsync()).Should().Be(0);
        (await Banco().Auditoria.AsNoTracking().SingleAsync(e => e.Acao == "TesteTissHomologacao")).Detalhe.Should().NotContain("SENHA0001");
    }

    // ==================== peças ====================

    private ClinicaDbContext Banco() => _b.NovoContexto();

    private ParametrosService Parametros(ClinicaRepositorio? repo = null) => new(repo ?? _b.Repo, _b.Protecao);

    private AutorizacaoTissService Servico(ClinicaRepositorio? repo = null)
    {
        repo ??= new ClinicaRepositorio(_b.NovoContexto());
        return new AutorizacaoTissService(repo, Parametros(repo), _b.Credenciais(repo), () => _b.Agora);
    }

    private AgendaService Agenda()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new AgendaService(repo, new AtendimentoService(repo), Parametros(repo), Servico(repo));
    }

    private async Task LigarAsync()
    {
        await new ParametrosService(_b.Repo, _b.Protecao).SalvarPrestadorAsync(new DadosPrestador
        {
            Cnpj = "00.000.000/0001-91", RazaoSocial = "Clínica de Teste Ltda", RegistroAnsOperadora = "422631",
            CodigosTuss = new() { [TipoCodigo.Consulta] = "10101012" }
        });
        await _b.Credenciais().SalvarAsync(Amil, "000123", "senha-ficticia-de-teste", TissBancoDeTeste.Operador);
        await _b.Credenciais().DefinirAutorizacaoAsync(Amil, true, TissBancoDeTeste.Operador);
    }

    private async Task<Agendamento> HorarioAmilAsync(string? cbos = "225105", bool particular = false)
    {
        var profissional = new Profissional
        {
            Nome = "Dra. Teste Ficticia", NomeCurto = "Dra. Teste " + Guid.NewGuid().ToString("N")[..6], ConselhoProfissional = "06",
            NumeroConselho = "520001", UfConselho = "33", Cbos = cbos, Ativo = true
        };
        var paciente = new Paciente { Nome = "Paciente Amil de Teste", Convenio = Convenio.Amil, ConvenioCodigo = Amil, Carteirinha = TissBancoDeTeste.CarteiraFicticia };
        _b.Db.AddRange(profissional, paciente);
        await _b.Db.SaveChangesAsync();
        var ag = new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = profissional.Id, DataHora = _b.Agora.AddMinutes(30), DuracaoMinutos = 30,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            Particular = particular, CriadoPor = "recepcao", CriadoEm = _b.Agora.AddDays(-3)
        };
        _b.Db.Add(ag);
        await _b.Db.SaveChangesAsync();
        return ag;
    }

    private async Task<AutorizacaoTiss> PedidaAsync()
    {
        await LigarAsync();
        var ag = await HorarioAmilAsync();
        return (await Servico().PedirAsync(ag.Id, TissBancoDeTeste.Operador)).Autorizacao!;
    }

    private async Task<AutorizacaoTiss> AutorizadaAsync()
    {
        var a = await PedidaAsync();
        _rede.Responder(HttpStatusCode.OK, TissAutorizacaoWsTests.Resposta("1"));
        await Conector().ProcessarProximaAsync();
        return await Banco().AutorizacoesTiss.AsNoTracking().SingleAsync(x => x.Id == a.Id);
    }

    /// <summary>O atendimento do horário com a guia de consulta em aberto (como o Concluir deixaria).</summary>
    private async Task<CodigoFaturamento> GuiaDoHorarioAsync(int agendamentoId)
    {
        var ag = await _b.Db.Agendamentos.SingleAsync(x => x.Id == agendamentoId);
        var atendimento = new Atendimento { PacienteId = ag.PacienteId, Data = DateOnly.FromDateTime(ag.DataHora), Modalidade = ModalidadeAtendimento.Consulta };
        _b.Db.Add(atendimento);
        await _b.Db.SaveChangesAsync();
        ag.AtendimentoId = atendimento.Id;
        ag.Status = StatusAgendamento.Realizado;
        var guia = new CodigoFaturamento { AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro, DataPrevistaFaturamento = atendimento.Data };
        _b.Db.Add(guia);
        await _b.Db.SaveChangesAsync();
        return guia;
    }

    private ExecutorDeTransacoesTiss Conector()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = Parametros(repo);
        IProcessadorTransacaoTiss[] processadores =
        [
            new ProcessadorDeAutorizacao(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeCancelamentoDeAutorizacao(repo, parametros, credenciais, () => _b.Agora)
        ];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores,
            new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = false, Consumidor = "conector-de-teste" },
            NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    private IServiceProvider Servicos()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new ServiceCollection()
            .AddSingleton<Clinica.Application.Abstracoes.IClinicaRepositorio>(repo).AddSingleton(Parametros(repo))
            .AddSingleton(_b.Credenciais(repo)).AddSingleton(_b.Fila(repo))
            .AddSingleton(new TissWebserviceCliente(new HttpClient(_rede)))
            .AddSingleton(new RelogioDoConector(() => _b.Agora))
            .BuildServiceProvider();
    }

    private static XElement Corpo(string envelope) => XDocument.Parse(envelope).Root!.Element(Soap + "Body")!.Elements().Single();

    private static string ReciboDoCancelamento(string numero) => TissWsDeTeste.Resposta("reciboCancelaGuiaWS", "CANCELAMENTO_GUIA_RECIBO", $"""
        <ans:reciboCancelaGuia><ans:reciboCancelaGuia>
          <ans:registroANS>326305</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>000123</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:retornoStatus><ans:guiasCanceladas><ans:dadosGuia>
            <ans:tipoGuia>1</ans:tipoGuia>
            <ans:numeroGuiaPrestador>{numero}</ans:numeroGuiaPrestador>
            <ans:statusCancelamento>1</ans:statusCancelamento>
          </ans:dadosGuia></ans:guiasCanceladas></ans:retornoStatus>
        </ans:reciboCancelaGuia></ans:reciboCancelaGuia>
        """);

    /// <summary>A "operadora": responde o combinado, na ordem, e anota cada ida.</summary>
    private sealed class Rede : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, string Corpo);

        private readonly Queue<HttpResponseMessage> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(corpo)) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            Idas.Add(new Ida(request.RequestUri!, Encoding.UTF8.GetString(bytes)));
            return _respostas.Count > 0 ? _respostas.Dequeue() : throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
        }
    }
}
