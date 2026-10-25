using System.Globalization;
using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quanto o paciente espera a clínica, na linha da lista: a frase inteira ("espera há 2 h", "janela fecha às 14:10" — a dica e o
/// leitor de tela), o tom e o <see cref="Curto"/> que toma o lugar da hora no canto da linha ("há 2 h", "fecha às 14:10"),
/// como as caixas de atendimento fazem: a coluna de 300 px não tem outro lugar sem cortar o selo ou o nome.
/// </summary>
public sealed record EsperaNaLinha(string Texto, string Tom, string Curto);

/// <summary>
/// O ACABAMENTO DA TELA DO WHATSAPP (25/10/2026 — docs/whatsapp-nativo-implementacao.md §15): o que a
/// tela afirma e que nasceu da revisão "está profissional?". Num arquivo à parte do
/// <c>ConversasNaTela.cs</c> para as outras frentes da mesma tela não esbarrarem; as regras são as
/// mesmas da classe (puras, sem relógio: o <c>agora</c> vem de fora, no fuso da clínica).
/// <list type="bullet">
/// <item><b>A linha do tempo é a conversa</b>: o evento é nota de rodapé, e o que ele repete sai
/// (<see cref="Limpar"/>).</item>
/// <item><b>A fila diz quem espera</b> (<see cref="Espera"/>) e, em "Novas", a mais antiga vem
/// primeiro (<see cref="Filtrar"/>, <see cref="OrdenarAsNovas"/>).</item>
/// <item><b>Falha é frase da clínica</b>: sem "CRM", sem o texto cru do provedor; o detalhe vai ao
/// <c>Diagnostico</c>, e a frase diz o que fazer (<see cref="FalhaNaEscrita"/>, <see cref="FalhaDeAbertura"/>).</item>
/// </list>
/// </summary>
public static partial class ConversasNaTela
{
    /// <summary>A partir de quanto falta para a janela fechar a linha avisa "janela fecha às…".</summary>
    public static readonly TimeSpan AvisoDaJanela = TimeSpan.FromHours(4);

    // ---------------------------------------------------------------- a linha do tempo limpa

    private static readonly Regex AoResponder = new(@"^(Assumiu|Reabriu) ao escrever para o paciente$", RegexOptions.CultureInvariant);

    /// <summary>O que a ponte grava enquanto a agenda da clínica ainda não respondeu (semdor-crm, <c>ClinicFlow.cs</c>).</summary>
    private static readonly Regex Provisorio = new(@"pendente\.?$|mantido até a equipe combinar|solicitou remarcação do horário", RegexOptions.CultureInvariant);

    private static readonly Regex DaAgenda = new(@"^(Clínica confirmou|A clínica recusou|Resultado da remarcação incerto)", RegexOptions.CultureInvariant);

    private static NaturezaDoEvento Natureza(AppAudit registro)
    {
        var acao = (registro.Action ?? "").Trim();
        if (AoResponder.IsMatch(acao)) return NaturezaDoEvento.AoResponder;
        if (string.Equals(registro.Actor?.Trim(), "Agenda da clínica", StringComparison.OrdinalIgnoreCase) || DaAgenda.IsMatch(acao))
            return NaturezaDoEvento.DaAgenda;
        return Provisorio.IsMatch(acao) ? NaturezaDoEvento.Provisorio : NaturezaDoEvento.Comum;
    }

    /// <summary>
    /// O que sai da linha do tempo por repetir o que ela já mostra:
    /// <list type="bullet">
    /// <item>o estado PROVISÓRIO ("Confirmação recebida; atualização da agenda pendente") quando a
    /// agenda da clínica já registrou o fim depois dele — cada resposta da agenda resolve um;</item>
    /// <item>o "assumiu (reabriu) a conversa ao responder" quando o balão ao lado, a menos de dois
    /// minutos, é da mesma pessoa — o nome dela já está no balão.</item>
    /// </list>
    /// Os eventos que ficam e caem no mesmo minuto se fundem em <see cref="LinhaDoTempo"/>.
    /// </summary>
    private static List<(DateTimeOffset At, ItemDaConversa Item)> Limpar(List<(DateTimeOffset At, ItemDaConversa Item, EventoDaTrilha? Evento)> ordem)
    {
        var fora = new bool[ordem.Count];
        var resolvido = false;
        for (var i = ordem.Count - 1; i >= 0; i--)
        {
            switch (ordem[i].Evento?.Natureza)
            {
                case NaturezaDoEvento.DaAgenda:
                    resolvido = true;
                    break;
                case NaturezaDoEvento.Provisorio when resolvido:
                    fora[i] = true;
                    resolvido = false;
                    break;
                case NaturezaDoEvento.AoResponder when BalaoDaMesmaPessoa(ordem, i, -1) || BalaoDaMesmaPessoa(ordem, i, 1):
                    fora[i] = true;
                    break;
            }
        }
        return ordem.Where((_, i) => !fora[i]).Select(f => (f.At, f.Item)).ToList();
    }

    /// <summary>O balão mais perto do evento <paramref name="i"/>, para trás ou para a frente, é enviado pela mesma pessoa, no mesmo instante?</summary>
    private static bool BalaoDaMesmaPessoa(List<(DateTimeOffset At, ItemDaConversa Item, EventoDaTrilha? Evento)> ordem, int i, int passo)
    {
        var ator = ordem[i].Evento?.Ator;
        for (var j = i + passo; j >= 0 && j < ordem.Count; j += passo)
        {
            var (at, item, _) = ordem[j];
            if (item.Tipo == TipoNaLinhaDoTempo.Evento) continue;
            return item.Tipo == TipoNaLinhaDoTempo.Enviada && ator is not null && item.Autor == ator
                   && (at - ordem[i].At).Duration() < TimeSpan.FromMinutes(2);
        }
        return false;
    }

    /// <summary>Dois eventos fundidos ficam com o tom que mais pede atenção: aviso, depois sucesso, depois neutro.</summary>
    private static string TomMaisForte(string a, string b)
    {
        static int Peso(string t) => t switch { "aviso" => 2, "sucesso" => 1, _ => 0 };
        return Peso(b) > Peso(a) ? b : a;
    }

    /// <summary>
    /// A frase sob o balão que não saiu, quando o CRM disse por quê: a da clínica, nunca o texto cru do
    /// provedor (ele fica no CRM). O tick, com a dica dele, continua dizendo o estado.
    /// </summary>
    public static string? ErroDaEntrega(TickDaMensagem? tick, string? detalhe)
        => string.IsNullOrWhiteSpace(detalhe) ? null : tick?.Estado switch
        {
            EstadoDaEntrega.Falhou => "Não entregue. Confira o número ou envie de novo.",
            EstadoDaEntrega.NaoConfirmada => "O WhatsApp não confirmou a entrega. Confira com a pessoa antes de reenviar.",
            _ => null
        };

    // ---------------------------------------------------------------- a fila: quem espera

    /// <summary>
    /// Quanto o paciente espera a clínica, para a linha da lista — só quando a ÚLTIMA fala é dele
    /// (<c>PreviewDirection</c> "in"), a conversa está aberta e fora do menu automático. Faltando
    /// menos de <see cref="AvisoDaJanela"/> para a janela de 24 h fechar, a linha diz a hora em que
    /// ela fecha (depois dela, só modelo pago); senão, há quanto tempo a pessoa espera (desde a
    /// última mensagem dela — o contrato não traz a primeira sem resposta). Menos de um minuto: nada.
    /// </summary>
    public static EsperaNaLinha? Espera(AppConversationSummary c, DateTimeOffset agora)
    {
        if (c.Closed || c.AwaitingMenu || !PacienteJaEscreveu(c.LastIncoming)
            || !string.Equals(c.PreviewDirection, "in", StringComparison.OrdinalIgnoreCase))
            return null;
        var desde = c.LastIncoming.ToOffset(agora.Offset);
        if (!EhSms(c.Channel))
        {
            var fim = desde + DuracaoDaJanela;
            var falta = fim - agora;
            if (falta > TimeSpan.Zero && falta < AvisoDaJanela)
                return new($"janela fecha às {fim.ToString("HH:mm", PtBr)}", "aviso", $"fecha às {fim.ToString("HH:mm", PtBr)}");
        }
        var espera = agora - desde;
        if (espera < TimeSpan.FromMinutes(1)) return null;
        return new($"espera há {Duracao(espera)}", espera >= TimeSpan.FromHours(1) ? "aviso" : "neutro", $"há {Duracao(espera)}");
    }

    /// <summary>
    /// A linha da lista depois de uma escrita da tela, antes da próxima releitura: responsável, encerrada,
    /// ficha e fila da conversa que o CRM devolveu, sem não lidas, a etiqueta do @SemDor que a resposta apaga
    /// e — 25/10/2026 — a ÚLTIMA mensagem como prévia (o texto, o lado e o tick): respondeu, a linha deixa na
    /// hora de dizer "há 8 min" e mostra o que a clínica mandou. Nota interna não é mensagem: a prévia fica.
    /// </summary>
    public static AppConversationSummary LinhaDepoisDaEscrita(AppConversationSummary linha, AppConversation nova)
    {
        var atualizada = linha with
        {
            AgentId = nova.AgentId, Closed = nova.Closed, Version = nova.Version, UpdatedAt = nova.UpdatedAt,
            PatientId = nova.PatientId, PatientName = nova.PatientName, Queue = nova.Queue, Unread = 0,
            Semdor = SemDorNaTela.EtiquetaDepoisDaEscrita(linha.Semdor, nova)
        };
        if (nova.Messages.OrderBy(m => m.At).LastOrDefault() is not { } ultima) return atualizada;
        var previa = !string.IsNullOrWhiteSpace(ultima.Text) ? ultima.Text.Trim() : ultima.FileName ?? ultima.Type;
        return atualizada with { Preview = previa, PreviewDirection = ultima.Direction, PreviewStatus = ultima.Status ?? "" };
    }

    /// <summary>"12 min", "2 h", "1 dia", "3 dias" — sempre para baixo (2 h 59 min é "2 h").</summary>
    internal static string Duracao(TimeSpan t) => t.TotalMinutes < 60 ? $"{(int)t.TotalMinutes} min"
        : t.TotalHours < 24 ? $"{(int)t.TotalHours} h"
        : (int)t.TotalDays == 1 ? "1 dia" : $"{(int)t.TotalDays} dias";

    /// <summary>O selo da linha pede ação da recepção (vermelho ou laranja): em "Novas", ela vem na frente.</summary>
    public static bool PedeAcaoNaLista(SeloDoWhatsApp selo) => selo.Tom is "perigo" or "aviso";

    /// <summary>
    /// A ordem de "Novas": quem pede ação na frente, e dentro de cada parte a MAIS ANTIGA primeiro
    /// (<see cref="Filtrar"/> já entrega por <c>UpdatedAt</c> crescente) — na segunda de manhã, quem
    /// escreveu no sábado é atendido antes. Estável: a ordem que veio se mantém dentro de cada parte.
    /// </summary>
    public static IReadOnlyList<T> OrdenarAsNovas<T>(IEnumerable<T> linhas, Func<T, SeloDoWhatsApp> selo)
        => linhas.OrderBy(l => PedeAcaoNaLista(selo(l)) ? 0 : 1).ToList();

    // ---------------------------------------------------------------- o vazio do centro

    /// <summary>O centro sem conversa aberta: uma frase por modo (a retenção começa por um paciente, não por uma conversa).</summary>
    public static (string Titulo, string Texto) SemConversaAberta(bool retencao)
        => retencao
            ? ("Escolha um paciente para preparar o convite", "O convite, a conversa e o \"Como foi?\" aparecem aqui.")
            : ("Escolha uma conversa", "Ela abre aqui, com a ficha e os horários do paciente ao lado.");

    // ---------------------------------------------------------------- sem ligação

    /// <summary>
    /// A frase de quando o WhatsApp da clínica não respondeu (o <see cref="CrmIndisponivel"/>): sem a
    /// palavra "CRM" — quem lê é a recepção. O acesso recusado diz com quem falar; o resto é falta de ligação.
    /// </summary>
    public static string SemLigacao(Exception e)
        => e.Message == CrmIndisponivel.AcessoRecusado
            ? "O WhatsApp da clínica não aceitou o seu acesso. Fale com a direção."
            : "Sem ligação com o WhatsApp da clínica agora.";

    /// <summary>A falha de uma escrita: o que houve e o que ficou ("Nada foi alterado; tente de novo em instantes.").</summary>
    public static string FalhaNaEscrita(Exception e, string oQueFicou) => $"{SemLigacao(e)} {oQueFicou}";

    /// <summary>O título do cartão de quando a tela não abriu.</summary>
    public const string TituloDaFalhaDeAbertura = "Não foi possível abrir o WhatsApp da clínica";

    /// <summary>
    /// A tela não abriu: o que houve e o que fazer enquanto isso. Sem ligação com o WhatsApp, a recepção
    /// atende pelo celular dela; a falha do próprio app (a agenda, o Acompanhamento) pede o suporte.
    /// </summary>
    public static string FalhaDeAbertura(Exception e)
        => e is CrmIndisponivel
            ? $"{SemLigacao(e)} Enquanto isso, atenda pelo celular da recepção e avise o suporte."
            : "Os dados da clínica não puderam ser lidos agora. Tente de novo em instantes; se continuar, avise o suporte.";

    /// <summary>A conversa não abriu.</summary>
    public static string FalhaDaConversa(Exception e)
        => e is CrmIndisponivel ? $"{SemLigacao(e)} Tente de novo em instantes." : "A conversa não pôde ser lida agora. Tente de novo em instantes.";

    /// <summary>
    /// A faixa amarela no topo da conversa quando a ligação cai no meio do uso: desde quando, e que o
    /// que ela enviar agora pode não sair (a tela continua tentando sozinha).
    /// </summary>
    public static string FaixaSemLigacao(DateTimeOffset desde, DateTimeOffset agora)
        => $"Sem ligação com o WhatsApp desde {desde.ToOffset(agora.Offset).ToString("HH:mm", CultureInfo.InvariantCulture)}. "
           + "O que você enviar agora pode não sair; tentando de novo…";

    /// <summary>O estado da ligação no rodapé da lista (os dois modos): o que a recepção precisa saber, sem o nome do sistema.</summary>
    public static (string Texto, bool Fora) EstadoDaLigacao(bool whatsappDesligado, DateTimeOffset? semLigacaoDesde, DateTimeOffset agora)
        => whatsappDesligado ? ("WhatsApp desligado", true)
            : semLigacaoDesde is { } desde ? ($"Sem ligação desde {desde.ToOffset(agora.Offset).ToString("HH:mm", CultureInfo.InvariantCulture)}", true)
            : ("Conectado", false);
}
