using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O WHATSAPP DA RECEPÇÃO COMPÕE E GRAVA O QUE PROMETE (out/2026, PR 3 e PR 4 do WhatsApp
/// nativo) — sobre o app montado pelo DI e o SQLite da casa, com um CRM falso que só guarda o
/// que recebeu. Cada teste afirma o BANCO (ou o pedido que saiu para o CRM), não a frase:
///
/// - a lista liga o aviso do sino à conversa da ficha conferida, e o telefone FORMATADO do
///   cadastro acha a ficha da conversa sem ficha (busca no banco, só dígitos);
/// - abrir marca como lida, e a coluna do paciente traz a ficha, os horários e a confirmação;
/// - confirmar, "não vem" e manter gravam a MESMA resposta da tela Confirmações;
/// - cancelar suspende pela agenda, com o motivo na trilha, e deixa a nota na conversa;
/// - o convite da retenção sai como modelo (Guid "D") e grava o contato no caso no mesmo clique;
///   sem autorização, nada sai para o CRM;
/// - "Não quer voltar" com PARAR encerra o caso e revoga a autorização da ficha.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class WhatsAppDaRecepcaoServiceTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly ServiceProvider _sp;
    private readonly CrmFalso _crm = new();
    private readonly UsuarioSistema _ana = new() { Nome = "Ana Paula", Login = "ana", Perfil = PerfilAcesso.Gerente, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
    private readonly Paciente _maria = new() { Nome = "Maria das Graças Silva", Telefone = "(22) 99876-5432", Documento = "12345678942", Convenio = Convenio.UnimedPadrao };
    private readonly Paciente _pedro = new() { Nome = "Pedro Henrique Souza", Telefone = "22 98811-2233" };
    private readonly Paciente _terezinha = new() { Nome = "Terezinha Souza", Telefone = "+55 (22) 98811-2233" };
    private readonly Paciente _sebastiao = new() { Nome = "Sebastião Ramos", Telefone = "22991204477" };

    public WhatsAppDaRecepcaoServiceTests()
    {
        _conexao.Open();
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=teste;Database=teste");
        foreach (var d in servicos.Where(d => d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>)
                     || d.ServiceType == typeof(DbContextOptions) || d.ServiceType == typeof(ClinicaDbContext)
                     || d.ServiceType == typeof(IClienteDoCrm)).ToList())
            servicos.Remove(d);
        servicos.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conexao));
        servicos.AddSingleton<IClienteDoCrm>(_crm);
        _sp = servicos.BuildServiceProvider();
        using (var db = Db())
        {
            db.Database.EnsureCreated();
            db.Usuarios.Add(_ana);
            db.Pacientes.AddRange(_maria, _pedro, _terezinha, _sebastiao);
            db.SaveChanges();
        }
        new SessaoUsuario().Entrar(_ana);
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _sp.Dispose();
        _conexao.Dispose();
    }

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);

    private async Task<T> Com<T>(Func<WhatsAppDaRecepcaoService, Task<T>> f)
    {
        using var escopo = _sp.CreateScope();
        return await f(escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>());
    }

    private async Task Com(Func<WhatsAppDaRecepcaoService, Task> f)
    {
        using var escopo = _sp.CreateScope();
        await f(escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>());
    }

    /// <summary>Um horário de amanhã às 10:00 (dentro do horizonte do sino em qualquer dia da semana) com o contato da rodada.</summary>
    private (int Agendamento, int Contato) HorarioComContato(Paciente p, StatusContato status, string? comentario)
    {
        using var db = Db();
        var ag = new Agendamento { PacienteId = p.Id, DataHora = DateTime.Today.AddDays(1).AddHours(10), ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples };
        db.Agendamentos.Add(ag);
        db.SaveChanges();
        var contato = new ContatoCampanha
        {
            PacienteId = p.Id, Tipo = TipoContato.ConfirmacaoSessao, AgendamentoId = ag.Id, Status = status,
            EnviadoEm = DateTime.Now.AddHours(-4), RespondidoEm = status == StatusContato.Respondido ? DateTime.Now.AddHours(-1) : null,
            Comentario = comentario, Referencia = DateOnly.FromDateTime(ag.DataHora), Origem = "AGD:" + ag.Id
        };
        db.Contatos.Add(contato);
        db.SaveChanges();
        return (ag.Id, contato.Id);
    }

    private int CasoDeRecall(Paciente p, bool autorizou)
    {
        using var db = Db();
        if (autorizou)
            db.Consentimentos.Add(new ConsentimentoLgpd { PacienteId = p.Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true });
        var caso = new AcompanhamentoPaciente
        {
            PacienteId = p.Id, Tipo = TipoAcompanhamento.Recall, Modalidade = ModalidadeAtendimento.AcupunturaSimples,
            ReferenciaEm = DateTime.Today.AddDays(-32), ProximoContato = DateOnly.FromDateTime(DateTime.Today),
            Etapa = EtapaAcompanhamento.AContatar, CriadoEm = DateTime.Now, CriadoPor = "teste"
        };
        db.Acompanhamentos.Add(caso);
        db.SaveChanges();
        return caso.Id;
    }

    // ------------------------------------------------------------------ leituras

    [Fact]
    public async Task Lista_liga_o_aviso_do_sino_a_ficha_conferida_e_acha_as_fichas_pelo_telefone_formatado()
    {
        HorarioComContato(_maria, StatusContato.Respondido, RodadaDeConfirmacao.RespostaRemarcar);
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome));
        _crm.Pôr(Conversa("c-terezinha", "Terezinha", "5522988112233"));
        _crm.Pôr(Conversa("c-novo", "5522997120045", "5522997120045"));

        var painel = await Com(s => s.LerAsync());
        var linhas = WhatsAppDaRecepcaoService.Linhas(painel, FiltroDasConversas.Todas, null, false, WhatsAppDaRecepcaoService.Agora);

        Assert.Equal("Não vem — remarcar", linhas.Single(l => l.Id == "c-maria").Selo.Texto);
        // O telefone gravado de três jeitos ("22 98811-2233", "+55 (22) 98811-2233") acha as duas fichas.
        Assert.Equal(2, painel.FichasPorConversa["c-terezinha"].Count);
        Assert.Equal("2 fichas — conferir", linhas.Single(l => l.Id == "c-terezinha").Selo.Texto);
        Assert.Equal("Novo contato · sem ficha", linhas.Single(l => l.Id == "c-novo").Selo.Texto);
        Assert.Equal("?", linhas.Single(l => l.Id == "c-novo").Iniciais);
        Assert.True(painel.RetencaoPermitida);
    }

    [Fact]
    public async Task Abrir_marca_como_lida_e_traz_a_ficha_os_horarios_e_a_confirmacao()
    {
        var (agendamento, _) = HorarioComContato(_maria, StatusContato.Respondido, RodadaDeConfirmacao.RespostaRemarcar);
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome, recebidas: 3), naoLidas: 1);
        var painel = await Com(s => s.LerAsync());

        var aberta = await Com(s => s.AbrirAsync(painel, "c-maria", naoLidas: 1));

        Assert.Equal(3, Assert.Single(_crm.Lidas).IncomingCount);
        Assert.NotNull(aberta.Paciente);
        Assert.True(aberta.Paciente!.TelefoneConfere);
        Assert.Equal("final 42", aberta.Paciente.FimDoDocumento);
        var h = Assert.Single(aberta.Dados.Horarios);
        Assert.Equal(agendamento, h.AgendamentoId);
        Assert.Equal(RodadaDeConfirmacao.SeloNaoVem, h.Situacao);
        Assert.True(h.PodeManter);
        Assert.False(h.PodeConfirmar);
        Assert.Equal(TipoDaAcaoPedida.NaoVem, aberta.Acao?.Tipo);
        Assert.Contains(aberta.LinhaDoTempo, i => i.Tipo == TipoNaLinhaDoTempo.Novas);
    }

    [Fact]
    public async Task Conversa_sem_ficha_lista_as_candidatas_e_nao_le_ficha_nenhuma()
    {
        _crm.Pôr(Conversa("c-terezinha", "Terezinha", "5522988112233"));
        var painel = await Com(s => s.LerAsync());

        var aberta = await Com(s => s.AbrirAsync(painel, "c-terezinha", 0));

        Assert.Null(aberta.Paciente);
        Assert.Equal(new[] { _pedro.Id, _terezinha.Id }.Order(), aberta.Dados.Candidatas.Select(c => c.Id).Order());
        Assert.Equal(EstadoFicha.AConferir, aberta.Ficha.Estado);
        Assert.Equal(TipoDaAcaoPedida.ConferirFicha, aberta.Acao?.Tipo);
        Assert.Empty(_crm.Lidas);
    }

    [Fact]
    public async Task Repositorio_acha_ficha_por_telefone_e_dono_do_horario_no_banco()
    {
        var (agendamento, _) = HorarioComContato(_maria, StatusContato.Enviado, null);
        using var escopo = _sp.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();

        var fichas = await repo.FichasComTelefoneAsync(WhatsAppDaRecepcaoService.Variantes("5522998765432"), 5);
        Assert.Equal(_maria.Id, Assert.Single(fichas).Id);
        Assert.Empty(await repo.FichasComTelefoneAsync(WhatsAppDaRecepcaoService.Variantes("5521900000000"), 5));
        Assert.Equal(_maria.Id, (await repo.PacientesDosAgendamentosAsync([agendamento, 999]))[agendamento]);
    }

    // ------------------------------------------------------------------ confirmação e agenda

    [Fact]
    public async Task Confirmar_nao_vem_e_manter_gravam_a_mesma_resposta_da_tela_Confirmacoes()
    {
        var (a1, c1) = HorarioComContato(_maria, StatusContato.Enviado, null);
        var (a2, c2) = HorarioComContato(_pedro, StatusContato.Enviado, null);
        var (a3, c3) = HorarioComContato(_sebastiao, StatusContato.Respondido, RodadaDeConfirmacao.RespostaCancelar);

        await Com(s => s.ConfirmouPorTelefoneAsync(a1));
        await Com(s => s.NaoVemAsync(a2));
        await Com(s => s.ManterHorarioAsync(a3));

        using var db = Db();
        Assert.Equal(SituacaoConfirmacao.Confirmou, Situacao(db.Contatos.Single(c => c.Id == c1)));
        Assert.Equal(SituacaoConfirmacao.PediuRemarcar, Situacao(db.Contatos.Single(c => c.Id == c2)));
        var mantido = db.Contatos.Single(c => c.Id == c3);
        Assert.Equal(RodadaDeConfirmacao.RespostaManteveHorario, mantido.Comentario);
        Assert.Contains(db.Auditoria.ToList(), e => e.Acao == "PedidoDeCancelamentoRevertido" && e.Operador == _ana.Login);
    }

    [Fact]
    public async Task Confirmar_horario_fora_da_rodada_recusa_sem_inventar_resposta()
    {
        int ag;
        using (var db = Db())
        {
            var a = new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Today.AddDays(2).AddHours(9) };
            db.Agendamentos.Add(a);
            db.SaveChanges();
            ag = a.Id;
        }
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.ConfirmouPorTelefoneAsync(ag)));
        Assert.Contains("rodada", erro.Message);
        using var depois = Db();
        Assert.Empty(depois.Contatos.ToList());
    }

    [Fact]
    public async Task Cancelar_pela_conversa_suspende_pela_agenda_com_motivo_e_deixa_a_nota()
    {
        var (agendamento, _) = HorarioComContato(_maria, StatusContato.Respondido, RodadaDeConfirmacao.RespostaRemarcar);
        var conversa = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id));

        var (_, depois, pendencia) = await Com(s => s.CancelarHorarioAsync(agendamento, "Paciente pediu", conversa, "amanhã 10:00"));

        Assert.Null(pendencia);
        Assert.NotNull(depois);
        using var db = Db();
        Assert.Equal(StatusAgendamento.Cancelado, db.Agendamentos.Single(a => a.Id == agendamento).Status);
        Assert.Contains(db.Auditoria.ToList(), e => e.Acao == "AgendamentoCancelado" && e.Detalhe!.Contains("motivo: Paciente pediu") && e.Operador == _ana.Login);
        var nota = Assert.Single(_crm.Notas);
        Assert.Contains("motivo: Paciente pediu", nota.Text);
        Assert.Contains("Nenhuma mensagem foi ao paciente", nota.Text);
        Assert.True(Guid.TryParseExact(nota.Id, "D", out _));
    }

    [Fact]
    public async Task Cancelar_sem_motivo_recusa_e_o_horario_fica()
    {
        var (agendamento, _) = HorarioComContato(_maria, StatusContato.Enviado, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.CancelarHorarioAsync(agendamento, "  ", null, "amanhã")));
        using var db = Db();
        Assert.Equal(StatusAgendamento.Agendado, db.Agendamentos.Single(a => a.Id == agendamento).Status);
    }

    // ------------------------------------------------------------------ retenção

    [Fact]
    public async Task Convite_da_retencao_sai_como_modelo_e_grava_o_contato_no_caso_no_mesmo_clique()
    {
        var casoId = CasoDeRecall(_sebastiao, autorizou: true);
        var painel = await Com(s => s.LerAsync());
        var caso = painel.Retencao.Single(l => l.Id == casoId);

        var aberta = await Com(s => s.AbrirCasoAsync(painel, caso));
        Assert.Null(aberta.Conversa);
        Assert.Equal(SituacaoDaJanela.NuncaEscreveu, aberta.Janela.Estado);
        Assert.Equal(ModeloDoWhatsApp.RetornoTratamento, aberta.Abertura?.ModeloSugerido);

        var modelo = new AppTemplate(ModeloDoWhatsApp.RetornoTratamento, "pt_BR", "APPROVED", "Olá, {{1}}! Sentimos sua falta na {{2}}: já são {{3}} dias.", 3, true, "", [], "UTILITY");
        var envio = Guid.NewGuid().ToString("D");
        var r = await Com(s => s.EnviarModeloAsync(null, "5522333300000", modelo, ["Sebastião", "acupuntura", "32"], caso, envio));

        Assert.Null(r.Pendencia);
        var nova = Assert.Single(_crm.Novas);
        Assert.Equal("5522991204477", nova.Contact);
        Assert.Equal(_sebastiao.Id, Assert.Single(_crm.Ligacoes).PatientId);
        var resposta = Assert.Single(_crm.Respostas);
        Assert.Equal(envio, resposta.Id);
        Assert.Equal(ModeloDoWhatsApp.RetornoTratamento, resposta.Template?.Name);
        using var db = Db();
        var gravado = db.Acompanhamentos.Include(a => a.Contatos).Single(a => a.Id == casoId);
        Assert.Equal(EtapaAcompanhamento.SemResposta, gravado.Etapa);
        Assert.True(gravado.ProximoContato > DateOnly.FromDateTime(DateTime.Today));
        Assert.Contains(gravado.Contatos, c => c.Canal == CanalContato.WhatsApp && c.Operador == _ana.Login && c.Observacao.Contains(modelo.Name));
    }

    [Fact]
    public async Task Sem_autorizacao_nenhum_convite_sai_para_o_CRM()
    {
        var casoId = CasoDeRecall(_sebastiao, autorizou: false);
        var painel = await Com(s => s.LerAsync());
        var caso = painel.Retencao.Single(l => l.Id == casoId);
        var modelo = new AppTemplate(ModeloDoWhatsApp.RetornoTratamento, "pt_BR", "APPROVED", "Olá, {{1}}!", 1, true, "", [], "UTILITY");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Com(s => s.EnviarModeloAsync(null, "5522333300000", modelo, ["Sebastião"], caso, Guid.NewGuid().ToString("D"))));

        Assert.Empty(_crm.Novas);
        Assert.Empty(_crm.Respostas);
        var aberta = await Com(s => s.AbrirCasoAsync(painel, caso));
        Assert.Null(aberta.Abertura?.ModeloSugerido);
        Assert.DoesNotContain(aberta.Abertura!.Botoes, b => b.Acao is BotaoDaAcao.PrepararConvite or BotaoDaAcao.PrepararMensagem);
    }

    [Fact]
    public async Task Nao_quer_voltar_com_PARAR_encerra_o_caso_e_revoga_a_autorizacao()
    {
        var casoId = CasoDeRecall(_sebastiao, autorizou: true);
        using (var db = Db())
        {
            db.MotivosAcompanhamento.Add(new MotivoAcompanhamento { Nome = "Mudou de cidade", Ativo = true });
            db.SaveChanges();
        }
        var painel = await Com(s => s.LerAsync());
        var caso = painel.Retencao.Single(l => l.Id == casoId);
        var motivo = (await Com(s => s.MotivosAsync())).Single(m => m.Nome == "Mudou de cidade");

        // Sem justificativa: o impedimento volta inline, nada grava.
        Assert.NotNull(await Com(s => s.RegistrarDesfechoAsync(caso, DesfechoContato.NaoQuerVoltar, CanalContato.Telefone, null, "", Guid.NewGuid(), motivo.Id)));
        Assert.Null(await Com(s => s.RegistrarDesfechoAsync(caso, DesfechoContato.NaoQuerVoltar, CanalContato.Telefone, null,
            "Mudou para Campos e vai continuar lá", Guid.NewGuid(), motivo.Id)));
        Assert.True(await Com(s => s.RegistrarPararAsync(_sebastiao.Id)));
        Assert.False(await Com(s => s.RegistrarPararAsync(_sebastiao.Id)));

        using var depois = Db();
        Assert.NotNull(depois.Acompanhamentos.Single(a => a.Id == casoId).EncerradoEm);
        var consentimento = depois.Consentimentos.Single(c => c.PacienteId == _sebastiao.Id);
        Assert.NotNull(consentimento.RevogadoEm);
        Assert.Contains(depois.Auditoria.ToList(), e => e.Acao == "ConsentimentoRevogado" && e.Operador == _ana.Login);
    }

    [Fact]
    public async Task Caso_com_conversa_da_mesma_ficha_abre_a_conversa_do_CRM()
    {
        var casoId = CasoDeRecall(_sebastiao, autorizou: true);
        _crm.Pôr(Conversa("c-seb", "Sebastião", "5522991204477", pacienteId: _sebastiao.Id));
        var painel = await Com(s => s.LerAsync());

        var aberta = await Com(s => s.AbrirCasoAsync(painel, painel.Retencao.Single(l => l.Id == casoId)));

        Assert.Equal("c-seb", aberta.Id);
        Assert.Equal(casoId, aberta.Caso?.Id);
    }

    // ------------------------------------------------------------------ peças puras

    [Fact]
    public void Releitura_junta_pelo_id_e_marca_a_que_fechou()
    {
        var agora = DateTimeOffset.Now;
        var a = Resumo("a", agora.AddMinutes(-5));
        var b = Resumo("b", agora.AddMinutes(-4));
        var juntadas = WhatsAppDaRecepcaoService.Juntar([a, b],
            new AppConversationsSince([a with { Unread = 2, UpdatedAt = agora }], ["b"], agora));
        Assert.Equal(["a", "b"], juntadas.Select(c => c.Id));
        Assert.Equal(2, juntadas[0].Unread);
        Assert.True(juntadas[1].Closed);
    }

    [Fact]
    public void Hora_da_linha_tom_do_avatar_e_documento()
    {
        var agora = new DateTimeOffset(2026, 10, 24, 10, 12, 0, TimeSpan.FromHours(-3));
        Assert.Equal("10:09", WhatsAppDaRecepcaoService.HoraDaLinha(agora.AddMinutes(-3), agora));
        Assert.Equal("ontem", WhatsAppDaRecepcaoService.HoraDaLinha(agora.AddDays(-1), agora));
        // 24/10/2026 é sábado (o mockup erra os dias da semana): 22/10 é quinta.
        Assert.Equal("qui", WhatsAppDaRecepcaoService.HoraDaLinha(agora.AddDays(-2), agora));
        Assert.Equal("22/09", WhatsAppDaRecepcaoService.HoraDaLinha(agora.AddDays(-32), agora));
        // O CRM manda o carimbo em UTC: a hora é a do fuso da clínica.
        Assert.Equal("10:09", WhatsAppDaRecepcaoService.HoraDaLinha(agora.AddMinutes(-3).ToUniversalTime(), agora));
        Assert.Equal(WhatsAppDaRecepcaoService.TomDoAvatar("c-maria"), WhatsAppDaRecepcaoService.TomDoAvatar("c-maria"));
        Assert.Equal("cinza", WhatsAppDaRecepcaoService.TomDoAvatar(null));
        Assert.Equal("final 42", WhatsAppDaRecepcaoService.FimDoDocumento("123.456.789-42"));
        Assert.Equal("—", WhatsAppDaRecepcaoService.FimDoDocumento(null));
        Assert.Equal(["22998765432", "5522998765432"], WhatsAppDaRecepcaoService.Variantes("5522998765432"));
        Assert.Empty(WhatsAppDaRecepcaoService.Variantes("123"));
        Assert.Equal("5522998765432", WhatsAppDaRecepcaoService.ContatoParaOCrm("(22) 99876-5432"));
    }

    [Fact]
    public void Rascunho_de_remarcacao_diz_o_que_a_clinica_fez()
    {
        var hoje = new DateOnly(2026, 10, 24);
        var novo = new HorarioDoPaciente(1, new DateTime(2026, 10, 28, 14, 0, 0), "", "", "", "", false, false, false, 2,
            ModalidadeAtendimento.AcupunturaSimples, "Dra. Helena");
        Assert.Equal("Combinado, Maria! Remarquei a sua acupuntura para quarta-feira, 28/10 às 14:00 com Dra. Helena. O lembrete chega na manhã do dia.",
            WhatsAppDaRecepcaoService.RascunhoDeRemarcacao("Maria das Graças Silva", novo, hoje));
    }

    [Fact]
    public async Task Convite_usa_o_modelo_aprovado_na_Meta_e_so_vale_para_modelo_de_convite()
    {
        var casoId = CasoDeRecall(_sebastiao, autorizou: true);
        var painel = await Com(s => s.LerAsync());
        var caso = painel.Retencao.Single(l => l.Id == casoId);
        AppTemplate M(string nome, bool aceito = true) => new(nome, "pt_BR", "APPROVED", "Olá, {{1}}!", 1, aceito, aceito ? "" : "Não suportado", [], "UTILITY");

        Assert.Equal(ModeloDoWhatsApp.RecallReal, WhatsAppDaRecepcaoService.ModeloDoConvite(caso, [M(ModeloDoWhatsApp.RetornoTratamento), M(ModeloDoWhatsApp.RecallReal)])?.Name);
        Assert.Equal(ModeloDoWhatsApp.RetornoTratamento, WhatsAppDaRecepcaoService.ModeloDoConvite(caso, [M(ModeloDoWhatsApp.RecallReal, aceito: false), M(ModeloDoWhatsApp.RetornoTratamento)])?.Name);
        Assert.Null(WhatsAppDaRecepcaoService.ModeloDoConvite(caso, [M("semdor_lembrete_sessao")]));

        var aberta = await Com(s => s.AbrirCasoAsync(painel, caso));
        Assert.Same(caso, WhatsAppDaRecepcaoService.CasoDoConvite(aberta, M(ModeloDoWhatsApp.RecallReal)));
        Assert.Null(WhatsAppDaRecepcaoService.CasoDoConvite(aberta, M("semdor_lembrete_sessao")));
    }

    [Fact]
    public async Task Envio_no_409_rele_e_repete_uma_vez_com_o_mesmo_id_e_o_segundo_409_sobe()
    {
        var c = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id));
        var id = Guid.NewGuid().ToString("D");
        _crm.ConflitosNoEnvio = 1;
        var depois = await Com(s => s.ResponderAsync(c, "Bom dia!", null, id));
        Assert.Equal(2, _crm.Respostas.Count);
        Assert.All(_crm.Respostas, r => Assert.Equal(id, r.Id));
        Assert.Contains(depois.Messages, m => m.Id == id);

        _crm.ConflitosNoEnvio = 2;
        var erro = await Assert.ThrowsAsync<RecusaDoCrm>(() => Com(s => s.ResponderAsync(depois, "De novo", null, Guid.NewGuid().ToString("D"))));
        Assert.True(erro.Conflito);
    }

    [Fact]
    public async Task Devolver_exige_o_motivo_que_o_CRM_pede()
    {
        var c = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.DevolverAsync(c, "ok")));
        var depois = await Com(s => s.DevolverAsync(c, "Paciente quer falar com o financeiro"));
        Assert.Null(depois.AgentId);
    }

    private static SituacaoConfirmacao Situacao(ContatoCampanha c) => RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario);

    // ------------------------------------------------------------------ o CRM falso

    private static AppConversationSummary Resumo(string id, DateTimeOffset em)
        => new(id, id, "5522900000000", "whatsapp", "5522333300000", "Recepção", null, false, 1, em, em, null, "", 0, "oi", false, false, null, null);

    private static AppConversation Conversa(string id, string nome, string contato, int? pacienteId = null, string nomeDaFicha = "", int recebidas = 1)
    {
        var agora = DateTimeOffset.UtcNow;
        var mensagens = Enumerable.Range(0, recebidas)
            .Select(i => new AppMessage($"{id}-m{i}", "in", $"mensagem {i}", "TEXT", null, agora.AddMinutes(-30 + i), "received")).ToArray();
        return new AppConversation(id, nome, contato, "whatsapp", "5522333300000", "Recepção", null, false, 3, agora,
            mensagens.Length == 0 ? default : mensagens[^1].At, pacienteId, nomeDaFicha, mensagens, [], [], [], null);
    }

    private sealed class CrmFalso : IClienteDoCrm
    {
        private readonly Dictionary<string, AppConversation> _conversas = new();
        private readonly Dictionary<string, int> _naoLidas = new();
        public List<AppReadMarker> Lidas { get; } = [];
        public List<AppReply> Respostas { get; } = [];
        public List<AppNoteRequest> Notas { get; } = [];
        public List<AppNewConversation> Novas { get; } = [];
        public List<AppPatientLink> Ligacoes { get; } = [];
        /// <summary>Quantos 409 os próximos envios recebem antes de passar (a Version que mudou sozinha).</summary>
        public int ConflitosNoEnvio { get; set; }

        public AppConversation Pôr(AppConversation c, int naoLidas = 0)
        {
            _conversas[c.Id] = c;
            _naoLidas[c.Id] = naoLidas;
            return c;
        }

        private AppConversation Trocar(string id, Func<AppConversation, AppConversation> f)
            => _conversas[id] = f(_conversas[id]) with { Version = _conversas[id].Version + 1 };

        public Task<bool> ConfiguradoAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<AppSession> SessaoAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(new AppSession("ana", "csrf", [new("ana", "Ana Paula"), new("bruna", "Bruna")],
                [new("5522333300000", "WhatsApp da clínica")], ["Recepção"], true, true, false));

        public Task<AppConversationSummary[]> ListarConversasAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(_conversas.Values.Select(c => new AppConversationSummary(c.Id, c.Name, c.Contact, c.Channel, c.Sender, c.Queue,
                c.AgentId, c.Closed, c.Version, c.UpdatedAt, c.LastIncoming, c.PatientId, c.PatientName, _naoLidas.GetValueOrDefault(c.Id),
                c.Messages.LastOrDefault()?.Text ?? "", false, false, null, c.ClosedAt)).ToArray());

        public Task<AppConversationsSince> ListarDesdeAsync(int usuarioId, DateTimeOffset desde, CancellationToken ct = default)
            => Task.FromResult(new AppConversationsSince([], [], DateTimeOffset.UtcNow));

        public Task<AppConversation> AbrirConversaAsync(int usuarioId, string conversaId, CancellationToken ct = default)
            => Task.FromResult(_conversas[conversaId]);

        public Task<AppConversation> NovaConversaAsync(int usuarioId, AppNewConversation pedido, CancellationToken ct = default)
        {
            Novas.Add(pedido);
            return Task.FromResult(Pôr(new AppConversation("nova-" + Novas.Count, pedido.Name, pedido.Contact, "whatsapp", pedido.Sender, "Recepção",
                "ana", false, 1, DateTimeOffset.UtcNow, default, null, "", [], [], [], [], null)));
        }

        public Task<AppConversation> ResponderAsync(int usuarioId, string conversaId, AppReply resposta, CancellationToken ct = default)
        {
            Respostas.Add(resposta);
            if (ConflitosNoEnvio > 0)
            {
                ConflitosNoEnvio--;
                _conversas[conversaId] = _conversas[conversaId] with { Version = _conversas[conversaId].Version + 1 };
                throw new RecusaDoCrm("Esta conversa mudou. Atualize antes de continuar.", 409);
            }
            if (resposta.Version != _conversas[conversaId].Version) throw new RecusaDoCrm("Versão velha.", 409);
            return Task.FromResult(Trocar(conversaId, c => c with
            {
                Messages = [.. c.Messages, new AppMessage(resposta.Id, "out", resposta.Text, resposta.Template is null ? "TEXT" : "TEMPLATE", "ana",
                    DateTimeOffset.UtcNow, "sending", Template: resposta.Template)]
            }));
        }

        public Task<AppConversation> AnotarAsync(int usuarioId, string conversaId, AppNoteRequest nota, CancellationToken ct = default)
        {
            Notas.Add(nota);
            return Task.FromResult(Trocar(conversaId, c => c with { Notes = [.. c.Notes, new AppNote(nota.Id, nota.Text, "ana", DateTimeOffset.UtcNow)] }));
        }

        public Task MarcarLidaAsync(int usuarioId, string conversaId, AppReadMarker marcador, CancellationToken ct = default)
        {
            Lidas.Add(marcador);
            return Task.CompletedTask;
        }

        public Task<AppConversation> AssumirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { AgentId = mudanca.AgentId }));

        public Task<AppConversation> TransferirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { AgentId = mudanca.AgentId }));

        public Task<AppConversation> ReabrirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { Closed = false, AgentId = mudanca.AgentId }));

        public Task<AppConversation> DevolverAFilaAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { AgentId = null }));

        public Task<AppConversation> EncerrarAsync(int usuarioId, string conversaId, AppClose encerramento, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { Closed = true }));

        public Task<AppConversation> LigarFichaAsync(int usuarioId, string conversaId, AppPatientLink ficha, CancellationToken ct = default)
        {
            Ligacoes.Add(ficha);
            return Task.FromResult(Trocar(conversaId, c => c with { PatientId = ficha.PatientId, PatientName = ficha.Name }));
        }

        public Task<AppAttachment> AnexarAsync(int usuarioId, string conversaId, Stream arquivo, string nome, string mime, CancellationToken ct = default)
            => Task.FromResult(new AppAttachment("anexo-1", conversaId, nome, mime, "DOCUMENT", 10));

        public Task<AppTemplate[]> ModelosAsync(int usuarioId, string remetente, CancellationToken ct = default) => Task.FromResult(Array.Empty<AppTemplate>());

        public Task<AppWorkspace> AreaDeTrabalhoAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(new AppWorkspace([], new Dictionary<string, string> { ["resolved"] = "Resolvido" }));

        public void EsquecerSessao(int usuarioId) { }
    }

    [Fact]
    public void O_lembrete_automatico_do_CRM_nao_aparece_com_o_nome_tecnico()
    {
        Assert.Equal(" pelo número oficial (automático)", WhatsAppDaRecepcaoService.QuemEnviou("CRM · WhatsApp"));
        Assert.Equal(" pelo número oficial (automático)", WhatsAppDaRecepcaoService.QuemEnviou("crm"));
        Assert.Equal(" · Ana Paula", WhatsAppDaRecepcaoService.QuemEnviou(" Ana Paula "));
        Assert.Equal("", WhatsAppDaRecepcaoService.QuemEnviou(null));
    }
}
