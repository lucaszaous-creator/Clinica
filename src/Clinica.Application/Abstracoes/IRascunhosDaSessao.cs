namespace Clinica.Application.Abstracoes;

/// <summary>De quem é o rascunho: o paciente, quem escreve e a sessão (o horário, ou o dia sem horário).</summary>
public sealed record ChaveDoRascunho(int PacienteId, int? ProfissionalId, int? AgendamentoId, DateOnly Dia);

/// <summary>O rascunho aberto, como a folha o recupera.</summary>
public sealed record RascunhoAberto(string Conteudo, DateTime AtualizadoEm, int? EvolucaoId);

/// <summary>
/// O RASCUNHO AUTOMÁTICO da evolução (out/2026) — a porta da folha. Uma linha por sessão,
/// atualizada no lugar (<c>RascunhoEvolucao</c>). Quem grava no prontuário continua sendo o
/// Salvar ou, no prazo, a conclusão automática; aqui só se guarda e se encerra o rascunho.
/// </summary>
public interface IRascunhosDaSessao
{
    /// <summary>Guarda o que está na tela; devolve a hora da guarda.</summary>
    Task<DateTime> GuardarAsync(ChaveDoRascunho chave, string conteudo, int? evolucaoId, string? operador, CancellationToken ct = default);

    /// <summary>O rascunho ainda aberto desta sessão, ou nulo.</summary>
    Task<RascunhoAberto?> AbertoAsync(ChaveDoRascunho chave, CancellationToken ct = default);

    /// <summary>O médico salvou: o rascunho encerra apontando para a evolução gravada.</summary>
    Task EncerrarAsync(ChaveDoRascunho chave, int evolucaoId, CancellationToken ct = default);
}
