using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um paciente que faz aniversário, com o contato para a clínica ligar.</summary>
public sealed record Aniversariante(
    int PacienteId,
    string Nome,
    string? Telefone,
    DateOnly Nascimento,
    int? Idade,
    bool VemHoje)
{
    /// <summary>Dia e mês, que é o que a lista mostra.</summary>
    public string DiaEMes => $"{Nascimento.Day:00}/{Nascimento.Month:00}";
}

/// <summary>
/// Histórico de falta de um paciente — o que a recepção precisa saber antes de dar a ele
/// o horário mais disputado da semana.
/// </summary>
public sealed record HistoricoFaltas(
    int PacienteId,
    int Faltas,
    int Cancelamentos,
    int Fechados,
    DateOnly? UltimaFalta)
{
    /// <summary>
    /// % de falta sobre os horários que chegaram ao fim (atendidos + faltas). Nulo sem
    /// base: paciente novo não tem taxa, e 0% diria que ele já provou que comparece.
    /// </summary>
    public double? TaxaPercentual
        => Fechados == 0 ? null : Math.Round(Faltas * 100.0 / Fechados, 1);

    /// <summary>
    /// Faltou o bastante para a recepção pensar duas vezes antes do horário de pico. Não
    /// é impedimento — é informação. Exige base mínima: uma falta em duas sessões dá 50%
    /// e não significa nada.
    /// </summary>
    public bool Reincidente
        => Faltas >= RelacionamentoService.FaltasParaAvisar
           && Fechados >= RelacionamentoService.SessoesMinimasParaTaxa
           && TaxaPercentual >= RelacionamentoService.TaxaFaltaParaAvisar;
}

/// <summary>
/// Relacionamento com o paciente (parcela 28): as duas perguntas que o balcão faz e que
/// o sistema não respondia.
///
/// **Aniversário** é a ligação mais barata que uma clínica faz — trinta segundos, sem
/// nada a vender — e era a que se perdia, porque a data estava no cadastro e nenhuma tela
/// a lia. Quinta ocorrência do defeito recorrente do projeto, agora no dado mais antigo
/// de todos.
///
/// **Falta** é o outro lado: a agenda registra `StatusAgendamento.Faltou` desde a parcela
/// 1, e os indicadores calculam a taxa da CLÍNICA — nunca a do paciente. Quem está no
/// balcão decidindo se dá o horário das 18h de terça (o mais disputado) para alguém que
/// faltou três vezes não tinha como saber.
///
/// Nenhuma das duas impede nada. Uma é motivo para ligar, a outra é motivo para
/// confirmar com mais cuidado.
/// </summary>
public sealed class RelacionamentoService
{
    /// <summary>Faltas absolutas a partir das quais o balcão é avisado.</summary>
    public const int FaltasParaAvisar = 2;

    /// <summary>
    /// Sessões fechadas mínimas para a taxa significar alguma coisa. Uma falta em duas
    /// sessões dá 50% e não diz nada sobre a pessoa.
    /// </summary>
    public const int SessoesMinimasParaTaxa = 4;

    /// <summary>Taxa a partir da qual o padrão vira aviso.</summary>
    public const double TaxaFaltaParaAvisar = 25.0;

    private readonly IClinicaRepositorio _repo;
    private readonly AgendaService _agenda;

    public RelacionamentoService(IClinicaRepositorio repo, AgendaService agenda)
    {
        _repo = repo;
        _agenda = agenda;
    }

    /// <summary>
    /// Quem faz aniversário no dia. <paramref name="janelaDias"/> amplia para a semana —
    /// a clínica que abre de segunda a sexta perderia todo aniversário de domingo se a
    /// lista fosse só do dia.
    ///
    /// Marca quem **vem hoje**: com o paciente no balcão, o parabéns é dado na hora e não
    /// custa nem a ligação.
    /// </summary>
    public async Task<IReadOnlyList<Aniversariante>> AniversariantesAsync(
        DateOnly dia, int janelaDias = 0, CancellationToken ct = default)
    {
        var pacientes = await _repo.AniversariantesAsync(dia, janelaDias, ct);
        if (pacientes.Count == 0) return [];

        var doDia = await _agenda.DoDiaAsync(dia, ct);
        var vemHoje = doDia
            .Where(a => a.OcupaAgenda)
            .Select(a => a.PacienteId)
            .ToHashSet();

        return pacientes
            .Where(p => p.DataNascimento is not null)
            .Select(p =>
            {
                var nascimento = p.DataNascimento!.Value;
                // Idade só quando o ano de nascimento é plausível: cadastro antigo às
                // vezes traz 01/01/0001, e "2025 anos" na tela é pior que campo vazio.
                int? idade = nascimento.Year > 1900
                    ? dia.Year - nascimento.Year -
                      (dia.Month < nascimento.Month ||
                       (dia.Month == nascimento.Month && dia.Day < nascimento.Day) ? 1 : 0)
                    : null;

                return new Aniversariante(
                    p.Id, p.Nome, p.Telefone, nascimento, idade, vemHoje.Contains(p.Id));
            })
            .OrderByDescending(a => a.VemHoje)
            .ThenBy(a => a.Nascimento.Month)
            .ThenBy(a => a.Nascimento.Day)
            .ToList();
    }

    /// <summary>
    /// Quantas vezes o paciente faltou, e quando foi a última.
    ///
    /// **Cancelamento avisado não entra na conta de faltas** — quem desmarcou deu à
    /// clínica a chance de reocupar o horário, e misturar os dois esconderia justamente
    /// o comportamento que o número existe para mostrar. Ele aparece ao lado, separado,
    /// porque muito cancelamento também é padrão (só que outro).
    /// </summary>
    public async Task<HistoricoFaltas> FaltasDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
    {
        var historico = await _repo.AgendamentosDoPacienteAsync(pacienteId, ct);

        var faltas = historico.Where(a => a.Status == StatusAgendamento.Faltou).ToList();
        var atendidos = historico.Count(a => a.Status == StatusAgendamento.Realizado);

        return new HistoricoFaltas(
            pacienteId,
            faltas.Count,
            historico.Count(a => a.Status == StatusAgendamento.Cancelado),
            atendidos + faltas.Count,
            faltas.Count == 0
                ? null
                : DateOnly.FromDateTime(faltas.Max(a => a.DataHora)));
    }
}
