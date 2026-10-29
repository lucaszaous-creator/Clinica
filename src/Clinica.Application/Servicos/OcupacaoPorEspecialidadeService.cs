using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// Lê o que o mapa de ocupação por especialidade precisa (out/2026) e o entrega montado por
/// <see cref="OcupacaoPorEspecialidade.Montar"/>. Três leituras, em FILA — o mesmo
/// <c>DbContext</c> não aceita duas de uma vez.
/// </summary>
public sealed class OcupacaoPorEspecialidadeService
{
    /// <summary>Duas semanas: o que a secretária precisa para dizer "o próximo dia com vaga".</summary>
    public const int DiasDoMapa = 14;

    private readonly IClinicaRepositorio _repo;

    public OcupacaoPorEspecialidadeService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<IReadOnlyList<LinhaDeOcupacao>> ConsultarAsync(DateOnly de, CancellationToken ct = default)
    {
        var ate = de.AddDays(DiasDoMapa - 1);
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var profissionais = await _repo.ProfissionaisAsync(ct);
        var ocupados = await _repo.AgendamentosQueSobrepoemAsync(inicio, fim, ct);
        var bloqueios = await _repo.BloqueiosNoPeriodoAsync(inicio, fim, ct);
        return OcupacaoPorEspecialidade.Montar(profissionais, ocupados, bloqueios, de, ate);
    }

    /// <summary>As coletas de preventivo marcadas para o profissional no dia — o painel do clique.</summary>
    public async Task<IReadOnlyList<ColetaMarcada>> ColetasDoDiaAsync(int profissionalId, DateOnly dia, CancellationToken ct = default)
    {
        var horarios = await _repo.AgendamentosNoPeriodoParaLeituraAsync(
            dia.ToDateTime(TimeOnly.MinValue), dia.AddDays(1).ToDateTime(TimeOnly.MinValue), ct);
        return horarios
            .Where(a => a.ProfissionalId == profissionalId && a.ColetaPreventivo && a.OcupaAgenda
                        && DateOnly.FromDateTime(a.DataHora) == dia)
            .OrderBy(a => a.DataHora)
            .Select(a => new ColetaMarcada(a.Id, a.DataHora, a.Paciente?.Nome ?? "Paciente"))
            .ToList();
    }
}

/// <summary>Uma coleta de preventivo marcada: a hora e quem vem.</summary>
public sealed record ColetaMarcada(int AgendamentoId, DateTime DataHora, string Paciente)
{
    public string Hora => DataHora.ToString("HH:mm");
}
