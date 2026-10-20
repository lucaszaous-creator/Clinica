using System.Text;
using System.Text.Json;
using Clinica.Application.Email;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Application.Sms;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O CANAL DE SMS (out/2026): a confirmação 24h antes com link, a resposta que volta pelo
/// balde, a idempotência de banco e a campanha só para quem consentiu. O provedor é FALSO —
/// o que se prova é quem recebe, o que fica gravado e o que NÃO acontece. O provedor de
/// verdade se prova pelo botão "Enviar SMS de teste", na clínica.
/// </summary>
public class CanalSmsTests : IDisposable
{
    // Quinta-feira, 10/09/2026, 8h.
    private static readonly DateTime Agora = new(2026, 9, 10, 8, 0, 0);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly CampanhaService _campanhas;
    private readonly ParametrosService _parametros;
    private readonly GatewayFalso _gateway = new();
    private readonly ArmazenamentoFake _balde = new();

    public CanalSmsTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _campanhas = new CampanhaService(_repo);
        _parametros = new ParametrosService(_repo,
            new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private LembreteSmsService Servico(DateTime? agora = null)
        => new(_repo, _campanhas, _parametros, _gateway, _balde, () => agora ?? Agora);

    private async Task LigarAsync(bool adiantar = true, bool comLink = true)
    {
        await _parametros.SalvarCamposSmsAsync(new CamposSms(
            ProvedorSms.Zenvia, null, null, "tok-123", "clinica", null, null, 24, adiantar));
        if (comLink) await _parametros.SalvarUrlPublicacaoAsync("https://docs.clinica.exemplo");
    }

    private async Task<Paciente> PacienteAsync(string nome, string? telefone = "(11) 98765-4321", string? email = null)
    {
        var p = new Paciente { Nome = nome, Telefone = telefone, Email = email, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    private async Task<Agendamento> SessaoAsync(int pacienteId, DateTime quando)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId, DataHora = quando, DuracaoMinutos = 30, Status = StatusAgendamento.Agendado,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = "Consulta"
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        return ag;
    }

    private Task<ContatoCampanha?> ContatoDeAsync(int agendamentoId)
        => _db.Contatos.AsNoTracking().FirstOrDefaultAsync(c => c.AgendamentoId == agendamentoId);

    // ==================== A regra do momento (domínio) ====================

    [Fact]
    public void O_lembrete_sai_24h_antes_e_o_fim_de_semana_adianta_para_a_sexta()
    {
        var segunda9h = new DateTime(2026, 9, 14, 9, 0, 0);
        ConfirmacaoPorLink.MomentoDoLembrete(segunda9h, 24).Should().Be(new DateTime(2026, 9, 11, 9, 0, 0));
        ConfirmacaoPorLink.MomentoDoLembrete(segunda9h, 24, adiantarFimDeSemana: false)
            .Should().Be(new DateTime(2026, 9, 13, 9, 0, 0));
        var sexta14h = new DateTime(2026, 9, 11, 14, 0, 0);
        ConfirmacaoPorLink.MomentoDoLembrete(sexta14h, 24).Should().Be(new DateTime(2026, 9, 10, 14, 0, 0));
    }

    [Fact]
    public void Perto_demais_da_sessao_nao_manda()
    {
        var sessao = Agora.AddMinutes(90);
        ConfirmacaoPorLink.HoraDeEnviar(sessao, Agora, 24).Should().BeFalse();
        ConfirmacaoPorLink.HoraDeEnviar(Agora.AddHours(3), Agora, 24).Should().BeTrue();
        ConfirmacaoPorLink.HoraDeEnviar(Agora.AddHours(30), Agora, 24).Should().BeFalse();
    }

    [Theory]
    [InlineData("(11) 98765-4321", "+5511987654321")]
    [InlineData("011987654321", "+5511987654321")]
    [InlineData("+55 11 98765-4321", "+5511987654321")]
    [InlineData("(11) 3456-7890", null)]   // fixo não recebe SMS
    [InlineData("98765-4321", null)]       // sem DDD
    [InlineData(null, null)]
    public void So_celular_brasileiro_vira_destino(string? telefone, string? esperado)
        => TextoSms.CelularE164(telefone).Should().Be(esperado);

    [Fact]
    public void O_texto_sai_sem_acento_e_a_confirmacao_cabe_num_segmento_com_o_link()
    {
        TextoSms.ParaGsm("Sessão amanhã às 14h — confirmação").Should().Be("Sessao amanha as 14h - confirmacao");
        TextoSms.Segmentos(new string('a', 160)).Should().Be(1);
        TextoSms.Segmentos(new string('a', 161)).Should().Be(2);

        var link = ConfirmacaoPorLink.Url("https://divine-bush-1075.clinica-semdor.workers.dev", "ABCDEFGHJKMN");
        var texto = MensagensDeContato.ConfirmacaoPorSms("Maria Aparecida da Silva", new DateTime(2026, 9, 16, 14, 30, 0),
            Hoje, "Clinica SemDor", link);
        texto.Should().Contain(link).And.Contain("Maria").And.NotContain("Aparecida");
        TextoSms.Segmentos(texto).Should().Be(1);
        texto.All(c => c < 128).Should().BeTrue();
    }

    // ==================== A rodada ====================

    [Fact]
    public async Task Desligado_nao_gera_contato_nem_manda()
    {
        var maria = await PacienteAsync("Maria Souza");
        await SessaoAsync(maria.Id, Agora.AddHours(20));

        var r = await Servico().RodarAsync();

        r.Desligado.Should().BeTrue();
        _gateway.Enviados.Should().BeEmpty();
        (await _db.Contatos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Manda_so_a_sessao_cuja_hora_chegou_com_link_e_pedido_minimizado()
    {
        await LigarAsync();
        var maria = await PacienteAsync("Maria Souza Prado");
        var joao = await PacienteAsync("João Lima");
        var naJanela = await SessaoAsync(maria.Id, Agora.AddHours(20));
        var foraDaJanela = await SessaoAsync(joao.Id, Agora.AddHours(30));

        var r = await Servico().RodarAsync();

        r.Enviados.Should().Be(1);
        _gateway.Enviados.Should().ContainSingle(e => e.Telefone == "+5511987654321");

        var c = await ContatoDeAsync(naJanela.Id);
        c!.Canal.Should().Be(CanalContato.Sms);
        c.Status.Should().Be(StatusContato.Enviado);
        c.TokenResposta.Should().NotBeNull();
        ConfirmacaoPorLink.TokenValido(c.TokenResposta).Should().BeTrue();
        _gateway.Enviados[0].Texto.Should().Contain($"/c/{c.TokenResposta}");

        // O pedido no balde: primeiro nome, quando, clínica — sobrenome e telefone NÃO.
        var pedido = Encoding.UTF8.GetString(_balde.Objetos[ConfirmacaoPorLink.CaminhoPedido(c.TokenResposta!)]);
        pedido.Should().Contain("\"Maria\"").And.NotContain("Prado").And.NotContain("98765");

        // A rodada do DIA gera o contato das duas (é a mesma rodada do balcão); a de 30h
        // à frente continua PENDENTE — o SMS dela sai quando chegar a hora dela.
        (await ContatoDeAsync(foraDaJanela.Id))!.Status.Should().Be(StatusContato.Pendente);

        var linha = await _db.MensagensSms.SingleAsync();
        linha.Situacao.Should().Be(SituacaoSms.Enviada);
        linha.Finalidade.Should().Be(FinalidadeSms.ConfirmacaoSessao);
        linha.Segmentos.Should().Be(1);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_manda_duas_vezes()
    {
        await LigarAsync();
        var maria = await PacienteAsync("Maria Souza");
        await SessaoAsync(maria.Id, Agora.AddHours(20));

        await Servico().RodarAsync();
        var r = await Servico(Agora.AddMinutes(5)).RodarAsync();

        _gateway.Enviados.Should().HaveCount(1);
        r.Enviados.Should().Be(0);
    }

    [Fact]
    public async Task A_reserva_e_de_banco_a_segunda_maquina_esbarra_no_indice()
    {
        var m1 = new MensagemSms { ChaveIdempotencia = "CONF:1", Telefone = "+5511987654321", Texto = "x" };
        var m2 = new MensagemSms { ChaveIdempotencia = "CONF:1", Telefone = "+5511987654321", Texto = "x" };
        (await _repo.ReservarMensagemSmsAsync(m1)).Should().BeTrue();
        (await _repo.ReservarMensagemSmsAsync(m2)).Should().BeFalse();
        // O contexto continua utilizável: a recusada saiu dele.
        await _repo.SalvarAsync();
        (await _db.MensagensSms.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Fixo_conta_como_sem_celular_e_quem_ja_foi_avisado_pelo_balcao_nao_recebe()
    {
        await LigarAsync();
        var fixo = await PacienteAsync("Ana Fixo", "(11) 3456-7890");
        var avisada = await PacienteAsync("Bia Avisada");
        await SessaoAsync(fixo.Id, Agora.AddHours(20));
        var sessaoBia = await SessaoAsync(avisada.Id, Agora.AddHours(21));
        await _campanhas.GerarConfirmacoesAsync(DateOnly.FromDateTime(sessaoBia.DataHora));
        var contatoBia = await _db.Contatos.SingleAsync(c => c.AgendamentoId == sessaoBia.Id);
        await _campanhas.RegistrarEnvioAsync(contatoBia.Id, "ana", CanalContato.WhatsApp);

        var r = await Servico().RodarAsync();

        r.SemCelular.Should().Be(1);
        r.JaTratados.Should().Be(1);
        _gateway.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task Falha_do_provedor_deixa_o_contato_pendente_e_a_proxima_rodada_tenta_de_novo()
    {
        await LigarAsync();
        var maria = await PacienteAsync("Maria Souza");
        var s = await SessaoAsync(maria.Id, Agora.AddHours(20));
        _gateway.Falhar = true;

        var r1 = await Servico().RodarAsync();
        r1.Falhas.Should().Be(1);
        (await ContatoDeAsync(s.Id))!.Status.Should().Be(StatusContato.Pendente);
        (await _db.MensagensSms.AsNoTracking().SingleAsync()).Situacao.Should().Be(SituacaoSms.Falhou);
        _balde.Objetos.Keys.Should().NotContain(k => k.StartsWith("c/"), "link de SMS que não saiu sai do ar");

        _gateway.Falhar = false;
        var r2 = await Servico(Agora.AddMinutes(5)).RodarAsync();
        r2.Enviados.Should().Be(1);
        var linha = await _db.MensagensSms.AsNoTracking().SingleAsync();
        linha.Situacao.Should().Be(SituacaoSms.Enviada);
        linha.Tentativas.Should().Be(2);
    }

    [Fact]
    public async Task Sem_endereco_publico_o_sms_sai_sem_link_pedindo_para_ligar()
    {
        await LigarAsync(comLink: false);
        var maria = await PacienteAsync("Maria Souza");
        await SessaoAsync(maria.Id, Agora.AddHours(20));

        var r = await Servico().RodarAsync();

        r.SemLink.Should().Be(1);
        _gateway.Enviados.Single().Texto.Should().Contain("ligue").And.NotContain("http");
    }

    // ==================== A resposta ====================

    [Fact]
    public async Task Nao_vou_pelo_link_vira_nao_vem_e_o_pedido_sai_do_ar()
    {
        await LigarAsync();
        var maria = await PacienteAsync("Maria Souza");
        var s = await SessaoAsync(maria.Id, Agora.AddHours(20));
        await Servico().RodarAsync();
        var token = (await ContatoDeAsync(s.Id))!.TokenResposta!;

        _balde.Objetos[ConfirmacaoPorLink.CaminhoResposta(token)] =
            JsonSerializer.SerializeToUtf8Bytes(new { resposta = "nao-vai", respondidoEmUnixMs = 1_757_500_000_000 });

        var recebidas = await Servico(Agora.AddMinutes(10)).SincronizarRespostasAsync();

        recebidas.Should().Be(1);
        var c = await ContatoDeAsync(s.Id);
        c!.Status.Should().Be(StatusContato.Respondido);
        RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario).Should().Be(SituacaoConfirmacao.PediuRemarcar);

        var respostas = await _repo.RespostasDeConfirmacaoAsync([s.Id]);
        RodadaDeConfirmacao.SeloDaLista(respostas[s.Id]).Should().Be(RodadaDeConfirmacao.SeloNaoVem);
        _balde.Objetos.Keys.Should().NotContain(ConfirmacaoPorLink.CaminhoPedido(token));
        // Responder NÃO cancela o horário: quem libera é a recepção.
        (await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == s.Id)).Status.Should().Be(StatusAgendamento.Agendado);
    }

    [Fact]
    public async Task Confirmo_pelo_link_vira_confirmou()
    {
        await LigarAsync();
        var maria = await PacienteAsync("Maria Souza");
        var s = await SessaoAsync(maria.Id, Agora.AddHours(20));
        await Servico().RodarAsync();
        var token = (await ContatoDeAsync(s.Id))!.TokenResposta!;
        _balde.Objetos[ConfirmacaoPorLink.CaminhoResposta(token)] =
            JsonSerializer.SerializeToUtf8Bytes(new { resposta = "confirma" });

        await Servico(Agora.AddMinutes(10)).SincronizarRespostasAsync();

        var respostas = await _repo.RespostasDeConfirmacaoAsync([s.Id]);
        RodadaDeConfirmacao.SeloDaLista(respostas[s.Id]).Should().Be("Confirmou");
    }

    [Fact]
    public void Resposta_estragada_e_ignorada_nunca_adivinhada()
    {
        LembreteSmsService.LerResposta(Encoding.UTF8.GetBytes("{\"resposta\":\"talvez\"}")).Should().BeNull();
        LembreteSmsService.LerResposta(Encoding.UTF8.GetBytes("não é json")).Should().BeNull();
        LembreteSmsService.LerResposta(Encoding.UTF8.GetBytes("{\"resposta\":\"confirma\"}"))!.Value.Resposta
            .Should().Be(RespostaPeloLink.Confirmou);
    }

    [Fact]
    public async Task Leitura_do_balde_que_falha_nao_estoura()
    {
        await LigarAsync();
        var maria = await PacienteAsync("Maria Souza");
        await SessaoAsync(maria.Id, Agora.AddHours(20));
        await Servico().RodarAsync();
        _balde.RecusaLer = true;

        (await Servico(Agora.AddMinutes(10)).SincronizarRespostasAsync()).Should().Be(0);
    }

    // ==================== Campanha ====================

    [Fact]
    public async Task Campanha_so_alcanca_quem_consentiu_e_tem_celular_e_conta_os_outros()
    {
        await LigarAsync();
        var consentiu = await PacienteAsync("Maria Souza");
        var naoConsentiu = await PacienteAsync("João Lima");
        var consentiuFixo = await PacienteAsync("Ana Fixo", "(11) 3456-7890");
        foreach (var id in new[] { consentiu.Id, consentiuFixo.Id })
            _db.Consentimentos.Add(new() { PacienteId = id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true });
        await _db.SaveChangesAsync();
        var ids = new[] { consentiu.Id, naoConsentiu.Id, consentiuFixo.Id };

        var previa = await Servico().PreverCampanhaAsync(ids, "Semana de acupuntura com horários extras!");
        previa.Destinatarios.Should().ContainSingle(d => d.PacienteId == consentiu.Id);
        previa.SemConsentimento.Should().Be(1);
        previa.SemCelular.Should().Be(1);

        var r = await Servico().EnviarCampanhaAsync(ids, "Semana de acupuntura com horários extras!", "L1", "ana");
        r.Enviados.Should().Be(1);
        _gateway.Enviados.Single().Texto.Should().EndWith(LembreteSmsService.RodapeCampanha.Trim());

        // O mesmo lote de novo não manda de novo.
        (await Servico().EnviarCampanhaAsync(ids, "Semana de acupuntura com horários extras!", "L1", "ana")).JaEnviados.Should().Be(1);
        _gateway.Enviados.Should().HaveCount(1);
    }

    [Fact]
    public async Task Campanha_longa_demais_e_recusada()
    {
        await LigarAsync();
        var act = () => Servico().EnviarCampanhaAsync([1], new string('a', 600), "L2", "ana");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*encurte*");
    }

    // ==================== Provedores ====================

    [Fact]
    public void Configuracao_pela_metade_e_configuracao_nenhuma_e_diz_o_que_falta()
    {
        var c = new CamposSms(ProvedorSms.Twilio, null, null, "tok", null, null, null, 24, true);
        OpcoesSms.De(c).Should().BeNull();
        OpcoesSms.OQueFalta(c).Should().Contain("remetente").And.Contain("Account SID");
        OpcoesSms.De(c with { Remetente = "+15550001111", Conta = "AC1" })!.Endereco.Should().Contain("/Accounts/AC1/");

        var generico = new CamposSms(ProvedorSms.Generico, "http://api.exemplo/sms", null, "t", null, null, null, 24, true);
        OpcoesSms.De(generico).Should().BeNull("token em texto aberto pela rede não é configuração");
        OpcoesSms.OQueFalta(generico).Should().Contain("HTTPS");
        OpcoesSms.OQueFalta(c with { Provedor = ProvedorSms.Desligado }).Should().BeNull();
    }

    [Fact]
    public async Task Cada_provedor_recebe_o_formato_dele()
    {
        var zenvia = OpcoesSms.De(new CamposSms(ProvedorSms.Zenvia, null, null, "tok", "clinica", null, null, 24, true))!;
        using (var p = GatewaySmsHttp.Montar(zenvia, "+5511987654321", "Oi"))
        {
            p.Headers.GetValues("X-API-TOKEN").Should().Equal("tok");
            (await p.Content!.ReadAsStringAsync()).Should().Contain("\"to\":\"5511987654321\"").And.Contain("\"text\":\"Oi\"");
        }

        var generico = OpcoesSms.De(new CamposSms(ProvedorSms.Generico, "https://api.exemplo/sms", null, "tok",
            "Clinica \"A\"", "X-Api-Key", null, 24, true))!;
        using (var p = GatewaySmsHttp.Montar(generico, "+5511987654321", "linha\"aspas"))
        {
            p.Headers.GetValues("X-Api-Key").Should().Equal("tok");
            var corpo = await p.Content!.ReadAsStringAsync();
            var doc = JsonDocument.Parse(corpo).RootElement;    // o escape mantém o JSON válido
            doc.GetProperty("message").GetString().Should().Be("linha\"aspas");
            doc.GetProperty("from").GetString().Should().Be("Clinica \"A\"");
        }
    }

    // ==================== Falsos ====================

    private sealed class GatewayFalso : IGatewaySms
    {
        public List<(string Telefone, string Texto)> Enviados { get; } = [];
        public bool Falhar { get; set; }

        public Task<string?> EnviarAsync(OpcoesSms opcoes, string celularE164, string texto, CancellationToken ct = default)
        {
            if (Falhar) throw new InvalidOperationException("O provedor de SMS recusou (402): saldo insuficiente");
            Enviados.Add((celularE164, texto));
            return Task.FromResult<string?>($"id-{Enviados.Count}");
        }
    }
}
