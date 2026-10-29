using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>Uma célula do mapa: quanto sobra num dia. Nulo em <see cref="Livres"/> = ninguém atende.</summary>
public sealed record CelulaDeOcupacao(DateOnly Dia, int? Livres, int Total)
{
    public bool Atende => Livres is not null;
    public bool Lotado => Livres == 0;

    /// <summary>Até um quinto livre conta como "quase cheio" — o laranja do mapa.</summary>
    public bool QuaseCheio => Livres is { } l && l > 0 && Total > 0 && l * 5 <= Total;
}

/// <summary>
/// Uma linha do mapa: uma especialidade (horários livres) ou a coleta de preventivo de um
/// profissional (vagas da agenda paralela).
/// </summary>
public sealed record LinhaDeOcupacao(
    string Titulo, string? Detalhe, bool Preventivo, int? ProfissionalId, IReadOnlyList<CelulaDeOcupacao> Dias);

/// <summary>
/// O MAPA DE OCUPAÇÃO POR ESPECIALIDADE (out/2026), no jeito do mapa de quartos de um hotel:
/// linhas são as especialidades da clínica e, logo abaixo de cada uma, a coleta de
/// preventivo de quem a faz; colunas são os dias; cada célula diz quanto SOBRA.
///
/// A conta de horário livre é a MESMA do "Próximas vagas" (<see cref="BuscaDeVagas"/>),
/// com a jornada e os bloqueios do profissional — o mapa não pode dizer "3 livres" num dia
/// em que a busca de vagas não acha nenhuma. A de preventivo é a de
/// <see cref="VagasDePreventivoNoDia"/>, a mesma que recusa a marcação.
/// Puro: o serviço lê, isto monta (e o teste monta sem banco).
/// </summary>
public static class OcupacaoPorEspecialidade
{
    /// <summary>Especialidade de quem não declarou a sua em Profissionais e salas.</summary>
    public const string SemEspecialidade = "Sem especialidade";

    public static IReadOnlyList<LinhaDeOcupacao> Montar(
        IReadOnlyList<Profissional> profissionais, IReadOnlyList<Agendamento> ocupados,
        IReadOnlyList<BloqueioAgenda> bloqueios, DateOnly de, DateOnly ate)
    {
        var ativos = profissionais.Where(p => p.Ativo).ToList();
        var dias = new List<DateOnly>();
        for (var d = de; d <= ate; d = d.AddDays(1)) dias.Add(d);

        // Horários livres e totais por profissional e dia — total é a mesma conta sem ninguém marcado.
        var porProfissional = ativos.ToDictionary(p => p.Id, p =>
        {
            var duracao = p.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos;
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            var janela = ate.DayNumber - de.DayNumber + 1;
            var livres = BuscaDeVagas.Calcular(inicio, duracao, p, ocupados, bloqueios,
                    quantidade: int.MaxValue, diasMaximos: janela)
                .GroupBy(v => DateOnly.FromDateTime(v.Inicio)).ToDictionary(g => g.Key, g => g.Count());
            var totais = BuscaDeVagas.Calcular(inicio, duracao, p, [], bloqueios,
                    quantidade: int.MaxValue, diasMaximos: janela)
                .GroupBy(v => DateOnly.FromDateTime(v.Inicio)).ToDictionary(g => g.Key, g => g.Count());
            return (livres, totais);
        });

        var linhas = new List<LinhaDeOcupacao>();
        foreach (var grupo in ativos
                     .GroupBy(p => string.IsNullOrWhiteSpace(p.EspecialidadeCodigo) ? null : p.EspecialidadeCodigo)
                     .OrderBy(g => g.Key is null)
                     .ThenBy(g => g.Key is null ? SemEspecialidade : CatalogoEspecialidades.Nome(g.Key), StringComparer.CurrentCulture))
        {
            var celulas = dias.Select(d =>
            {
                var total = grupo.Sum(p => porProfissional[p.Id].totais.GetValueOrDefault(d));
                var livres = grupo.Sum(p => porProfissional[p.Id].livres.GetValueOrDefault(d));
                return new CelulaDeOcupacao(d, total == 0 ? null : livres, total);
            }).ToList();
            linhas.Add(new LinhaDeOcupacao(
                grupo.Key is null ? SemEspecialidade : CatalogoEspecialidades.Nome(grupo.Key),
                string.Join(" · ", grupo.Select(p => p.Rotulo)), false, null, celulas));

            // A coleta de preventivo de cada profissional desta especialidade que a faz.
            foreach (var p in grupo.Where(p => FazPreventivo(p, ocupados)).OrderBy(p => p.Rotulo, StringComparer.CurrentCulture))
            {
                var vagas = VagasDePreventivoNoDia.Contar(p, ocupados, de, ate);
                var prev = vagas.Select(v => new CelulaDeOcupacao(v.Dia,
                    porProfissional[p.Id].totais.GetValueOrDefault(v.Dia) == 0 ? null : v.Restantes, v.Limite)).ToList();
                linhas.Add(new LinhaDeOcupacao("Coleta de preventivo",
                    $"{p.Rotulo} · limite {p.LimitePreventivoEfetivo} por dia", true, p.Id, prev));
            }
        }
        return linhas;
    }

    /// <summary>
    /// Quem aparece com a linha de preventivo: quem declarou um limite, quem é da ginecologia
    /// ou quem já tem coleta marcada no período. O padrão (5) vale para todos, mas mostrar uma
    /// linha de preventivo para a acupuntura seria ruído.
    /// </summary>
    public static bool FazPreventivo(Profissional p, IEnumerable<Agendamento> ocupados)
        => p.LimitePreventivoEfetivo > 0
           && (p.LimitePreventivoPorDia is not null
               || string.Equals(p.EspecialidadeCodigo, "Ginecologia", StringComparison.OrdinalIgnoreCase)
               || ocupados.Any(a => a.ProfissionalId == p.Id && a.ColetaPreventivo && a.OcupaAgenda));
}
