using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma hora oferecida no passo "Quando e com quem" — ocupada aparece tracejada.</summary>
public sealed record PilulaDeHorario(TimeOnly Hora, bool Ocupada)
{
    public string Rotulo => Hora.ToString("HH:mm");
    public string Dica => Ocupada
        ? $"{Rotulo} já tem paciente com este profissional — dá para marcar assim mesmo (a agenda avisa, não impede)"
        : $"{Rotulo} livre";
}

/// <summary>
/// As horas do dia de UM profissional como pílulas (out/2026, mockup aprovado "6 · Marcar").
/// Pura, pela regra da casa.
///
/// - **A jornada declarada manda**; sem ela, a régua presumida da agenda (8h–18h): a pílula
///   é atalho para a hora, e o campo "Outro horário" continua aceitando qualquer uma.
/// - **O passo é a duração da sessão** (a do campo, senão a do profissional, senão a da
///   casa) — é o que faz "14:30" e "15:20" serem as horas que de fato cabem.
/// - **Ocupada não some: fica tracejada e clicável** — a agenda avisa e não impede
///   (set/2026); sumir com ela esconderia que há alguém ali.
/// - **Hoje começa agora**: hora que já passou não é oferecida.
/// </summary>
public static class PilulasDeHorario
{
    public static readonly TimeOnly AberturaPresumida = new(8, 0);
    public static readonly TimeOnly FechamentoPresumido = new(18, 0);

    public static IReadOnlyList<PilulaDeHorario> Montar(
        Profissional profissional,
        DateOnly dia,
        IEnumerable<(DateTime Inicio, DateTime Fim)> ocupados,
        int? duracaoMinutos,
        DateTime agora)
    {
        var passo = duracaoMinutos is > 0 ? duracaoMinutos.Value
            : profissional.DuracaoPadraoMinutos is > 0 ? profissional.DuracaoPadraoMinutos.Value
            : Agendamento.DuracaoPadraoMinutos;
        if (dia < DateOnly.FromDateTime(agora)) return [];
        if (!BuscaDeVagas.AtendeNoDia(profissional, dia.DayOfWeek)) return [];

        var das = profissional.AtendeDas ?? AberturaPresumida;
        var ate = profissional.AtendeAte ?? FechamentoPresumido;
        if (ate <= das) return [];

        var lista = ocupados.ToList();
        var pilulas = new List<PilulaDeHorario>();
        for (var t = dia.ToDateTime(das); t.AddMinutes(passo) <= dia.ToDateTime(ate); t = t.AddMinutes(passo))
        {
            if (t < agora) continue;
            var fim = t.AddMinutes(passo);
            pilulas.Add(new PilulaDeHorario(TimeOnly.FromDateTime(t), lista.Any(o => o.Inicio < fim && o.Fim > t)));
        }
        return pilulas;
    }
}
