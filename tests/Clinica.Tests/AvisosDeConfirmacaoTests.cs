using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// AS RESPOSTAS DOS PACIENTES VIRAM AVISO (out/2026): confirmou, não vem e lembrete sem
/// resposta, no sino da Recepção/Gerente e no painel da direção, pela MESMA decisão
/// (<see cref="AvisosDeConfirmacao"/>). O que estes testes protegem:
/// 1. "Sem resposta" é CONSERVADOR: só SMS com link, enviado há 3 h ou mais, sessão futura
///    no horizonte — e o horizonte da sexta enxerga a segunda.
/// 2. Confirmação que a própria recepção marcou NÃO é notícia; a do paciente é.
/// 3. "Não vem" fica até o horário ser remarcado ou cancelado — de qualquer origem.
/// 4. Cada aviso pisca UMA vez, e reabrir o app não repete o dia.
/// 5. A consulta EXECUTA (SQLite aqui, tradução Npgsql em TraducaoNoNpgsqlTests) e o painel
///    da direção repete o número do sino.
/// </summary>
public class AvisosDeConfirmacaoTests : IDisposable
{
    // Quinta-feira, 15/10/2026, 10h.
    private static readonly DateTime Agora = new(2026, 10, 15, 10, 0, 0);
    private static readonly DateTime InicioDoDia = Agora.Date;

    private static ContatoParaAvisoDeConfirmacao Contato(
        int agendamento, DateTime sessao, StatusContato status = StatusContato.Enviado,
        bool link = true, DateTime? enviado = null, DateTime? respondido = null, string? comentario = null,
        string paciente = "Maria Souza", string? profissional = null)
        => new(agendamento, paciente, sessao, profissional, status, link,
            enviado ?? (status == StatusContato.Pendente ? null : Agora.AddHours(-5)), respondido, comentario);

    private static IReadOnlyList<AvisoDeConfirmacao> Decidir(params ContatoParaAvisoDeConfirmacao[] contatos)
        => AvisosDeConfirmacao.Decidir(contatos, Agora, InicioDoDia);

    // ==================== O horizonte ====================

    [Fact]
    public void Horizonte_de_dia_de_semana_e_36h_ou_o_fim_do_dia_seguinte()
    {
        // Quinta 10h: 36 h = sexta 22h; o fim da sexta (sábado 0h) é mais longe.
        AvisosDeConfirmacao.FimDoHorizonte(Agora).Should().Be(new DateTime(2026, 10, 17, 0, 0, 0));
        // Segunda 14h: 36 h = quarta 2h passa do fim da terça.
        AvisosDeConfirmacao.FimDoHorizonte(new DateTime(2026, 10, 19, 14, 0, 0))
            .Should().Be(new DateTime(2026, 10, 21, 2, 0, 0));
    }

    [Fact]
    public void Na_sexta_e_no_fim_de_semana_o_horizonte_alcanca_a_segunda_inteira()
    {
        var segundaFim = new DateTime(2026, 10, 20, 0, 0, 0);
        AvisosDeConfirmacao.FimDoHorizonte(new DateTime(2026, 10, 16, 10, 0, 0)).Should().Be(segundaFim);
        AvisosDeConfirmacao.FimDoHorizonte(new DateTime(2026, 10, 17, 10, 0, 0)).Should().Be(segundaFim);
        AvisosDeConfirmacao.FimDoHorizonte(new DateTime(2026, 10, 18, 22, 0, 0))
            .Should().Be(new DateTime(2026, 10, 20, 10, 0, 0)); // domingo 22h + 36 h
    }

    [Fact]
    public void O_lembrete_de_segunda_sem_resposta_aparece_na_sexta_a_tarde()
    {
        var sexta = new DateTime(2026, 10, 16, 14, 0, 0);
        var segunda8h = new DateTime(2026, 10, 19, 8, 0, 0);
        var c = Contato(1, segunda8h, enviado: new DateTime(2026, 10, 16, 8, 0, 0));
        var avisos = AvisosDeConfirmacao.Decidir([c], sexta, sexta.Date);
        avisos.Should().ContainSingle().Which.Tipo.Should().Be(TipoAvisoConfirmacao.SemResposta);
    }

    [Fact]
    public void Sessao_que_ja_passou_ou_fora_do_horizonte_nao_vira_aviso()
    {
        Decidir(
            Contato(1, Agora.AddMinutes(-1), StatusContato.Respondido, respondido: Agora.AddMinutes(-30),
                comentario: RodadaDeConfirmacao.RespostaNaoVaiPeloLink),
            Contato(2, Agora, StatusContato.Respondido, respondido: Agora.AddMinutes(-30),
                comentario: RodadaDeConfirmacao.RespostaNaoVaiPeloLink),
            Contato(3, new DateTime(2026, 10, 17, 9, 0, 0), StatusContato.Respondido, respondido: Agora,
                comentario: RodadaDeConfirmacao.RespostaNaoVaiPeloLink))
            .Should().BeEmpty();
    }

    // ==================== Sem resposta ====================

    [Fact]
    public void Sem_resposta_e_o_SMS_com_link_enviado_ha_tres_horas_ou_mais()
    {
        var amanha9h = Agora.Date.AddDays(1).AddHours(9);
        var avisos = Decidir(
            Contato(1, amanha9h, enviado: Agora.AddHours(-3)),
            Contato(2, amanha9h, enviado: Agora.AddHours(-3).AddMinutes(1)));
        var aviso = avisos.Should().ContainSingle().Subject;
        aviso.AgendamentoId.Should().Be(1);
        aviso.Tipo.Should().Be(TipoAvisoConfirmacao.SemResposta);
        aviso.Momento.Should().Be(Agora, "o aviso nasce quando a espera de 3 h termina");
        aviso.PedeAcao.Should().BeTrue();
    }

    [Fact]
    public void Sem_link_pendente_ou_dispensado_nao_e_sem_resposta()
    {
        var amanha9h = Agora.Date.AddDays(1).AddHours(9);
        Decidir(
            Contato(1, amanha9h, link: false),                 // WhatsApp manual: quem registra é a recepção
            Contato(2, amanha9h, StatusContato.Pendente),      // não saiu
            Contato(3, amanha9h, StatusContato.Dispensado),    // saiu da rodada por decisão
            new ContatoParaAvisoDeConfirmacao(4, "Sem carimbo", amanha9h, null, StatusContato.Enviado,
                true, null, null, null))                       // enviado sem carimbo: não se acusa
            .Should().BeEmpty();
    }

    // ==================== Confirmou ====================

    [Fact]
    public void Confirmou_pelo_link_ou_pelo_WhatsApp_do_CRM_hoje_vira_noticia()
    {
        var amanha = Agora.Date.AddDays(1).AddHours(14);
        var avisos = Decidir(
            Contato(1, amanha, StatusContato.Respondido, respondido: Agora.AddMinutes(-5),
                comentario: RodadaDeConfirmacao.RespostaConfirmouPeloLink),
            Contato(2, amanha.AddHours(1), StatusContato.Respondido, respondido: Agora.AddMinutes(-2),
                comentario: RodadaDeConfirmacao.RespostaConfirmouPeloWhatsApp));
        avisos.Select(a => a.Tipo).Should().AllBeEquivalentTo(TipoAvisoConfirmacao.Confirmou);
        avisos.Should().HaveCount(2);
        avisos.Should().OnlyContain(a => !a.PedeAcao);
    }

    [Fact]
    public void Confirmacao_marcada_pela_recepcao_ou_de_ontem_nao_e_noticia()
    {
        var amanha = Agora.Date.AddDays(1).AddHours(14);
        Decidir(
            Contato(1, amanha, StatusContato.Respondido, respondido: Agora.AddMinutes(-5), comentario: null),
            Contato(2, amanha, StatusContato.Respondido, respondido: Agora.AddMinutes(-5), comentario: "confirmou por telefone"),
            Contato(3, amanha, StatusContato.Respondido, respondido: InicioDoDia.AddMinutes(-1),
                comentario: RodadaDeConfirmacao.RespostaConfirmouPeloLink))
            .Should().BeEmpty();
    }

    // ==================== Não vem ====================

    [Fact]
    public void Nao_vem_de_qualquer_origem_fica_ate_o_horario_ser_resolvido()
    {
        var amanha = Agora.Date.AddDays(1).AddHours(9);
        var avisos = Decidir(
            Contato(1, amanha, StatusContato.Respondido, respondido: Agora.AddDays(-1),
                comentario: RodadaDeConfirmacao.RespostaNaoVaiPeloLink),
            Contato(2, amanha, StatusContato.Respondido, respondido: Agora.AddHours(-1),
                comentario: RodadaDeConfirmacao.RespostaRemarcar, link: false));
        avisos.Should().HaveCount(2).And.OnlyContain(a => a.Tipo == TipoAvisoConfirmacao.NaoVem && a.PedeAcao);
        avisos[0].Momento.Should().Be(Agora.AddDays(-1));
    }

    // Out/2026: "Cancelar" no WhatsApp é PEDIDO — o sino diz para aceitar na agenda e conta como "não vem".
    [Fact]
    public void Pedido_de_cancelamento_pede_acao_e_conta_como_nao_vem()
    {
        var amanha = Agora.Date.AddDays(1).AddHours(10);
        var avisos = Decidir(
            Contato(1, amanha, StatusContato.Respondido, respondido: Agora.AddHours(-1),
                comentario: RodadaDeConfirmacao.RespostaCancelar, link: false));
        var aviso = avisos.Should().ContainSingle().Subject;
        aviso.Tipo.Should().Be(TipoAvisoConfirmacao.PediuCancelar);
        aviso.PedeAcao.Should().BeTrue();
        AvisosDeConfirmacao.Titulo(aviso).Should().Contain("CANCELAR").And.Contain("agenda");
        AvisosDeConfirmacao.Contar(avisos).Should().Be((1, 0));
    }

    [Fact]
    public void Nao_vem_vem_primeiro_depois_sem_resposta_depois_confirmou_e_pela_hora_da_sessao()
    {
        var d = Agora.Date.AddDays(1);
        var avisos = Decidir(
            Contato(1, d.AddHours(8), StatusContato.Respondido, respondido: Agora.AddMinutes(-1),
                comentario: RodadaDeConfirmacao.RespostaConfirmouPeloLink),
            Contato(2, d.AddHours(15)),
            Contato(3, d.AddHours(16), StatusContato.Respondido, respondido: Agora.AddMinutes(-1),
                comentario: RodadaDeConfirmacao.RespostaNaoVaiPeloLink),
            Contato(4, d.AddHours(9)),
            Contato(5, d.AddHours(11), StatusContato.Respondido, respondido: Agora.AddMinutes(-1),
                comentario: RodadaDeConfirmacao.RespostaRemarcar));
        avisos.Select(a => a.AgendamentoId).Should().Equal(5, 3, 4, 2, 1);
        AvisosDeConfirmacao.Contar(avisos).Should().Be((2, 2));
    }

    [Fact]
    public void Dois_contatos_do_mesmo_horario_decide_o_respondido()
    {
        var amanha = Agora.Date.AddDays(1).AddHours(9);
        Decidir(
                Contato(1, amanha),
                Contato(1, amanha, StatusContato.Respondido, respondido: Agora.AddMinutes(-1),
                    comentario: RodadaDeConfirmacao.RespostaNaoVaiPeloLink))
            .Should().ContainSingle().Which.Tipo.Should().Be(TipoAvisoConfirmacao.NaoVem);
    }

    // ==================== Anunciar uma vez ====================

    private static AvisoDeConfirmacao Aviso(int id, TipoAvisoConfirmacao tipo, DateTime momento)
        => new(id, "Ana", Agora.AddDays(1), null, tipo, momento);

    [Fact]
    public void Primeira_leitura_semeia_o_que_e_anterior_a_abertura_e_anuncia_o_resto()
    {
        var abertura = Agora;
        var anunciados = new HashSet<(int, TipoAvisoConfirmacao)>();
        var avisos = new[]
        {
            Aviso(1, TipoAvisoConfirmacao.NaoVem, abertura.AddHours(-2)),        // antes: semeia
            Aviso(2, TipoAvisoConfirmacao.Confirmou, abertura.AddMinutes(-5)),   // dentro da folga
        };
        var novos = AvisosDeConfirmacao.ParaAnunciar(avisos, anunciados, abertura - AvisosDeConfirmacao.FolgaDaAbertura);
        novos.Select(a => a.AgendamentoId).Should().Equal(2);
        anunciados.Should().HaveCount(2, "o semeado não pisca, mas também não volta a ser novo");

        // A releitura seguinte: nada se repete; o aviso velho que só agora chegou (o relógio do
        // SMS atrasou) é notícia; o "sem resposta" que virou "não vem" também.
        var depois = avisos.Concat([
            Aviso(3, TipoAvisoConfirmacao.Confirmou, abertura.AddHours(-3)),
            Aviso(1, TipoAvisoConfirmacao.SemResposta, abertura.AddHours(-1))
        ]).ToList();
        AvisosDeConfirmacao.ParaAnunciar(depois, anunciados, null).Select(a => a.Chave)
            .Should().Equal((3, TipoAvisoConfirmacao.Confirmou), (1, TipoAvisoConfirmacao.SemResposta));
        AvisosDeConfirmacao.ParaAnunciar(depois, anunciados, null).Should().BeEmpty();
    }

    // ==================== As frases ====================

    [Fact]
    public void As_frases_dizem_quem_quando_e_o_que_fazer()
    {
        var amanha14h = Agora.Date.AddDays(1).AddHours(14);
        AvisosDeConfirmacao.Frase(new(1, "Maria", amanha14h, null, TipoAvisoConfirmacao.Confirmou, Agora), Agora)
            .Should().Be("Maria confirmou a sessão de amanhã 14:00");
        AvisosDeConfirmacao.Frase(new(1, "João", new DateTime(2026, 10, 19, 9, 0, 0), "Dra. Ana",
                TipoAvisoConfirmacao.NaoVem, Agora), Agora)
            .Should().Be("João avisou que NÃO VEM — seg, 19/10 09:00 (Dra. Ana): ligue para remarcar");
        AvisosDeConfirmacao.Frase(new(1, "Ana", Agora.AddHours(4), null, TipoAvisoConfirmacao.SemResposta, Agora), Agora)
            .Should().Be("Ana não respondeu ao lembrete de hoje 14:00: vale uma ligação");
    }

    [Fact]
    public void A_linha_do_sino_tem_titulo_sessao_e_desde_quando()
    {
        var a = new AvisoDeConfirmacao(1, "João", Agora.Date.AddDays(1).AddHours(9), "Dra. Ana",
            TipoAvisoConfirmacao.NaoVem, Agora.AddDays(-1).Date.AddHours(18).AddMinutes(40));
        AvisosDeConfirmacao.Titulo(a).Should().Be("João NÃO VEM — ligar para remarcar");
        AvisosDeConfirmacao.Sessao(a, Agora).Should().Be("amanhã 09:00 · Dra. Ana");
        AvisosDeConfirmacao.Detalhe(a, Agora).Should().Be("avisou ontem às 18:40");
        AvisosDeConfirmacao.Detalhe(a with { Tipo = TipoAvisoConfirmacao.SemResposta, Momento = Agora.AddHours(-1) }, Agora)
            .Should().Be("SMS sem resposta desde 09:00");
        AvisosDeConfirmacao.Detalhe(a with { Momento = DateTime.MinValue }, Agora).Should().Be("avisou que não vai");
    }

    // ==================== A leitura do banco e o painel da direção ====================

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public AvisosDeConfirmacaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> SessaoComContatoAsync(string nome, DateTime quando, StatusContato status,
        string? comentario = null, DateTime? respondido = null, bool link = true,
        StatusAgendamento situacao = StatusAgendamento.Agendado, int? profissionalId = null)
    {
        var p = new Paciente { Nome = nome, Telefone = "(11) 98765-4321", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var ag = new Agendamento
        {
            PacienteId = p.Id, DataHora = quando, DuracaoMinutos = 30, Status = situacao, ProfissionalId = profissionalId,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = "Consulta"
        };
        _db.Agendamentos.Add(ag);
        await _db.SaveChangesAsync();
        _db.Contatos.Add(new ContatoCampanha
        {
            PacienteId = p.Id, AgendamentoId = ag.Id, Tipo = TipoContato.ConfirmacaoSessao,
            Origem = ContatoCampanha.OrigemDeAgendamento(ag.Id), Referencia = DateOnly.FromDateTime(quando),
            Status = status, Canal = CanalContato.Sms, EnviadoEm = Agora.AddHours(-20), RespondidoEm = respondido,
            Comentario = comentario, TokenResposta = link ? $"TOKEN{ag.Id:D7}" : null
        });
        await _db.SaveChangesAsync();
        return ag.Id;
    }

    [Fact]
    public async Task A_consulta_executa_e_so_traz_sessao_marcada_na_janela()
    {
        var dra = new Profissional { Nome = "Dra. Ana Souza", NomeCurto = "Dra. Ana" };
        var dr = new Profissional { Nome = "Dr. Bruno Lima" };
        _db.Profissionais.AddRange(dra, dr);
        await _db.SaveChangesAsync();
        var amanha = Agora.Date.AddDays(1);

        var naoVem = await SessaoComContatoAsync("João", amanha.AddHours(9), StatusContato.Respondido,
            RodadaDeConfirmacao.RespostaNaoVaiPeloLink, Agora.AddHours(-2), profissionalId: dra.Id);
        var semResposta = await SessaoComContatoAsync("Ana", amanha.AddHours(8).AddMinutes(30), StatusContato.Enviado,
            profissionalId: dr.Id);
        await SessaoComContatoAsync("Cancelada", amanha.AddHours(10), StatusContato.Respondido,
            RodadaDeConfirmacao.RespostaNaoVaiPeloLink, Agora.AddHours(-2), situacao: StatusAgendamento.Cancelado);
        await SessaoComContatoAsync("Pendente", amanha.AddHours(11), StatusContato.Pendente);
        await SessaoComContatoAsync("Longe", Agora.AddDays(5), StatusContato.Enviado);
        await SessaoComContatoAsync("Passou", Agora.AddHours(-1), StatusContato.Enviado);

        var linhas = await _repo.ContatosParaAvisosDeConfirmacaoAsync(Agora, AvisosDeConfirmacao.FimDoHorizonte(Agora));
        linhas.Select(l => l.Paciente).Should().Equal("Ana", "João");
        linhas.Single(l => l.Paciente == "João").Profissional.Should().Be("Dra. Ana");
        linhas.Single(l => l.Paciente == "Ana").Profissional.Should().Be("Dr. Bruno Lima");
        linhas.Should().OnlyContain(l => l.TemLink);

        var avisos = await new AvisosDeConfirmacaoService(_repo).ListarAsync(Agora, InicioDoDia);
        avisos.Select(a => (a.AgendamentoId, a.Tipo)).Should().Equal(
            (naoVem, TipoAvisoConfirmacao.NaoVem), (semResposta, TipoAvisoConfirmacao.SemResposta));
    }

    [Fact]
    public async Task O_painel_da_direcao_repete_o_numero_do_sino_e_leva_as_confirmacoes()
    {
        var amanha = Agora.Date.AddDays(1);
        await SessaoComContatoAsync("João", amanha.AddHours(9), StatusContato.Respondido,
            RodadaDeConfirmacao.RespostaNaoVaiPeloLink, Agora.AddHours(-2));
        await SessaoComContatoAsync("Rita", amanha.AddHours(10), StatusContato.Respondido,
            RodadaDeConfirmacao.RespostaRemarcar, Agora.AddHours(-1), link: false);
        await SessaoComContatoAsync("Ana", amanha.AddHours(8), StatusContato.Enviado);
        await SessaoComContatoAsync("Bia", amanha.AddHours(15), StatusContato.Respondido,
            RodadaDeConfirmacao.RespostaConfirmouPeloLink, Agora.AddMinutes(-3));

        var sino = AvisosDeConfirmacao.Contar(await new AvisosDeConfirmacaoService(_repo).ListarAsync(Agora, InicioDoDia));
        sino.Should().Be((2, 1));

        var painel = await Painel().MontarAsync(DateOnly.FromDateTime(Agora), agora: Agora);
        painel.PacientesNaoVem.Should().Be(sino.NaoVem);
        painel.LembretesSemResposta.Should().Be(sino.SemResposta);
        painel.Alertas.Should().Contain(a => a.Assunto == AssuntoDirecao.PacientesNaoVem && a.Titulo.StartsWith("2 "));
        painel.Alertas.Should().Contain(a => a.Assunto == AssuntoDirecao.LembretesSemResposta && a.Titulo.StartsWith("1 "));
        painel.NaoVerificados.Should().NotContain("Respostas dos pacientes");
    }

    [Fact]
    public async Task Sem_resposta_nenhuma_o_painel_nao_alerta()
    {
        var painel = await Painel().MontarAsync(DateOnly.FromDateTime(Agora), agora: Agora);
        painel.Alertas.Should().NotContain(a => a.Assunto == AssuntoDirecao.PacientesNaoVem
                                               || a.Assunto == AssuntoDirecao.LembretesSemResposta);
        painel.PacientesNaoVem.Should().Be(0);
    }

    private PainelDirecaoService Painel()
    {
        var financeiro = new FinanceiroService(_repo);
        var contas = new ContasService(_repo);
        var parametros = new ParametrosService(_repo);
        var pendencias = new PendenciaService(_repo, parametros);
        return new PainelDirecaoService(
            _repo, financeiro, contas,
            new RecebiveisService(_repo),
            new FechamentoCaixaService(_repo),
            new RodadaPendenciasService(_repo, pendencias, parametros),
            pendencias,
            new InadimplenciaService(_repo, financeiro),
            new ReceitaGlosadaService(_repo, financeiro),
            new MetaService(_repo, financeiro, new IndicadoresService(_repo, parametros)),
            new OrcamentoService(_repo, contas),
            new ConsultorioService(_repo),
            new ChecagemPrescricaoService(_repo));
    }
}
