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
    string Previa, TickDaMensagem? Tick, int NaoLidas, SeloDoWhatsApp Selo, string Responsavel, bool Minha, bool TemResponsavel);

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
}

/// <summary>Uma ficha que tem o telefone da conversa, com o que a recepção pergunta para conferir.</summary>
public sealed record FichaCandidata(int Id, string Nome, string Iniciais, string TomDoAvatar, string Nascimento,
    string FimDoDocumento, string Proxima, string Ultima);

/// <summary>O paciente da ficha conferida, lido do app (nunca do CRM).</summary>
public sealed record CartaoDoPaciente(int Id, string Nome, string Iniciais, string Idade, string Telefone, bool TelefoneConfere,
    string Convenio, string? Carteirinha, bool CarteirinhaVencida, string FimDoDocumento, bool MensagensAutorizadas, string Mensagens);

/// <summary>
/// Um horário do paciente, com o que a confirmação diz dele e o que dá para fazer ali. As
/// ações seguem o contato de confirmação: sem contato na rodada não há "confirmar" nem "não
/// vem" — registrar uma resposta para um lembrete que não saiu seria inventar a conversa.
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
public sealed record DadosDaConversa(AppConversation? Conversa, string Contato, string? NomeDoCaso,
    IReadOnlyDictionary<string, string> Agentes, string? MeuAgente, int RecebidasJaLidas,
    IReadOnlyList<AvisoDeConfirmacao> Avisos, IReadOnlyList<FichaCandidata> Candidatas, CartaoDoPaciente? Paciente,
    IReadOnlyList<HorarioDoPaciente> Horarios, IReadOnlyList<PassoDaConfirmacao> Confirmacao,
    LinhaAcompanhamento? Caso, IReadOnlyList<ContatoAcompanhamento> Contatos, IReadOnlyDictionary<string, string> Resultados);

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
public sealed class WhatsAppDaRecepcaoService(
    IClienteDoCrm crm, IClinicaRepositorio repo, IAcompanhamentoPacienteService acompanhamento,
    AvisosDeConfirmacaoService avisos, CampanhaService campanha, AgendaService agenda,
    ConsentimentoService consentimentos, ParametrosService parametros)
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
        var conversas = await crm.ListarConversasAsync(u, ct);
        var agora = Agora;
        var lista = await avisos.ListarAsync(agora.DateTime, agora.Date, ct);
        var donos = await repo.PacientesDosAgendamentosAsync(lista.Select(a => a.AgendamentoId).Distinct().ToList(), ct);
        var fichas = await FichasDasConversasAsync(conversas, ct);
        var (retencao, permitida) = await RetencaoAsync(ct);
        var dias = await parametros.ObterDiasInatividadeRecallAsync(ct);
        return new PainelDoWhatsApp(sessao, area, conversas, agora, lista, donos, fichas, retencao, permitida, [dias]);
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

    private async Task<(IReadOnlyList<LinhaAcompanhamento> Linhas, bool Permitida)> RetencaoAsync(CancellationToken ct)
    {
        try { return (await acompanhamento.ListarAsync(UsuarioId, ct), true); }
        catch (UnauthorizedAccessException) { return ([], false); }
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
        var d = Telefone.Normalizar(telefone);
        if (d.Length is < 10 or > 13) return [];
        var sem = d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal) ? d[2..] : d;
        return sem.Length is 10 or 11 ? [sem, "55" + sem] : [d];
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
                var autorizou = await consentimentos.VigenteAsync(id, FinalidadeConsentimento.ComunicacaoEMarketing, ct);
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
        var nome = c is null ? (d.Caso?.Paciente ?? ConversasNaTela.TelefoneNaTela(d.Contato))
            : d.Paciente?.Nome ?? ConversasNaTela.NomeDaConversa(c);
        var sms = ConversasNaTela.EhSms(c?.Channel);
        var janela = c is null ? ConversasNaTela.Janela(null, agora, "whatsapp") : ConversasNaTela.Janela(c, agora);
        var ficha = c is null
            ? ConversasNaTela.EstadoDaFicha(d.Caso?.PacienteId, d.Caso?.Paciente, 1)
            : ConversasNaTela.EstadoDaFicha(c, d.Candidatas.Count);
        var acao = ConversasNaTela.PedeAcao(c, d.Avisos, d.Candidatas.Count, d.Caso, agora);
        var abertura = d.Caso is { } caso && (c is null || c.Messages.Length == 0) ? RetencaoNaTela.Abertura(caso, c, agora) : null;
        var minha = c?.AgentId is { } a && a == d.MeuAgente;
        var encerramento = c is { Closed: true } ? Encerramento(c, d.Agentes, d.Resultados, agora) : null;
        // O avatar: o tom é da conversa (ou do caso, antes de a conversa existir); sem nome nenhum, cinza e "?".
        var semNome = c is not null && d.Paciente is null && c.PatientId is null && !TemLetra(c.Name);
        var chave = c is null ? "caso-" + d.Caso?.Id : semNome ? null : c.Id;
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        return new ConversaDoWhatsApp(d, c?.Id ?? "", nome, semNome ? "?" : Iniciais(nome), TomDoAvatar(chave),
            ConversasNaTela.TelefoneNaTela(d.Contato), sms, c?.Queue ?? "Recepção",
            Responsavel(c?.AgentId, d.MeuAgente, d.Agentes), minha, c?.Closed ?? false, encerramento,
            c is null ? [new ItemDaConversa(TipoNaLinhaDoTempo.Dia, ConversasNaTela.Dia(hoje, hoje))]
                : ConversasNaTela.LinhaDoTempo(c, d.RecebidasJaLidas, agora, d.Agentes),
            janela, ficha, acao, abertura, ConversasNaTela.CampoFechado(janela, nome, agora));
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

    /// <summary>As linhas da lista de conversas, no filtro e na busca, com o selo de cada uma.</summary>
    public static IReadOnlyList<LinhaDaConversa> Linhas(PainelDoWhatsApp painel, FiltroDasConversas filtro, string? busca,
        bool soNaoLidas, DateTimeOffset agora)
        => ConversasNaTela.Filtrar(painel.Conversas, filtro, painel.Sessao.AgentId, busca, soNaoLidas)
            .Select(c => Linha(painel, c, agora)).ToList();

    public static LinhaDaConversa Linha(PainelDoWhatsApp painel, AppConversationSummary c, DateTimeOffset agora)
    {
        var avisosDele = ConversasNaTela.AvisosDaConversa(c.PatientId, painel.Avisos, painel.PacienteDoAgendamento);
        var fichas = painel.FichasPorConversa.TryGetValue(c.Id, out var f) ? f.Count : 0;
        var caso = ConversasNaTela.CasoDoPaciente(c.PatientId, painel.Retencao);
        var selo = ConversasNaTela.Selo(c, avisosDele, fichas, caso, agora, painel.Resultados);
        var nome = ConversasNaTela.NomeDaConversa(c);
        var semNome = c.PatientId is null && !TemLetra(c.Name) && string.IsNullOrWhiteSpace(c.PatientName);
        return new LinhaDaConversa(c.Id, nome, semNome ? "?" : Iniciais(nome), TomDoAvatar(semNome ? null : c.Id),
            ConversasNaTela.EhSms(c.Channel), HoraDaLinha(c.UpdatedAt, agora),
            string.IsNullOrWhiteSpace(c.Preview) ? "Sem mensagens ainda" : c.Preview.Trim(), ConversasNaTela.TickDaPrevia(c),
            Math.Max(0, c.Unread), selo, Responsavel(c.AgentId, painel.Sessao.AgentId, painel.Agentes),
            c.AgentId is not null && c.AgentId == painel.Sessao.AgentId, c.AgentId is not null);
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
            mensagensAutorizadas ? "Autorizou mensagens" : "Não autorizou mensagens · a ação é ligar");
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
                    SituacaoConfirmacao.AvisadoSemResposta => (RodadaDeConfirmacao.SeloSemResposta, "aviso"),
                    SituacaoConfirmacao.Dispensado => ("Fora da rodada", "neutro"),
                    _ => ("Marcado", "neutro")
                };
                var pedido = situacao is SituacaoConfirmacao.PediuRemarcar or SituacaoConfirmacao.PediuCancelar;
                var modalidade = CatalogoModalidades.Nome(a.ModalidadeCodigo, a.ModalidadePrevista);
                var detalhe = string.Join(" · ", new[] { modalidade, a.Profissional?.Nome }.Where(x => !string.IsNullOrWhiteSpace(x)));
                return new HorarioDoPaciente(a.Id, a.DataHora, Maiuscula(AvisosDeConfirmacao.QuandoDaSessao(a.DataHora, agora.DateTime)), detalhe,
                    texto, tom,
                    PodeConfirmar: contato is not null && situacao is not (SituacaoConfirmacao.Confirmou or SituacaoConfirmacao.Dispensado) && !pedido,
                    PodeNaoVem: contato is not null && situacao is not (SituacaoConfirmacao.Dispensado) && !pedido,
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
            passos.Add(new(enviado.ToString("HH:mm", PtBr), RodadaDeConfirmacao.Canal(contato.Canal)
                + (string.IsNullOrWhiteSpace(contato.EnviadoPor) ? "" : $" · {contato.EnviadoPor}")));
        var situacao = RodadaDeConfirmacao.Classificar(contato.Status, contato.EnviadoEm, contato.Comentario);
        if (contato.RespondidoEm is { } respondido && situacao != SituacaoConfirmacao.Dispensado)
            passos.Add(new(respondido.ToString("HH:mm", PtBr), "Respondeu: " + RodadaDeConfirmacao.SeloDaLista(
                new RespostaDeConfirmacao(contato.Status, contato.EnviadoEm, contato.Comentario, contato.Canal))));
        else if (situacao == SituacaoConfirmacao.Dispensado)
            passos.Add(new(contato.RespondidoEm?.ToString("HH:mm", PtBr) ?? "", "Saiu da rodada sem aviso" + (string.IsNullOrWhiteSpace(contato.Comentario) ? "" : $" · {contato.Comentario}")));
        return passos;
    }

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
    /// que a clínica FEZ, e a recepção revisa antes de enviar (regra 3 do CRM).
    /// </summary>
    public static string RascunhoDeRemarcacao(string? nome, HorarioDoPaciente novo, DateOnly hoje)
    {
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        var com = string.IsNullOrWhiteSpace(novo.Profissional) ? "" : $" com {novo.Profissional}";
        return $"Combinado{(quem.Length == 0 ? "" : ", " + quem)}! Remarquei a sua {ModeloDoWhatsApp.NaFrase(novo.Modalidade)} para "
            + $"{MensagensDeContato.QuandoPorExtenso(novo.Quando, hoje)}{com}. O lembrete chega na manhã do dia.";
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

    /// <summary>Os modelos aprovados do remetente da conversa.</summary>
    public Task<AppTemplate[]> ModelosAsync(string remetente, CancellationToken ct = default) => crm.ModelosAsync(UsuarioId, remetente, ct);

    /// <summary>
    /// Responde ao paciente (texto e/ou anexo). <paramref name="idDoEnvio"/> é o Guid "D" do
    /// rascunho: o MESMO no segundo clique e na repetição depois de uma resposta perdida — é
    /// o que torna o envio idempotente no CRM.
    /// </summary>
    public Task<AppConversation> ResponderAsync(AppConversation c, string texto, string? anexoId, string idDoEnvio, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto) && anexoId is null) throw new InvalidOperationException("Escreva a mensagem ou anexe um arquivo.");
        var id = IdDoEnvio(idDoEnvio);
        return ComVersaoNovaAsync(c, v => crm.ResponderAsync(UsuarioId, c.Id, new AppReply(v, id, texto?.Trim() ?? "", anexoId), ct), ct);
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

    /// <summary>Sobe UM arquivo; o id devolvido vai no envio.</summary>
    public Task<AppAttachment> AnexarAsync(AppConversation c, Stream arquivo, string nome, string mime, CancellationToken ct = default)
        => crm.AnexarAsync(UsuarioId, c.Id, arquivo, nome, mime, ct);

    public Task<AppConversation> AssumirAsync(AppConversation c, string? meuAgente, CancellationToken ct = default)
        => crm.AssumirAsync(UsuarioId, c.Id, new AppChange(c.Version, meuAgente, c.Queue), ct);

    /// <summary>O mínimo e o máximo do motivo de devolver à fila (a regra do CRM: 400 fora disso).</summary>
    public const int MinimoDoMotivoDeDevolver = 5, MaximoDoMotivoDeDevolver = 2000;

    /// <summary>Devolve à fila da Recepção, com o motivo que o CRM exige (fica na trilha da conversa).</summary>
    public Task<AppConversation> DevolverAsync(AppConversation c, string motivo, CancellationToken ct = default)
    {
        var m = motivo?.Trim() ?? "";
        if (m.Length < MinimoDoMotivoDeDevolver || m.Length > MaximoDoMotivoDeDevolver)
            throw new InvalidOperationException($"Diga por que a conversa volta à fila (de {MinimoDoMotivoDeDevolver} a {MaximoDoMotivoDeDevolver} letras).");
        return crm.DevolverAFilaAsync(UsuarioId, c.Id, new AppChange(c.Version, null, c.Queue, m), ct);
    }

    public Task<AppConversation> TransferirAsync(AppConversation c, string? agenteId, string? fila, string? nota, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agenteId) && string.IsNullOrWhiteSpace(fila)) throw new InvalidOperationException("Escolha para quem transferir.");
        return crm.TransferirAsync(UsuarioId, c.Id, new AppChange(c.Version, string.IsNullOrWhiteSpace(agenteId) ? null : agenteId,
            string.IsNullOrWhiteSpace(fila) ? c.Queue : fila, string.IsNullOrWhiteSpace(nota) ? null : nota.Trim()), ct);
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
    public async Task<ResultadoDoEnvio> EnviarModeloAsync(AppConversation? c, string remetente, AppTemplate modelo,
        IReadOnlyList<string> valores, LinhaAcompanhamento? caso, string idDoEnvio, CancellationToken ct = default)
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
        c = await ComVersaoNovaAsync(conversa, v => crm.ResponderAsync(u, conversa.Id, new AppReply(v, id, "", Template: selecao), ct), ct);
        if (!registrar) return new ResultadoDoEnvio(c, null);

        var pendencia = await RegistrarDesfechoAsync(caso!, DesfechoContato.NaoRespondeu, CanalContato.WhatsApp, null,
            $"Convite enviado pelo WhatsApp da clínica (modelo {modelo.Name}); aguardando a resposta.", Guid.NewGuid(), ct: ct);
        return new ResultadoDoEnvio(c, pendencia is null ? null
            : $"O convite saiu, mas o contato não foi gravado no Acompanhamento: {pendencia} Registre pelo \"Como foi?\".");
    }

    private static string IdDoEnvio(string id)
        => Guid.TryParse(id, out var g) ? g.ToString("D") : throw new InvalidOperationException("Identificador de envio inválido.");

    // ======================================================================== escritas no app

    /// <summary>O paciente ligou (ou respondeu no balcão) e confirmou: a MESMA resposta da tela Confirmações.</summary>
    public async Task ConfirmouPorTelefoneAsync(int agendamentoId, CancellationToken ct = default)
    {
        var contato = await ContatoDoHorarioAsync(agendamentoId, ct);
        await campanha.RegistrarRespostaAsync(contato.Id, "confirmou", ct);
    }

    /// <summary>O paciente disse que não vem: a resposta "pediu para remarcar" — o aviso "Não vem — remarcar" do sino.</summary>
    public async Task NaoVemAsync(int agendamentoId, CancellationToken ct = default)
    {
        var contato = await ContatoDoHorarioAsync(agendamentoId, ct);
        await campanha.RegistrarRespostaAsync(contato.Id, RodadaDeConfirmacao.RespostaRemarcar, ct);
    }

    /// <summary>A recepção falou com quem pediu para não vir (ou cancelar) e o paciente MANTEVE o horário.</summary>
    public Task ManterHorarioAsync(int agendamentoId, CancellationToken ct = default)
        => campanha.ManterHorarioAsync(agendamentoId, Operador, ct);

    private async Task<ContatoCampanha> ContatoDoHorarioAsync(int agendamentoId, CancellationToken ct)
        => await repo.ContatoDeConfirmacaoDoAgendamentoAsync(agendamentoId, ct)
           ?? throw new InvalidOperationException("Este horário ainda não está na rodada de confirmação. Gere a rodada em Confirmações de agenda.");

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
