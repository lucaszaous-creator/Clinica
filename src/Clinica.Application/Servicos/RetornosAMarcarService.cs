using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// A leitura da fila "Retornos a marcar" (set/2026): duas idas ao banco, em SEQUÊNCIA
/// (mesmo <c>DbContext</c> — parcela 74), e a montagem pura de
/// <see cref="RetornosAMarcar.Montar"/>. Não grava nada; o que resolve a linha é o
/// Marcar da Recepção, e a linha some sozinha quando o horário passa a existir.
/// </summary>
public sealed class RetornosAMarcarService
{
    private readonly IClinicaRepositorio _repo;
    private readonly Func<DateOnly> _hoje;

    public RetornosAMarcarService(IClinicaRepositorio repo) : this(repo, () => DateOnly.FromDateTime(DateTime.Today)) { }

    /// <summary>Relógio injetável: o atraso é contado de HOJE, e o teste precisa fixá-lo.</summary>
    public RetornosAMarcarService(IClinicaRepositorio repo, Func<DateOnly> hoje)
    {
        _repo = repo;
        _hoje = hoje;
    }

    public async Task<IReadOnlyList<RetornoAMarcar>> ListarAsync(CancellationToken ct = default)
    {
        var hoje = _hoje();
        var sugestoes = await _repo.RetornosSugeridosAsync(
            hoje.AddDays(-RetornosAMarcar.JanelaDiasParaTras), ct);
        if (sugestoes.Count == 0) return [];

        // Os horários dos pacientes da lista a partir da sessão mais antiga entre eles —
        // uma consulta para o conjunto, nunca uma por paciente (o banco é remoto).
        var horarios = await _repo.HorariosAtivosDosPacientesAsync(
            sugestoes.Select(s => s.PacienteId).Distinct().ToList(),
            sugestoes.Min(s => s.Sessao),
            ct);

        return RetornosAMarcar.Montar(sugestoes, horarios, hoje);
    }
}
