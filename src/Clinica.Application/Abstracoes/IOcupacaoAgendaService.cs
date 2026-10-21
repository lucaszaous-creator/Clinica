namespace Clinica.Application.Abstracoes;

public sealed record HorarioOcupacao(int Id, DateTime Inicio, DateTime Fim, int? ProfissionalId,
    string Profissional, string Paciente, string Modalidade, string Situacao, bool Ocupa)
{
    /// <summary>A família da modalidade — a cor do traço na semana (os mesmos tokens da agenda).</summary>
    public Clinica.Domain.ModalidadeAtendimento Familia { get; init; }

    /// <summary>
    /// A etapa do dia, derivada dos carimbos (out/2026 — o "Chegou" da Visão geral): é ela que
    /// decide se a linha oferece o check-in, com a MESMA regra da Agenda do dia.
    /// </summary>
    public Clinica.Domain.Entities.EtapaFila Etapa { get; init; }

    /// <summary>Quando o paciente chegou ao local — a hora que a coluna Situação escreve.</summary>
    public DateTime? ChegadaEm { get; init; }

    /// <summary>A situação no vocabulário das listas do dia ("No local", "Em atendimento"…).</summary>
    public string SituacaoDoDia { get; init; } = string.Empty;
}
public sealed record MedidaOcupacao(int Atendimentos, int Cancelados, int Faltas, int MinutosOcupados,
    int MinutosDisponiveis, int MinutosBloqueados, int SemJornada, int ForaDaJornada)
{
    public decimal? Percentual => SemJornada > 0 || MinutosDisponiveis == 0 ? null
        : Math.Round(100m * MinutosOcupados / MinutosDisponiveis, 1);
    public int MinutosLivres => Math.Max(0, MinutosDisponiveis - MinutosOcupados);
}
public sealed record DiaOcupacao(DateOnly Dia, MedidaOcupacao Resumo);
public sealed record ProfissionalOcupacao(int Id, string Nome, string Jornada, MedidaOcupacao Resumo)
{
    /// <summary>
    /// O profissional dia a dia (out/2026, a matriz da Visão geral): a medida de cada dia, as
    /// faixas do horário dele naquele dia (vazia = não atende; nula = sem horário cadastrado) e
    /// o motivo quando a agenda dele está fechada o dia inteiro. Vazia quando a consulta é só
    /// de movimento (sem horários).
    /// </summary>
    public IReadOnlyList<DiaDoProfissionalOcupacao> Dias { get; init; } = [];

    /// <summary>Sem horário de atendimento cadastrado — a ocupação dele não é medida (não é erro).</summary>
    public bool SemHorario => Resumo.SemJornada > 0;
}

/// <summary>Um dia de um profissional na Visão geral.</summary>
public sealed record DiaDoProfissionalOcupacao(DateOnly Dia, MedidaOcupacao Resumo,
    IReadOnlyList<Clinica.Domain.FaixaDeAtendimento>? Faixas, string? Fechado);
public sealed record OcupacaoAgenda(DateOnly Inicio, DateOnly Fim, DateTime AtualizadoEm,
    MedidaOcupacao Resumo, IReadOnlyList<DiaOcupacao> Dias,
    IReadOnlyList<ProfissionalOcupacao> Profissionais, IReadOnlyList<HorarioOcupacao> Horarios)
{
    /// <summary>Os vãos livres do profissional FILTRADO (vazio para toda a equipe) — out/2026.</summary>
    public IReadOnlyList<Clinica.Application.Modelos.VaoLivre> Vagas { get; init; } = [];
    /// <summary>Os dias inteiramente fechados, com o motivo.</summary>
    public IReadOnlyList<Clinica.Application.Modelos.DiaFechado> Fechados { get; init; } = [];
    /// <summary>O profissional filtrado não tem jornada com horas — não há como dizer o que está livre.</summary>
    public bool SemJornadaParaVagas { get; init; }
}

public interface IOcupacaoAgendaService
{
    Task<OcupacaoAgenda> ConsultarMovimentoAsync(int usuarioId, DateOnly inicio, DateOnly fim, int? profissionalId = null,
        CancellationToken ct = default);
    Task<OcupacaoAgenda> ConsultarAsync(int usuarioId, DateOnly inicio, DateOnly fim, int? profissionalId = null,
        CancellationToken ct = default);
}
