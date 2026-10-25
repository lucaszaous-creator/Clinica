using System.Globalization;
using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Em que pé está a janela de 24 h da Meta para esta conversa.</summary>
public enum SituacaoDaJanela
{
    /// <summary>O paciente escreveu há menos de 24 h: o texto livre sai.</summary>
    Aberta,

    /// <summary>A última mensagem do paciente tem 24 h ou mais: só modelo aprovado.</summary>
    Fechada,

    /// <summary>O paciente nunca escreveu nesta conversa: a primeira mensagem tem de ser modelo.</summary>
    NuncaEscreveu,

    /// <summary>SMS não tem janela: texto livre a qualquer hora (sem modelo e sem anexo).</summary>
    SemJanela
}

/// <summary>
/// A janela de 24 h, já com a frase da faixa da conversa. <see cref="Ate"/> e <see cref="Desde"/>
/// estão no fuso de quem perguntou (o offset do <c>agora</c>).
/// </summary>
public sealed record JanelaDaConversa(SituacaoDaJanela Estado, DateTimeOffset? Ate, DateTimeOffset? Desde, string Texto, string Dica)
{
    /// <summary>O campo livre aparece: janela aberta ou SMS (que não tem janela).</summary>
    public bool PodeEscreverLivre => Estado is SituacaoDaJanela.Aberta or SituacaoDaJanela.SemJanela;
}

/// <summary>O que o tick de uma mensagem enviada afirma.</summary>
public enum EstadoDaEntrega { Enviando, Enviada, Entregue, Lida, Falhou, NaoConfirmada }

/// <summary>O tick, a frase dele (o <c>title</c> do ícone) e o tom ("neutro", "info" = lida, "aviso" = não confirmado, "perigo" = falhou).</summary>
public sealed record TickDaMensagem(EstadoDaEntrega Estado, string Texto, string Tom);

/// <summary>
/// Um selo de linha: o texto e o tom da classe <c>selo tom-…</c> do design system
/// ("perigo", "aviso", "sucesso", "info" ou "neutro").
/// </summary>
public sealed record SeloDoWhatsApp(string Texto, string Tom);

/// <summary>A ficha da conversa, do ponto de vista da recepção (regra 5: o telefone não é identidade).</summary>
public enum EstadoFicha
{
    /// <summary>A recepção ligou uma ficha a esta conversa (<c>PatientId</c>).</summary>
    Conferida,

    /// <summary>Há ficha(s) com o telefone, mas ninguém conferiu qual é a pessoa.</summary>
    AConferir,

    /// <summary>Nenhuma ficha tem este telefone: novo contato.</summary>
    Nenhuma
}

/// <summary>O selo da ficha no cabeçalho da conversa, com a dica que o explica.</summary>
public sealed record FichaDaConversa(EstadoFicha Estado, int Candidatas, string Texto, string Tom, string Dica)
{
    public bool PedeConferir => Estado != EstadoFicha.Conferida;
}

/// <summary>Os filtros da lista de conversas.</summary>
public enum FiltroDasConversas { Novas, Minhas, Todas, Encerradas }

/// <summary>Os números do seletor e dos filtros da lista de conversas.</summary>
public sealed record ContagemDasConversas(int Novas, int Minhas, int Todas, int Encerradas, int NaoLidas);

/// <summary>O que pede ação dentro da conversa (o aviso acima do campo de escrever).</summary>
public enum TipoDaAcaoPedida
{
    NaoVem,
    PediuCancelar,
    SemResposta,
    ConferirFicha,
    NovoContato,
    RetencaoRespondeu,
    RetencaoAguardando,
    RetencaoSemAutorizacao,
    RetencaoNovoBsv,
    RetencaoConvidar
}

/// <summary>O que o botão faz — a tela liga cada valor a um serviço que já existe.</summary>
public enum BotaoDaAcao
{
    Remarcar,
    ManterHorario,
    AceitarCancelamento,
    ConfirmouPorTelefone,
    NaoVem,
    ConferirFicha,
    BuscarFicha,
    CadastrarPaciente,
    /// <summary>Um dos cinco "Como foi?" (<see cref="BotaoPedido.Desfecho"/>).</summary>
    Desfecho,
    JaLiguei,
    ColherAutorizacao,
    /// <summary>Abre o modelo do convite (<c>retorno_tratamento</c>), preenchido pelo caso.</summary>
    PrepararConvite,
    /// <summary>Abre o modelo do novo no BSV (<c>primeiro_contato</c>), preenchido pelo caso.</summary>
    PrepararMensagem,
    NotaInterna,
    AtualizarTelefone
}

/// <summary>Um botão do aviso: o que faz, o rótulo, o peso e, quando cabe, o horário ou o desfecho.</summary>
public sealed record BotaoPedido(BotaoDaAcao Acao, string Rotulo, bool Primario = false, bool Perigo = false,
    DesfechoContato? Desfecho = null, int? AgendamentoId = null);

/// <summary>
/// O aviso que pede ação. <see cref="Tom"/> é o do componente <c>Aviso</c> da casa ("atencao",
/// "info", "sucesso" ou "erro").
/// </summary>
public sealed record AcaoPedida(TipoDaAcaoPedida Tipo, string Tom, string Titulo, string Frase, IReadOnlyList<BotaoPedido> Botoes);

/// <summary>O que é cada item da linha do tempo da conversa.</summary>
public enum TipoNaLinhaDoTempo { Dia, Novas, Evento, Recebida, Enviada, Nota }

/// <summary>
/// Um item da linha do tempo: o separador de dia, a divisória "N novas mensagens", um evento
/// da trilha do CRM, um balão (recebido ou enviado) ou uma nota interna. <see cref="Seguida"/>
/// diz que o balão continua o anterior (mesmo lado e mesmo autor): ele perde o espaço e o canto.
/// <see cref="Tom"/> só vale para o evento ("sucesso", "aviso" ou "neutro").
/// </summary>
public sealed record ItemDaConversa(TipoNaLinhaDoTempo Tipo, string Texto, string Hora = "", string? Autor = null,
    bool Seguida = false, TickDaMensagem? Tick = null, string? Anexo = null, string? Modelo = null,
    string Tom = "neutro", string? Erro = null, string? Id = null, DateTimeOffset? At = null, MidiaDaMensagem? Midia = null);

/// <summary>Um evento da trilha do CRM, traduzido para a linha do tempo.</summary>
public sealed record EventoDaTrilha(string Texto, string Tom);

/// <summary>
/// O QUE A TELA "WHATSAPP DA CLÍNICA" AFIRMA (out/2026 — PR 2 do roteiro
/// <c>docs/whatsapp-nativo-implementacao.md</c>; o mockup aprovado é
/// <c>docs/mockups/whatsapp-nativo/app.js</c>). Puro: sem banco, sem HTTP e sem relógio — o
/// <c>agora</c> vem de fora, NO FUSO DA CLÍNICA, e o offset dele é o que converte os carimbos
/// do CRM (UTC) para a hora que a recepção lê (o Brasil não tem horário de verão desde 2019,
/// então um offset só serve ao dia inteiro). O Razor só desenha o que sai daqui.
///
/// As decisões que o roteiro deixou para cá, e por quê:
/// <list type="bullet">
/// <item><b>Aviso de confirmação → conversa</b>: o <see cref="AvisoDeConfirmacao"/> só tem o
/// <c>AgendamentoId</c>. A tela entrega o mapa agendamento → paciente (ela já lê a agenda do
/// período) e o aviso vale para a conversa CUJA FICHA FOI CONFERIDA (<c>PatientId</c>). Sem
/// ficha conferida, nenhum aviso: atribuir a confirmação pelo telefone seria decidir pela
/// recepção quem é a pessoa (regra 5).</item>
/// <item><b>A trilha do CRM</b> (<c>AppAudit.Action</c>) é frase livre escrita lá. Só aparece
/// o que está na tabela de <see cref="Evento"/>; o que é de função que saiu (IA, funil,
/// a nota que já aparece como nota) some, e o desconhecido vira uma frase neutra — nunca o
/// texto cru, com número de horário ou data ISO.</item>
/// </list>
/// </summary>
public static class ConversasNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CompareInfo Comparar = PtBr.CompareInfo;
    private const CompareOptions SemCaixaNemAcento = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>A janela da Meta: 24 h desde a última mensagem do paciente (a mesma conta do CRM, <c>Store.Reserve</c>).</summary>
    public static readonly TimeSpan DuracaoDaJanela = TimeSpan.FromHours(24);

    /// <summary>Quantos dígitos a busca precisa para procurar pelo telefone (o mesmo da fila do Acompanhamento).</summary>
    public const int DigitosParaBuscarTelefone = 4;

    // ---------------------------------------------------------------- janela de 24 h

    /// <summary>
    /// A janela de 24 h. Aberta enquanto <c>agora</c> &lt; última recebida + 24 h — a borda
    /// exata já é fechada, como o CRM recusa (<c>now - LastIncoming &gt;= 24h</c>). Sem
    /// mensagem do paciente (o CRM manda <c>LastIncoming</c> no valor padrão) = nunca escreveu.
    /// Canal <c>sms</c> não tem janela; canal desconhecido segue a regra do WhatsApp, a mais estrita.
    /// </summary>
    public static JanelaDaConversa Janela(DateTimeOffset? ultimaRecebida, DateTimeOffset agora, string? canal)
    {
        if (EhSms(canal))
            return new(SituacaoDaJanela.SemJanela, null, null, "SMS · sem janela de 24 h",
                "SMS não tem janela de 24 h: o texto sai a qualquer hora, sem modelo e sem anexo.");
        if (!PacienteJaEscreveu(ultimaRecebida))
            return new(SituacaoDaJanela.NuncaEscreveu, null, null, "Janela fechada: a pessoa ainda não escreveu",
                "A primeira mensagem para quem ainda não escreveu tem de ser um modelo aprovado pela Meta.");
        var fim = ultimaRecebida!.Value.ToOffset(agora.Offset) + DuracaoDaJanela;
        return agora < fim
            ? new(SituacaoDaJanela.Aberta, fim, null, $"Janela aberta até {Quando(fim, agora)}",
                "A Meta aceita texto livre por 24 h depois da última mensagem do paciente.")
            : new(SituacaoDaJanela.Fechada, null, fim, $"Janela fechada desde {Quando(fim, agora)}",
                "Fora da janela de 24 h, só modelo aprovado.");
    }

    /// <summary>A janela da conversa aberta.</summary>
    public static JanelaDaConversa Janela(AppConversation c, DateTimeOffset agora) => Janela(c.LastIncoming, agora, c.Channel);

    /// <summary>O campo livre aparece (janela aberta, ou SMS).</summary>
    public static bool PodeEscreverLivre(JanelaDaConversa janela) => janela.PodeEscreverLivre;

    /// <summary>O campo livre aparece, a partir do que o CRM devolve.</summary>
    public static bool PodeEscreverLivre(DateTimeOffset? ultimaRecebida, DateTimeOffset agora, string? canal)
        => Janela(ultimaRecebida, agora, canal).PodeEscreverLivre;

    /// <summary>
    /// O bloco que toma o lugar do campo quando ele não pode aparecer (título e texto do
    /// mockup); nulo quando o campo livre aparece.
    /// </summary>
    public static (string Titulo, string Texto)? CampoFechado(JanelaDaConversa janela, string? nomeDoPaciente, DateTimeOffset agora)
    {
        var quem = PrimeiroNomeOu(nomeDoPaciente, "a pessoa");
        return janela.Estado switch
        {
            SituacaoDaJanela.Fechada when janela.Desde is { } desde => ($"Janela de 24 h fechada desde {Quando(desde, agora)}",
                $"Fora dela a Meta só aceita modelo aprovado. Quando {quem} responder, o campo livre volta."),
            SituacaoDaJanela.NuncaEscreveu => ("A primeira mensagem tem de ser um modelo aprovado",
                $"{Maiuscula(quem)} ainda não escreveu, então a Meta só aceita modelo. Depois da resposta, o campo livre abre."),
            _ => null
        };
    }

    public static bool PacienteJaEscreveu(DateTimeOffset? ultimaRecebida) => ultimaRecebida is { Year: > 1 };

    public static bool EhSms(string? canal) => string.Equals(canal?.Trim(), "sms", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------- ticks

    /// <summary>
    /// O tick de uma mensagem ENVIADA, pelo <c>Status</c> que o CRM grava (conferido no servidor
    /// real, out/2026 — o comentário do contrato ainda fala em queued/sent/read, que NÃO
    /// existem): <c>sending</c> → enviando; <c>accepted</c> e <c>simulated</c> (demonstração) →
    /// enviada; <c>delivered</c> → entregue; <c>seen</c> → lida; <c>failed</c> → falhou;
    /// <c>uncertain</c> → "envio não confirmado", em atenção. Status DESCONHECIDO também é
    /// "não confirmado": dizer "enviada" sem o CRM ter dito faria a recepção não conferir, e
    /// dizer "falhou" a faria reenviar em dobro. Recebida (<c>received</c>) ou sem status não
    /// tem tick: nulo.
    /// </summary>
    public static TickDaMensagem? Tick(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        null or "" or "received" => null,
        "sending" => new(EstadoDaEntrega.Enviando, "Enviando", "neutro"),
        "accepted" or "simulated" => new(EstadoDaEntrega.Enviada, "Enviada", "neutro"),
        "delivered" => new(EstadoDaEntrega.Entregue, "Entregue", "neutro"),
        "seen" => new(EstadoDaEntrega.Lida, "Lida", "info"),
        "failed" => new(EstadoDaEntrega.Falhou, "Falhou", "perigo"),
        _ => new(EstadoDaEntrega.NaoConfirmada, "Envio não confirmado: confira antes de reenviar", "aviso")
    };

    /// <summary>O tick da prévia da linha: só quando a última mensagem foi da clínica.</summary>
    public static TickDaMensagem? TickDaPrevia(AppConversationSummary c)
        => string.Equals(c.PreviewDirection, "out", StringComparison.OrdinalIgnoreCase) ? Tick(c.PreviewStatus) : null;

    // ---------------------------------------------------------------- nome, telefone e ficha

    /// <summary>
    /// O número do CRM (ou do cadastro) como a recepção lê: "5522998765432" → "(22) 99876-5432".
    /// O 55 do Brasil sai; o resto vai para o <see cref="Telefone.Formatar"/> da casa.
    /// </summary>
    public static string TelefoneNaTela(string? numero)
    {
        var d = Telefone.Normalizar(numero);
        if (d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal)) d = d[2..];
        return Telefone.Formatar(d);
    }

    /// <summary>
    /// O mesmo número no CRM e no cadastro? A regra da ponte (<c>ClinicAgendaBridge.Phone</c>):
    /// 10 ou 11 dígitos ganham o 55; o que não fica entre 10 e 15 dígitos não serve.
    /// </summary>
    public static bool MesmoTelefone(string? a, string? b)
    {
        var x = NumeroComDdi(a);
        return x.Length > 0 && x == NumeroComDdi(b);
    }

    private static string NumeroComDdi(string? telefone)
    {
        var d = Telefone.Normalizar(telefone);
        if (d.Length is 10 or 11) d = "55" + d;
        return d.Length is >= 10 and <= 15 ? d : "";
    }

    /// <summary>
    /// O nome da conversa: o da ficha conferida; senão o que o CRM tem, quando é nome; senão
    /// o telefone formatado (o CRM nomeia o novo contato pelo próprio número).
    /// </summary>
    public static string NomeDaConversa(string? nome, string? nomeDaFicha, string? contato)
        => !string.IsNullOrWhiteSpace(nomeDaFicha) ? nomeDaFicha.Trim()
            : TemLetra(nome) ? nome!.Trim()
            : TelefoneNaTela(contato);

    public static string NomeDaConversa(AppConversationSummary c) => NomeDaConversa(c.Name, c.PatientName, c.Contact);
    public static string NomeDaConversa(AppConversation c) => NomeDaConversa(c.Name, c.PatientName, c.Contact);

    /// <summary>
    /// A ficha da conversa: conferida (a recepção ligou uma — <c>PatientId</c>), a conferir
    /// (uma ou mais fichas têm o telefone, e quem decide é a recepção, mesmo com uma só) ou
    /// nenhuma (novo contato). <paramref name="fichasComOTelefone"/> vem da busca do app.
    /// </summary>
    public static FichaDaConversa EstadoDaFicha(int? pacienteId, string? nomeDaFicha, int fichasComOTelefone)
    {
        if (pacienteId is not null)
            return new(EstadoFicha.Conferida, Math.Max(1, fichasComOTelefone), "Ficha conferida", "sucesso",
                string.IsNullOrWhiteSpace(nomeDaFicha) ? "A recepção ligou esta conversa a uma ficha."
                    : $"A recepção ligou esta conversa à ficha de {nomeDaFicha.Trim()}.");
        return fichasComOTelefone switch
        {
            <= 0 => new(EstadoFicha.Nenhuma, 0, "Sem ficha — buscar", "neutro",
                "Nenhuma ficha tem este telefone. Busque pelo nome ou cadastre com o telefone preenchido."),
            1 => new(EstadoFicha.AConferir, 1, "Ficha a conferir", "info",
                "Uma ficha tem este telefone, mas o telefone não é identidade: confira com quem está falando antes de ligar."),
            var n => new(EstadoFicha.AConferir, n, $"{n} fichas — conferir", "info",
                $"{n} fichas têm este telefone: confira com quem está falando antes de responder.")
        };
    }

    public static FichaDaConversa EstadoDaFicha(AppConversation c, int fichasComOTelefone) => EstadoDaFicha(c.PatientId, c.PatientName, fichasComOTelefone);
    public static FichaDaConversa EstadoDaFicha(AppConversationSummary c, int fichasComOTelefone) => EstadoDaFicha(c.PatientId, c.PatientName, fichasComOTelefone);

    // ---------------------------------------------------------------- avisos de confirmação

    /// <summary>
    /// Os avisos do sino que valem para a conversa: os do paciente da FICHA CONFERIDA, pelo mapa
    /// agendamento → paciente que a tela monta da agenda. Sem ficha conferida, nenhum. A ordem
    /// é a do <see cref="AvisosDeConfirmacao.Decidir"/> (o que pede ação primeiro).
    /// </summary>
    public static IReadOnlyList<AvisoDeConfirmacao> AvisosDaConversa(int? pacienteId, IEnumerable<AvisoDeConfirmacao> avisos,
        IReadOnlyDictionary<int, int> pacienteDoAgendamento)
        => pacienteId is not { } id ? []
            : avisos.Where(a => pacienteDoAgendamento.TryGetValue(a.AgendamentoId, out var p) && p == id).ToList();

    /// <summary>O aviso que manda: não vem e pediu cancelar, depois sem resposta, depois confirmou; dentro deles, a sessão mais próxima.</summary>
    private static AvisoDeConfirmacao? Principal(IEnumerable<AvisoDeConfirmacao> avisos)
        => avisos.OrderBy(a => a.Tipo switch
            {
                TipoAvisoConfirmacao.NaoVem or TipoAvisoConfirmacao.PediuCancelar => 0,
                TipoAvisoConfirmacao.SemResposta => 1,
                _ => 2
            }).ThenBy(a => a.Sessao).ThenBy(a => a.AgendamentoId).FirstOrDefault();

    /// <summary>"16:00" para a sessão de hoje; "amanhã 08:30", "seg, 26/10 09:00" para as outras.</summary>
    private static string HoraDaSessao(DateTime sessao, DateTimeOffset agora)
        => sessao.Date == agora.DateTime.Date ? sessao.ToString("HH:mm", PtBr) : AvisosDeConfirmacao.QuandoDaSessao(sessao, agora.DateTime);

    // ---------------------------------------------------------------- selo da linha

    /// <summary>
    /// O selo da linha da conversa, na ordem do que pede ação:
    /// <list type="number">
    /// <item>encerrada · o resultado (o rótulo vem de <c>AppWorkspace.Outcomes</c>; chave sem rótulo não aparece);</item>
    /// <item>o aviso do sino do paciente da ficha conferida — Não vem — remarcar, Pediu para cancelar, Sem resposta · HH:mm
    /// (o MESMO <see cref="AvisoDeConfirmacao"/>, nunca um segundo cálculo);</item>
    /// <item>mensagem não entregue (o <c>DeliveryAttention</c> do CRM);</item>
    /// <item>sem ficha conferida: N fichas — conferir, Ficha a conferir ou Novo contato · sem ficha;</item>
    /// <item>no menu de atendimento do CRM;</item>
    /// <item>o caso do Acompanhamento: Respondeu ao convite · decidir, ou Acompanhamento · N dias;</item>
    /// <item>Confirmou · HH:mm (notícia, não pede ação);</item>
    /// <item>Aguardando paciente (a última foi da clínica), Em atendimento, ou Nova · sem responsável.</item>
    /// </list>
    /// </summary>
    public static SeloDoWhatsApp Selo(AppConversationSummary c, IReadOnlyList<AvisoDeConfirmacao> avisosDoPaciente,
        int fichasComOTelefone, LinhaAcompanhamento? caso, DateTimeOffset agora,
        IReadOnlyDictionary<string, string>? resultados = null)
    {
        if (c.Closed)
            return new(c.Resolution is { } r && resultados is not null && resultados.TryGetValue(r.Outcome, out var rotulo)
                       && !string.IsNullOrWhiteSpace(rotulo) ? $"Encerrada · {rotulo}" : "Encerrada", "neutro");

        var aviso = c.PatientId is null ? null : Principal(avisosDoPaciente);
        switch (aviso?.Tipo)
        {
            case TipoAvisoConfirmacao.NaoVem: return new("Não vem — remarcar", "perigo");
            case TipoAvisoConfirmacao.PediuCancelar: return new("Pediu para cancelar", "perigo");
            case TipoAvisoConfirmacao.SemResposta: return new($"Sem resposta · {HoraDaSessao(aviso.Sessao, agora)}", "aviso");
        }
        if (c.DeliveryAttention) return new("Mensagem não entregue · conferir", "perigo");
        if (c.PatientId is null)
            return fichasComOTelefone switch
            {
                <= 0 => new("Novo contato · sem ficha", "neutro"),
                1 => new("Ficha a conferir", "info"),
                var n => new($"{n} fichas — conferir", "info")
            };
        if (c.AwaitingMenu) return new("No menu de atendimento", "neutro");
        if (CasoDaConversa(c.PatientId, caso) is { } l)
            return RetencaoNaTela.Situacao(l, c.LastIncoming, agora) == SituacaoNaRetencao.Respondeu
                ? new("Respondeu ao convite · decidir", "sucesso")
                : new($"Acompanhamento · {ConversaDoAcompanhamento.TempoTexto(l)}", "info");
        if (aviso is { Tipo: TipoAvisoConfirmacao.Confirmou })
            return new($"Confirmou · {HoraDaSessao(aviso.Sessao, agora)}", "sucesso");
        if (c.AgentId is null) return new("Nova · sem responsável", "neutro");
        return string.Equals(c.PreviewDirection, "out", StringComparison.OrdinalIgnoreCase)
            ? new("Aguardando paciente", "neutro")
            : new("Em atendimento", "neutro");
    }

    /// <summary>O caso só vale para a conversa da MESMA ficha, aberto, e de quem trata.</summary>
    private static LinhaAcompanhamento? CasoDaConversa(int? pacienteId, LinhaAcompanhamento? caso)
        => caso is { Pendente: true } l && RetencaoNaTela.EmTratamento(l) && (pacienteId is null || l.PacienteId == pacienteId) ? l : null;

    /// <summary>
    /// O caso do Acompanhamento de um paciente, para a conversa: o aberto de quem trata (o
    /// mais antigo primeiro — é o que está há mais tempo esperando); sem aberto, o mais recente.
    /// </summary>
    public static LinhaAcompanhamento? CasoDoPaciente(int? pacienteId, IEnumerable<LinhaAcompanhamento> linhas)
        => pacienteId is not { } id ? null
            : linhas.Where(l => l.PacienteId == id && RetencaoNaTela.EmTratamento(l))
                .OrderByDescending(l => l.Pendente).ThenByDescending(l => l.Pendente ? l.Dias : -l.Dias).FirstOrDefault();

    // ---------------------------------------------------------------- o que pede ação

    /// <summary>
    /// No máximo UM aviso que pede ação, na ordem do mockup: a confirmação do paciente (não
    /// vem, pediu cancelar, sem resposta), a ficha (conferir, novo contato) e o caso da
    /// retenção (respondeu, aguardando, sem autorização, novo no BSV, convidar). Conversa
    /// encerrada não pede nada. A retenção SEM mensagens não pede aqui: quem responde é o
    /// bloco de <see cref="RetencaoNaTela.Abertura"/>. <paramref name="conversa"/> nulo é o
    /// caso da retenção que ainda não tem conversa no CRM. Sem autorização, nenhum botão envia.
    /// </summary>
    public static AcaoPedida? PedeAcao(AppConversation? conversa, IReadOnlyList<AvisoDeConfirmacao> avisosDoPaciente,
        int fichasComOTelefone, LinhaAcompanhamento? caso, DateTimeOffset agora)
    {
        if (conversa is { Closed: true }) return null;
        var nomeCompleto = conversa is null ? caso?.Paciente : NomeDaConversa(conversa);
        var nome = PrimeiroNomeOu(nomeCompleto, "A pessoa");

        if ((conversa is null || conversa.PatientId is not null) && Principal(avisosDoPaciente) is { } a && a.PedeAcao)
        {
            var quando = AvisosDeConfirmacao.QuandoDaSessao(a.Sessao, agora.DateTime);
            var com = string.IsNullOrWhiteSpace(a.Profissional) ? "" : $" ({a.Profissional})";
            return a.Tipo switch
            {
                TipoAvisoConfirmacao.NaoVem => new(TipoDaAcaoPedida.NaoVem, "atencao", $"{nome} não vem {quando}{com}.",
                    "Remarcar pela agenda já deixa o novo horário marcado e libera este para outro paciente.",
                    [new(BotaoDaAcao.Remarcar, "Remarcar", Primario: true, AgendamentoId: a.AgendamentoId),
                     new(BotaoDaAcao.ManterHorario, "Manter o horário", AgendamentoId: a.AgendamentoId)]),
                TipoAvisoConfirmacao.PediuCancelar => new(TipoDaAcaoPedida.PediuCancelar, "atencao", $"{nome} pediu para cancelar {quando}{com}.",
                    "O horário continua marcado até a recepção aceitar o cancelamento na agenda.",
                    [new(BotaoDaAcao.AceitarCancelamento, "Aceitar o cancelamento", Primario: true, AgendamentoId: a.AgendamentoId),
                     new(BotaoDaAcao.Remarcar, "Remarcar", AgendamentoId: a.AgendamentoId)]),
                _ => new(TipoDaAcaoPedida.SemResposta, "atencao", $"{nome} não respondeu ao lembrete de {quando}{com}.",
                    "Ligou? Registre aqui a resposta: a agenda do dia e o Gerente veem na hora.",
                    [new(BotaoDaAcao.ConfirmouPorTelefone, "Confirmou por telefone", Primario: true, AgendamentoId: a.AgendamentoId),
                     new(BotaoDaAcao.NaoVem, "Não vem", Perigo: true, AgendamentoId: a.AgendamentoId)])
            };
        }

        if (conversa is { PatientId: null })
            return fichasComOTelefone switch
            {
                <= 0 => new(TipoDaAcaoPedida.NovoContato, "info", "Novo contato, ainda sem ficha.",
                    "Se a pessoa já é paciente, ligue a ficha; senão, cadastre pelo app com o telefone preenchido.",
                    [new(BotaoDaAcao.BuscarFicha, "Buscar ficha", Primario: true), new(BotaoDaAcao.CadastrarPaciente, "Cadastrar paciente")]),
                1 => new(TipoDaAcaoPedida.ConferirFicha, "info", "Uma ficha tem este telefone.",
                    "Confira com quem está no telefone antes de ligar a ficha: o telefone não é identidade.",
                    [new(BotaoDaAcao.ConferirFicha, "Conferir a ficha", Primario: true)]),
                var n => new(TipoDaAcaoPedida.ConferirFicha, "info", $"{(n == 2 ? "Duas" : n.ToString(PtBr))} fichas têm este telefone.",
                    "Confira com quem está no telefone antes de responder: a ficha ligada aqui nunca troca sozinha.",
                    [new(BotaoDaAcao.ConferirFicha, "Conferir a ficha", Primario: true)])
            };

        if (CasoDaConversa(conversa?.PatientId, caso) is not { } l) return null;
        var situacao = RetencaoNaTela.Situacao(l, conversa?.LastIncoming, agora);
        var semMensagens = conversa is null || conversa.Messages.Length == 0;
        switch (situacao)
        {
            case SituacaoNaRetencao.Respondeu:
                return new(TipoDaAcaoPedida.RetencaoRespondeu, "sucesso", $"{nome} respondeu ao convite. Como foi?",
                    "O desfecho grava a etapa do Acompanhamento e o próximo contato; \"Quer marcar\" abre as vagas da agenda.",
                    RetencaoNaTela.BotoesDoComoFoi());
            case SituacaoNaRetencao.Aguardando:
                return new(TipoDaAcaoPedida.RetencaoAguardando, "info", $"Contato feito · aguardando {nome}.",
                    "Quando a pessoa responder, a janela de 24 h abre e o caso volta para \"Para hoje\". Sem resposta, o próximo contato é "
                    + $"em {l.ProximoContato.ToString("dd/MM", PtBr)}.",
                    [new(BotaoDaAcao.Desfecho, $"Não respondeu · tentar em {AcompanhamentoNaTela.DiasAteNovaTentativa} dias úteis", Desfecho: DesfechoContato.NaoRespondeu),
                     new(BotaoDaAcao.JaLiguei, "Já liguei")]);
        }
        if (situacao != SituacaoNaRetencao.ParaHoje || semMensagens) return null;
        if (!l.Consentimento)
            return new(TipoDaAcaoPedida.RetencaoSemAutorizacao, "atencao", $"{nome} não autorizou mensagens: a ação é ligar.",
                "Sem a autorização colhida no termo, nenhum convite sai daqui (PARAR vale para os dois canais). Ligou? Registre o que a pessoa disse.",
                [new(BotaoDaAcao.JaLiguei, "Já liguei", Primario: true), new(BotaoDaAcao.ColherAutorizacao, "Colher autorização")]);
        if (l.Tipo == TipoAcompanhamento.NovoBsv)
            return new(TipoDaAcaoPedida.RetencaoNovoBsv, "info",
                $"{nome} tem indicação de BSV desde {l.ReferenciaEm.ToString("dd/MM", PtBr)} e ainda não marcou a 1ª sessão.",
                "Convide para marcar a primeira sessão. O modelo primeiro_contato já vem preenchido; você revisa e envia.",
                [new(BotaoDaAcao.PrepararMensagem, "Preparar mensagem", Primario: true), new(BotaoDaAcao.JaLiguei, "Já liguei")]);
        return new(TipoDaAcaoPedida.RetencaoConvidar, "info",
            $"{nome} está há {ConversaDoAcompanhamento.TempoTexto(l)} sem vir ({RetencaoNaTela.TratamentoNaFrase(l.Modalidade)}) e autorizou mensagens.",
            "O convite para voltar é o modelo aprovado retorno_tratamento, já preenchido com nome, tratamento e dias. Você revisa e envia.",
            [new(BotaoDaAcao.PrepararConvite, "Preparar convite", Primario: true), new(BotaoDaAcao.JaLiguei, "Já liguei")]);
    }

    // ---------------------------------------------------------------- a lista

    /// <summary>"Nova": aberta, sem responsável e fora do menu automático do CRM.</summary>
    public static bool EhNova(AppConversationSummary c) => !c.Closed && c.AgentId is null && !c.AwaitingMenu;

    private static bool NoFiltro(AppConversationSummary c, FiltroDasConversas filtro, string? meuAgente) => filtro switch
    {
        FiltroDasConversas.Novas => EhNova(c),
        FiltroDasConversas.Minhas => !c.Closed && meuAgente is not null && c.AgentId == meuAgente,
        FiltroDasConversas.Todas => !c.Closed,
        _ => c.Closed
    };

    /// <summary>
    /// A lista: o filtro, "só não lidas", e a busca pelo nome (da conversa ou da ficha, sem
    /// caixa nem acento) ou pelos dígitos do telefone (a partir de <see cref="DigitosParaBuscarTelefone"/>).
    /// A mais recente primeiro.
    /// </summary>
    public static IReadOnlyList<AppConversationSummary> Filtrar(IEnumerable<AppConversationSummary> conversas,
        FiltroDasConversas filtro, string? meuAgente, string? busca = null, bool soNaoLidas = false)
    {
        var termo = busca?.Trim() ?? "";
        var digitos = Telefone.Normalizar(termo);
        return conversas
            .Where(c => NoFiltro(c, filtro, meuAgente) && (!soNaoLidas || c.Unread > 0)
                && (termo.Length == 0 || Contem(c.Name, termo) || Contem(c.PatientName, termo)
                    || digitos.Length >= DigitosParaBuscarTelefone && Telefone.Normalizar(c.Contact).Contains(digitos, StringComparison.Ordinal)))
            .OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Os números do seletor e dos filtros (não lidas = conversas abertas com recebida não lida).</summary>
    public static ContagemDasConversas Contagens(IEnumerable<AppConversationSummary> conversas, string? meuAgente)
    {
        var lista = conversas as IReadOnlyCollection<AppConversationSummary> ?? conversas.ToList();
        return new(lista.Count(c => NoFiltro(c, FiltroDasConversas.Novas, meuAgente)),
            lista.Count(c => NoFiltro(c, FiltroDasConversas.Minhas, meuAgente)),
            lista.Count(c => !c.Closed), lista.Count(c => c.Closed), lista.Count(c => !c.Closed && c.Unread > 0));
    }

    public static string Rotulo(FiltroDasConversas f) => f switch
    {
        FiltroDasConversas.Novas => "Novas",
        FiltroDasConversas.Minhas => "Minhas",
        FiltroDasConversas.Todas => "Todas",
        _ => "Encerradas"
    };

    /// <summary>
    /// O número ao lado da aba do filtro: só nas que pedem ação (Novas e Minhas), e "99+" acima de 99 — o
    /// total de abertas ("Todas") não pede nada, e com dois dígitos nele a fileira passava da coluna da lista.
    /// </summary>
    public static string? ContagemDoFiltro(FiltroDasConversas f, ContagemDasConversas c)
    {
        int? n = f switch { FiltroDasConversas.Novas => c.Novas, FiltroDasConversas.Minhas => c.Minhas, _ => null };
        return n switch { null => null, > 99 => "99+", var x => x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    }

    /// <summary>O vazio que separa "a busca não achou" de "não há nada" (as frases do mockup).</summary>
    public static (string Titulo, string Texto) Vazio(FiltroDasConversas filtro, bool comBusca)
        => comBusca ? ("Nenhuma conversa com esse nome", "Confira o nome ou o número.")
            : filtro == FiltroDasConversas.Novas ? ("Nenhuma conversa nova", "Toda conversa já tem alguém cuidando dela.")
            : ("Nada por aqui", "As conversas aparecem aqui assim que chegam.");

    /// <summary>A última mensagem recebida de cada ficha (pelas conversas com ficha conferida) — a entrada da retenção.</summary>
    public static IReadOnlyDictionary<int, DateTimeOffset> UltimaRecebidaPorPaciente(IEnumerable<AppConversationSummary> conversas)
        => conversas.Where(c => c.PatientId is not null && PacienteJaEscreveu(c.LastIncoming))
            .GroupBy(c => c.PatientId!.Value).ToDictionary(g => g.Key, g => g.Max(c => c.LastIncoming));

    // ---------------------------------------------------------------- a linha do tempo

    /// <summary>Quantas recebidas a pessoa já viu (o <c>AppReadMarker.IncomingCount</c>), a partir das não lidas da lista.</summary>
    public static int RecebidasJaLidas(AppConversation c, int naoLidas)
        => Math.Max(0, c.Messages.Count(EhRecebida) - Math.Max(0, naoLidas));

    private static bool EhRecebida(AppMessage m) => string.Equals(m.Direction, "in", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A conversa como a recepção lê: mensagens, notas internas e os eventos da trilha
    /// intercalados por <c>At</c> (no empate, a mensagem, depois a nota, depois o evento), com
    /// o separador de dia, a divisória "N novas mensagens" antes da primeira recebida não lida
    /// (depois das <paramref name="recebidasJaLidas"/>) e <c>Seguida</c> nos balões que
    /// continuam o anterior (mesmo lado e mesmo autor, sem nada entre eles).
    /// <paramref name="nomesDosAgentes"/> é o <c>AppSession.Agents</c> (id → nome); id
    /// desconhecido vira "Equipe", nunca o id cru; saída sem autor é "Mensagem automática".
    /// </summary>
    public static IReadOnlyList<ItemDaConversa> LinhaDoTempo(AppConversation conversa, int recebidasJaLidas, DateTimeOffset agora,
        IReadOnlyDictionary<string, string>? nomesDosAgentes = null)
    {
        var fontes = new List<(DateTimeOffset At, int Ordem, int Indice, ItemDaConversa Item)>();
        for (var i = 0; i < conversa.Messages.Length; i++)
        {
            var m = conversa.Messages[i];
            var recebida = EhRecebida(m);
            // Foto, áudio, vídeo e documento aparecem no balão (MidiaDaConversa): o texto é só a legenda, e sem
            // legenda o balão não inventa "Mensagem sem texto" nem repete o nome que o cartão do arquivo já mostra.
            var midia = MidiaDaConversa.Da(conversa.Id, m);
            var texto = !string.IsNullOrWhiteSpace(m.Text) ? m.Text
                : midia is not null ? ""
                : m.FileName ?? (recebida ? "Mensagem sem texto" : "Arquivo");
            var tick = recebida ? null : Tick(m.Status);
            fontes.Add((m.At, 0, i, new ItemDaConversa(recebida ? TipoNaLinhaDoTempo.Recebida : TipoNaLinhaDoTempo.Enviada, texto,
                Hora(m.At, agora), recebida ? null : Agente(m.AgentId, nomesDosAgentes, "Mensagem automática"),
                Tick: tick, Anexo: midia is not null || string.IsNullOrWhiteSpace(m.FileName) ? null : m.FileName, Modelo: m.Template?.Name,
                Erro: tick is { Estado: EstadoDaEntrega.Falhou or EstadoDaEntrega.NaoConfirmada } && !string.IsNullOrWhiteSpace(m.Error) ? m.Error : null,
                Id: m.Id, At: m.At, Midia: midia)));
        }
        for (var i = 0; i < conversa.Notes.Length; i++)
        {
            var n = conversa.Notes[i];
            fontes.Add((n.At, 1, i, new ItemDaConversa(TipoNaLinhaDoTempo.Nota, n.Text, Hora(n.At, agora),
                Agente(n.AgentId, nomesDosAgentes, "Equipe"), Id: n.Id, At: n.At)));
        }
        for (var i = 0; i < conversa.Audit.Length; i++)
        {
            var a = conversa.Audit[i];
            if (Evento(a, agora) is not { } e) continue;
            fontes.Add((a.At, 2, i, new ItemDaConversa(TipoNaLinhaDoTempo.Evento, e.Texto, Hora(a.At, agora), Tom: e.Tom, At: a.At)));
        }

        var naoLidas = conversa.Messages.Count(EhRecebida) - Math.Max(0, recebidasJaLidas);
        var itens = new List<ItemDaConversa>();
        DateOnly? dia = null;
        ItemDaConversa? anterior = null;
        var recebidas = 0;
        foreach (var (at, _, _, item) in fontes.OrderBy(f => f.At).ThenBy(f => f.Ordem).ThenBy(f => f.Indice))
        {
            var d = DateOnly.FromDateTime(at.ToOffset(agora.Offset).DateTime);
            if (d != dia)
            {
                itens.Add(new(TipoNaLinhaDoTempo.Dia, Dia(d, DateOnly.FromDateTime(agora.DateTime))));
                dia = d;
                anterior = null;
            }
            if (item.Tipo == TipoNaLinhaDoTempo.Recebida && ++recebidas == Math.Max(0, recebidasJaLidas) + 1 && naoLidas > 0)
            {
                itens.Add(new(TipoNaLinhaDoTempo.Novas, naoLidas == 1 ? "1 nova mensagem" : $"{naoLidas} novas mensagens"));
                anterior = null;
            }
            var seguida = item.Tipo is TipoNaLinhaDoTempo.Recebida or TipoNaLinhaDoTempo.Enviada or TipoNaLinhaDoTempo.Nota
                && anterior is not null && anterior.Tipo == item.Tipo && anterior.Autor == item.Autor;
            var final = seguida ? item with { Seguida = true } : item;
            itens.Add(final);
            anterior = item.Tipo == TipoNaLinhaDoTempo.Evento ? null : final;
        }
        return itens;
    }

    private static string Agente(string? id, IReadOnlyDictionary<string, string>? nomes, string semAutor)
        => string.IsNullOrWhiteSpace(id) || id == "automation" ? semAutor
            : nomes is not null && nomes.TryGetValue(id, out var nome) && !string.IsNullOrWhiteSpace(nome) ? nome : "Equipe";

    // ---------------------------------------------------------------- a trilha do CRM

    private sealed record RegraDaTrilha(Regex Padrao, Func<Match, string, DateTimeOffset, string?> Texto, string Tom);

    private static RegraDaTrilha R(string padrao, Func<Match, string, DateTimeOffset, string?> texto, string tom = "neutro")
        => new(new Regex(padrao, RegexOptions.CultureInvariant), texto, tom);

    /// <summary>O que é de função que saiu do CRM ou já aparece de outro jeito: some da linha do tempo.</summary>
    private static readonly Regex Omitida = new(
        @"^Adicionou uma nota interna$|^Etapa: |Jade|\bIA\b|assistente virtual|Assistente virtual|aprendizado|simulação|sugestão|[Cc]oleta de|Especialidade do atendimento",
        RegexOptions.CultureInvariant);

    /// <summary>Atores do CRM que não são pessoas, e o nome que a recepção lê.</summary>
    private static readonly Dictionary<string, string> Atores = new(StringComparer.OrdinalIgnoreCase)
    {
        ["system"] = "O CRM", ["Sistema"] = "O CRM", ["Automação"] = "A automação do CRM", ["clinic"] = "O app da clínica",
        ["Recall"] = "O convite de retorno", ["Paciente"] = "O paciente"
    };

    /// <summary>Atores cujas frases o CRM escreve para gente ler: passam como estão (sem número de horário nem data ISO).</summary>
    private static readonly HashSet<string> AtoresDeFrasePronta = new(StringComparer.OrdinalIgnoreCase)
    {
        "Paciente", "Agenda da clínica", "Menu de atendimento", "Distribuição por especialidade", "@SemDor"
    };

    private static readonly RegraDaTrilha[] Regras =
    [
        R(@"^Assumiu (o atendimento humano|o controle humano)", (_, a, _) => $"{a} assumiu a conversa"),
        R(@"^Assumiu ao escrever para o paciente$", (_, a, _) => $"{a} assumiu a conversa ao responder"),
        R(@"^Reabriu ao escrever para o paciente$", (_, a, _) => $"{a} reabriu a conversa ao responder"),
        R(@"^Reabriu e assumiu o atendimento humano$", (_, a, _) => $"{a} reabriu a conversa"),
        R(@"^Reaberta após nova mensagem$", (_, _, _) => "A conversa reabriu sozinha com a nova mensagem do paciente"),
        R(@"^Encerrou o atendimento$", (_, a, _) => $"Encerrada por {a}", "sucesso"),
        R(@"^Concluiu: (?<r>.+?)( · retorno registrado)?$", (m, a, _) => $"Encerrada por {a} · {m.Groups["r"].Value}", "sucesso"),
        R(@"^Transferiu de (?<de>.+) para (?<para>.+)$", (m, a, _) => $"{a} transferiu de {m.Groups["de"].Value} para {m.Groups["para"].Value}"),
        R(@"^Encaminhou para (?<d>(o setor|a fila) .+)$", (m, a, _) => $"{a} encaminhou para {m.Groups["d"].Value}"),
        R(@"^Solicitou transferência para (?<d>.+); aguardando aceite$", (m, a, _) => $"{a} pediu a transferência para {m.Groups["d"].Value}; aguardando aceite"),
        R(@"^(?<v>Aceitou|Recusou|Cancelou) a transferência(?<m>: .+)?$", (m, a, _) => $"{a} {m.Groups["v"].Value.ToLowerInvariant()} a transferência{m.Groups["m"].Value}"),
        R(@"^Solicitação de transferência invalidada: (?<m>.+)$", (m, _, _) => $"A transferência pedida caiu: {m.Groups["m"].Value}", "aviso"),
        R(@"^Reorganizou atendimento de (?<de>.+) para (?<para>.+)$", (m, a, _) => $"{a} reorganizou a conversa de {m.Groups["de"].Value} para {m.Groups["para"].Value}"),
        R(@"^Acompanhamento: (?<e>[^·]+?)( · (?<q>\S+))?$", (m, a, agora) =>
            $"{a} marcou o acompanhamento: {m.Groups["e"].Value.ToLower(PtBr)}"
            + (m.Groups["q"].Success && DateTimeOffset.TryParse(m.Groups["q"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var q)
                ? $" · retomar {Quando(q, agora)}" : "")),
        R(@"^(?<v>Fixou|Desafixou) uma nota interna$", (m, a, _) => $"{a} {m.Groups["v"].Value.ToLowerInvariant()} uma nota interna"),
        R(@"^Nova mensagem do paciente: acompanhamento retomado$", (_, _, _) => "Nova mensagem do paciente: o acompanhamento foi retomado"),
        R(@"^Regra (?<n>.+): encaminhou para (?<d>.+)$", (m, _, _) => $"A regra {m.Groups["n"].Value} encaminhou para {m.Groups["d"].Value}"),
        R(@"^Criou um atendimento por SMS\.?$", (_, a, _) => $"{a} começou a conversa por SMS"),
        R(@"^Criou um novo atendimento\.", (_, a, _) => $"{a} começou a conversa · aguardando o modelo ou a mensagem do paciente"),
        R(@"^Identificou o paciente: ficha (?<n>.+?) \(nº \d+\)\.?$", (m, a, _) => $"{a} ligou a ficha de {m.Groups["n"].Value} a esta conversa", "sucesso"),
        R(@"^Desligou a ficha (?<n>.*?) desta conversa\.?$", (m, a, _) =>
            m.Groups["n"].Value.Trim().Length == 0 ? $"{a} desligou a ficha desta conversa" : $"{a} desligou a ficha de {m.Groups["n"].Value.Trim()} desta conversa", "aviso"),
        R(@"^Cancelou na clínica o horário #\d+ \((?<m>.*)\)\.", (m, a, _) => $"{a} cancelou o horário na agenda da clínica ({m.Groups["m"].Value}). Nenhuma mensagem foi ao paciente", "aviso"),
        R(@"^Confirmou paciente e solicitou remarcação do horário #\d+", (_, a, _) => $"{a} pediu à agenda da clínica a remarcação do horário; aguardando a agenda"),
        R(@"^Clínica confirmou remarcação do horário #\d+", (_, _, _) => "A agenda da clínica confirmou a remarcação. Nenhuma mensagem foi ao paciente", "sucesso"),
        R(@"^Clínica confirmou novo agendamento #\d+", (_, _, _) => "A agenda da clínica confirmou o novo horário. Nenhuma mensagem foi ao paciente", "sucesso"),
        R(@"^A clínica recusou a remarcação", (_, _, _) => "A agenda da clínica recusou a remarcação: consulte as vagas de novo", "aviso"),
        R(@"^Resultado da remarcação incerto", (_, _, _) => "A remarcação não foi confirmada pela agenda: confira o horário antes de repetir", "aviso"),
        R(@"^Pediu que a clínica ligue de volta", (_, _, _) => "O paciente pediu que a clínica ligue de volta", "aviso"),
        R(@"^Pediu (por SMS )?para suspender marketing e convites de retorno", (_, _, _) => "O paciente pediu PARAR: nenhum convite de retorno, nem pelo WhatsApp nem por SMS", "aviso"),
        R(@"^Paciente respondeu ao recall", (_, _, _) => "O paciente respondeu ao convite de retorno", "sucesso"),
        R(@"^WhatsApp indisponível \((?<m>.+)\); convite segue por SMS$", (m, _, _) => $"O convite de retorno seguiu por SMS: WhatsApp indisponível ({m.Groups["m"].Value})", "aviso"),
        R(@"^Pediu atendimento humano", (_, _, _) => "O paciente pediu para falar com a equipe", "aviso"),
        R(@"^Tratado pela equipe: (?<m>.+)$", (m, a, _) => $"{a} tratou o pendente: {m.Groups["m"].Value}"),
        R(@"^Atendimento criado por solicitação do sistema Clínico$", (_, _, _) => "Conversa aberta pelo app do Consultório"),
        R(@"^Envio solicitado por (?<q>.+?) · ", (m, _, _) => $"Envio pedido por {m.Groups["q"].Value} pelos apps da clínica"),
    ];

    private static readonly Regex NumeroDoHorario = new(@"\s?#\d+", RegexOptions.CultureInvariant);
    private static readonly Regex DataIso = new(@"\d{4}-\d{2}-\d{2}T[\d:.]+(Z|[+-]\d{2}:\d{2})?", RegexOptions.CultureInvariant);

    /// <summary>
    /// Um registro da trilha do CRM como evento da linha do tempo, ou nulo quando ele não
    /// deve aparecer (função que saiu, ou repetição do que a linha já mostra). Ação que a
    /// tabela não conhece vira "Movimentação registrada no CRM" — nunca o texto cru.
    /// </summary>
    public static EventoDaTrilha? Evento(AppAudit registro, DateTimeOffset agora)
    {
        var acao = (registro.Action ?? "").Trim();
        var ator = Ator(registro.Actor);
        if (acao.Length == 0 || Omitida.IsMatch(acao) || Omitida.IsMatch(registro.Actor ?? "")) return null;
        foreach (var regra in Regras)
        {
            var m = regra.Padrao.Match(acao);
            if (m.Success) return regra.Texto(m, ator, agora) is { } texto ? new(texto, regra.Tom) : null;
        }
        if (AtoresDeFrasePronta.Contains(registro.Actor?.Trim() ?? ""))
        {
            var limpa = DataIso.Replace(NumeroDoHorario.Replace(acao, ""), d =>
                DateTimeOffset.TryParse(d.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var q) ? Quando(q, agora) : "");
            return new(limpa.TrimEnd('.', ' '), "neutro");
        }
        return new(ator == "O CRM" ? "Movimentação registrada no CRM" : $"{ator}: movimentação registrada no CRM", "neutro");
    }

    private static string Ator(string? ator)
    {
        var a = ator?.Trim() ?? "";
        if (a.Length == 0) return "O CRM";
        return Atores.TryGetValue(a, out var nome) ? nome : a;
    }

    // ---------------------------------------------------------------- datas

    private static string Hora(DateTimeOffset at, DateTimeOffset agora) => at.ToOffset(agora.Offset).ToString("HH:mm", PtBr);

    /// <summary>"hoje às 17:02", "amanhã às 10:09", "ontem às 11:05", "qua 22/10 às 11:05" (com o ano, se não for o deste ano).</summary>
    internal static string Quando(DateTimeOffset momento, DateTimeOffset agora)
    {
        var local = momento.ToOffset(agora.Offset).DateTime;
        var hoje = agora.DateTime.Date;
        var hora = local.ToString("HH:mm", PtBr);
        var dias = (local.Date - hoje).Days;
        return dias switch
        {
            0 => $"hoje às {hora}",
            1 => $"amanhã às {hora}",
            -1 => $"ontem às {hora}",
            _ => $"{AvisosDeConfirmacao.DiaDaSemana(local.DayOfWeek)} {local.ToString(local.Year == hoje.Year ? "dd/MM" : "dd/MM/yyyy", PtBr)} às {hora}"
        };
    }

    /// <summary>O separador de dia: "Hoje · sexta-feira, 24/10", "Ontem · quinta-feira, 23/10", "Quarta-feira, 22/10".</summary>
    public static string Dia(DateOnly dia, DateOnly hoje)
    {
        var data = dia.ToString(dia.Year == hoje.Year ? "dd/MM" : "dd/MM/yyyy", PtBr);
        var semana = DiaPorExtenso(dia.DayOfWeek);
        return (hoje.DayNumber - dia.DayNumber) switch
        {
            0 => $"Hoje · {semana}, {data}",
            1 => $"Ontem · {semana}, {data}",
            _ => $"{Maiuscula(semana)}, {data}"
        };
    }

    // Escrito à mão, como as abreviações de AvisosDeConfirmacao: a frase não pode depender do ICU da máquina.
    private static string DiaPorExtenso(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "segunda-feira", DayOfWeek.Tuesday => "terça-feira", DayOfWeek.Wednesday => "quarta-feira",
        DayOfWeek.Thursday => "quinta-feira", DayOfWeek.Friday => "sexta-feira", DayOfWeek.Saturday => "sábado", _ => "domingo"
    };

    // ---------------------------------------------------------------- texto

    private static bool TemLetra(string? s) => !string.IsNullOrWhiteSpace(s) && s.Any(char.IsLetter);

    private static bool Contem(string? onde, string termo)
        => !string.IsNullOrEmpty(onde) && Comparar.IndexOf(onde, termo, SemCaixaNemAcento) >= 0;

    /// <summary>O primeiro nome para a frase, ou <paramref name="semNome"/> quando o "nome" é só o telefone.</summary>
    internal static string PrimeiroNomeOu(string? nome, string semNome)
        => TemLetra(nome) ? MensagensDeContato.PrimeiroNome(nome!.Trim()) : semNome;

    private static string Maiuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0], PtBr) + s[1..];
}
