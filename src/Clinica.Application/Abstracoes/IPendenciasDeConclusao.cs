namespace Clinica.Application.Abstracoes;

/// <summary>
/// As sessões cujo prazo de conclusão automática venceu e que continuam abertas, com o
/// motivo (out/2026, Configurações em Blazor). O motor é o <c>ConclusaoAutomaticaService</c>
/// do Infrastructure — esta é a porta que a tela alcança, devolvendo as MESMAS linhas que a
/// tela WPF lia dele, sem segunda regra de "o que está pendente".
/// </summary>
public interface IPendenciasDeConclusao
{
    /// <summary>Até 100 sessões, com o impedimento de cada uma. Vazio quando a regra está desligada.</summary>
    Task<IReadOnlyList<SessaoPendenteDeConclusao>> PendenciasAsync(CancellationToken ct = default);
}

/// <summary>Uma sessão com prazo vencido e ainda não concluída.</summary>
public sealed record SessaoPendenteDeConclusao(
    int AgendamentoId, string Paciente, string Profissional, DateTime Data, string Motivo);
