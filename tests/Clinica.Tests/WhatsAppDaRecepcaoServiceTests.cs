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
        // Sem registro conta como autorizado (termo do BSV, out/2026): "não autorizou" aqui é a RECUSA gravada.
        db.Consentimentos.Add(new ConsentimentoLgpd { PacienteId = p.Id, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = autorizou });
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
        Assert.Equal("2 fichas", linhas.Single(l => l.Id == "c-terezinha").Selo.Texto);
        Assert.Equal("Sem ficha", linhas.Single(l => l.Id == "c-novo").Selo.Texto);
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

    // Out/2026: a ficha que guarda o 0 do interurbano ("022998999334") é achada pela conversa do número certo
    // (55 22 99899-9334) e, sendo a única, o aviso liga num clique — sem abrir a janela de conferência.
    [Fact]
    public async Task Ficha_unica_com_zero_na_frente_liga_num_clique()
    {
        var jamile = new Paciente { Nome = "Jamile Leandro Barcelos", Telefone = "022998999334" };
        using (var db = Db()) { db.Pacientes.Add(jamile); db.SaveChanges(); }
        _crm.Pôr(Conversa("c-jamile", "Jamili", "5522998999334"));
        var painel = await Com(s => s.LerAsync());

        var aberta = await Com(s => s.AbrirAsync(painel, "c-jamile", 0));

        Assert.Equal(jamile.Id, Assert.Single(aberta.Dados.Candidatas).Id);
        Assert.Equal("É Jamile Leandro Barcelos?", aberta.Acao?.Titulo);
        Assert.Equal([BotaoDaAcao.LigarFicha, BotaoDaAcao.BuscarFicha], aberta.Acao!.Botoes.Select(b => b.Acao));
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

    /// <summary>
    /// R1 (25/10): o paciente diz "confirmo" ou "não vou" na conversa e a resposta vai à agenda SEM a rodada de
    /// Confirmações: o contato de confirmação do horário nasce com a MESMA origem AGD:{id} da ponte do CRM, uma vez só, e
    /// o "não vem" chega ao sino (e ao Gerente) pela mesma decisão de sempre.
    /// </summary>
    [Fact]
    public async Task Confirmar_e_nao_vem_sem_rodada_criam_o_contato_do_horario_e_o_aviso_chega_ao_sino()
    {
        int a1, a2;
        using (var db = Db())
        {
            var h1 = new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Today.AddDays(1).AddHours(9) };
            var h2 = new Agendamento { PacienteId = _pedro.Id, DataHora = DateTime.Today.AddDays(1).AddHours(11) };
            db.Agendamentos.AddRange(h1, h2);
            db.SaveChanges();
            (a1, a2) = (h1.Id, h2.Id);
        }

        await Com(s => s.ConfirmouPorTelefoneAsync(a1));
        await Com(s => s.NaoVemAsync(a2));
        await Com(s => s.ConfirmouPorTelefoneAsync(a1)); // o segundo clique não cria outro contato

        using var depois = Db();
        var contatos = depois.Contatos.ToList();
        Assert.Equal(2, contatos.Count);
        var c1 = contatos.Single(c => c.AgendamentoId == a1);
        Assert.Equal((ContatoCampanha.OrigemDeAgendamento(a1), TipoContato.ConfirmacaoSessao, _maria.Id), (c1.Origem, c1.Tipo, c1.PacienteId));
        Assert.Equal(SituacaoConfirmacao.Confirmou, Situacao(c1));
        Assert.Equal(SituacaoConfirmacao.PediuRemarcar, Situacao(contatos.Single(c => c.AgendamentoId == a2)));
        using var escopo = _sp.CreateScope();
        var avisos = await escopo.ServiceProvider.GetRequiredService<AvisosDeConfirmacaoService>().ListarAsync(DateTime.Now, DateTime.Today);
        Assert.Contains(avisos, a => a.AgendamentoId == a2 && a.Tipo == TipoAvisoConfirmacao.NaoVem);
    }

    [Fact]
    public async Task Horario_que_ja_passou_ou_saiu_da_agenda_nao_ganha_resposta_inventada()
    {
        int passou, cancelado;
        using (var db = Db())
        {
            var h1 = new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Now.AddHours(-2) };
            var h2 = new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Today.AddDays(2).AddHours(9), Status = StatusAgendamento.Cancelado };
            db.Agendamentos.AddRange(h1, h2);
            db.SaveChanges();
            (passou, cancelado) = (h1.Id, h2.Id);
        }
        Assert.Contains("não está mais marcado", (await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.ConfirmouPorTelefoneAsync(passou)))).Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.NaoVemAsync(cancelado)));
        using var depois = Db();
        Assert.Empty(depois.Contatos.ToList());
    }

    [Fact]
    public async Task Horario_futuro_sem_rodada_tem_confirmar_e_nao_vem_na_coluna()
    {
        using (var db = Db())
        {
            db.Agendamentos.AddRange(
                new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Today.AddDays(1).AddHours(9) },
                new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Today }); // hoje à 00:00: já passou, e ainda está na coluna
            db.SaveChanges();
        }
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome));
        var painel = await Com(s => s.LerAsync());
        var aberta = await Com(s => s.AbrirAsync(painel, "c-maria", 0));

        var futuro = aberta.Dados.Horarios.Single(h => h.Quando > DateTime.Now);
        Assert.True(futuro.PodeConfirmar && futuro.PodeNaoVem);
        var jaPassou = aberta.Dados.Horarios.Single(h => h.Quando < DateTime.Now);
        Assert.False(jaPassou.PodeConfirmar || jaPassou.PodeNaoVem);
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
        Assert.True(resposta.ContactRecordedByApp, "o app grava o contato; o CRM não pode gravar de novo");
        using var db = Db();
        var gravado = db.Acompanhamentos.Include(a => a.Contatos).Single(a => a.Id == casoId);
        Assert.Equal(EtapaAcompanhamento.SemResposta, gravado.Etapa);
        Assert.True(gravado.ProximoContato > DateOnly.FromDateTime(DateTime.Today));
        Assert.Contains(gravado.Contatos, c => c.Canal == CanalContato.WhatsApp && c.Operador == _ana.Login
            && c.Observacao.StartsWith("Convite enviado pelo WhatsApp da clínica", StringComparison.Ordinal));
        // O histórico do caso é lido pela recepção: o código do modelo (semdor_recall…) não vai à tela.
        Assert.DoesNotContain(gravado.Contatos, c => c.Observacao.Contains(modelo.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quem_recusou_mensagens_nao_recebe_convite_pelo_CRM()
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
        Assert.Equal(["22998765432", "5522998765432", "022998765432"], WhatsAppDaRecepcaoService.Variantes("5522998765432"));
        Assert.Empty(WhatsAppDaRecepcaoService.Variantes("123"));
        Assert.Equal("5522998765432", WhatsAppDaRecepcaoService.ContatoParaOCrm("(22) 99876-5432"));
    }

    [Fact]
    public void Rascunho_de_remarcacao_diz_o_que_a_clinica_fez_e_so_promete_o_lembrete_ligado()
    {
        var hoje = new DateOnly(2026, 10, 24);
        var novo = new HorarioDoPaciente(1, new DateTime(2026, 10, 28, 14, 0, 0), "", "", "", "", false, false, false, 2,
            ModalidadeAtendimento.AcupunturaSimples, "Dra. Helena");
        Assert.Equal("Combinado, Maria! Remarquei a sua acupuntura para quarta-feira, 28/10 às 14:00 com Dra. Helena.",
            WhatsAppDaRecepcaoService.RascunhoDeRemarcacao("Maria das Graças Silva", novo, hoje, lembreteAutomatico: false));
        Assert.Equal("Combinado, Maria! Remarquei a sua acupuntura para quarta-feira, 28/10 às 14:00 com Dra. Helena. Antes da sessão chega o lembrete para você confirmar.",
            WhatsAppDaRecepcaoService.RascunhoDeRemarcacao("Maria das Graças Silva", novo, hoje, lembreteAutomatico: true));
        // Sessão de hoje: o lembrete do dia já passou (ou nem sai), e a mensagem não o promete (R3).
        Assert.DoesNotContain("lembrete", WhatsAppDaRecepcaoService.RascunhoDeRemarcacao("Maria", novo with { Quando = new DateTime(2026, 10, 24, 18, 0, 0) }, hoje, lembreteAutomatico: true));
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
        Assert.All(_crm.Respostas, r => Assert.False(r.ContactRecordedByApp, "texto livre: quem registra o contato é o CRM"));
        Assert.Contains(depois.Messages, m => m.Id == id);

        _crm.ConflitosNoEnvio = 2;
        var erro = await Assert.ThrowsAsync<RecusaDoCrm>(() => Com(s => s.ResponderAsync(depois, "De novo", null, Guid.NewGuid().ToString("D"))));
        Assert.True(erro.Conflito);
    }

    [Fact]
    public async Task A_midia_chega_pronta_para_a_tela_e_a_gravacao_sobe_como_gravacao()
    {
        var c = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id));
        _crm.Arquivo = new ArquivoDaMidia(PreviaBlazor.MidiaDeExemplo.NotaDeVoz.Conteudo, "audio/wav", null);
        var midia = MidiaDaConversa.Da(c.Id, new AppMessage("m-voz", "in", "", "AUDIO", null, DateTimeOffset.UtcNow, "received"))!;

        var pronta = await Com(s => s.MidiaAsync(midia));

        Assert.StartsWith("data:audio/wav;base64,", pronta.Endereco);
        Assert.Equal(6.5, pronta.Duracao!.Value, 2);

        await Com(s => s.AnexarAsync(c, new MemoryStream([1, 2]), "gravacao.webm", "audio/webm", gravacao: true));
        Assert.Equal(("gravacao.webm", "audio/webm", true), _crm.UltimoAnexo);
        await Com(s => s.AnexarAsync(c, new MemoryStream([1, 2]), "exame.pdf", "application/pdf"));
        Assert.Equal(("exame.pdf", "application/pdf", false), _crm.UltimoAnexo);
    }

    [Fact]
    public async Task Devolver_exige_o_motivo_que_o_CRM_pede()
    {
        var c = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.DevolverAsync(c, "ok")));
        var depois = await Com(s => s.DevolverAsync(c, "Paciente quer falar com o financeiro"));
        Assert.Null(depois.AgentId);
        // A devolução da DONA é a transferência para a mesma fila, sem responsável — o /reassign é o "Reorganizar"
        // da gestão e recusa a secretária comum com 403 (25/10).
        var (rota, conversa, mudanca) = Assert.Single(_crm.Mudancas);
        Assert.Equal(("transfer", "c-maria", null, "Recepção", "Paciente quer falar com o financeiro"),
            (rota, conversa, mudanca.AgentId, mudanca.Queue, mudanca.Note));
    }

    // ------------------------------------------------------------------ passar o turno (25/10, D12)

    private static AppConversationSummary NaLista(AppConversation c, int? versao = null, string? agente = "ana")
        => new(c.Id, c.Name, c.Contact, c.Channel, c.Sender, c.Queue, agente, c.Closed, versao ?? c.Version, c.UpdatedAt, c.LastIncoming,
            c.PatientId, c.PatientName, 0, "", false, false, null, null, PendingTransfer: c.PendingTransfer);

    [Fact]
    public async Task Devolver_e_passar_o_turno_cancelam_antes_o_pedido_de_transferencia_pendente()
    {
        // Com o pedido "aguardando aceite" o CRM recusa qualquer transferência, inclusive à fila (achado na junção, foto 79):
        // quem devolve desiste do pedido, e a conversa não fica com quem saiu.
        AppPendingTransfer Pedido(string id) => new("pedido-" + id, "ana", "bruna", "", DateTimeOffset.UtcNow);
        var maria = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432") with { AgentId = "ana", PendingTransfer = Pedido("c-maria") });
        var joao = _crm.Pôr(Conversa("c-joao", "João", "5522996501010") with { AgentId = "ana", PendingTransfer = Pedido("c-joao") });

        var r = await Com(s => s.PassarOTurnoAsync([NaLista(maria)], "Fim do turno da manhã", "ana"));
        var devolvido = await Com(s => s.DevolverAsync(joao, "Paciente quer falar com o financeiro"));

        Assert.Equal(["Maria"], r.Passaram);
        Assert.Empty(r.Falharam);
        Assert.Null(devolvido.AgentId);
        Assert.Equal([("pedido-c-maria", "cancel"), ("pedido-c-joao", "cancel")], _crm.Decisoes.Select(d => (d.Pedido, d.Acao)));
        Assert.All(_crm.Mudancas, m => Assert.Null(m.Mudanca.AgentId));
    }

    [Fact]
    public async Task Passar_o_turno_devolve_cada_conversa_com_a_mesma_nota_e_nao_para_no_primeiro_erro()
    {
        var maria = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432") with { AgentId = "ana" });
        var pedro = _crm.Pôr(Conversa("c-pedro", "Pedro", "5522988112233") with { AgentId = "ana" });
        var joao = _crm.Pôr(Conversa("c-joao", "João", "5522996501010") with { AgentId = "ana" });
        _crm.RecusasNaTransferencia.Add("c-pedro");

        var r = await Com(s => s.PassarOTurnoAsync([NaLista(maria), NaLista(pedro), NaLista(joao)], "Fim do turno da manhã", "ana"));

        Assert.Equal(["Maria", "João"], r.Passaram);
        var falha = Assert.Single(r.Falharam);
        Assert.Equal(("c-pedro", "Escolha uma fila válida."), (falha.Id, falha.Motivo));
        Assert.All(_crm.Mudancas, m => Assert.Equal(("transfer", null, "Fim do turno da manhã"), (m.Rota, m.Mudanca.AgentId, m.Mudanca.Note)));
        Assert.Equal(["c-maria", "c-pedro", "c-joao"], _crm.Mudancas.Select(m => m.Conversa));
    }

    [Fact]
    public async Task Passar_o_turno_no_409_rele_e_so_repete_se_a_conversa_ainda_e_de_quem_sai()
    {
        // A Version mudou sozinha (o recibo de entrega): ainda é da Ana, então repete com a nova e passa.
        var maria = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432") with { AgentId = "ana" });
        // A Bruna assumiu no meio: não se tira a conversa dela.
        var pedro = _crm.Pôr(Conversa("c-pedro", "Pedro", "5522988112233") with { AgentId = "bruna" });

        var r = await Com(s => s.PassarOTurnoAsync([NaLista(maria, versao: maria.Version - 1), NaLista(pedro, versao: pedro.Version - 1)],
            "Fim do turno da manhã", "ana"));

        Assert.Equal(["Maria"], r.Passaram);
        Assert.Equal(("c-pedro", TurnoNoWhatsApp.JaNaoEraSua), (r.Falharam.Single().Id, r.Falharam.Single().Motivo));
        Assert.Equal(["c-maria", "c-maria", "c-pedro"], _crm.Mudancas.Select(m => m.Conversa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.PassarOTurnoAsync([NaLista(maria)], "ok", "ana")));
    }

    // ------------------------------------------------------------------ 25/10: o lembrete conta no ENVIO (D11)

    private static readonly AppTemplate Lembrete = new("semdor_lembrete_sessao", "pt_BR", "APPROVED", "Olá, {{1}}! Sua sessão é {{2}}.", 2, true, "", [], "UTILITY");

    [Fact]
    public async Task O_lembrete_enviado_pela_conversa_marca_o_horario_como_avisado_e_o_clique_sozinho_nao()
    {
        int ag;
        using (var db = Db())
        {
            var h = new Agendamento { PacienteId = _maria.Id, DataHora = DateTime.Today.AddDays(1).AddHours(9) };
            db.Agendamentos.Add(h);
            db.SaveChanges();
            ag = h.Id;
        }
        var conversa = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome));
        var painel = await Com(s => s.LerAsync());
        var aberta = await Com(s => s.AbrirAsync(painel, "c-maria", 0));
        var outro = Lembrete with { Name = "semdor_retorno_contato", Text = "Olá, {{1}}!", Parameters = 1 };

        Assert.Null(WhatsAppDaRecepcaoService.HorarioDoLembrete(aberta, outro, WhatsAppDaRecepcaoService.Agora));
        var horario = WhatsAppDaRecepcaoService.HorarioDoLembrete(aberta, Lembrete, WhatsAppDaRecepcaoService.Agora);
        Assert.Equal(ag, horario);
        // Abrir a conversa (o "Avisar" de Confirmações) não grava nada: só o envio grava.
        using (var antes = Db()) Assert.Empty(antes.Contatos.ToList());

        var r = await Com(s => s.EnviarModeloAsync(conversa, "5522333300000", Lembrete, ["Maria", "amanhã às 09:00"], null, Guid.NewGuid().ToString("D"), horario));

        Assert.Null(r.Pendencia);
        using var depois = Db();
        var c = Assert.Single(depois.Contatos.ToList());
        Assert.Equal((ag, SituacaoConfirmacao.AvisadoSemResposta, _ana.Login, CanalContato.WhatsApp), (c.AgendamentoId!.Value, Situacao(c), c.EnviadoPor, c.Canal));
        // O lembrete que a recepção mandou à mão não vira "sem resposta" no sino: ali quem registra a resposta é ela.
        Assert.NotEqual(RodadaDeConfirmacao.LembreteAutomatico, c.EnviadoPor);
    }

    [Fact]
    public async Task O_lembrete_repetido_nao_apaga_a_resposta_que_o_paciente_ja_deu()
    {
        var (ag, contato) = HorarioComContato(_maria, StatusContato.Respondido, RodadaDeConfirmacao.RespostaRemarcar);
        var conversa = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome));

        var r = await Com(s => s.EnviarModeloAsync(conversa, "5522333300000", Lembrete, ["Maria", "amanhã às 10:00"], null, Guid.NewGuid().ToString("D"), ag));

        Assert.Null(r.Pendencia);
        using var db = Db();
        Assert.Equal(SituacaoConfirmacao.PediuRemarcar, Situacao(db.Contatos.Single(x => x.Id == contato)));
    }

    // ------------------------------------------------------------------ 25/10: a transferência entre pessoas (R7)

    [Fact]
    public async Task Transferir_para_uma_pessoa_vira_pedido_e_quem_pediu_cancela_pelo_id_dele()
    {
        var c = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id) with { AgentId = "ana" });

        var pedido = await Com(s => s.TransferirAsync(c, "bruna", null, "ver o pacote"));

        Assert.Equal("ana", pedido.AgentId);
        Assert.Equal(("bruna", "ver o pacote"), (pedido.PendingTransfer!.To, pedido.PendingTransfer.Note));
        // Recusar sem motivo não sai daqui; cancelar vai ao CRM com o id do pedido.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.RecusarTransferenciaAsync(pedido, "ok")));
        Assert.Empty(_crm.Decisoes);
        var cancelada = await Com(s => s.CancelarTransferenciaAsync(pedido));
        Assert.Equal(("pedido-c-maria", "cancel", (string?)null), _crm.Decisoes.Single());
        Assert.Null(cancelada.PendingTransfer);
        // Sem pedido, aceitar nem chega ao CRM.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Com(s => s.AceitarTransferenciaAsync(cancelada)));
        // Para o setor passa na hora, sem responsável.
        var setor = await Com(s => s.TransferirAsync(cancelada, null, "Agendamento", null));
        Assert.Null(setor.AgentId);
        Assert.Null(setor.PendingTransfer);
    }

    [Fact]
    public async Task Quem_recebe_o_pedido_ve_o_aviso_e_o_selo_e_aceita()
    {
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome)
            with { AgentId = "bruna", PendingTransfer = new AppPendingTransfer("p-1", "bruna", "ana", "Ela quer o valor do pacote.", DateTimeOffset.UtcNow) });
        var painel = await Com(s => s.LerAsync());

        var linha = WhatsAppDaRecepcaoService.Linhas(painel, FiltroDasConversas.Todas, null, false, WhatsAppDaRecepcaoService.Agora).Single();
        Assert.Equal("Transferência para você", linha.Selo.Texto);
        var aberta = await Com(s => s.AbrirAsync(painel, "c-maria", 0));
        Assert.Equal(TipoDaAcaoPedida.TransferenciaParaVoce, aberta.Acao?.Tipo);
        Assert.Equal("Bruna quer passar esta conversa para você.", aberta.Acao!.Titulo);

        var aceita = await Com(s => s.AceitarTransferenciaAsync(aberta.Conversa!));
        Assert.Equal(("p-1", "accept"), (_crm.Decisoes.Single().Pedido, _crm.Decisoes.Single().Acao));
        Assert.Equal("ana", aceita.AgentId);
        Assert.Null(aceita.PendingTransfer);
    }

    // ------------------------------------------------------------------ 25/10: "Para resolver" (R6)

    [Fact]
    public async Task Para_resolver_traz_os_alertas_da_ficha_do_balcao_e_nada_sem_o_bit_da_ficha()
    {
        using (var db = Db())
        {
            var p = db.Pacientes.Single(x => x.Id == _maria.Id);
            p.Carteirinha = "0 064 1234 5678 9";
            p.ValidadeCarteirinha = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
            db.SaveChanges();
        }

        var r = await Com(s => s.ParaResolverAsync(_maria.Id));

        Assert.Equal(_maria.Id, r.PacienteId);
        Assert.False(r.Falhou);
        Assert.Contains(r.Alertas, a => a.EhVermelho && a.Descricao.StartsWith("Carteirinha vencida", StringComparison.Ordinal));
        // O serviço de elegibilidade não filtra por permissão: sem o bit da Ficha do balcão, nada é lido.
        var caixa = new UsuarioSistema { Nome = "Caixa", Login = "caixa", Perfil = PerfilAcesso.Recepcao, PermissoesNegadas = Permissao.VerFichaPaciente,
            SenhaHash = "-", SenhaSalt = "-", Ativo = true };
        using (var db = Db()) { db.Usuarios.Add(caixa); db.SaveChanges(); }
        new SessaoUsuario().Entrar(caixa);
        Assert.Empty((await Com(s => s.ParaResolverAsync(_maria.Id))).Alertas);
    }

    // ------------------------------------------------------------------ 25/10: a fila do recall se renova pela aba (item 8)

    [Fact]
    public async Task Ler_a_aba_renova_a_fila_do_recall_uma_vez_por_dia_sem_duplicar_caso()
    {
        using (var db = Db())
        {
            for (var i = 0; i < 3; i++)
            {
                var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-75 - i * 7));
                db.Atendimentos.Add(new Atendimento { PacienteId = _sebastiao.Id, Modalidade = ModalidadeAtendimento.AcupunturaSimples, Data = data,
                    RealizadoEm = data.ToDateTime(new TimeOnly(10, 0)) });
            }
            db.SaveChanges();
        }

        var painel = await Com(s => s.LerAsync());

        Assert.Contains(painel.Retencao, l => l.PacienteId == _sebastiao.Id && l.Tipo == TipoAcompanhamento.Recall);
        Assert.False(_sp.GetRequiredService<RecallDoDia>().Reservar(DateOnly.FromDateTime(DateTime.Today)), "o dia já foi renovado");
        await Com(s => s.LerAsync());
        using var depois = Db();
        Assert.Single(depois.Acompanhamentos.Where(a => a.PacienteId == _sebastiao.Id).ToList());
    }

    // ------------------------------------------------------------------ 25/10: o que a tela afirma (itens 15 e 16, R3)

    [Fact]
    public async Task Sem_ficha_conferida_o_titulo_e_o_telefone_e_o_nome_do_perfil_vai_rotulado()
    {
        _crm.Pôr(Conversa("c-terezinha", "Terezinha Souza", "5522988112233"));
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome));
        var painel = await Com(s => s.LerAsync());

        var semConferir = await Com(s => s.AbrirAsync(painel, "c-terezinha", 0));
        Assert.Equal("(22) 98811-2233", semConferir.Nome);
        Assert.Equal("Terezinha Souza", semConferir.NomeNoWhatsApp);
        Assert.Equal("Terezinha Souza (nome no WhatsApp)", semConferir.NomeDoPerfil);
        Assert.Equal("?", semConferir.Iniciais);
        var conferida = await Com(s => s.AbrirAsync(painel, "c-maria", 0));
        Assert.Equal(_maria.Nome, conferida.Nome);
        Assert.Null(conferida.NomeNoWhatsApp);
        Assert.False(conferida.SemTelefone);
    }

    [Fact]
    public async Task O_caso_sem_telefone_abre_sem_WhatsApp_e_o_contato_mostra_o_nome_de_quem_fez()
    {
        var semTelefone = new Paciente { Nome = "Helena Marques" };
        using (var db = Db()) { db.Pacientes.Add(semTelefone); db.SaveChanges(); }
        var id = CasoDeRecall(semTelefone, autorizou: false);
        using (var db = Db())
        {
            db.ContatosAcompanhamento.Add(new ContatoAcompanhamento { AcompanhamentoPacienteId = id, Idempotencia = Guid.NewGuid(), Em = DateTime.Now,
                Operador = _ana.Login, Canal = CanalContato.Telefone, Resultado = "Não atendeu" });
            db.SaveChanges();
        }
        var painel = await Com(s => s.LerAsync());

        var aberta = await Com(s => s.AbrirCasoAsync(painel, painel.Retencao.Single(l => l.Id == id)));

        Assert.True(aberta.SemTelefone);
        Assert.Equal(_ana.Nome, Assert.Single(aberta.Dados.Contatos, x => x.Canal == CanalContato.Telefone).Operador);
    }

    private static SituacaoConfirmacao Situacao(ContatoCampanha c) => RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario);

    // ------------------------------------------------------------------ o @SemDor (24/10)

    [Fact]
    public async Task SemDor_ligado_poe_o_estado_no_painel_e_a_etiqueta_na_linha_de_quem_espera()
    {
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432", pacienteId: _maria.Id, nomeDaFicha: _maria.Nome));
        _crm.Pôr(Conversa("c-novo", "5522997120045", "5522997120045"));
        _crm.Etiquetas["c-maria"] = new AppSemDorTag("urgencia", "Urgência clínica", true);
        _crm.Etiquetas["c-novo"] = new AppSemDorTag("valores", "Valores", false);

        var painel = await Com(s => s.LerAsync());
        var linhas = WhatsAppDaRecepcaoService.Linhas(painel, FiltroDasConversas.Todas, null, false, WhatsAppDaRecepcaoService.Agora);

        Assert.True(SemDorNaTela.Ligado(painel.SemDor));
        Assert.Equal(("Urgência clínica", true), (linhas.Single(l => l.Id == "c-maria").SemDor!.Texto, linhas.Single(l => l.Id == "c-maria").SemDor!.Atencao));
        Assert.Equal(("Valores", false), (linhas.Single(l => l.Id == "c-novo").SemDor!.Texto, linhas.Single(l => l.Id == "c-novo").SemDor!.Atencao));
        // A releitura de 7 s guarda o estado da leitura inteira (o "desde" não o traz).
        Assert.Same(painel.SemDor, (await Com(s => s.AtualizarAsync(painel))).SemDor);
    }

    [Fact]
    public async Task SemDor_desligado_ou_sem_estado_tira_as_etiquetas_e_a_aba_abre_igual()
    {
        _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432"));
        _crm.Etiquetas["c-maria"] = new AppSemDorTag("remarcar", "Remarcar", false);

        foreach (var estado in new Func<AppSemDorState>[]
                 {
                     () => new AppSemDorState(false, false, null, null, ""),
                     () => throw new RecusaDoCrm("O CRM recusou o pedido (código 404).", 404), // CRM de antes do @SemDor
                     () => throw new CrmIndisponivel("O CRM demorou demais para responder."),
                 })
        {
            _crm.EstadoDoSemDor = estado;
            var painel = await Com(s => s.LerAsync());
            Assert.False(SemDorNaTela.Ligado(painel.SemDor));
            Assert.Null(Assert.Single(WhatsAppDaRecepcaoService.Linhas(painel, FiltroDasConversas.Todas, null, false, WhatsAppDaRecepcaoService.Agora)).SemDor);
        }
    }

    [Fact]
    public async Task SemDor_sugere_com_o_campo_vazio_revisa_com_texto_e_nada_e_enviado()
    {
        var c = _crm.Pôr(Conversa("c-maria", "Maria", "5522998765432"));

        var sugestao = await Com(s => s.SugerirComSemDorAsync(c, "   "));
        var revisao = await Com(s => s.SugerirComSemDorAsync(c, "  pode vir quinta  "));
        var resumo = await Com(s => s.ResumirComSemDorAsync(c));

        Assert.Equal([("c-maria", (string?)null), ("c-maria", "pode vir quinta")], _crm.PedidosAoSemDor.Take(2).Select(p => (p.Conversa, p.Pedido!.Rascunho)));
        Assert.Equal(("sugestao", "revisao", "Remarcar"), (sugestao.Modo, revisao.Modo, resumo.Quer));
        Assert.Empty(_crm.Respostas);
        Assert.Empty(_crm.Notas);
    }

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
        /// <summary>O @SemDor: o estado que o CRM devolve (ou a falha dele), as etiquetas das linhas e os pedidos que chegaram.</summary>
        public Func<AppSemDorState> EstadoDoSemDor { get; set; } = () => new(true, true, null, null, "");
        public Dictionary<string, AppSemDorTag> Etiquetas { get; } = new();
        public List<(string Conversa, AppSemDorRequest? Pedido)> PedidosAoSemDor { get; } = [];

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
                c.Messages.LastOrDefault()?.Text ?? "", false, false, null, c.ClosedAt, Semdor: Etiquetas.GetValueOrDefault(c.Id),
                PendingTransfer: c.PendingTransfer)).ToArray());

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

        /// <summary>As mudanças de responsável que chegaram, com a rota (25/10: devolver é /transfer, não o /reassign da gestão).</summary>
        public List<(string Rota, string Conversa, AppChange Mudanca)> Mudancas { get; } = [];
        /// <summary>As conversas cuja transferência o CRM recusa (400): a passagem do turno segue para as outras.</summary>
        public HashSet<string> RecusasNaTransferencia { get; } = [];

        /// <summary>Como o CRM: para uma pessoa nasce o pedido (a conversa fica com quem pediu); para a fila, passa na hora.</summary>
        public Task<AppConversation> TransferirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
        {
            Mudancas.Add(("transfer", conversaId, mudanca));
            if (RecusasNaTransferencia.Contains(conversaId)) throw new RecusaDoCrm("Escolha uma fila válida.", 400);
            if (mudanca.Version != _conversas[conversaId].Version) throw new RecusaDoCrm("A conversa mudou. Atualize e tente novamente.", 409);
            // Como o CRM (ConversationWorkflow): com um pedido pendente, nenhuma transferência — nem à fila.
            if (_conversas[conversaId].PendingTransfer is not null)
                throw new RecusaDoCrm("Já existe uma transferência aguardando aceite. Cancele antes de solicitar outra.", 409);
            return Task.FromResult(Trocar(conversaId, c => mudanca.AgentId is { } para
                ? c with { PendingTransfer = new AppPendingTransfer("pedido-" + conversaId, "ana", para, mudanca.Note ?? "", DateTimeOffset.UtcNow) }
                : c with { AgentId = null, Queue = mudanca.Queue ?? c.Queue }));
        }

        /// <summary>As decisões que chegaram: (pedido, ação, motivo).</summary>
        public List<(string Pedido, string Acao, string? Motivo)> Decisoes { get; } = [];

        public Task<AppTransferDecided> DecidirTransferenciaAsync(int usuarioId, string pedidoId, string acao, AppTransferDecision decisao,
            CancellationToken ct = default)
        {
            Decisoes.Add((pedidoId, acao, decisao.Reason));
            var c = _conversas.Values.Single(x => x.PendingTransfer?.Id == pedidoId);
            Trocar(c.Id, x => acao == "accept" ? x with { AgentId = x.PendingTransfer!.To, PendingTransfer = null } : x with { PendingTransfer = null });
            return Task.FromResult(new AppTransferDecided(c.Id));
        }

        public Task<AppConversation> ReabrirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { Closed = false, AgentId = mudanca.AgentId }));

        public Task<AppConversation> DevolverAFilaAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
        {
            Mudancas.Add(("reassign", conversaId, mudanca));
            return Task.FromResult(Trocar(conversaId, c => c with { AgentId = null }));
        }

        public Task<AppPresence> PulsoDePresencaAsync(int usuarioId, CancellationToken ct = default) => Task.FromResult(new AppPresence(true, 120));

        public Task<AppConversation> EncerrarAsync(int usuarioId, string conversaId, AppClose encerramento, CancellationToken ct = default)
            => Task.FromResult(Trocar(conversaId, c => c with { Closed = true }));

        public Task<AppConversation> LigarFichaAsync(int usuarioId, string conversaId, AppPatientLink ficha, CancellationToken ct = default)
        {
            Ligacoes.Add(ficha);
            return Task.FromResult(Trocar(conversaId, c => c with { PatientId = ficha.PatientId, PatientName = ficha.Name }));
        }

        /// <summary>O último anexo pedido: (nome, mime, é gravação).</summary>
        public (string Nome, string Mime, bool Gravacao)? UltimoAnexo { get; private set; }

        public Task<AppAttachment> AnexarAsync(int usuarioId, string conversaId, Stream arquivo, string nome, string mime, bool gravacao = false,
            CancellationToken ct = default)
        {
            UltimoAnexo = (nome, mime, gravacao);
            return Task.FromResult(new AppAttachment("anexo-1", conversaId, nome, mime, gravacao ? "AUDIO" : "DOCUMENT", 10));
        }

        /// <summary>O arquivo que toda mídia devolve (o teste do serviço confere o que ele faz com ele).</summary>
        public ArquivoDaMidia Arquivo { get; set; } = new([1, 2, 3], "application/octet-stream", null);

        public Task<ArquivoDaMidia> MidiaAsync(int usuarioId, string conversaId, AppMessage mensagem, CancellationToken ct = default)
            => Task.FromResult(Arquivo);

        public Task<AppTemplate[]> ModelosAsync(int usuarioId, string remetente, CancellationToken ct = default) => Task.FromResult(Array.Empty<AppTemplate>());

        public Task<AppWorkspace> AreaDeTrabalhoAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(new AppWorkspace([], new Dictionary<string, string> { ["resolved"] = "Resolvido" }));

        public Task<AppSemDorState> EstadoDoSemDorAsync(int usuarioId, CancellationToken ct = default) => Task.FromResult(EstadoDoSemDor());

        public Task<AppSemDorSuggestion> SugerirComSemDorAsync(int usuarioId, string conversaId, AppSemDorRequest pedido, CancellationToken ct = default)
        {
            PedidosAoSemDor.Add((conversaId, pedido));
            return Task.FromResult(new AppSemDorSuggestion(pedido.Rascunho is null ? "Olá! Vou verificar e já te retorno." : pedido.Rascunho + ".", pedido.Rascunho is null ? "sugestao" : "revisao"));
        }

        public Task<AppSemDorSummary> ResumirComSemDorAsync(int usuarioId, string conversaId, CancellationToken ct = default)
        {
            PedidosAoSemDor.Add((conversaId, null));
            return Task.FromResult(new AppSemDorSummary("Remarcar", "Nada ainda", "Responder"));
        }

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
