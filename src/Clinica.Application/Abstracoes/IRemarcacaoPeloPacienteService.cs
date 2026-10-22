namespace Clinica.Application.Abstracoes;

/// <summary>Um horário livre que a recepção pode oferecer ao paciente.</summary>
public sealed record VagaParaOferta(DateTime Inicio, string Rotulo);

/// <summary>
/// A remarcação pedida pelo PACIENTE, do lado de quem atende (@SemDor, out/2026 —
/// docs/semdor-assistente.md): ver os horários livres, oferecer até 3 ao paciente e, quando ele
/// escolher, remarcar com um clique. Quem pode é quem trata pedidos (<c>PedidosRecepcaoRegras.PodeTratar</c>).
/// </summary>
public interface IRemarcacaoPeloPacienteService
{
    /// <summary>Os próximos horários livres do mesmo profissional, sala e duração do horário do paciente.</summary>
    Task<IReadOnlyList<VagaParaOferta>> VagasAsync(int usuarioId, int pedidoId, CancellationToken ct = default);

    /// <summary>Oferece os horários: a mensagem com os botões entra na fila do CRM.</summary>
    Task OferecerAsync(int usuarioId, int pedidoId, IReadOnlyList<DateTime> horarios, CancellationToken ct = default);

    /// <summary>Remarca para o horário que o paciente escolheu (os conflitos são conferidos de novo) e confirma a ele.</summary>
    Task<DateTime> RemarcarAsync(int usuarioId, int pedidoId, string operador, CancellationToken ct = default);
}
