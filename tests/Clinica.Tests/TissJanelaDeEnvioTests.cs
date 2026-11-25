using System.Net;
using System.Text;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A JANELA DE ENVIO como regra da fila (nov/2026). A homologação da Amil recusou o lote fora do 1º ao
/// 5º dia útil com a glosa 3091 — a janela vale também para o web service. Então: o lote pedido fora
/// dela entra AGENDADO para a próxima abertura, o conector só o envia dentro dela, e a recusa 3091 que
/// vier mesmo assim o devolve a "agendado" sem gastar tentativa. A janela é dado do catálogo; a Saúde
/// Petrobras não tem. Rede de mentira, dados fictícios. Nov/2026: dia 2 (segunda) é o 1º dia útil.
/// </summary>
public sealed class TissJanelaDeEnvioTests : IDisposable
{
    private const string Amil = "Amil";
    private const string Login = "000123";
    private const string Senha = "senha-ficticia-de-teste";
    private static readonly JanelaDeEnvioTiss Janela = OperadorasTiss.Amil.JanelaDeEnvio!;
    private static readonly DateTime PrimeiroDeDezembro = new(2026, 12, 1, 7, 0, 0);

    private readonly TissBancoDeTeste _b = new();
    private readonly Rede _rede = new();

    public void Dispose() => _b.Dispose();

    // ==================== a regra ====================

    [Theory]
    [InlineData("2026-11-03 10:00", false, "2026-11-03 10:00")] // dentro: é agora
    [InlineData("2026-11-03 06:00", false, "2026-11-03 07:00")] // antes das 7h de um dia da janela
    [InlineData("2026-11-09 19:30", false, "2026-12-01 07:00")] // 5º dia útil (dia 2 é Finados) depois das 19h
    [InlineData("2026-11-17 10:00", false, "2026-12-01 07:00")] // meio do mês
    [InlineData("2026-10-31 10:00", false, "2026-11-03 07:00")] // sábado antes do 1º dia útil (dia 2 é Finados)
    [InlineData("2026-11-03 10:00", true, "2026-11-04 07:00")]  // recusada: o próximo dia útil da janela
    [InlineData("2026-11-06 10:00", true, "2026-11-09 07:00")]  // recusada no 4º (sexta): segunda é o 5º
    [InlineData("2026-11-09 10:00", true, "2026-12-01 07:00")]  // recusada no 5º: acabou a janela, o mês seguinte
    public void A_proxima_abertura_da_janela(string agora, bool depoisDaAtual, string esperada)
        => PrazosTiss.ProximaAbertura(Janela, DateTime.Parse(agora), depoisDaAtual).Should().Be(DateTime.Parse(esperada));

    [Fact]
    public void Feriado_nacional_nao_e_dia_util_da_janela()
    {
        // O portal da Amil: "para os dias não úteis será acrescentado o próximo dia útil".
        Clinica.Domain.Regras.FeriadosNacionais.Nome(new DateOnly(2026, 11, 2)).Should().Be("Finados");
        Clinica.Domain.Regras.FeriadosNacionais.Pascoa(2026).Should().Be(new DateOnly(2026, 4, 5));
        Clinica.Domain.Regras.FeriadosNacionais.Eh(new DateOnly(2026, 4, 3)).Should().BeTrue("Paixão de Cristo");
        Clinica.Domain.Regras.FeriadosNacionais.Eh(new DateOnly(2026, 11, 20)).Should().BeTrue("Consciência Negra");
        Clinica.Domain.Regras.FeriadosNacionais.Eh(new DateOnly(2026, 2, 17)).Should().BeFalse("Carnaval é ponto facultativo, fica de fora");
        PrazosTiss.DiaUtilDoMes(new DateOnly(2026, 11, 2)).Should().BeNull("Finados");
        PrazosTiss.DiaUtilDoMes(new DateOnly(2026, 11, 3)).Should().Be(1);
        PrazosTiss.DiaUtilDoMes(new DateOnly(2026, 11, 9)).Should().Be(5);
        PrazosTiss.ProximaAbertura(Janela, new DateTime(2026, 10, 31, 10, 0, 0)).Should().Be(new DateTime(2026, 11, 3, 7, 0, 0));
        // Janeiro de 2027: dia 1 é feriado (sexta) — o 1º dia útil é segunda, dia 4.
        PrazosTiss.ProximaAbertura(Janela, new DateTime(2026, 12, 31, 10, 0, 0)).Should().Be(new DateTime(2027, 1, 4, 7, 0, 0));
    }

    [Fact]
    public void A_guia_que_vence_antes_da_proxima_janela_e_aviso_grave()
    {
        var agora = new DateTime(2026, 11, 17, 10, 0, 0);
        var grave = PrazosTiss.AvisoDaGuia(OperadorasTiss.Amil, new DateOnly(2026, 8, 25), agora); // vence 23/11
        grave!.Value.Grave.Should().BeTrue();
        grave.Value.Texto.Should().Contain("vence em 23/11/2026").And.Contain("01/12");
        // Vence depois da próxima janela: aviso comum (ou nenhum).
        PrazosTiss.AvisoDaGuia(OperadorasTiss.Amil, new DateOnly(2026, 11, 10), agora).Should().BeNull();
        // Já venceu: grave.
        PrazosTiss.AvisoDaGuia(OperadorasTiss.Amil, new DateOnly(2026, 8, 1), agora)!.Value.Grave.Should().BeTrue();
        // Sem janela (Petrobras): nada.
        PrazosTiss.AvisoDaGuia(OperadorasTiss.SaudePetrobras, new DateOnly(2026, 8, 25), agora).Should().BeNull();
    }

    // ==================== a fila e o conector ====================

    [Fact]
    public async Task Fora_da_janela_o_lote_entra_agendado_o_conector_nao_o_pega_antes_e_o_envia_na_abertura()
    {
        _b.Agora = new DateTime(2026, 11, 17, 10, 0, 0);
        var guia = await ProntoAsync();

        var pedido = await _b.Fila().EnfileirarEnvioAsync(Amil, [guia], TissBancoDeTeste.Operador);

        pedido.ProximaTentativaEm.Should().Be(PrimeiroDeDezembro);
        FilaTiss.Agendado(pedido).Should().BeTrue();
        pedido.Resultado.Should().StartWith("Agendado para 01/12/2026 às 07:00");
        EnvioTissNaTela.DoEnvio(pedido).Rotulo.Should().Be("Agendado");
        BaixaComEnvioNaTela.Ler(pedido, guia, "Amil", _b.Agora.AddDays(-3), _b.Agora).Fase.Should().Be(FaseDaBaixaComEnvio.NaFila,
            "esperar a janela não é 'o serviço não respondeu'");

        (await Conector().ProcessarProximaAsync()).Should().BeFalse("antes da abertura não há o que pegar");
        _rede.Idas.Should().BeEmpty();

        _b.Agora = PrimeiroDeDezembro.AddMinutes(5);
        _rede.Responder(HttpStatusCode.InternalServerError, "");
        (await Conector().ProcessarProximaAsync()).Should().BeTrue();
        _rede.Idas.Should().ContainSingle();
    }

    [Fact]
    public async Task O_lote_agendado_pode_ser_cancelado_antes()
    {
        _b.Agora = new DateTime(2026, 11, 17, 10, 0, 0);
        var pedido = await _b.Fila().EnfileirarEnvioAsync(Amil, [await ProntoAsync()], TissBancoDeTeste.Operador);

        (await _b.Fila().CancelarAsync(pedido.Id, TissBancoDeTeste.Operador)).Should().BeTrue();
        (await Gravada(pedido.Id)).Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
    }

    [Fact]
    public async Task Pedido_dentro_da_janela_que_o_conector_so_pega_fora_dela_volta_agendado_sem_gastar_tentativa()
    {
        _b.Agora = new DateTime(2026, 11, 6, 18, 59, 0); // 4º dia útil (dia 2 é Finados), um minuto antes de fechar
        var pedido = await _b.Fila().EnfileirarEnvioAsync(Amil, [await ProntoAsync()], TissBancoDeTeste.Operador);
        pedido.ProximaTentativaEm.Should().BeNull("dentro da janela o pedido não é agendado");

        _b.Agora = new DateTime(2026, 11, 10, 10, 0, 0); // o serviço só o pega na terça: 6º dia útil (dia 2 é Finados)
        (await Conector().ProcessarProximaAsync()).Should().BeTrue();

        _rede.Idas.Should().BeEmpty();
        var t = await Gravada(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        t.Tentativas.Should().Be(0);
        t.ProximaTentativaEm.Should().Be(PrimeiroDeDezembro);
    }

    [Fact]
    public async Task A_recusa_3091_volta_o_lote_a_agendado_para_a_janela_seguinte_sem_gastar_tentativa_e_ele_sai_de_novo()
    {
        _b.Agora = new DateTime(2026, 11, 3, 10, 0, 0); // dentro da janela: o lote sai
        var pedido = await _b.Fila().EnfileirarEnvioAsync(Amil, [await ProntoAsync()], TissBancoDeTeste.Operador);
        _rede.Responder(HttpStatusCode.OK, TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO",
            "<ans:recebimentoLote><ans:mensagemErro><ans:codigoGlosa>3091</ans:codigoGlosa>"
            + "<ans:descricaoGlosa>COBRANCA FORA DO PRAZO ESTIPULADO NO CONTRATO</ans:descricaoGlosa></ans:mensagemErro></ans:recebimentoLote>"));

        await Conector().ProcessarProximaAsync();

        var t = await Gravada(pedido.Id);
        t.Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        t.Tentativas.Should().Be(0, "a recusa por prazo não gasta tentativa");
        t.EnvioIniciadoEm.Should().BeNull("a operadora disse que não recebeu: pode sair de novo");
        t.ProximaTentativaEm.Should().Be(new DateTime(2026, 11, 4, 7, 0, 0), "o próximo dia útil da mesma janela: uma ida por dia");
        t.Resultado.Should().StartWith("Agendado para 04/11/2026").And.Contain("3091");

        (await Conector().ProcessarProximaAsync()).Should().BeFalse("nada de laço no mesmo dia");
        _b.Agora = new DateTime(2026, 11, 4, 8, 0, 0);
        _rede.Responder(HttpStatusCode.InternalServerError, "");
        await Conector().ProcessarProximaAsync();
        _rede.Idas.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_Petrobras_nao_tem_janela_e_o_3091_dela_e_recusa_comum()
    {
        _b.Agora = new DateTime(2026, 11, 17, 10, 0, 0);
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(TissBancoDeTeste.Petrobras, Login, Senha, TissBancoDeTeste.Operador);
        await _b.Credenciais().DefinirEnvioAsync(TissBancoDeTeste.Petrobras, true, TissBancoDeTeste.Operador);
        var pedido = await _b.Fila().EnfileirarEnvioAsync(TissBancoDeTeste.Petrobras, [(await _b.GuiaAsync()).Guia.Id], TissBancoDeTeste.Operador);
        pedido.ProximaTentativaEm.Should().BeNull();
        _rede.Responder(HttpStatusCode.OK, TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO",
            "<ans:recebimentoLote><ans:mensagemErro><ans:codigoGlosa>3091</ans:codigoGlosa></ans:mensagemErro></ans:recebimentoLote>"));

        await Conector().ProcessarProximaAsync();

        (await Gravada(pedido.Id)).Estado.Should().Be(EstadoTransacaoTiss.Falhou);
    }

    // ==================== peças ====================

    /// <summary>Credencial, envio ligado (só no teste, rede de mentira) e uma guia Amil conferida.</summary>
    private async Task<int> ProntoAsync()
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        await _b.Credenciais().DefinirEnvioAsync(Amil, true, TissBancoDeTeste.Operador);
        var (_, guia) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        var db = _b.NovoContexto();
        var codigo = await db.Codigos.SingleAsync(c => c.Id == guia.Id);
        var data = DateOnly.FromDateTime(_b.Agora);
        GuiaAnsNaBaixa.Aplicar(codigo, new DadosGuiaConsultaAns
        {
            Modelo = ModeloDaGuiaTiss.SpSadt, RegistroAns = "326305", NumeroCarteira = TissBancoDeTeste.CarteiraFicticia,
            NomeBeneficiario = "Paciente de Teste", CodigoPrestadorNaOperadora = Login, NomeContratado = "Clínica de Teste Ltda",
            Cnes = "9999999", NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "123456",
            UfConselho = "33", Cbos = "225105", IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = data,
            CodigoTabela = "22", TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139.00m,
            Senha = "SENHA0001", DataAutorizacao = data, CodigoValidacao = "123456", TipoAtendimento = "04",
            CaraterAtendimento = "1", DescricaoProcedimento = "Consulta em consultório"
        }, TissBancoDeTeste.Operador, _b.Agora);
        await db.SaveChangesAsync();
        return guia.Id;
    }

    private Task<TransacaoTiss> Gravada(int id) => _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(t => t.Id == id);

    private ExecutorDeTransacoesTiss Conector()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores = [new ProcessadorDeEnvioDeGuia(repo, parametros, credenciais, () => _b.Agora)];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores, new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = true, Consumidor = "conector-de-teste" }, NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    private sealed class Rede : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respostas = new();
        public List<Uri> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Idas.Add(request.RequestUri!);
            return Task.FromResult(_respostas.Count > 0 ? _respostas.Dequeue() : throw new InvalidOperationException("O teste não combinou resposta para esta ida."));
        }
    }
}
