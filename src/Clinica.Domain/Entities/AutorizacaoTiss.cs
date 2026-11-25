namespace Clinica.Domain.Entities;

/// <summary>Em que ponto está a autorização de um atendimento na operadora.</summary>
public enum SituacaoAutorizacaoTiss
{
    /// <summary>Na fila: o pedido ainda não foi (ou está indo) à operadora.</summary>
    Pedida,
    /// <summary>A operadora respondeu "em análise" (ou aguardando justificativa/documento): o conector pergunta de novo.</summary>
    EmAnalise,
    /// <summary>Autorizada (inclusive parcialmente): a senha está aqui.</summary>
    Autorizada,
    /// <summary>A operadora negou, ou recusou o pedido (mensagem de erro): o motivo está aqui.</summary>
    Negada,
    /// <summary>A mensagem saiu e não houve resposta em que confiar: NÃO é negada. Pedir de novo é decisão de quem está no balcão.</summary>
    NaoSeSabe,
    /// <summary>O horário foi desmarcado e o cancelamento da senha foi pedido à operadora.</summary>
    CancelamentoPedido,
    /// <summary>Cancelada (pela operadora, ou o pedido nem saiu).</summary>
    Cancelada
}

/// <summary>
/// A AUTORIZAÇÃO de um atendimento na operadora (nov/2026, Amil): o sistema pede pelo web service
/// (<c>solicitacaoProcedimento</c>) quando o paciente chega, e guarda a senha, a data e a validade — que a
/// baixa da guia usa, travadas. É uma FOTOGRAFIA do pedido: carteira, procedimento e profissional como
/// foram pedidos (o conector lê só esta linha, nunca a ficha do paciente). Um horário tem UMA autorização
/// válida; pedir de novo só depois de negada, cancelada ou sem resposta.
/// </summary>
public class AutorizacaoTiss
{
    public int Id { get; set; }

    /// <summary>O horário da agenda (o atendimento) que esta autorização cobre.</summary>
    public int AgendamentoId { get; set; }
    public int PacienteId { get; set; }
    public string ConvenioCodigo { get; set; } = string.Empty;

    public SituacaoAutorizacaoTiss Situacao { get; set; } = SituacaoAutorizacaoTiss.Pedida;

    // ---- o pedido (fotografia) ----
    public string NumeroCarteira { get; set; } = string.Empty;
    public string CodigoTabela { get; set; } = "22";
    public string CodigoProcedimento { get; set; } = string.Empty;
    public string DescricaoProcedimento { get; set; } = string.Empty;
    public int Quantidade { get; set; } = 1;
    public string CaraterAtendimento { get; set; } = "1";
    public string ProfissionalNome { get; set; } = string.Empty;
    public string ConselhoProfissional { get; set; } = string.Empty;
    public string NumeroConselho { get; set; } = string.Empty;
    public string UfConselho { get; set; } = string.Empty;
    public string Cbos { get; set; } = string.Empty;

    /// <summary>O número da guia de solicitação no prestador ("A" + Id): é por ele que a situação e o cancelamento a acham.</summary>
    public string NumeroGuiaPrestador { get; set; } = string.Empty;

    // ---- a resposta ----
    public string? NumeroGuiaOperadora { get; set; }
    public string? Senha { get; set; }
    public DateOnly? DataAutorizacao { get; set; }
    public DateOnly? ValidadeSenha { get; set; }
    public string? Motivo { get; set; }

    // ---- a confirmação do beneficiário, colhida NA RECEPÇÃO com o paciente presente ----
    /// <summary>
    /// O token de 6 dígitos que o paciente mostrou no app (dado de AUTENTICAÇÃO do beneficiário):
    /// guardado só aqui e na guia, nunca em log nem na trilha; fora da baixa aparece mascarado.
    /// </summary>
    public string? Token { get; set; }
    /// <summary>O paciente não confirmou: o motivo da tabela TISS (<c>ausenciaCodValidacao</c>).</summary>
    public string? AusenciaToken { get; set; }
    public string? TokenRegistradoPor { get; set; }
    public DateTime? TokenRegistradoEm { get; set; }

    /// <summary>A recepção já resolveu a confirmação: digitou o token ou disse por que não há.</summary>
    public bool ConfirmacaoResolvida => !string.IsNullOrWhiteSpace(Token) || !string.IsNullOrWhiteSpace(AusenciaToken);

    /// <summary>O token mascarado para mostrar fora da baixa ("••••13").</summary>
    public static string Mascarar(string? token)
        => string.IsNullOrWhiteSpace(token) ? "" : "••••" + (token.Trim().Length >= 2 ? token.Trim()[^2..] : "");

    /// <summary>Quantas vezes a situação foi perguntada enquanto em análise (há teto).</summary>
    public int ConsultasDeStatus { get; set; }

    public string PedidaPor { get; set; } = string.Empty;
    public DateTime PedidaEm { get; set; }
    public DateTime? AtualizadaEm { get; set; }

    /// <summary>A autorização ainda vale para o atendimento (autorizada e dentro da validade).</summary>
    public bool ValidaEm(DateOnly dia) => Situacao == SituacaoAutorizacaoTiss.Autorizada && (ValidadeSenha is null || ValidadeSenha >= dia);

    /// <summary>Prende o horário: não se pede outra enquanto esta está na fila, em análise ou valendo.</summary>
    public bool PrendeOHorario(DateOnly dia)
        => Situacao is SituacaoAutorizacaoTiss.Pedida or SituacaoAutorizacaoTiss.EmAnalise or SituacaoAutorizacaoTiss.CancelamentoPedido || ValidaEm(dia);
}
