using System.Globalization;
using System.Text.RegularExpressions;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O horário que a recepção quis marcar e a trava recusou — o que o pedido de liberação guarda.</summary>
public sealed record NovoPedidoDeLiberacao(
    int PacienteId, DateTime DataHora, string ModalidadeCodigo, string? EspecialidadeConsultaCodigo,
    int? ProfissionalId, int? SalaId, int? DuracaoMinutos, bool Encaixe, bool ColetaPreventivo,
    string? Observacoes, string Motivo, int? AgendamentoARemarcarId = null);

/// <summary>O pedido criado e a quem ele chegou — a tela diz isso, em vez de "enviado" sem destinatário.</summary>
public sealed record PedidoDeLiberacaoCriado(LiberacaoDeTrava Pedido, int Aprovadores, int ComWhatsApp);

/// <summary>
/// O que as TELAS alcançam do pedido de liberação (a implementação usa o banco direto e mora
/// na Infrastructure, como a dos pedidos à recepção). Decidir NÃO está aqui de propósito: a
/// decisão chega pela conversa com o sistema (app ou WhatsApp), em nome do login de quem aprova.
/// </summary>
public interface ILiberacaoDeTravaService
{
    Task<PedidoDeLiberacaoCriado> PedirAsync(NovoPedidoDeLiberacao novo, int usuarioId, CancellationToken ct = default);
    Task<LiberacaoDeTrava?> PendenteAsync(int pacienteId, DateOnly dia, CancellationToken ct = default);
}

/// <summary>A decisão escrita pela gerência: liberar ou negar, de qual pedido (se disse) e por quê (se disse).</summary>
public sealed record DecisaoPorTexto(bool Liberar, int? PedidoId, string? Motivo);

/// <summary>
/// AS REGRAS E AS FRASES do pedido de liberação da trava de consulta (nov/2026), sem banco.
///
/// A gerência responde pelo WhatsApp (ou pela conversa com o sistema, no app) com palavras
/// simples — "sim", "não", "liberar 12", "negar 12 já consultou semana passada". O que conta
/// como decisão e o que cada mensagem diz mora aqui, com teste: são textos que autorizam uma
/// consulta que o convênio pode glosar, e que chegam ao celular do paciente.
/// </summary>
public static partial class LiberacaoDeTravaRegras
{
    /// <summary>Sem resposta em 48 h o pedido expira — e nunca passa da hora da própria consulta.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(48);

    public const int MotivoMaximo = 500;

    public static DateTime ExpiraEm(DateTime pedidoEm, DateTime consultaEm)
    {
        var limite = pedidoEm + Validade;
        return consultaEm > pedidoEm && consultaEm < limite ? consultaEm : limite;
    }

    [GeneratedRegex(@"^\s*(sim|s|libera|liberar|liberado|liberada|libero|autorizo|autorizar|autorizado|aprovo|aprovar|aprovado|pode|n[aã]o|n|nega|negar|negado|negada|nego|recuso|recusar|recusado)\b[\s:,.\-]*(?:(?:n[ºo°]\.?|pedido|#)\s*)?(\d{1,9})?[\s:,.\-]*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Decisao();

    /// <summary>
    /// Lê a decisão. Nulo = a mensagem não é uma decisão (e segue para as perguntas de sempre:
    /// agenda, pedidos…). "sim" e "não" sozinhos SÓ valem para quem aprova e tem pedido
    /// pendente — quem decide isso é o serviço, não esta leitura.
    /// </summary>
    public static DecisaoPorTexto? Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 600) return null;
        var m = Decisao().Match(texto.Trim());
        if (!m.Success) return null;
        var palavra = m.Groups[1].Value.ToLowerInvariant();
        var liberar = palavra[0] != 'n' && palavra[0] != 'r';
        int? id = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        var resto = m.Groups[3].Value.Trim();
        // Sem o número do pedido, só a palavra SOZINHA decide ("sim", "não"): "não vou conseguir
        // hoje" ou "pode me mandar a agenda?" são conversa, e negar um pedido por causa delas
        // seria o sistema decidindo no lugar da gerência. Com o número, o resto é o motivo.
        if (id is null && resto.Length > 0) return null;
        return new DecisaoPorTexto(liberar, id, resto.Length == 0 ? null : resto);
    }

    private static string Quando(DateTime d) => d.ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.InvariantCulture);

    /// <summary>O pedido, como chega ao WhatsApp e ao chat de quem aprova.</summary>
    public static string TextoParaGerencia(LiberacaoDeTrava l)
        => $"*Pedido de liberação nº {l.Id}* — consulta antes do prazo do convênio\n"
           + $"Paciente: {l.PacienteNome}\n"
           + $"Convênio: {l.ConvenioNome} (uma consulta a cada {l.PrazoDias} dias)\n"
           + $"Última consulta: {l.ConsultaAnteriorEm:dd/MM/yyyy}"
           + (l.AtendimentoAnteriorNumero is { } n ? $" · atendimento {n}" : "") + "\n"
           + $"Nova consulta pedida: {Quando(l.DataHora)}"
           + (l.ProfissionalNome.Length > 0 ? $" · {l.ProfissionalNome}" : "") + "\n"
           + $"Motivo: {l.Motivo}\n"
           + $"Pedido por {l.PedidoPorNome}.\n\n"
           + $"Responda *liberar {l.Id}* ou *negar {l.Id}* (se for o único pedido aberto, basta *sim* ou *não*). "
           + $"Sem resposta até {Quando(l.ExpiraEm)}, o pedido expira e nada é marcado.";

    /// <summary>Vários pedidos abertos e a gerência respondeu só "sim": a lista, para ela dizer qual.</summary>
    public static string TextoVarios(IReadOnlyList<LiberacaoDeTrava> pendentes)
        => $"Há {pendentes.Count} pedidos de liberação abertos. Diga qual:\n"
           + string.Join("\n", pendentes.Select(l =>
               $"• nº {l.Id} — {l.PacienteNome}, {l.ConvenioNome}, {Quando(l.DataHora)}"))
           + "\n\nResponda, por exemplo, *liberar " + pendentes[0].Id + "* ou *negar " + pendentes[0].Id + "*.";

    public const string SemPendentes = "Não há pedido de liberação aberto no momento.";

    public static string NaoAchei(int id) => $"Não achei pedido de liberação aberto com o nº {id}. Ele pode já ter sido decidido ou expirado.";

    /// <summary>O que a gerência lê depois de decidir.</summary>
    public static string TextoDaDecisao(LiberacaoDeTrava l)
        => l.Estado == LiberacaoDeTrava.Negada
            ? $"Pedido nº {l.Id} NEGADO — {l.PacienteNome}. Nada foi marcado, e a recepção foi avisada."
            : l.AgendamentoId is not null
                ? $"Pedido nº {l.Id} LIBERADO — {l.PacienteNome} está marcado para {Quando(l.DataHora)}. A recepção foi avisada."
                : $"Pedido nº {l.Id} LIBERADO, mas o horário NÃO pôde ser marcado sozinho: {l.Desfecho} "
                  + "A recepção foi avisada para marcar — a liberação vale para este paciente nesse dia.";

    /// <summary>O desfecho, para quem pediu (chat do sistema e, se tiver o número ligado, WhatsApp).</summary>
    public static string TextoParaQuemPediu(LiberacaoDeTrava l)
        => l.Estado switch
        {
            LiberacaoDeTrava.Negada =>
                $"*Liberação NEGADA* por {l.DecididoPorNome} (pedido nº {l.Id}) — {l.PacienteNome}, consulta de {Quando(l.DataHora)}. "
                + "Nada foi marcado. Ofereça outra data, ou a consulta particular."
                + (string.IsNullOrWhiteSpace(l.Desfecho) ? "" : $" Motivo: {l.Desfecho}"),
            LiberacaoDeTrava.Expirada =>
                $"O pedido de liberação nº {l.Id} ({l.PacienteNome}, consulta de {Quando(l.DataHora)}) EXPIROU sem resposta da gerência. "
                + "Nada foi marcado.",
            _ when l.AgendamentoId is not null =>
                $"*Liberado por {l.DecididoPorNome}* (pedido nº {l.Id}) — {l.PacienteNome} está MARCADO para {Quando(l.DataHora)}"
                + (l.ProfissionalNome.Length > 0 ? $", com {l.ProfissionalNome}" : "") + ".",
            _ =>
                $"*Liberado por {l.DecididoPorNome}* (pedido nº {l.Id}) — {l.PacienteNome}. O horário de {Quando(l.DataHora)} NÃO pôde ser "
                + $"marcado sozinho: {l.Desfecho} Marque pela agenda — a trava está liberada para este paciente nesse dia."
        };

    /// <summary>
    /// A confirmação ao paciente. Diz o que a clínica FEZ (o horário marcado) e nada do
    /// bastidor: o paciente não precisa saber que houve trava nem quem liberou.
    /// </summary>
    public static string TextoParaPaciente(LiberacaoDeTrava l, string? nomeDaClinica)
        => $"Olá, {PrimeiroNome(l.PacienteNome)}! Sua consulta"
           + (string.IsNullOrWhiteSpace(nomeDaClinica) ? "" : $" na {nomeDaClinica}")
           + $" está marcada para {Quando(l.DataHora)}"
           + (l.ProfissionalNome.Length > 0 ? $", com {l.ProfissionalNome}" : "")
           + ". Se precisar remarcar, é só responder por aqui.";

    private static string PrimeiroNome(string nome)
    {
        var p = (nome ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return p.Length <= 1 ? p : char.ToUpper(p[0], CultureInfo.GetCultureInfo("pt-BR")) + p[1..].ToLower(CultureInfo.GetCultureInfo("pt-BR"));
    }
}
