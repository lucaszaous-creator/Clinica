using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

// ============================================================================ o que a tela desenha

/// <summary>Uma linha da lista de conversas, pronta para desenhar.</summary>
public sealed record LinhaDaConversa(string Id, string Nome, string Iniciais, string TomDoAvatar, bool Sms, string Hora,
    string Previa, TickDaMensagem? Tick, int NaoLidas, SeloDoWhatsApp Selo, string Responsavel, bool Minha, bool TemResponsavel,
    EtiquetaDoSemDor? SemDor = null, int? PacienteId = null, EsperaNaLinha? Espera = null);

/// <summary>Uma linha do modo Retenção (um caso do Acompanhamento), pronta para desenhar.</summary>
public sealed record LinhaDaRetencao(LinhaAcompanhamento Caso, string Iniciais, string TomDoAvatar, string Dias, string TomDosDias,
    string Detalhe, SeloDoWhatsApp Selo, string Responsavel, bool Minha, bool NaoLida, string? ConversaId);

/// <summary>Um grupo do modo Retenção, com as linhas já compostas.</summary>
public sealed record GrupoNaTela(string Chave, string Titulo, string Tom, IReadOnlyList<LinhaDaRetencao> Linhas);

/// <summary>
/// A FOTOGRAFIA da aba: o que o CRM e o app responderam na última leitura inteira (ou na
/// última releitura de 7 s, juntada por cima). A tela guarda UMA e desenha a lista a partir
/// dela; nada aqui é estado de tela (filtro, busca, rascunho).
/// </summary>
/// <param name="LidoEm">O <c>Now</c> do CRM na última leitura: é o <c>desde</c> da próxima releitura.</param>
/// <param name="FichasPorConversa">Conversa sem ficha conferida → as fichas que têm aquele telefone.</param>
/// <param name="RetencaoPermitida">O acesso de quem entrou alcança o Acompanhamento (sem ele, o modo Retenção diz por quê).</param>
public sealed record PainelDoWhatsApp(
    AppSession Sessao, AppWorkspace Area, IReadOnlyList<AppConversationSummary> Conversas, DateTimeOffset LidoEm,
    IReadOnlyList<AvisoDeConfirmacao> Avisos, IReadOnlyDictionary<int, int> PacienteDoAgendamento,
    IReadOnlyDictionary<string, IReadOnlyList<FichaComTelefone>> FichasPorConversa,
    IReadOnlyList<LinhaAcompanhamento> Retencao, bool RetencaoPermitida, IReadOnlyList<int> LimitesDeDias)
{
    /// <summary>Os nomes da equipe do CRM (id → nome), para a linha do tempo e o responsável.</summary>
    public IReadOnlyDictionary<string, string> Agentes { get; } =
        Sessao.Agents.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First().Name);

    /// <summary>Os resultados de encerrar (chave → rótulo).</summary>
    public IReadOnlyDictionary<string, string> Resultados => Area.Outcomes;

    /// <summary>O estado do @SemDor na leitura inteira; nulo = não pôde ser lido (conta como desligado: <see cref="SemDorNaTela.Ligado"/>).</summary>
    public AppSemDorState? SemDor { get; init; }
}

/// <summary>Uma ficha que tem o telefone da conversa, com o que a recepção pergunta para conferir.</summary>
public sealed record FichaCandidata(int Id, string Nome, string Iniciais, string TomDoAvatar, string Nascimento,
    string FimDoDocumento, string Proxima, string Ultima);

/// <summary>O paciente da ficha conferida, lido do app (nunca do CRM).</summary>
public sealed record CartaoDoPaciente(int Id, string Nome, string Iniciais, string Idade, string Telefone, bool TelefoneConfere,
    string Convenio, string? Carteirinha, bool CarteirinhaVencida, string FimDoDocumento, bool MensagensAutorizadas, string Mensagens);

/// <summary>
/// Um horário do paciente, com o que a confirmação diz dele e o que dá para fazer ali. "Confirmar"
/// e "Não vem" valem para todo horário MARCADO E FUTURO da ficha conferida, com ou sem a rodada de
/// Confirmações (R1, 25/10): o paciente diz "confirmo" ou "não vou" na própria conversa, e a resposta
/// grava o contato de confirmação daquele horário (<see cref="CampanhaService.RegistrarRespostaDoHorarioAsync"/>).
/// Nenhum dos dois depois que ele já respondeu que não vem (aí é "Manter" ou remarcar) nem para o horário
/// que saiu da rodada por decisão de alguém.
/// </summary>
public sealed record HorarioDoPaciente(int AgendamentoId, DateTime Quando, string QuandoTexto, string Detalhe,
    string Situacao, string Tom, bool PodeConfirmar, bool PodeNaoVem, bool PodeManter, int? ProfissionalId,
    ModalidadeAtendimento Modalidade, string? Profissional);

/// <summary>Um passo da confirmação de hoje (a hora e o que aconteceu).</summary>
public sealed record PassoDaConfirmacao(string Hora, string Texto);

/// <summary>
/// Tudo o que a conversa aberta precisa, LIDO — a entrada pura de <see cref="WhatsAppDaRecepcaoService.Compor"/>.
/// Depois de uma escrita no CRM a tela troca só a <see cref="Conversa"/> e compõe de novo, sem
/// reler o app.
/// </summary>
/// <param name="Conversa">Nulo: o caso da retenção que ainda não tem conversa no CRM (nasce no primeiro envio).</param>
/// <param name="ParaResolver">O que resolver com o paciente da ficha conferida (os alertas da Ficha do balcão), lido
/// DEPOIS de a conversa estar desenhada — nulo enquanto não foi lido (<see cref="WhatsAppDaRecepcaoService.ParaResolverAsync"/>).</param>
public sealed record DadosDaConversa(AppConversation? Conversa, string Contato, string? NomeDoCaso,
    IReadOnlyDictionary<string, string> Agentes, string? MeuAgente, int RecebidasJaLidas,
    IReadOnlyList<AvisoDeConfirmacao> Avisos, IReadOnlyList<FichaCandidata> Candidatas, CartaoDoPaciente? Paciente,
    IReadOnlyList<HorarioDoPaciente> Horarios, IReadOnlyList<PassoDaConfirmacao> Confirmacao,
    LinhaAcompanhamento? Caso, IReadOnlyList<ContatoAcompanhamento> Contatos, IReadOnlyDictionary<string, string> Resultados,
    ParaResolverDoPaciente? ParaResolver = null);

/// <summary>
/// "Para resolver" na coluna do paciente (R6, 25/10): os MESMOS alertas que a Ficha do balcão mostra
/// (<see cref="ElegibilidadeService"/>: consulta a renovar, cota, carteirinha, guia glosada, pacote vencendo, conta em
/// atraso, termo pendente), do paciente <see cref="PacienteId"/>. <see cref="Falhou"/>: a leitura não veio — a tela diz
/// que NÃO conferiu, nunca "nada a resolver".
/// </summary>
public sealed record ParaResolverDoPaciente(int PacienteId, IReadOnlyList<FichaAdministrativaNaTela.Alerta> Alertas, bool Falhou = false);

/// <summary>A conversa aberta, composta: o que as colunas 2 e 3 desenham.</summary>
public sealed record ConversaDoWhatsApp(DadosDaConversa Dados, string Id, string Nome, string Iniciais, string TomDoAvatar,
    string Telefone, bool Sms, string Fila, string Responsavel, bool Minha, bool Encerrada, string? Encerramento,
    IReadOnlyList<ItemDaConversa> LinhaDoTempo, JanelaDaConversa Janela, FichaDaConversa Ficha,
    AcaoPedida? Acao, AberturaDaRetencao? Abertura, (string Titulo, string Texto)? CampoFechado)
{
    public AppConversation? Conversa => Dados.Conversa;
    public LinhaAcompanhamento? Caso => Dados.Caso;
    public CartaoDoPaciente? Paciente => Dados.Paciente;

    /// <summary>A primeira sessão marcada daqui para a frente (a faixa da conversa e o modelo do lembrete).</summary>
    public HorarioDoPaciente? ProximoHorario => Dados.Horarios.FirstOrDefault();

    /// <summary>
    /// O nome que a pessoa usa no WhatsApp, enquanto a ficha não foi conferida (item 16 da revisão, 25/10): o título
    /// da conversa é o TELEFONE e este nome aparece rotulado como "nome no WhatsApp" — com duas fichas no mesmo número,
    /// o nome do perfil não diz qual delas é a pessoa (regra 5: o telefone não é identidade). Nulo com a ficha conferida.
    /// </summary>
    public string? NomeNoWhatsApp { get; init; }

    /// <summary>O nome do perfil, rotulado para o cabeçalho: "Terezinha (nome no WhatsApp)"; no SMS, "(nome na conversa)" — SMS não tem perfil.</summary>
    public string? NomeDoPerfil => NomeNoWhatsApp is { } n ? $"{n} (nome {(Sms ? "na conversa" : "no WhatsApp")})" : null;

    /// <summary>Sem telefone (o caso da retenção de quem não tem celular na ficha): não há WhatsApp, janela nem selo do canal.</summary>
    public bool SemTelefone => string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>O envio feito, e o que não deu certo DEPOIS dele (a mensagem já saiu: a frase diz o que falta).</summary>
public sealed record ResultadoDoEnvio(AppConversation Conversa, string? Pendencia);

/// <summary>
/// O WHATSAPP DA RECEPÇÃO, COMPOSTO (out/2026 — PR 3 e PR 4 de
/// <c>docs/whatsapp-nativo-implementacao.md</c>). A tela nativa chama ESTE serviço e mais
/// nenhum: ele lê o CRM (<see cref="IClienteDoCrm"/>) e o app (agenda, confirmação,
/// Acompanhamento, ficha) e devolve os records prontos para desenhar; e cada escrita é o
/// MESMO método que a tela vizinha já usa — o CampanhaService das Confirmações, o
/// AgendaService da agenda, o AtualizarAsync do Acompanhamento. Nenhuma regra nova de agenda,
/// de confirmação ou de retenção mora aqui: o que a tela AFIRMA vem de
/// <see cref="ConversasNaTela"/>, <see cref="RetencaoNaTela"/> e <see cref="ModeloDoWhatsApp"/>.
///
/// As regras da casa que valem aqui:
/// <list type="bullet">
/// <item><b>Leituras SEQUENCIAIS</b> no mesmo escopo (§3 do CLAUDE.md): o mesmo DbContext não
/// aceita duas operações ao mesmo tempo.</item>
/// <item><b>Quem assina é quem fez LOGIN</b> (<see cref="SessaoUsuario.Atual"/>): o
/// <c>usuarioId</c> da sessão do CRM e o operador da agenda e da campanha.</item>
/// <item><b>Leitura de UM paciente se filtra no banco</b>: a ficha, os horários e os contatos da
/// conversa aberta são do paciente dela, nunca da clínica inteira.</item>
/// <item><b>O telefone não é identidade</b>: ligar a ficha é ato da recepção, nunca daqui.</item>
/// </list>
/// </summary>
public sealed partial class WhatsAppDaRecepcaoService(
    IClienteDoCrm crm, IClinicaRepositorio repo, IAcompanhamentoPacienteService acompanhamento,
    AvisosDeConfirmacaoService avisos, CampanhaService campanha, AgendaService agenda,
    ConsentimentoService consentimentos, ParametrosService parametros, ElegibilidadeService elegibilidade,
    RecallDoDia recallDoDia)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantas fichas a conferência mostra (mais que isso, o telefone é de uma recepção ou de um asilo: busque pelo nome).</summary>
    public const int FichasNaConferencia = 6;

    /// <summary>Até quantos dias à frente a coluna do paciente mostra os horários.</summary>
    public const int DiasDeHorarios = 60;

    /// <summary>Os tons do avatar (classes <c>tom-…</c> de conversas.css): um por pessoa, estável.</summary>
    public static readonly IReadOnlyList<string> TonsDoAvatar = ["azul", "teal", "violeta", "rosa", "ciano", "laranja"];

    private static int UsuarioId => SessaoUsuario.Atual.UsuarioId;
    private static string Operador => SessaoUsuario.Atual.Operador;

    /// <summary>O relógio da clínica (o Windows do balcão está no fuso dela).</summary>
    public static DateTimeOffset Agora => DateTimeOffset.Now;

    // ======================================================================== leituras

    /// <summary>A Gerência informou o endereço do CRM?</summary>
    public Task<bool> ConfiguradoAsync(CancellationToken ct = default) => crm.ConfiguradoAsync(ct);

    /// <summary>
    /// A conversa AO LADO das telas de trabalho (fase 2, 24/10) serve? Com o CRM com endereço (a moldura
    /// saiu na fase 3: a aba é sempre a tela nativa). Sem endereço, os botões "Escrever no WhatsApp"
    /// seguem no wa.me. Se a aba existe para quem entrou é o hospedeiro que diz.
    /// </summary>
    public Task<bool> ConversaAoLadoAsync(CancellationToken ct = default) => crm.ConfiguradoAsync(ct);

    /// <summary>
    /// A leitura INTEIRA da aba (ao abrir, e a cada poucos minutos): a sessão e a área de
    /// trabalho do CRM, a lista de conversas, os avisos do sino (a MESMA lista dele) com o dono
    /// de cada horário, as fichas dos telefones sem ficha conferida e a fila do Acompanhamento
    /// com os dias do Gerente. Em fila, no mesmo escopo.
    /// </summary>
    public async Task<PainelDoWhatsApp> LerAsync(CancellationToken ct = default)
    {
        var u = UsuarioId;
        var sessao = await crm.SessaoAsync(u, ct);
        var area = await crm.AreaDeTrabalhoAsync(u, ct);
        var semdor = await EstadoDoSemDorAsync(u, ct);
        var conversas = await crm.ListarConversasAsync(u, ct);
        var agora = Agora;
        var lista = await avisos.ListarAsync(agora.DateTime, agora.Date, ct);
        var donos = await repo.PacientesDosAgendamentosAsync(lista.Select(a => a.AgendamentoId).Distinct().ToList(), ct);
        var fichas = await FichasDasConversasAsync(conversas, ct);
        var dias = await parametros.ObterDiasInatividadeRecallAsync(ct);
        var (retencao, permitida) = await RetencaoAsync(dias, DateOnly.FromDateTime(agora.DateTime), ct);
        return new PainelDoWhatsApp(sessao, area, conversas, agora, lista, donos, fichas, retencao, permitida, [dias]) { SemDor = semdor };
    }

    /// <summary>
    /// O estado do @SemDor. A IA é ajuda: se o estado não vem, a aba abre igual e sem os botões dela. A recusa
    /// (o CRM de antes do @SemDor responde 404) é silenciosa; o CRM que falhou só nesta leitura vai ao log.
    /// </summary>
    private async Task<AppSemDorState?> EstadoDoSemDorAsync(int usuarioId, CancellationToken ct)
    {
        try { return await crm.EstadoDoSemDorAsync(usuarioId, ct); }
        catch (RecusaDoCrm) { return null; }
        catch (CrmIndisponivel e)
        {
            Diagnostico.Registrar("WhatsApp — o estado do @SemDor não pôde ser lido (a tela segue sem ele)", e);
            return null;
        }
    }

    /// <summary>
    /// A releitura de 7 s: só o que mudou no CRM desde a última, JUNTADO por <c>Id</c> sobre a
    /// fotografia (o CRM devolve com margem: a mesma conversa pode vir de novo). As conversas
    /// que fecharam ficam marcadas como fechadas; a telefone novo sem ficha conferida busca as
    /// fichas só dele. O resto do painel (avisos, retenção) é da leitura inteira.
    /// </summary>
    public async Task<PainelDoWhatsApp> AtualizarAsync(PainelDoWhatsApp painel, CancellationToken ct = default)
    {
        var desde = await crm.ListarDesdeAsync(UsuarioId, painel.LidoEm, ct);
        var juntadas = Juntar(painel.Conversas, desde);
        var semFicha = juntadas.Where(c => c.PatientId is null && !painel.FichasPorConversa.ContainsKey(c.Id)).ToList();
        var fichas = painel.FichasPorConversa;
        if (semFicha.Count > 0)
        {
            var novas = await FichasDasConversasAsync(semFicha, ct);
            fichas = fichas.Concat(novas.Where(n => !fichas.ContainsKey(n.Key))).ToDictionary(k => k.Key, k => k.Value);
        }
        return painel with { Conversas = juntadas, LidoEm = desde.Now, FichasPorConversa = fichas };
    }

    /// <summary>A junção da releitura: o que veio substitui pelo <c>Id</c>; o id que fechou e não veio fica fechado.</summary>
    public static IReadOnlyList<AppConversationSummary> Juntar(IReadOnlyList<AppConversationSummary> atuais, AppConversationsSince desde)
    {
        var porId = atuais.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var c in desde.Conversations) porId[c.Id] = c;
        foreach (var id in desde.Closed)
            if (porId.TryGetValue(id, out var c) && !desde.Conversations.Any(x => x.Id == id))
                porId[id] = c with { Closed = true };
        return porId.Values.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// A fila da Retenção. Antes de ler, a fila do recall se RENOVA uma vez por dia (item 8, 25/10): ela só se renovava
    /// quando alguém abria a tela Acompanhamento, e quem trabalha só pela aba WhatsApp via a fila envelhecer sem aviso.
    /// É o MESMO <see cref="IAcompanhamentoPacienteService.GerarRecallAsync"/>, com os dias do Gerente, e ele não
    /// duplica caso (o caso que já está no ciclo da última sessão fica como está). O dia é marcado em
    /// <see cref="RecallDoDia"/>, dividido com a tela Acompanhamento.
    /// </summary>
    private async Task<(IReadOnlyList<LinhaAcompanhamento> Linhas, bool Permitida)> RetencaoAsync(int dias, DateOnly hoje, CancellationToken ct)
    {
        await RenovarRecallDoDiaAsync(dias, hoje, ct);
        try { return (await acompanhamento.ListarAsync(UsuarioId, ct), true); }
        catch (UnauthorizedAccessException) { return ([], false); }
    }

    /// <summary>
    /// Renova a fila do recall se ainda não foi renovada hoje neste app. Quem não tem o acesso do Acompanhamento não
    /// renova (o serviço recusa) e não marca o dia: a próxima pessoa com acesso renova. A falha vai ao log e libera o
    /// dia — a leitura seguinte tenta de novo; a aba abre do mesmo jeito.
    /// </summary>
    private async Task RenovarRecallDoDiaAsync(int dias, DateOnly hoje, CancellationToken ct)
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.GerenciarCampanhas) || !recallDoDia.Reservar(hoje)) return;
        try { await acompanhamento.GerarRecallAsync(UsuarioId, dias, ct: ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            recallDoDia.Liberar(hoje);
            if (ex is not UnauthorizedAccessException) Diagnostico.Registrar("WhatsApp — a fila do recall não pôde ser renovada (a Retenção segue com a de antes)", ex);
        }
    }

    /// <summary>As fichas que têm o telefone de cada conversa sem ficha conferida — UMA consulta para todas.</summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<FichaComTelefone>>> FichasDasConversasAsync(
        IEnumerable<AppConversationSummary> conversas, CancellationToken ct)
    {
        var semFicha = conversas.Where(c => c.PatientId is null && !c.Closed).ToList();
        var numeros = semFicha.SelectMany(c => Variantes(c.Contact)).Distinct().ToList();
        var achadas = numeros.Count == 0 ? [] : await repo.FichasComTelefoneAsync(numeros, FichasNaConferencia * Math.Max(1, semFicha.Count), ct);
        return semFicha.ToDictionary(c => c.Id,
            c => (IReadOnlyList<FichaComTelefone>)achadas.Where(f => ConversasNaTela.MesmoTelefone(c.Contact, f.Telefone)).Take(FichasNaConferencia).ToList());
    }

    /// <summary>
    /// As formas do MESMO número só com dígitos — como o CRM grava (com o 55) e como a ficha
    /// costuma ter (sem ele). É o que a consulta compara; quem confirma é <see cref="ConversasNaTela.MesmoTelefone"/>.
    /// </summary>
    public static IReadOnlyList<string> Variantes(string? telefone)
    {
        var d = Telefone.SemPrefixoDeDiscagem(Telefone.Normalizar(telefone));
        if (d.Length is < 10 or > 13) return [];
        var sem = d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal) ? d[2..] : d;
        // A ficha às vezes guarda o 0 do interurbano ("022998999334"): a busca no banco compara dígitos, então ele entra.
        return sem.Length is 10 or 11 ? [sem, "55" + sem, "0" + sem] : [d];
    }

    /// <summary>O número no formato que o CRM grava (dígitos, com o 55), ou nulo quando ele não serve para mensagem.</summary>
    public static string? ContatoParaOCrm(string? telefone)
        => ConversaNoCrm.Telefone(telefone) is { } d ? (d.Length is 10 or 11 ? "55" + d : d) : null;

    /// <summary>
    /// A conversa aberta, com o que a coluna do paciente precisa: a linha do tempo do CRM e,
    /// do app, a ficha conferida (ou as fichas a conferir), os horários dos próximos
    /// <see cref="DiasDeHorarios"/> dias com a confirmação de cada um, e o caso do Acompanhamento
    /// com os contatos dele. Marca como vistas as recebidas que estavam não lidas.
    /// </summary>
    /// <param name="naoLidas">As não lidas da lista no clique (a divisória "N novas" fica antes delas).</param>
    /// <param name="caso">O caso da retenção que abriu esta conversa, quando ela ainda não tem a ficha do caso ligada.</param>
    public async Task<ConversaDoWhatsApp> AbrirAsync(PainelDoWhatsApp painel, string conversaId, int naoLidas,
        LinhaAcompanhamento? caso = null, CancellationToken ct = default)
    {
        var u = UsuarioId;
        var conversa = await crm.AbrirConversaAsync(u, conversaId, ct);
        var recebidas = conversa.Messages.Count(m => string.Equals(m.Direction, "in", StringComparison.OrdinalIgnoreCase));
        var jaLidas = ConversasNaTela.RecebidasJaLidas(conversa, naoLidas);
        if (naoLidas > 0 && recebidas > 0) await crm.MarcarLidaAsync(u, conversaId, new AppReadMarker(recebidas), ct);
        var pacienteId = conversa.PatientId;
        caso ??= ConversasNaTela.CasoDoPaciente(pacienteId, painel.Retencao);
        return await LerOAppAsync(painel, conversa, conversa.Contact, pacienteId ?? (caso?.PacienteId), jaLidas, caso,
            conferida: pacienteId is not null, ct);
    }

    /// <summary>
    /// O caso da retenção clicado na lista: a conversa que o CRM já tem com a ficha (ou, sem
    /// ela, com o telefone); sem nenhuma, a conversa EM MEMÓRIA — o CRM só é chamado no
    /// primeiro envio (é o que o mockup faz). A janela nasce fechada: só modelo.
    /// </summary>
    public async Task<ConversaDoWhatsApp> AbrirCasoAsync(PainelDoWhatsApp painel, LinhaAcompanhamento caso, CancellationToken ct = default)
    {
        if (ConversaDoCaso(painel, caso) is { } existente)
            return await AbrirAsync(painel, existente.Id, existente.Unread, caso, ct);
        return await LerOAppAsync(painel, null, ContatoParaOCrm(caso.Telefone) ?? caso.Telefone ?? "", caso.PacienteId, 0, caso,
            conferida: true, ct);
    }

    /// <summary>A conversa do caso: a da MESMA ficha (a mais recente); sem ela, a do mesmo telefone ainda sem ficha.</summary>
    public static AppConversationSummary? ConversaDoCaso(PainelDoWhatsApp painel, LinhaAcompanhamento caso)
        => painel.Conversas.Where(c => c.PatientId == caso.PacienteId).OrderByDescending(c => c.UpdatedAt).FirstOrDefault()
           ?? painel.Conversas.Where(c => c.PatientId is null && ConversasNaTela.MesmoTelefone(c.Contact, caso.Telefone))
               .OrderByDescending(c => c.UpdatedAt).FirstOrDefault();

    private async Task<ConversaDoWhatsApp> LerOAppAsync(PainelDoWhatsApp painel, AppConversation? conversa, string contato, int? pacienteId,
        int jaLidas, LinhaAcompanhamento? caso, bool conferida, CancellationToken ct)
    {
        var agora = Agora;
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        CartaoDoPaciente? cartao = null;
        IReadOnlyList<HorarioDoPaciente> horarios = [];
        IReadOnlyList<PassoDaConfirmacao> confirmacao = [];
        IReadOnlyList<FichaCandidata> candidatas = [];
        if (conferida && pacienteId is { } id)
        {
            var p = await repo.ObterPacienteAsync(id, ct);
            if (p is not null)
            {
                // Só quem RECUSOU (PARAR, "Não" no termo) fica sem mensagem; sem registro conta como autorizado (termo do BSV).
                var autorizou = (await repo.PacientesComMensagensLiberadasAsync([id], ct)).Contains(id);
                cartao = Cartao(p, contato, autorizou, hoje);
            }
            var marcados = await repo.AgendamentosDoPacienteNoPeriodoAsync(id, hoje, hoje.AddDays(DiasDeHorarios), ct);
            var contatos = await campanha.DoPacienteAsync(id, 30, ct);
            horarios = Horarios(marcados, contatos, agora);
            confirmacao = ConfirmacaoDeHoje(marcados, contatos, hoje);
        }
        else
        {
            var fichas = await repo.FichasComTelefoneAsync(Variantes(contato), FichasNaConferencia, ct);
            var lista = new List<FichaCandidata>();
            foreach (var f in fichas.Where(f => ConversasNaTela.MesmoTelefone(contato, f.Telefone)))
            {
                var agendaDela = await repo.AgendamentosDoPacienteNoPeriodoAsync(f.Id, hoje.AddDays(-365), hoje.AddDays(DiasDeHorarios), ct);
                lista.Add(Candidata(f, agendaDela, agora));
            }
            candidatas = lista;
        }
        IReadOnlyList<ContatoAcompanhamento> doCaso = [];
        if (caso is not null && painel.RetencaoPermitida)
        {
            try { doCaso = (await acompanhamento.HistoricoAsync(UsuarioId, caso.Id, ct)).Where(c => c.AcompanhamentoPacienteId == caso.Id).ToList(); }
            catch (UnauthorizedAccessException) { doCaso = []; }
            if (doCaso.Count > 0) doCaso = ComNomeDeQuemFez(doCaso, await repo.UsuariosAsync(ct));
        }
        var avisosDele = ConversasNaTela.AvisosDaConversa(conferida ? pacienteId : null, painel.Avisos, painel.PacienteDoAgendamento);
        var dados = new DadosDaConversa(conversa, contato, caso?.Paciente, painel.Agentes, painel.Sessao.AgentId, jaLidas,
            avisosDele, candidatas, cartao, horarios, confirmacao, caso, doCaso, painel.Resultados);
        return Compor(dados, agora);
    }

    // ======================================================================== composição (pura)

    /// <summary>
    /// A conversa aberta a partir do que foi lido. Puro: a tela chama de novo depois de cada
    /// escrita no CRM, com a conversa que a escrita devolveu (<c>dados with { Conversa = … }</c>).
    /// </summary>
    public static ConversaDoWhatsApp Compor(DadosDaConversa d, DateTimeOffset agora)
    {
        var c = d.Conversa;
        // Sem ficha conferida o título é o telefone, e o nome do perfil vai rotulado (item 16): com duas fichas no mesmo
        // número, o nome que a pessoa usa no WhatsApp não diz qual delas ela é.
        var semConferir = c is not null && d.Paciente is null && c.PatientId is null;
        var nomeNoWhatsApp = semConferir && TemLetra(c!.Name) ? c.Name.Trim() : null;
        var nome = c is null ? (d.Caso?.Paciente ?? ConversasNaTela.TelefoneNaTela(d.Contato))
            : d.Paciente?.Nome ?? (semConferir && ConversasNaTela.TelefoneNaTela(c.Contact) is { Length: > 0 } numero ? numero : ConversasNaTela.NomeDaConversa(c));
        var sms = ConversasNaTela.EhSms(c?.Channel);
        var janela = c is null ? ConversasNaTela.Janela(null, agora, "whatsapp") : ConversasNaTela.Janela(c, agora);
        var ficha = c is null
            ? ConversasNaTela.EstadoDaFicha(d.Caso?.PacienteId, d.Caso?.Paciente, 1)
            : ConversasNaTela.EstadoDaFicha(c, d.Candidatas.Count);
        var acao = ConversasNaTela.PedeAcao(c, d.Avisos, d.Candidatas.Count, d.Caso, agora, d.MeuAgente, d.Agentes,
            d.Candidatas is [var unica] ? unica : null);
        var abertura = d.Caso is { } caso && (c is null || c.Messages.Length == 0) ? RetencaoNaTela.Abertura(caso, c, agora) : null;
        var minha = c?.AgentId is { } a && a == d.MeuAgente;
        var encerramento = c is { Closed: true } ? Encerramento(c, d.Agentes, d.Resultados, agora) : null;
        // O avatar: o tom é da conversa (ou do caso, antes de a conversa existir); sem nome nenhum, cinza e "?".
        var semNome = c is not null && d.Paciente is null && c.PatientId is null && !TemLetra(c.Name);
        var chave = c is null ? "caso-" + d.Caso?.Id : semNome ? null : c.Id;
        return new ConversaDoWhatsApp(d, c?.Id ?? "", nome, semNome ? "?" : Iniciais(nome), TomDoAvatar(chave),
            ConversasNaTela.TelefoneNaTela(d.Contato), sms, c?.Queue ?? "Recepção",
            Responsavel(c?.AgentId, d.MeuAgente, d.Agentes), minha, c?.Closed ?? false, encerramento,
            // O caso sem conversa ainda não tem o que mostrar: nem o separador "Hoje" sozinho (foto 82 da revisão).
            c is null ? [] : ConversasNaTela.LinhaDoTempo(c, d.RecebidasJaLidas, agora, d.Agentes),
            janela, ficha, acao, abertura, ConversasNaTela.CampoFechado(janela, nomeNoWhatsApp ?? nome, agora))
        { NomeNoWhatsApp = nomeNoWhatsApp };
    }

    private static string Encerramento(AppConversation c, IReadOnlyDictionary<string, string> agentes,
        IReadOnlyDictionary<string, string> resultados, DateTimeOffset agora)
    {
        var r = c.Resolutions.OrderByDescending(x => x.At).FirstOrDefault();
        var quando = c.ClosedAt ?? r?.At;
        var partes = new List<string> { "Encerrada" };
        if (quando is { } q) partes[0] += " " + ConversasNaTela.Quando(q, agora);
        if (r is not null && agentes.TryGetValue(r.ActorId, out var quem) && !string.IsNullOrWhiteSpace(quem)) partes[0] += $" por {quem}";
        if (r is not null && resultados.TryGetValue(r.Outcome, out var rotulo) && !string.IsNullOrWhiteSpace(rotulo)) partes.Add(rotulo);
        return string.Join(" · ", partes);
    }

    /// <summary>
    /// As linhas da lista de conversas, no filtro e na busca, com o selo de cada uma. Em "Novas", a fila:
    /// o que pede ação na frente, e a mais antiga primeiro (<see cref="ConversasNaTela.OrdenarAsNovas"/>).
    /// </summary>
    public static IReadOnlyList<LinhaDaConversa> Linhas(PainelDoWhatsApp painel, FiltroDasConversas filtro, string? busca,
        bool soNaoLidas, DateTimeOffset agora)
    {
        var linhas = ConversasNaTela.Filtrar(painel.Conversas, filtro, painel.Sessao.AgentId, busca, soNaoLidas)
            .Select(c => Linha(painel, c, agora)).ToList();
        return filtro == FiltroDasConversas.Novas ? ConversasNaTela.OrdenarAsNovas(linhas, l => l.Selo) : linhas;
    }

    public static LinhaDaConversa Linha(PainelDoWhatsApp painel, AppConversationSummary c, DateTimeOffset agora)
    {
        var avisosDele = ConversasNaTela.AvisosDaConversa(c.PatientId, painel.Avisos, painel.PacienteDoAgendamento);
        var fichas = painel.FichasPorConversa.TryGetValue(c.Id, out var f) ? f.Count : 0;
        var caso = ConversasNaTela.CasoDoPaciente(c.PatientId, painel.Retencao);
        var selo = ConversasNaTela.Selo(c, avisosDele, fichas, caso, agora, painel.Resultados, painel.Sessao.AgentId, painel.Agentes);
        var nome = ConversasNaTela.NomeDaConversa(c);
        var semNome = c.PatientId is null && !TemLetra(c.Name) && string.IsNullOrWhiteSpace(c.PatientName);
        return new LinhaDaConversa(c.Id, nome, semNome ? "?" : Iniciais(nome), TomDoAvatar(semNome ? null : c.Id),
            ConversasNaTela.EhSms(c.Channel), HoraDaLinha(c.UpdatedAt, agora),
            string.IsNullOrWhiteSpace(c.Preview) ? "Sem mensagens ainda" : MidiaDaConversa.PreviaDaLista(c.Preview), ConversasNaTela.TickDaPrevia(c),
            Math.Max(0, c.Unread), selo, Responsavel(c.AgentId, painel.Sessao.AgentId, painel.Agentes),
            c.AgentId is not null && c.AgentId == painel.Sessao.AgentId, c.AgentId is not null,
            SemDorNaTela.EtiquetaNaLinha(SemDorNaTela.Etiqueta(painel.SemDor, c), selo), c.PatientId, ConversasNaTela.Espera(c, agora));
    }

    /// <summary>Os números do seletor e dos filtros dos dois modos.</summary>
    public static (ContagemDasConversas Conversas, ContagemDaRetencao Retencao) Contagens(PainelDoWhatsApp painel, DateTimeOffset agora)
        => (ConversasNaTela.Contagens(painel.Conversas, painel.Sessao.AgentId),
            RetencaoNaTela.Contagens(painel.Retencao, ConversasNaTela.UltimaRecebidaPorPaciente(painel.Conversas), agora));

    /// <summary>Os grupos do modo Retenção, com os dias do Gerente, e cada linha composta.</summary>
    public static IReadOnlyList<GrupoNaTela> Grupos(PainelDoWhatsApp painel, FiltroDaRetencao filtro, string? busca, DateTimeOffset agora)
    {
        var ultimas = ConversasNaTela.UltimaRecebidaPorPaciente(painel.Conversas);
        var usuario = UsuarioId;
        return RetencaoNaTela.Agrupar(painel.Retencao, filtro, painel.LimitesDeDias, ultimas, agora, busca)
            .Select(g => new GrupoNaTela(g.Chave, g.Titulo, g.Tom, g.Linhas.Select(l =>
            {
                var conversa = ConversaDoCaso(painel, l);
                DateTimeOffset? ultima = ultimas.TryGetValue(l.PacienteId, out var x) ? x : null;
                return new LinhaDaRetencao(l, Iniciais(l.Paciente), TomDoAvatar("caso-" + l.Id), RetencaoNaTela.Dias(l),
                    g.Chave == "novo-bsv" ? "info" : g.Tom, RetencaoNaTela.Detalhe(l), RetencaoNaTela.Selo(l, ultima, agora),
                    l.ResponsavelId is null ? "Sem responsável" : ConversaDoAcompanhamento.Dono(l, usuario),
                    l.ResponsavelId is not null && l.ResponsavelId == usuario, conversa is { Unread: > 0 }, conversa?.Id);
            }).ToList())).ToList();
    }

    // ======================================================================== peças puras

    /// <summary>"10:09" hoje, "ontem", "qua" na semana, "22/09" antes disso — a hora da linha da lista.</summary>
    public static string HoraDaLinha(DateTimeOffset momento, DateTimeOffset agora)
    {
        if (momento.Year <= 1) return "";
        var local = momento.ToOffset(agora.Offset).DateTime;
        var dias = (agora.DateTime.Date - local.Date).Days;
        return dias switch
        {
            <= 0 => local.ToString("HH:mm", PtBr),
            1 => "ontem",
            < 7 => AvisosDeConfirmacao.DiaDaSemana(local.DayOfWeek),
            _ => local.ToString(local.Year == agora.Year ? "dd/MM" : "dd/MM/yy", PtBr)
        };
    }

    /// <summary>O tom do avatar: o mesmo para a mesma conversa em toda abertura (nunca o <c>GetHashCode</c>, que muda a cada processo). Sem nome, cinza.</summary>
    public static string TomDoAvatar(string? chave)
    {
        if (string.IsNullOrEmpty(chave)) return "cinza";
        var soma = 0;
        foreach (var ch in chave) soma = unchecked(soma * 31 + ch);
        return TonsDoAvatar[(int)((uint)soma % (uint)TonsDoAvatar.Count)];
    }

    /// <summary>As duas iniciais do avatar; "?" quando o nome é só o telefone.</summary>
    public static string Iniciais(string? nome) => TemLetra(nome) ? ConversaDoAcompanhamento.Iniciais(nome!) : "?";

    /// <summary>"Você", o nome de quem está com a conversa, "Equipe" para um id que a sessão não conhece, ou "Sem responsável".</summary>
    public static string Responsavel(string? agenteId, string? meuAgente, IReadOnlyDictionary<string, string> agentes)
        => string.IsNullOrWhiteSpace(agenteId) ? "Sem responsável"
            : agenteId == meuAgente ? "Você"
            : agentes.TryGetValue(agenteId, out var n) && !string.IsNullOrWhiteSpace(n) ? n : "Equipe";

    /// <summary>"final 42": só os dois últimos dígitos do documento, o que a recepção pergunta ao telefone.</summary>
    public static string FimDoDocumento(string? documento)
    {
        var d = Telefone.Normalizar(documento);
        return d.Length < 2 ? "—" : "final " + d[^2..];
    }

    /// <summary>O cartão do paciente da ficha conferida.</summary>
    public static CartaoDoPaciente Cartao(Paciente p, string contatoDaConversa, bool mensagensAutorizadas, DateOnly hoje)
    {
        var cab = FichaDoBalcao.Cabecalho(p, hoje);
        return new CartaoDoPaciente(p.Id, p.Nome, Iniciais(p.Nome), cab.Idade, cab.Telefone,
            ConversasNaTela.MesmoTelefone(contatoDaConversa, p.Telefone), cab.Convenio, cab.Carteirinha, cab.CarteirinhaVencida,
            FimDoDocumento(p.Documento), mensagensAutorizadas,
            mensagensAutorizadas ? "Liberadas" : "Pediu para não receber · a ação é ligar");
    }

    /// <summary>
    /// Os horários MARCADOS do paciente, do mais próximo, com a confirmação de cada um (o
    /// contato de confirmação mais recente daquele horário, pela regra de
    /// <see cref="RodadaDeConfirmacao.Classificar"/>).
    /// </summary>
    public static IReadOnlyList<HorarioDoPaciente> Horarios(IEnumerable<Agendamento> marcados, IEnumerable<ContatoCampanha> contatos, DateTimeOffset agora)
    {
        var confirmacoes = contatos.Where(c => c.Tipo == TipoContato.ConfirmacaoSessao && c.AgendamentoId is not null)
            .GroupBy(c => c.AgendamentoId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());
        return marcados.Where(a => a.Status == StatusAgendamento.Agendado && a.DataHora >= agora.DateTime.Date)
            .OrderBy(a => a.DataHora).ThenBy(a => a.Id)
            .Select(a =>
            {
                confirmacoes.TryGetValue(a.Id, out var contato);
                var situacao = contato is null ? (SituacaoConfirmacao?)null : RodadaDeConfirmacao.Classificar(contato.Status, contato.EnviadoEm, contato.Comentario);
                var (texto, tom) = situacao switch
                {
                    SituacaoConfirmacao.Confirmou => (RodadaDeConfirmacao.SeloConfirmou, "sucesso"),
                    SituacaoConfirmacao.PediuRemarcar => (RodadaDeConfirmacao.SeloNaoVem, "perigo"),
                    SituacaoConfirmacao.PediuCancelar => (RodadaDeConfirmacao.SeloPediuCancelar, "perigo"),
                    SituacaoConfirmacao.AvisadoSemResposta => (RodadaDeConfirmacao.SemResposta, "aviso"),
                    SituacaoConfirmacao.Dispensado => ("Fora da rodada", "neutro"),
                    _ => ("Marcado", "neutro")
                };
                var pedido = situacao is SituacaoConfirmacao.PediuRemarcar or SituacaoConfirmacao.PediuCancelar;
                var futuro = a.DataHora > agora.DateTime;
                var modalidade = CatalogoModalidades.Nome(a.ModalidadeCodigo, a.ModalidadePrevista);
                var detalhe = string.Join(" · ", new[] { modalidade, a.Profissional?.Nome }.Where(x => !string.IsNullOrWhiteSpace(x)));
                return new HorarioDoPaciente(a.Id, a.DataHora, Maiuscula(AvisosDeConfirmacao.QuandoDaSessao(a.DataHora, agora.DateTime)), detalhe,
                    texto, tom,
                    PodeConfirmar: futuro && situacao is not (SituacaoConfirmacao.Confirmou or SituacaoConfirmacao.Dispensado) && !pedido,
                    PodeNaoVem: futuro && situacao is not SituacaoConfirmacao.Dispensado && !pedido,
                    PodeManter: pedido, a.ProfissionalId, a.ModalidadePrevista, a.Profissional?.Nome);
            }).ToList();
    }

    /// <summary>A confirmação da sessão de HOJE: quando o lembrete saiu (e por onde) e o que o paciente respondeu.</summary>
    public static IReadOnlyList<PassoDaConfirmacao> ConfirmacaoDeHoje(IEnumerable<Agendamento> marcados, IEnumerable<ContatoCampanha> contatos, DateOnly hoje)
    {
        var deHoje = marcados.Where(a => DateOnly.FromDateTime(a.DataHora) == hoje).Select(a => a.Id).ToHashSet();
        var contato = contatos.Where(c => c.Tipo == TipoContato.ConfirmacaoSessao && c.AgendamentoId is { } id && deHoje.Contains(id))
            .OrderByDescending(c => c.Id).FirstOrDefault();
        if (contato is null) return [];
        var passos = new List<PassoDaConfirmacao>();
        if (contato.EnviadoEm is { } enviado)
            passos.Add(new(enviado.ToString("HH:mm", PtBr), RodadaDeConfirmacao.Canal(contato.Canal) + QuemEnviou(contato.EnviadoPor)));
        var situacao = RodadaDeConfirmacao.Classificar(contato.Status, contato.EnviadoEm, contato.Comentario);
        if (contato.RespondidoEm is { } respondido && situacao != SituacaoConfirmacao.Dispensado)
            passos.Add(new(respondido.ToString("HH:mm", PtBr), "Respondeu: " + RodadaDeConfirmacao.SeloDaLista(
                new RespostaDeConfirmacao(contato.Status, contato.EnviadoEm, contato.Comentario, contato.Canal))));
        else if (situacao == SituacaoConfirmacao.Dispensado)
            passos.Add(new(contato.RespondidoEm?.ToString("HH:mm", PtBr) ?? "", "Saiu da rodada sem aviso" + (string.IsNullOrWhiteSpace(contato.Comentario) ? "" : $" · {contato.Comentario}")));
        return passos;
    }

    /// <summary>
    /// Quem mandou o lembrete, na frase da tela. O lembrete automático é gravado pela ponte do CRM
    /// como "CRM · WhatsApp" (e pela prévia como "crm"): mostrar isso cru daria "WhatsApp enviado ·
    /// CRM · WhatsApp". O automático vira "pelo número oficial (automático)"; o resto é o nome de
    /// quem fez login e mandou.
    /// </summary>
    public static string QuemEnviou(string? enviadoPor)
        => string.IsNullOrWhiteSpace(enviadoPor) ? ""
            : enviadoPor.TrimStart().StartsWith("CRM", StringComparison.OrdinalIgnoreCase) ? " pelo número oficial (automático)"
            : $" · {enviadoPor.Trim()}";

    /// <summary>Uma ficha a conferir, com nascimento, o fim do documento, a próxima sessão e a última.</summary>
    public static FichaCandidata Candidata(FichaComTelefone f, IEnumerable<Agendamento> agenda, DateTimeOffset agora)
    {
        var lista = agenda.ToList();
        var proxima = lista.Where(a => a.Status == StatusAgendamento.Agendado && a.DataHora >= agora.DateTime).OrderBy(a => a.DataHora).FirstOrDefault();
        var ultima = lista.Where(a => a.Status == StatusAgendamento.Realizado && a.DataHora < agora.DateTime).OrderByDescending(a => a.DataHora).FirstOrDefault();
        var nascimento = f.DataNascimento is { } n ? $"{n.ToString("dd/MM/yyyy", PtBr)} · {IdadeDoPaciente.Texto(n, DateOnly.FromDateTime(agora.DateTime))}" : "—";
        return new FichaCandidata(f.Id, f.Nome, Iniciais(f.Nome), TomDoAvatar("ficha-" + f.Id), nascimento, FimDoDocumento(f.Documento),
            proxima is null ? "nenhuma" : $"{AvisosDeConfirmacao.QuandoDaSessao(proxima.DataHora, agora.DateTime)} · {CatalogoModalidades.Nome(proxima.ModalidadeCodigo, proxima.ModalidadePrevista)}",
            ultima is null ? "—" : ultima.DataHora.ToString("dd/MM/yyyy", PtBr));
    }

    /// <summary>
    /// A mensagem pronta depois de remarcar pela janela do horário (o mockup, foto 04): diz o
    /// que a clínica FEZ, e a recepção revisa antes de enviar (regra 3 do CRM). O lembrete só é
    /// prometido quando o pedido de confirmação automático está LIGADO no CRM
    /// (<see cref="AppSession.RemindersEnabled"/>) e a sessão não é hoje (R3, 25/10): com ele
    /// desligado a frase "o lembrete chega" era uma promessa que a clínica não cumpria. O texto não
    /// diz a hora do lembrete — o CRM manda na manhã do dia ou 24 h antes, conforme a Automação.
    /// </summary>
    public static string RascunhoDeRemarcacao(string? nome, HorarioDoPaciente novo, DateOnly hoje, bool lembreteAutomatico)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        var com = string.IsNullOrWhiteSpace(novo.Profissional) ? "" : $" com {novo.Profissional}";
        var lembrete = lembreteAutomatico && DateOnly.FromDateTime(novo.Quando) > hoje ? " Antes da sessão chega o lembrete para você confirmar." : "";
        return $"Combinado{(quem.Length == 0 ? "" : ", " + quem)}! Remarquei a sua {ModeloDoWhatsApp.NaFrase(novo.Modalidade)} para "
            + $"{MensagensDeContato.QuandoPorExtenso(novo.Quando, hoje)}{com}.{lembrete}";
    }

    /// <summary>
    /// O horário que o modelo de LEMBRETE avisa (D11, 25/10): o primeiro horário marcado e futuro da ficha conferida —
    /// o mesmo que preenche o "Dia e hora" do modelo. Nulo para modelo que não é lembrete, sem ficha conferida ou sem
    /// horário à frente: aí o envio não grava aviso nenhum na rodada.
    /// </summary>
    public static int? HorarioDoLembrete(ConversaDoWhatsApp aberta, AppTemplate modelo, DateTimeOffset agora)
        => aberta.Paciente is not null && ModeloDoWhatsApp.Lembretes.Contains(modelo.Name ?? "")
            ? aberta.Dados.Horarios.FirstOrDefault(h => h.Quando > agora.DateTime)?.AgendamentoId
            : null;

    /// <summary>Os contatos do caso com o NOME de quem fez (item 15): o histórico grava o login ("ana"), e a recepção lê pessoas.</summary>
    public static IReadOnlyList<ContatoAcompanhamento> ComNomeDeQuemFez(IReadOnlyList<ContatoAcompanhamento> contatos, IEnumerable<UsuarioSistema> usuarios)
    {
        var nomes = usuarios.Where(u => !string.IsNullOrWhiteSpace(u.Login) && !string.IsNullOrWhiteSpace(u.Nome))
            .GroupBy(u => u.Login.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Nome.Trim(), StringComparer.OrdinalIgnoreCase);
        // Cópias: o histórico que veio do serviço não é alterado.
        return contatos.Select(c => new ContatoAcompanhamento
        {
            Id = c.Id, AcompanhamentoPacienteId = c.AcompanhamentoPacienteId, Idempotencia = c.Idempotencia, Em = c.Em,
            Operador = nomes.TryGetValue(c.Operador?.Trim() ?? "", out var nome) ? nome : c.Operador ?? "",
            Canal = c.Canal, Resultado = c.Resultado, Observacao = c.Observacao, ProximoContato = c.ProximoContato, ResponsavelId = c.ResponsavelId
        }).ToList();
    }

    /// <summary>O contexto que preenche as variáveis do modelo: a ficha, a próxima sessão e o caso.</summary>
    public static ContextoDoModelo ContextoDoModelo(ConversaDoWhatsApp aberta, DateTimeOffset agora)
        => new(aberta.Paciente?.Nome ?? aberta.Caso?.Paciente ?? (aberta.Conversa is { } c ? ConversasNaTela.NomeDaConversa(c) : null),
            DateOnly.FromDateTime(agora.DateTime), aberta.ProximoHorario?.Modalidade, aberta.ProximoHorario?.Quando, aberta.Caso);

    /// <summary>Os modelos que CONVIDAM a voltar: enviados a quem tem caso aberto, contam como contato da retenção e exigem a autorização.</summary>
    public static readonly IReadOnlySet<string> ModelosDeConvite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ModeloDoWhatsApp.RecallReal, ModeloDoWhatsApp.IniciarAtendimentoReal, ModeloDoWhatsApp.RetornoTratamento, ModeloDoWhatsApp.PrimeiroContato, "recall_semdor" };

    /// <summary>
    /// O modelo do convite que o CRM tem DE FATO: o primeiro de <see cref="RetencaoNaTela.CandidatosDoConvite"/>
    /// (o aprovado na Meta antes do nome do mockup) que veio em <c>GET /api/templates</c> e pode ser enviado.
    /// Nulo = nenhum serve, e a tela não oferece o convite (diz por quê).
    /// </summary>
    public static AppTemplate? ModeloDoConvite(LinhaAcompanhamento caso, IEnumerable<AppTemplate> doCrm)
    {
        var lista = doCrm.Where(m => m.Supported).ToList();
        return RetencaoNaTela.CandidatosDoConvite(caso)
            .Select(nome => lista.FirstOrDefault(m => string.Equals(m.Name, nome, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(m => m is not null);
    }

    /// <summary>
    /// O caso que o envio do modelo registra (e que obriga a conferir a autorização): o caso
    /// aberto da conversa, quando o modelo é um convite. O lembrete e o "documento pronto" não
    /// mexem no Acompanhamento.
    /// </summary>
    public static LinhaAcompanhamento? CasoDoConvite(ConversaDoWhatsApp aberta, AppTemplate modelo)
        => aberta.Caso is { Pendente: true } c && RetencaoNaTela.EmTratamento(c) && ModelosDeConvite.Contains(modelo.Name ?? "") ? c : null;

    private static bool TemLetra(string? s) => !string.IsNullOrWhiteSpace(s) && s.Any(char.IsLetter);

    private static string Maiuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0], PtBr) + s[1..];

    // ======================================================================== escritas no CRM

    // ======================================================================== o @SemDor (lê e redige; nada sai daqui)

    /// <summary>
    /// O ✨: com o campo vazio, a sugestão de resposta; com texto, a revisão dele. Devolve o TEXTO para o campo —
    /// o envio continua sendo o Enviar da secretária. As falhas chegam com a frase do CRM (<see cref="SemDorNaTela.Falha"/>).
    /// </summary>
    public Task<AppSemDorSuggestion> SugerirComSemDorAsync(AppConversation c, string? rascunho, CancellationToken ct = default)
        => crm.SugerirComSemDorAsync(UsuarioId, c.Id, new AppSemDorRequest(SemDorNaTela.RascunhoDoPedido(rascunho)), ct);

    /// <summary>O "✨ Resumo": o que o paciente quer, o que já foi feito e o que falta.</summary>
    public Task<AppSemDorSummary> ResumirComSemDorAsync(AppConversation c, CancellationToken ct = default)
        => crm.ResumirComSemDorAsync(UsuarioId, c.Id, ct);

    /// <summary>Os modelos aprovados do remetente da conversa.</summary>
    public Task<AppTemplate[]> ModelosAsync(string remetente, CancellationToken ct = default) => crm.ModelosAsync(UsuarioId, remetente, ct);

    /// <summary>
    /// Responde ao paciente (texto e/ou anexo). <paramref name="idDoEnvio"/> é o Guid "D" do
    /// rascunho: o MESMO no segundo clique e na repetição depois de uma resposta perdida — é
    /// o que torna o envio idempotente no CRM.
    /// </summary>
    /// <param name="citada">O id da mensagem que a resposta cita ("Responder citando", botão direito): o WhatsApp a mostra
    /// em cima da resposta. Só WhatsApp — o SMS não cita, e o CRM recusa.</param>
    public Task<AppConversation> ResponderAsync(AppConversation c, string texto, string? anexoId, string idDoEnvio, CancellationToken ct = default,
        string? citada = null)
    {
        if (string.IsNullOrWhiteSpace(texto) && anexoId is null) throw new InvalidOperationException("Escreva a mensagem ou anexe um arquivo.");
        if (citada is not null && ConversasNaTela.EhSms(c.Channel)) throw new InvalidOperationException("O SMS não cita mensagens: envie sem a citação.");
        var id = IdDoEnvio(idDoEnvio);
        return ComVersaoNovaAsync(c, v => crm.ResponderAsync(UsuarioId, c.Id, new AppReply(v, id, texto?.Trim() ?? "", anexoId, ReplyToId: citada), ct), ct);
    }

    /// <summary>"Marcar como não lida" (botão direito da lista): a última recebida volta a contar, só para quem pediu.</summary>
    public Task MarcarNaoLidaAsync(string conversaId, CancellationToken ct = default) => crm.MarcarNaoLidaAsync(UsuarioId, conversaId, ct);

    /// <summary>"Marcar como lida" sem abrir a conversa (botão direito da lista): lê as recebidas e marca todas.</summary>
    public async Task MarcarLidaAsync(string conversaId, CancellationToken ct = default)
    {
        var u = UsuarioId;
        var conversa = await crm.AbrirConversaAsync(u, conversaId, ct);
        var recebidas = conversa.Messages.Count(m => string.Equals(m.Direction, "in", StringComparison.OrdinalIgnoreCase));
        if (recebidas > 0) await crm.MarcarLidaAsync(u, conversaId, new AppReadMarker(recebidas), ct);
    }

    /// <summary>Guarda uma nota interna (nunca vai ao paciente).</summary>
    public Task<AppConversation> AnotarAsync(AppConversation c, string texto, string idDoEnvio, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto)) throw new InvalidOperationException("Escreva a nota.");
        var id = IdDoEnvio(idDoEnvio);
        return ComVersaoNovaAsync(c, v => crm.AnotarAsync(UsuarioId, c.Id, new AppNoteRequest(v, id, texto.Trim()), ct), ct);
    }

    /// <summary>
    /// O envio e a nota no 409: a <c>Version</c> da conversa muda sozinha (o recibo de entrega de
    /// uma mensagem anterior avança o status), então o conflito é COMUM e não quer dizer que alguém
    /// mexeu. Relê a conversa e repete UMA vez com o MESMO Id — o Id torna o envio idempotente no
    /// CRM, a mensagem não sai duas vezes. O segundo 409 sobe para a tela. (Assumir, transferir,
    /// encerrar e devolver NÃO repetem sozinhos: ali o conflito pode ser outra pessoa.)
    /// </summary>
    private async Task<AppConversation> ComVersaoNovaAsync(AppConversation c, Func<int, Task<AppConversation>> ato, CancellationToken ct)
    {
        try { return await ato(c.Version); }
        catch (RecusaDoCrm r) when (r.Conflito)
        {
            var relida = await crm.AbrirConversaAsync(UsuarioId, c.Id, ct);
            return await ato(relida.Version);
        }
    }

    /// <summary>Sobe UM arquivo; o id devolvido vai no envio. <paramref name="gravacao"/>: o áudio gravado na tela (o CRM o converte para a nota de voz do WhatsApp).</summary>
    public Task<AppAttachment> AnexarAsync(AppConversation c, Stream arquivo, string nome, string mime, bool gravacao = false, CancellationToken ct = default)
        => crm.AnexarAsync(UsuarioId, c.Id, arquivo, nome, mime, gravacao, ct);

    /// <summary>O arquivo de uma mensagem (foto, áudio, vídeo, documento), já pronto para a tela.</summary>
    public async Task<MidiaCarregada> MidiaAsync(MidiaDaMensagem midia, CancellationToken ct = default)
        => MidiaDaConversa.Preparar(midia, await crm.MidiaAsync(UsuarioId, midia.ConversaId, midia.Mensagem, ct));

    public Task<AppConversation> AssumirAsync(AppConversation c, string? meuAgente, CancellationToken ct = default)
        => crm.AssumirAsync(UsuarioId, c.Id, new AppChange(c.Version, meuAgente, c.Queue), ct);

    /// <summary>O mínimo e o máximo do motivo de devolver à fila (a regra do CRM: 400 fora disso).</summary>
    public const int MinimoDoMotivoDeDevolver = 5, MaximoDoMotivoDeDevolver = 2000;

    /// <summary>Devolve à fila da Recepção, com o motivo (fica na conversa como nota e na trilha). A rota: <see cref="DevolverAsync(string, int, string, string, CancellationToken)"/>.</summary>
    public Task<AppConversation> DevolverAsync(AppConversation c, string motivo, CancellationToken ct = default)
        => DevolverAsync(c.Id, c.Version, c.Queue, motivo, c.PendingTransfer, ct);

    /// <summary>
    /// A devolução pela DONA da conversa: a transferência para a MESMA fila, sem responsável (o que o CRM web faz no
    /// "Transferir" sem atendente — "Atendimento disponível para ser assumido pela equipe"). ⚠️ Não é o
    /// <c>/reassign</c>: aquele é o "Reorganizar" da GESTÃO (recusa com 403 quem não é admin ou supervisora,
    /// <c>ReceptionHandoffs.cs</c>), e era por ele que o "Com você → Devolver" ia — para a secretária comum, o botão
    /// respondia "Somente a gestão pode reorganizar…" (achado em 25/10, no "Passar o turno", que usa este método).
    /// Com um PEDIDO de transferência pendente (§14) o CRM recusa qualquer transferência ("Cancele antes de
    /// solicitar outra"): devolver cancela o pedido antes — quem devolve desiste de passar a conversa a uma pessoa —
    /// e relê a conversa para a Version nova. Sem isso a conversa ficava com quem saiu (achado na junção, foto 79).
    /// </summary>
    internal async Task<AppConversation> DevolverAsync(string conversaId, int versao, string fila, string motivo, AppPendingTransfer? pedido,
        CancellationToken ct = default)
    {
        var m = motivo?.Trim() ?? "";
        if (m.Length < MinimoDoMotivoDeDevolver || m.Length > MaximoDoMotivoDeDevolver)
            throw new InvalidOperationException($"Diga por que a conversa volta à fila (de {MinimoDoMotivoDeDevolver} a {MaximoDoMotivoDeDevolver} letras).");
        if (pedido is not null)
        {
            await crm.DecidirTransferenciaAsync(UsuarioId, pedido.Id, "cancel", new AppTransferDecision(), ct);
            var atual = await crm.AbrirConversaAsync(UsuarioId, conversaId, ct);
            (versao, fila) = (atual.Version, atual.Queue);
        }
        return await crm.TransferirAsync(UsuarioId, conversaId, new AppChange(versao, null, fila, m), ct);
    }

    public Task<AppConversation> TransferirAsync(AppConversation c, string? agenteId, string? fila, string? nota, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agenteId) && string.IsNullOrWhiteSpace(fila)) throw new InvalidOperationException("Escolha para quem transferir.");
        return crm.TransferirAsync(UsuarioId, c.Id, new AppChange(c.Version, string.IsNullOrWhiteSpace(agenteId) ? null : agenteId,
            string.IsNullOrWhiteSpace(fila) ? c.Queue : fila, string.IsNullOrWhiteSpace(nota) ? null : nota.Trim()), ct);
    }

    // ---------------------------------------------------------------- o pedido de transferência (R7, 25/10)

    /// <summary>O motivo de recusar uma transferência (a regra do CRM: 400 fora disso).</summary>
    public const int MinimoDoMotivoDeRecusar = 3, MaximoDoMotivoDeRecusar = 500;

    /// <summary>Quem recebe aceita o pedido: a conversa passa a ser dela. Devolve a conversa relida.</summary>
    public Task<AppConversation> AceitarTransferenciaAsync(AppConversation c, CancellationToken ct = default)
        => DecidirTransferenciaAsync(c, "accept", null, ct);

    /// <summary>Quem recebe recusa, com o motivo (fica na trilha): a conversa continua com quem pediu.</summary>
    public Task<AppConversation> RecusarTransferenciaAsync(AppConversation c, string motivo, CancellationToken ct = default)
    {
        var m = motivo?.Trim() ?? "";
        if (m.Length < MinimoDoMotivoDeRecusar || m.Length > MaximoDoMotivoDeRecusar)
            throw new InvalidOperationException($"Diga por que recusa (de {MinimoDoMotivoDeRecusar} a {MaximoDoMotivoDeRecusar} letras).");
        return DecidirTransferenciaAsync(c, "reject", m, ct);
    }

    /// <summary>Quem pediu desiste: a conversa continua com ela, sem pedido pendente.</summary>
    public Task<AppConversation> CancelarTransferenciaAsync(AppConversation c, CancellationToken ct = default)
        => DecidirTransferenciaAsync(c, "cancel", null, ct);

    private async Task<AppConversation> DecidirTransferenciaAsync(AppConversation c, string acao, string? motivo, CancellationToken ct)
    {
        if (c.PendingTransfer is not { } pedido) throw new InvalidOperationException("Esta conversa não tem pedido de transferência aguardando o aceite.");
        var decidido = await crm.DecidirTransferenciaAsync(UsuarioId, pedido.Id, acao, new AppTransferDecision(motivo), ct);
        return await crm.AbrirConversaAsync(UsuarioId, string.IsNullOrWhiteSpace(decidido.ConversationId) ? c.Id : decidido.ConversationId, ct);
    }

    // ---------------------------------------------------------------- "Para resolver" (R6, 25/10)

    /// <summary>
    /// O que resolver com o paciente da ficha conferida: os MESMOS alertas da Ficha do balcão (<see cref="ElegibilidadeService"/>,
    /// pela mesma tradução <see cref="FichaAdministrativaNaTela.AlertaDe"/>). O serviço de elegibilidade não filtra por
    /// permissão; a Ficha do balcão os mostra a quem a abre (<see cref="Permissao.VerFichaPaciente"/>), e aqui vale o
    /// mesmo bit — sem ele, nada é lido. A tela chama UMA vez por troca de paciente, depois de a conversa estar desenhada.
    /// </summary>
    public async Task<ParaResolverDoPaciente> ParaResolverAsync(int pacienteId, CancellationToken ct = default)
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.VerFichaPaciente)) return new(pacienteId, []);
        var r = await elegibilidade.ConferirAsync(pacienteId, DateOnly.FromDateTime(Agora.DateTime), ct);
        return new(pacienteId, r.Alertas.Select(FichaAdministrativaNaTela.AlertaDe).ToList());
    }

    public Task<AppConversation> EncerrarAsync(AppConversation c, string resultado, string? resumo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(resultado)) throw new InvalidOperationException("Escolha como terminou o atendimento.");
        return crm.EncerrarAsync(UsuarioId, c.Id, new AppClose(c.Version, resultado, resumo?.Trim() ?? ""), ct);
    }

    public Task<AppConversation> ReabrirAsync(AppConversation c, string? meuAgente, CancellationToken ct = default)
        => crm.ReabrirAsync(UsuarioId, c.Id, new AppChange(c.Version, meuAgente, c.Queue), ct);

    /// <summary>Liga a ficha que a RECEPÇÃO conferiu (regra 5). A conversa que já tem ficha não troca por aqui.</summary>
    public Task<AppConversation> LigarFichaAsync(AppConversation c, int pacienteId, string nome, CancellationToken ct = default)
    {
        if (c.PatientId is { } atual && atual != pacienteId)
            throw new InvalidOperationException("Esta conversa já está ligada a outra ficha. A troca é feita no CRM, com o registro de quem trocou.");
        return crm.LigarFichaAsync(UsuarioId, c.Id, new AppPatientLink(pacienteId, nome, c.Version), ct);
    }

    /// <summary>
    /// Começa uma conversa (a clínica escreve primeiro: a janela nasce fechada, só modelo). Com
    /// a ficha que a recepção escolheu no formulário, liga na mesma hora.
    /// </summary>
    public async Task<AppConversation> NovaConversaAsync(string remetente, string telefone, string? nome, int? pacienteId,
        string? nomeDaFicha, CancellationToken ct = default)
    {
        var contato = ContatoParaOCrm(telefone) ?? throw new InvalidOperationException("Informe um celular com DDD (10 ou 11 dígitos).");
        if (string.IsNullOrWhiteSpace(remetente)) throw new InvalidOperationException("O CRM não tem caixa de saída do WhatsApp ligada.");
        var c = await crm.NovaConversaAsync(UsuarioId, new AppNewConversation(remetente, contato, (nomeDaFicha ?? nome)?.Trim() ?? ""), ct);
        if (pacienteId is { } id && c.PatientId is null)
            c = await crm.LigarFichaAsync(UsuarioId, c.Id, new AppPatientLink(id, nomeDaFicha ?? nome ?? "", c.Version), ct);
        return c;
    }

    /// <summary>As fichas que têm um telefone (o formulário de nova conversa as mostra antes de começar).</summary>
    public async Task<IReadOnlyList<FichaComTelefone>> FichasDoTelefoneAsync(string telefone, CancellationToken ct = default)
        => (await repo.FichasComTelefoneAsync(Variantes(telefone), FichasNaConferencia, ct))
            .Where(f => ConversasNaTela.MesmoTelefone(telefone, f.Telefone)).ToList();

    /// <summary>
    /// Envia um modelo aprovado. Numa conversa de RETENÇÃO (<paramref name="caso"/> aberto, de
    /// quem trata, com autorização), o MESMO clique registra o contato no caso — o convite
    /// conta como tentativa e o caso passa a "aguardando". Sem conversa ainda, ela nasce aqui,
    /// já com a ficha do caso. O consentimento é conferido NO SERVIDOR antes de qualquer envio
    /// do convite. Se o convite sair e o registro falhar, a <see cref="ResultadoDoEnvio.Pendencia"/> diz.
    /// </summary>
    /// <param name="lembreteDoHorario">O horário que este modelo de lembrete avisa (<see cref="HorarioDoLembrete"/>): depois
    /// do envio, ele conta como avisado na rodada (D11) — se a gravação falhar, a mensagem já saiu e a pendência diz.</param>
    public async Task<ResultadoDoEnvio> EnviarModeloAsync(AppConversation? c, string remetente, AppTemplate modelo,
        IReadOnlyList<string> valores, LinhaAcompanhamento? caso, string idDoEnvio, int? lembreteDoHorario = null, CancellationToken ct = default)
    {
        var variaveis = ModeloDoWhatsApp.Sugestoes(modelo, new ContextoDoModelo(null, DateOnly.FromDateTime(Agora.DateTime)));
        if (ModeloDoWhatsApp.Impedimento(modelo, variaveis, valores) is { } impedimento) throw new InvalidOperationException(impedimento);
        var registrar = caso is { Pendente: true } && RetencaoNaTela.EmTratamento(caso);
        if (registrar && !RetencaoNaTela.PodeConvidar(caso!))
            throw new InvalidOperationException("Sem autorização de mensagens (ou sem celular na ficha), nenhum convite sai daqui: ligue e registre como foi.");
        if (registrar) await acompanhamento.ValidarContatoAsync(UsuarioId, caso!.Id, ct);

        var u = UsuarioId;
        if (c is null)
        {
            if (caso is null) throw new InvalidOperationException("Abra uma conversa antes de enviar o modelo.");
            c = await NovaConversaAsync(remetente, caso.Telefone ?? "", caso.Paciente, caso.PacienteId, caso.Paciente, ct);
        }
        else if (registrar && c.PatientId is null)
            c = await crm.LigarFichaAsync(u, c.Id, new AppPatientLink(caso!.PacienteId, caso.Paciente, c.Version), ct);

        var id = IdDoEnvio(idDoEnvio);
        var selecao = ModeloDoWhatsApp.Selecao(modelo, valores);
        var conversa = c;
        // Convite da retenção: o contato é gravado aqui, então o CRM não manda o aviso dele à ponte
        // (o mesmo envio contaria duas tentativas no caso — decisão da direção, 24/10).
        c = await ComVersaoNovaAsync(conversa, v => crm.ResponderAsync(u, conversa.Id,
            new AppReply(v, id, "", Template: selecao, ContactRecordedByApp: registrar), ct), ct);
        if (lembreteDoHorario is { } horario) return new ResultadoDoEnvio(c, await RegistrarLembreteAsync(horario, ct));
        if (!registrar) return new ResultadoDoEnvio(c, null);

        var pendencia = await RegistrarDesfechoAsync(caso!, DesfechoContato.NaoRespondeu, CanalContato.WhatsApp, null,
            "Convite enviado pelo WhatsApp da clínica (modelo aprovado); aguardando a resposta.", Guid.NewGuid(), ct: ct);
        return new ResultadoDoEnvio(c, pendencia is null ? null
            : $"O convite saiu, mas o contato não foi gravado no Acompanhamento: {pendencia} Registre pelo \"Como foi?\".");
    }

    /// <summary>O lembrete saiu: o horário conta como avisado, com quem mandou (D11). Devolve a frase do que faltou, ou nulo.</summary>
    private async Task<string?> RegistrarLembreteAsync(int agendamentoId, CancellationToken ct)
    {
        try
        {
            await campanha.RegistrarLembreteDoHorarioAsync(agendamentoId, Operador, ct);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is not InvalidOperationException) Diagnostico.Registrar("WhatsApp — o lembrete saiu e o aviso não foi gravado na rodada", ex);
            return "O lembrete saiu, mas o horário não ficou marcado como avisado em Confirmações"
                + (ex is InvalidOperationException ? $": {ex.Message}" : ". Registre por lá.");
        }
    }

    private static string IdDoEnvio(string id)
        => Guid.TryParse(id, out var g) ? g.ToString("D") : throw new InvalidOperationException("Identificador de envio inválido.");

    // ======================================================================== escritas no app

    /// <summary>
    /// O paciente confirmou — na conversa, ao telefone ou no balcão: a MESMA resposta da tela Confirmações, gravada no
    /// contato de confirmação do horário, com ou sem a rodada gerada (R1, 25/10).
    /// </summary>
    public Task ConfirmouPorTelefoneAsync(int agendamentoId, CancellationToken ct = default)
        => campanha.RegistrarRespostaDoHorarioAsync(agendamentoId, "confirmou", ct);

    /// <summary>O paciente disse que não vem: a resposta "pediu para remarcar" — o aviso "Não vem — remarcar" do sino e do Gerente. Com ou sem rodada (R1).</summary>
    public Task NaoVemAsync(int agendamentoId, CancellationToken ct = default)
        => campanha.RegistrarRespostaDoHorarioAsync(agendamentoId, RodadaDeConfirmacao.RespostaRemarcar, ct);

    /// <summary>A recepção falou com quem pediu para não vir (ou cancelar) e o paciente MANTEVE o horário.</summary>
    public Task ManterHorarioAsync(int agendamentoId, CancellationToken ct = default)
        => campanha.ManterHorarioAsync(agendamentoId, Operador, ct);

    /// <summary>
    /// Cancela o horário pelo caminho da agenda (as guias do dia ficam suspensas), com o motivo
    /// na trilha; e, havendo conversa, deixa a nota interna para a equipe — nenhuma mensagem vai
    /// ao paciente (regra 3 do CRM: quem avisa é a recepção, revisando). Devolve os avisos de
    /// guia da agenda e, se a nota não pôde ser guardada, a frase.
    /// </summary>
    public async Task<(IReadOnlyList<string> Avisos, AppConversation? Conversa, string? Pendencia)> CancelarHorarioAsync(
        int agendamentoId, string motivo, AppConversation? c, string quando, CancellationToken ct = default)
    {
        var avisosDaGuia = await agenda.CancelarComMotivoAsync(agendamentoId, motivo, Operador, ct);
        if (c is null) return (avisosDaGuia, null, null);
        try
        {
            var nota = $"Horário de {quando} cancelado na agenda por {SessaoUsuario.Atual.Nome} · motivo: {motivo.Trim()}. Nenhuma mensagem foi ao paciente.";
            var id = Guid.NewGuid().ToString("D");
            return (avisosDaGuia, await ComVersaoNovaAsync(c, v => crm.AnotarAsync(UsuarioId, c.Id, new AppNoteRequest(v, id, nota), ct), ct), null);
        }
        catch (Exception ex) when (ex is RecusaDoCrm or CrmIndisponivel)
        {
            Diagnostico.Registrar("WhatsApp da Recepção — a nota do cancelamento não foi guardada no CRM", ex);
            return (avisosDaGuia, null, "O horário foi cancelado, mas a nota não entrou na conversa (o CRM não respondeu).");
        }
    }

    /// <summary>Os motivos de encerramento do Acompanhamento ("Não quer voltar" pede um).</summary>
    public async Task<IReadOnlyList<OpcaoAcompanhamento>> MotivosAsync(CancellationToken ct = default)
        => (await acompanhamento.ConfiguracaoAsync(UsuarioId, ct)).Motivos;

    /// <summary>
    /// O "Como foi?" do caso: o MESMO pedido do Acompanhamento (<see cref="ConversaDoAcompanhamento.Pedido"/>,
    /// versão da linha e idempotência do clique). A legenda que o caso já tem continua. Devolve a
    /// frase do impedimento (inline na tela) ou nulo quando gravou.
    /// </summary>
    public async Task<string?> RegistrarDesfechoAsync(LinhaAcompanhamento caso, DesfechoContato desfecho, CanalContato? canal,
        DateOnly? data, string? observacao, Guid idempotencia, int? motivoId = null, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(Agora.DateTime);
        try
        {
            motivoId ??= caso.Motivo is null ? null : (await MotivosAsync(ct)).FirstOrDefault(m => m.Nome == caso.Motivo)?.Id;
            var (pedido, impedimento) = ConversaDoAcompanhamento.Pedido(caso, desfecho, hoje, data, canal, observacao, motivoId, UsuarioId, idempotencia);
            if (pedido is null) return impedimento;
            await acompanhamento.AtualizarAsync(UsuarioId, caso.Id, pedido, ct);
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// O PARAR pedido na conversa: revoga a autorização de mensagens da FICHA conferida (a
    /// linha vigente ganha o <c>RevogadoEm</c>, com quem registrou — a mesma revogação do
    /// termo). Sem autorização vigente, não há o que revogar: falso.
    /// </summary>
    public async Task<bool> RegistrarPararAsync(int pacienteId, CancellationToken ct = default)
    {
        var situacao = await consentimentos.SituacaoAsync(pacienteId, ct);
        if (!situacao.TryGetValue(FinalidadeConsentimento.ComunicacaoEMarketing, out var atual) || !atual.Vigente) return false;
        await consentimentos.RevogarAsync(atual.Id, Operador,
            $"PARAR pedido na conversa do WhatsApp da clínica, registrado por {SessaoUsuario.Atual.Nome}", ct);
        return true;
    }
}
