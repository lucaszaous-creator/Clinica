namespace Clinica.Application.Abstracoes;

public sealed record HorarioOcupacao(int Id, DateTime Inicio, DateTime Fim, int? ProfissionalId,
    string Profissional, string Paciente, string Modalidade, string Situacao, bool Ocupa);
public sealed record MedidaOcupacao(int Atendimentos, int Cancelados, int Faltas, int MinutosOcupados,
    int MinutosDisponiveis, int MinutosBloqueados, int SemJornada, int ForaDaJornada)
{
    public decimal? Percentual => SemJornada > 0 || MinutosDisponiveis == 0 ? null
        : Math.Round(100m * MinutosOcupados / MinutosDisponiveis, 1);
    public int MinutosLivres => Math.Max(0, MinutosDisponiveis - MinutosOcupados);
}
public sealed record DiaOcupacao(DateOnly Dia, MedidaOcupacao Resumo);
public sealed record ProfissionalOcupacao(int Id, string Nome, string Jornada, MedidaOcupacao Resumo);
public sealed record OcupacaoAgenda(DateOnly Inicio, DateOnly Fim, DateTime AtualizadoEm,
    MedidaOcupacao Resumo, IReadOnlyList<DiaOcupacao> Dias,
    IReadOnlyList<ProfissionalOcupacao> Profissionais, IReadOnlyList<HorarioOcupacao> Horarios);

public interface IOcupacaoAgendaService
{
    Task<OcupacaoAgenda> ConsultarMovimentoAsync(int usuarioId, DateOnly inicio, DateOnly fim, int? profissionalId = null,
        CancellationToken ct = default);
    Task<OcupacaoAgenda> ConsultarAsync(int usuarioId, DateOnly inicio, DateOnly fim, int? profissionalId = null,
        CancellationToken ct = default);
}
