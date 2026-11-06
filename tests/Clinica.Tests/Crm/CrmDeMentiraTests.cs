using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor;
using Xunit;
using Ids = PreviaBlazor.CrmDeMentira.Ids;

namespace Clinica.Tests.Crm;

/// <summary>
/// O CRM DE MENTIRA da prévia (tools/PreviaBlazor/CrmDeMentira.cs): ele só serve se recusar como o
/// CRM recusa — senão a tela fica verde na prévia e quebra na clínica. E a semente do WhatsApp +
/// o falso têm de produzir os casos que a tela existe para mostrar.
/// </summary>
public sealed class CrmDeMentiraTests
{
    private const int Usuario = 1;

    private static CrmDeMentira Falso() => new(null);

    private static string NovoId() => Guid.NewGuid().ToString("D");

    [Fact]
    public async Task Escrita_com_Version_velha_e_recusada_com_409()
    {
        var crm = Falso();
        var maria = await crm.AbrirConversaAsync(Usuario, Ids.Maria);

        var recusa = await Assert.ThrowsAsync<RecusaDoCrm>(() =>
            crm.AnotarAsync(Usuario, Ids.Maria, new AppNoteRequest(maria.Version - 1, NovoId(), "Ligar à tarde.")));

        Assert.True(recusa.Conflito);
        Assert.Equal("A conversa mudou. Atualize e tente novamente.", recusa.Message);
        Assert.Equal(maria.Version, (await crm.AbrirConversaAsync(Usuario, Ids.Maria)).Version);
    }

    [Fact]
    public async Task Id_no_formato_N_e_recusado_com_400_no_envio_e_na_nota()
    {
        var crm = Falso();
        var maria = await crm.AbrirConversaAsync(Usuario, Ids.Maria);
        var semHifen = Guid.NewGuid().ToString("N");

        var envio = await Assert.ThrowsAsync<RecusaDoCrm>(() =>
            crm.ResponderAsync(Usuario, Ids.Maria, new AppReply(maria.Version, semHifen, "Combinado!")));
        var nota = await Assert.ThrowsAsync<RecusaDoCrm>(() =>
            crm.AnotarAsync(Usuario, Ids.Maria, new AppNoteRequest(maria.Version, semHifen, "Ligar à tarde.")));

        Assert.Equal(400, envio.Status);
        Assert.Equal(400, nota.Status);
    }

    [Fact]
    public async Task O_envio_nasce_sending_e_avanca_um_passo_a_cada_leitura_com_Version_nova()
    {
        var crm = Falso();
        var maria = await crm.AbrirConversaAsync(Usuario, Ids.Maria);
        var id = NovoId();

        var enviada = await crm.ResponderAsync(Usuario, Ids.Maria, new AppReply(maria.Version, id, "Combinado, Maria!"));
        Assert.Equal("sending", enviada.Messages.Single(m => m.Id == id).Status);
        Assert.Equal(maria.Version + 1, enviada.Version);

        var vistos = new List<(string Status, int Version)>();
        for (var i = 0; i < 4; i++)
        {
            var lida = await crm.AbrirConversaAsync(Usuario, Ids.Maria);
            vistos.Add((lida.Messages.Single(m => m.Id == id).Status, lida.Version));
        }
        Assert.Equal(["accepted", "delivered", "seen", "seen"], vistos.Select(v => v.Status));
        Assert.Equal(enviada.Version + 3, vistos[^1].Version);

        // A resposta HTTP se perdeu e a tela repete: o mesmo Id não duplica a mensagem.
        var repetida = await crm.ResponderAsync(Usuario, Ids.Maria, new AppReply(enviada.Version, id, "Combinado, Maria!"));
        Assert.Single(repetida.Messages, m => m.Id == id);
    }

    [Fact]
    public async Task O_desde_traz_so_o_que_mudou_e_os_ids_encerrados()
    {
        var crm = Falso();
        var nada = await crm.ListarDesdeAsync(Usuario, DateTimeOffset.UtcNow);
        Assert.Empty(nada.Conversations);

        var terezinha = await crm.AbrirConversaAsync(Usuario, Ids.Terezinha);
        var assumida = await crm.AssumirAsync(Usuario, Ids.Terezinha, new AppChange(terezinha.Version, CrmDeMentira.Eu, null));
        await crm.EncerrarAsync(Usuario, Ids.Terezinha, new AppClose(assumida.Version, "information", "Confirmou que o Pedro vem amanhã."));

        var depois = await crm.ListarDesdeAsync(Usuario, nada.Now);
        Assert.Equal([Ids.Terezinha], depois.Conversations.Select(c => c.Id));
        Assert.Equal([Ids.Terezinha], depois.Closed);
        Assert.Equal("information", depois.Conversations[0].Resolution?.Outcome);
    }

    [Fact]
    public async Task Janela_fechada_recusa_texto_livre_e_aceita_o_modelo()
    {
        var crm = Falso();
        var carlos = await crm.AbrirConversaAsync(Usuario, Ids.Carlos);
        Assert.False(ConversasNaTela.Janela(carlos, DateTimeOffset.Now).PodeEscreverLivre);

        var livre = await Assert.ThrowsAsync<RecusaDoCrm>(() =>
            crm.ResponderAsync(Usuario, Ids.Carlos, new AppReply(carlos.Version, NovoId(), "Oi, Carlos!")));
        Assert.True(livre.Conflito);

        var modelo = new AppTemplateSelection(CrmDeMentira.ModeloRecall, "pt_BR", ["Carlos"]);
        var enviada = await crm.ResponderAsync(Usuario, Ids.Carlos, new AppReply(carlos.Version, NovoId(), "", Template: modelo));
        Assert.Contains("Olá, Carlos!", enviada.Messages[^1].Text);
        Assert.Equal("TEMPLATE", enviada.Messages[^1].Type);
    }

    [Fact]
    public async Task Cada_modelo_tem_tantas_variaveis_quantas_o_texto_e_os_reais_de_producao_estao_la()
    {
        var modelos = await Falso().ModelosAsync(Usuario, CrmDeMentira.RemetenteWhatsApp);
        foreach (var m in modelos)
            Assert.Equal(m.Parameters, System.Text.RegularExpressions.Regex.Matches(m.Text, @"\{\{\d+\}\}").Count);
        // Os nomes e as quantidades da tabela KNOWN do template-picker.js do CRM.
        var reais = new Dictionary<string, int>
        {
            ["semdor_recall"] = 1, ["semdor_iniciar_atendimento"] = 2, ["semdor_lembrete_sessao"] = 2, ["semdor_horario_remarcado"] = 2,
            ["semdor_agendamento_confirmado"] = 2, ["semdor_retorno_contato"] = 1, ["semdor_documento"] = 1
        };
        foreach (var (nome, quantas) in reais) Assert.Equal(quantas, modelos.Single(m => m.Name == nome).Parameters);
        // Os do mockup continuam, com a contagem que o ModeloDoWhatsApp espera.
        foreach (var nome in new[] { ModeloDoWhatsApp.LembreteSessao, ModeloDoWhatsApp.RetornoTratamento, ModeloDoWhatsApp.DocumentoPronto, ModeloDoWhatsApp.PrimeiroContato })
            Assert.NotNull(ModeloDoWhatsApp.Rotulos(modelos.Single(m => m.Name == nome)));
        Assert.Empty(await Falso().ModelosAsync(Usuario, CrmDeMentira.RemetenteSms));
    }

    [Fact]
    public async Task Fora_do_ar_e_nao_configurado_viram_as_falhas_tipadas()
    {
        var fora = Falso();
        fora.Modo = CrmDeMentira.ModoDoCrm.ForaDoAr;
        Assert.True(await fora.ConfiguradoAsync());
        var falha = await Assert.ThrowsAsync<CrmIndisponivel>(() => fora.ListarConversasAsync(Usuario));
        Assert.IsNotType<CrmNaoConfigurado>(falha);

        var semEndereco = Falso();
        semEndereco.Modo = CrmDeMentira.ModoDoCrm.NaoConfigurado;
        Assert.False(await semEndereco.ConfiguradoAsync());
        await Assert.ThrowsAsync<CrmNaoConfigurado>(() => semEndereco.ListarConversasAsync(Usuario));
    }

    [Fact]
    public async Task Toda_trilha_semeada_e_um_evento_que_a_tela_conhece_ou_some_de_proposito()
    {
        var crm = Falso();
        foreach (var linha in await crm.ListarConversasAsync(Usuario))
            foreach (var a in (await crm.AbrirConversaAsync(Usuario, linha.Id)).Audit)
            {
                var evento = ConversasNaTela.Evento(a, DateTimeOffset.Now);
                Assert.False(evento?.Texto.Contains("movimentação registrada no CRM", StringComparison.OrdinalIgnoreCase) ?? false,
                    $"{linha.Name}: «{a.Actor}: {a.Action}» não está na tabela de ConversasNaTela.Evento");
            }
    }

    // ---------- A auditoria do atendimento (26/10) ----------

    [Fact]
    public async Task A_fila_da_auditoria_traz_tres_rascunhos_do_SemDor_com_provas_da_conversa_e_duas_avaliadas()
    {
        var crm = Falso();

        var fila = await crm.FilaDaAuditoriaAsync(Usuario);

        Assert.Equal(5, fila.Length);
        Assert.Equal(["para-conferir", "para-conferir", "para-conferir", "avaliada", "avaliada"], fila.Select(i => i.State));
        Assert.Equal([(Ids.Maria, "ana"), (Ids.Alzira, "bruna"), (Ids.Lucia, "bruna")],
            fila.Take(3).Select(i => (i.ConversationId, i.SubjectId)).OrderBy(x => x.ConversationId));
        Assert.All(fila, i => Assert.False(i.Stale));
        Assert.Equal((80m, (decimal?)null), (fila.Single(i => i.ConversationId == Ids.Maria).SuggestedScore, fila.Single(i => i.ConversationId == Ids.Maria).Score));
        Assert.Equal((100m, 90m), (fila.Single(i => i.ConversationId == Ids.Roberto).SuggestedScore!.Value, fila.Single(i => i.ConversationId == Ids.Roberto).Score!.Value));

        // Cada rascunho é do @SemDor e cada prova é uma mensagem DAQUELA conversa.
        foreach (var item in fila)
        {
            var detalhe = await crm.DetalheDaAuditoriaAsync(Usuario, item.ConversationId);
            var rascunho = detalhe.History.Single(r => r.ReviewerId == CrmDeMentira.RevisorSemDor && r.SubjectId == item.SubjectId);
            Assert.Equal(("draft", 1, (decimal?)null), (rascunho.State, rascunho.Version, rascunho.Score));
            Assert.Equal(["courtesy", "understanding", "clarity", "resolution", "followup"], rascunho.Answers.Select(a => a.Criterion));
            Assert.All(rascunho.Answers.SelectMany(a => a.MessageIds), id => Assert.Contains(detalhe.Conversation.Messages, m => m.Id == id));
            Assert.Contains(item.SubjectId, detalhe.Subjects);
        }
        var criterios = (await crm.DetalheDaAuditoriaAsync(Usuario, Ids.Maria)).Criteria;
        Assert.Equal(["Cordialidade e respeito", "Entendimento da necessidade", "Clareza da orientação", "Encaminhamento e resolução",
            "Confirmação e continuidade"], criterios.Select(c => c.Title));

        var painel = await crm.PainelDaAuditoriaAsync(Usuario, 7);
        Assert.Equal(["Ana Lima", "Bruna Reis", "Carla Mendes"], painel.Agents.Select(a => a.Name));
        Assert.Equal([1, 2, 0], painel.Agents.Select(a => a.ToReview));
        Assert.Equal([1, 1, 0], painel.Agents.Select(a => a.Reviewed));
        Assert.Null(painel.Agents[2].AverageScore);
    }

    [Fact]
    public async Task Salvar_a_auditoria_confere_versao_e_impressao_e_a_final_vale_a_media_vezes_50()
    {
        var crm = Falso();
        var detalhe = await crm.DetalheDaAuditoriaAsync(Usuario, Ids.Maria);
        var rascunho = detalhe.History.Single(r => r.SubjectId == "ana");
        var notas = rascunho.Answers.Select(a => a with { Score = a.Criterion == "followup" ? 2 : a.Score }).ToArray(); // 2,2,2,1,2
        AppQualitySave Final(int versao, string impressao) => new(Ids.Maria, "ana", versao, impressao, "final", notas, "followup",
            "Boa condução.", "Confirmar o novo horário.", DateTimeOffset.UtcNow.AddDays(1));

        var velha = await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.SalvarAuditoriaAsync(Usuario, Final(0, detalhe.Fingerprint)));
        Assert.Equal(("Outra revisão foi salva. Atualize antes de continuar.", 409), (velha.Message, velha.Status));
        var incompleta = await Assert.ThrowsAsync<RecusaDoCrm>(() =>
            crm.SalvarAuditoriaAsync(Usuario, Final(1, detalhe.Fingerprint) with { Answers = notas[..4] }));
        Assert.Equal(("Preencha os cinco critérios de auditoria.", 400), (incompleta.Message, incompleta.Status));

        var salva = await crm.SalvarAuditoriaAsync(Usuario, Final(1, detalhe.Fingerprint));
        Assert.Equal((2, "final", CrmDeMentira.Eu, 90m), (salva.Version, salva.State, salva.ReviewerId, salva.Score!.Value));
        Assert.Equal("avaliada", (await crm.FilaDaAuditoriaAsync(Usuario)).Single(i => i.ConversationId == Ids.Maria).State);

        // A paciente escreveu depois: a avaliação fica desatualizada e a impressão antiga é 409.
        crm.ChegouDoPaciente(Ids.Maria, "Obrigada!");
        Assert.True((await crm.FilaDaAuditoriaAsync(Usuario)).Single(i => i.ConversationId == Ids.Maria).Stale);
        var mudou = await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.SalvarAuditoriaAsync(Usuario, Final(2, detalhe.Fingerprint)));
        Assert.True(mudou.Conflito);
        Assert.StartsWith("A conversa mudou durante a avaliação.", mudou.Message);
    }

    [Fact]
    public async Task Quem_nao_e_da_gestao_toma_403_nas_quatro_rotas_da_auditoria()
    {
        var crm = Falso();
        crm.DaGestao = false;

        var recusas = new[]
        {
            await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.PainelDaAuditoriaAsync(Usuario, 30)),
            await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.FilaDaAuditoriaAsync(Usuario)),
            await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.DetalheDaAuditoriaAsync(Usuario, Ids.Maria)),
            await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.SalvarAuditoriaAsync(Usuario,
                new AppQualitySave(Ids.Maria, "ana", 1, "x", "draft", [], "inconclusive", "", "", null))),
        };

        Assert.All(recusas, r => Assert.Equal((CrmDeMentira.SoGestao, 403), (r.Message, r.Status)));
    }

    [Fact]
    public async Task A_evolucao_traz_as_segundas_ate_a_corrente_com_buracos_e_recusa_fora_de_4_a_26()
    {
        var crm = Falso();
        var evolucao = await crm.EvolucaoDaAuditoriaAsync(Usuario, 8);

        Assert.Equal(8, evolucao.Weeks.Length);
        Assert.All(evolucao.Weeks, d => Assert.Equal(DayOfWeek.Monday, d.DayOfWeek));
        Assert.Equal(evolucao.Weeks.Order(), evolucao.Weeks);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Assert.InRange(hoje.DayNumber - evolucao.Weeks[^1].DayNumber, 0, 6); // a última é a semana corrente
        Assert.Equal(3, evolucao.Agents.Length);
        Assert.All(evolucao.Agents, a => Assert.Equal(evolucao.Weeks, a.Weeks.Select(w => w.WeekStart)));
        Assert.Contains(evolucao.Agents.SelectMany(a => a.Weeks), w => w.AverageScore is null && w.Reviewed == 0);
        Assert.All(evolucao.Agents, a => Assert.True(a.Weeks[^1].Conversations < a.Weeks[^2].Conversations, "a semana corrente é parcial"));

        Assert.Equal(400, (await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.EvolucaoDaAuditoriaAsync(Usuario, 3))).Status);
        crm.DaGestao = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<RecusaDoCrm>(() => crm.EvolucaoDaAuditoriaAsync(Usuario, 8))).Status);
    }
}

public sealed class SementeDoWhatsAppTests(BancoDasTelas banco) : IClassFixture<BancoDasTelas>
{
    private ServiceProvider Servicos()
    {
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=teste;Database=teste");
        foreach (var d in servicos.Where(d => d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>)
                     || d.ServiceType == typeof(DbContextOptions) || d.ServiceType == typeof(ClinicaDbContext)).ToList())
            servicos.Remove(d);
        servicos.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(banco.Conexao));
        CrmDeMentira.Registrar(servicos);
        return servicos.BuildServiceProvider();
    }

    [Fact]
    public async Task Produz_nao_vem_numa_conversa_conferida_duas_fichas_num_telefone_e_retencao_sem_autorizacao()
    {
        await using var sp = Servicos();
        var crm = sp.GetRequiredService<IClienteDoCrm>();
        Assert.IsType<CrmDeMentira>(crm);
        Assert.True(await crm.ConfiguradoAsync());
        var lista = await crm.ListarConversasAsync(banco.Gerente.Id);

        await using var escopo = sp.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var agora = DateTime.Now;
        var avisos = await escopo.ServiceProvider.GetRequiredService<AvisosDeConfirmacaoService>().ListarAsync(agora, agora.Date);
        var ids = avisos.Select(a => a.AgendamentoId).ToList();
        var pacienteDoAgendamento = await db.Agendamentos.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.PacienteId);

        // Maria: a ficha foi achada pelo nome + telefone, e o aviso "não vem" do sino é dela.
        var maria = lista.Single(c => c.Id == Ids.Maria);
        Assert.NotNull(maria.PatientId);
        Assert.Equal(PessoasDoWhatsApp.Maria.Nome, maria.PatientName);
        var avisosDaMaria = ConversasNaTela.AvisosDaConversa(maria.PatientId, avisos, pacienteDoAgendamento);
        Assert.Contains(avisosDaMaria, a => a.Tipo == TipoAvisoConfirmacao.NaoVem);
        Assert.Equal("Não vem — remarcar", ConversasNaTela.Selo(maria, avisosDaMaria, 1, null, DateTimeOffset.Now).Texto);

        // Carlos: o lembrete com link sem resposta há mais de 3 h.
        var carlos = lista.Single(c => c.Id == Ids.Carlos);
        Assert.Contains(ConversasNaTela.AvisosDaConversa(carlos.PatientId, avisos, pacienteDoAgendamento), a => a.Tipo == TipoAvisoConfirmacao.SemResposta);

        // Terezinha: sem ficha ligada, e DUAS fichas têm o telefone dela.
        var terezinha = lista.Single(c => c.Id == Ids.Terezinha);
        Assert.Null(terezinha.PatientId);
        var telefones = await db.Pacientes.AsNoTracking().Select(p => p.Telefone).ToListAsync();
        var fichas = telefones.Count(t => ConversasNaTela.MesmoTelefone(t, terezinha.Contact));
        Assert.Equal(2, fichas);
        Assert.Equal("2 fichas", ConversasNaTela.Selo(terezinha, [], fichas, null, DateTimeOffset.Now).Texto);

        // A retenção: Helena não autorizou (só ligar) e Alzira respondeu ao convite pelo CRM.
        var linhas = await escopo.ServiceProvider.GetRequiredService<IAcompanhamentoPacienteService>().ListarAsync(banco.Gerente.Id);
        var helena = linhas.Single(l => l.Paciente == PessoasDoWhatsApp.Helena.Nome);
        Assert.True(helena.Pendente && !helena.Consentimento && RetencaoNaTela.EmTratamento(helena));
        Assert.Equal("Pediu para não receber", RetencaoNaTela.Selo(helena, null, DateTimeOffset.Now).Texto);

        var ultima = ConversasNaTela.UltimaRecebidaPorPaciente(lista);
        var alzira = linhas.Single(l => l.Paciente == PessoasDoWhatsApp.Alzira.Nome);
        Assert.Equal(SituacaoNaRetencao.Respondeu, RetencaoNaTela.Situacao(alzira, ultima.GetValueOrDefault(alzira.PacienteId), DateTimeOffset.Now));
        var fernanda = linhas.Single(l => l.Paciente == PessoasDoWhatsApp.Fernanda.Nome);
        Assert.Equal(SituacaoNaRetencao.Aguardando, RetencaoNaTela.Situacao(fernanda, ultima.GetValueOrDefault(fernanda.PacienteId), DateTimeOffset.Now));
    }

}
