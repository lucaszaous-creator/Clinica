namespace Clinica.Application.Abstracoes;

/// <summary>O que se pede. <paramref name="Idempotencia"/> é a chave do comando: repetir não cria outro pedido.</summary>
public sealed record NovoPedidoRecepcao(Guid Idempotencia, string Tipo, int? PacienteId, int? AgendamentoId, string Detalhe);

/// <summary>Um pedido como as listas (chat, CRM) o mostram.</summary>
/// <param name="PodeTratar">Quem lê pode assumir e encerrar (a recepção); quem pediu só acompanha.</param>
public sealed record PedidoNaLista(int Id, string Tipo, string TipoRotulo, int? PacienteId, string PacienteNome,
    int? AgendamentoId, DateTime? HorarioEm, string Detalhe, int CriadoPorId, string CriadoPorNome, DateTime CriadoEm,
    string Situacao, string SituacaoRotulo, string ResponsavelNome, DateTime? AssumidoEm, string Desfecho,
    DateTime? EncerradoEm, bool PodeTratar, bool Meu);

/// <summary>Um horário futuro do paciente, para o formulário do pedido escolher de qual se fala.</summary>
public sealed record HorarioParaPedido(int AgendamentoId, DateTime DataHora, string Rotulo);

/// <summary>
/// Os pedidos à recepção (out/2026 — chat da equipe integrado ao CRM). Quem atende pede; a recepção
/// assume e encerra, pelo chat dos apps ou pelo CRM; quem pediu recebe o desfecho na conversa
/// com o Sistema SemDor. O pedido nunca mexe na agenda.
/// </summary>
public interface IPedidosRecepcaoService
{
    /// <summary>Cria o pedido, o cartão no # Recepção e o cartão na conversa de quem pediu.</summary>
    Task<PedidoNaLista> CriarAsync(int usuarioId, NovoPedidoRecepcao dados, CancellationToken ct = default);

    /// <summary>
    /// Para quem trata: os abertos e em andamento (mais os encerrados nas últimas 24 h, com
    /// <paramref name="comEncerrados"/>). Para quem pede: os seus dos últimos 14 dias.
    /// </summary>
    Task<IReadOnlyList<PedidoNaLista>> ListarAsync(int usuarioId, bool comEncerrados = false, CancellationToken ct = default);

    /// <summary>"Deixa comigo": o pedido fica com quem assumiu, e os dois cartões dizem com quem.</summary>
    Task<PedidoNaLista> AssumirAsync(int usuarioId, int pedidoId, CancellationToken ct = default);

    /// <summary>Encerra (feito ou não), com o desfecho — que chega a quem pediu, no chat.</summary>
    Task<PedidoNaLista> EncerrarAsync(int usuarioId, int pedidoId, bool feito, string desfecho, CancellationToken ct = default);

    /// <summary>Os próximos horários marcados do paciente (60 dias), para o pedido dizer de qual se fala.</summary>
    Task<IReadOnlyList<HorarioParaPedido>> HorariosAsync(int usuarioId, int pacienteId, CancellationToken ct = default);
}
