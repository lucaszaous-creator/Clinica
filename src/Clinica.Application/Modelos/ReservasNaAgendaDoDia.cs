using System.Globalization;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma linha da Agenda › Dia: um horário de paciente OU uma reserva sem ficha (nunca os dois).</summary>
public sealed record ItemDaAgendaDoDia(LinhaAgendaDoDia? Linha, ReservaDeHorario? Reserva)
{
    public DateTime Quando => Linha?.DataHora ?? Reserva!.DataHora;
}

/// <summary>
/// A RESERVA SEM FICHA NA AGENDA › DIA (nov/2026, ajuste do dono: "utilizamos mais a agenda › dia"): quais reservas a
/// lista mostra e onde elas entram. A lista do dia é de pacientes; a reserva entra nela pela hora, com a marca própria.
/// </summary>
public static class ReservasNaAgendaDoDia
{
    private static readonly CompareInfo Comparador = new CultureInfo("pt-BR").CompareInfo;

    /// <summary>
    /// As reservas que aparecem com o filtro da tela: só as que ocupam a agenda; do profissional escolhido; e a busca
    /// acha pelo nome. A situação, a resposta e a modalidade são filtros de PACIENTE (chegou, confirmou, BSV…): com um
    /// deles ligado a reserva sai, como sai quem não corresponde.
    /// </summary>
    public static IReadOnlyList<ReservaDeHorario> Visiveis(IEnumerable<ReservaDeHorario> reservas, FiltroAgendaDoDia f)
    {
        if (f.Situacao != "todos" || !string.IsNullOrEmpty(f.Resposta)
            || !string.IsNullOrEmpty(f.Modalidade) && f.Modalidade != AgendaDoDiaNaTela.TodasModalidades)
            return [];
        var busca = f.Busca?.Trim() ?? "";
        return reservas.Where(r => r.OcupaAgenda
                && (f.ProfissionalId is not { } p || r.ProfissionalId == p)
                && (busca.Length == 0 || Comparador.IndexOf(r.Nome, busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0))
            .OrderBy(r => r.DataHora).ToList();
    }

    /// <summary>
    /// Os horários e as reservas numa lista só. A ORDEM da lista do dia não muda (ela já vem na ordem que a recepção
    /// conhece, com as pendências de outro dia no topo): cada reserva entra antes do primeiro horário que começa DEPOIS
    /// dela — no mesmo horário, o paciente vem antes —, e as que sobram vão ao fim.
    /// </summary>
    public static IReadOnlyList<ItemDaAgendaDoDia> Mesclar(IEnumerable<LinhaAgendaDoDia> linhas, IEnumerable<ReservaDeHorario> reservas)
    {
        var fila = new Queue<ReservaDeHorario>(reservas.OrderBy(r => r.DataHora));
        var saida = new List<ItemDaAgendaDoDia>();
        foreach (var l in linhas)
        {
            while (fila.Count > 0 && fila.Peek().DataHora < l.DataHora) saida.Add(new(null, fila.Dequeue()));
            saida.Add(new(l, null));
        }
        while (fila.Count > 0) saida.Add(new(null, fila.Dequeue()));
        return saida;
    }
}
