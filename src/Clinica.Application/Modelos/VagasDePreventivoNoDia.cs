using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// A "agenda paralela" de preventivo de um profissional num dia (out/2026): quantas coletas
/// já estão marcadas e quantas cabem. É a mesma conta para a recusa na marcação
/// (<c>AgendaService</c>), para o aviso do formulário e para o mapa de ocupação — três
/// leitores, uma conta só.
/// </summary>
public sealed record VagasDePreventivoNoDia(DateOnly Dia, int Marcados, int Limite)
{
    /// <summary>Quantas ainda cabem (nunca negativo: um teto reduzido depois não vira "-2").</summary>
    public int Restantes => Math.Max(0, Limite - Marcados);

    /// <summary>Não cabe mais nenhuma — a marcação com preventivo é recusada.</summary>
    public bool Lotado => Marcados >= Limite;

    /// <summary>
    /// Conta, por dia do período, os horários do profissional que levam coleta e ainda
    /// ocupam a agenda. <paramref name="ignorarAgendamentoId"/> é o horário sendo
    /// remarcado: ele não disputa a vaga consigo mesmo.
    /// </summary>
    public static IReadOnlyList<VagasDePreventivoNoDia> Contar(
        Profissional profissional, IEnumerable<Agendamento> horarios, DateOnly de, DateOnly ate,
        int? ignorarAgendamentoId = null)
    {
        var porDia = horarios
            .Where(a => a.ProfissionalId == profissional.Id && a.ColetaPreventivo && a.OcupaAgenda
                        && a.Id != ignorarAgendamentoId)
            .GroupBy(a => DateOnly.FromDateTime(a.DataHora))
            .ToDictionary(g => g.Key, g => g.Count());

        var limite = Math.Max(0, profissional.LimitePreventivoEfetivo);
        var dias = new List<VagasDePreventivoNoDia>();
        for (var d = de; d <= ate; d = d.AddDays(1))
            dias.Add(new VagasDePreventivoNoDia(d, porDia.GetValueOrDefault(d), limite));
        return dias;
    }
}

/// <summary>
/// O que o formulário de marcação diz sobre a coleta de preventivo (out/2026). A frase mora
/// aqui, e não na tela, porque é ela que a secretária repete à paciente — e a recusa do
/// <c>AgendaService</c> diz a mesma conta.
/// </summary>
public static class PreventivoNaTela
{
    /// <summary>"25/11: 3 de 5" — o selo ao lado da chave.</summary>
    public static string Selo(VagasDePreventivoNoDia v) => $"{v.Dia.Day:00}/{v.Dia.Month:00}: {v.Marcados} de {v.Limite}";

    /// <summary>"3 vagas", "1 vaga", "lotado".</summary>
    public static string Vagas(VagasDePreventivoNoDia v)
        => v.Restantes switch { 0 => "lotado", 1 => "1 vaga", var n => $"{n} vagas" };

    /// <summary>A frase do dia que não cabe mais coleta.</summary>
    public static string Lotado(VagasDePreventivoNoDia v)
        => v.Limite == 0
            ? "Este profissional não faz coleta de preventivo."
            : $"O dia {v.Dia.Day:00}/{v.Dia.Month:00} já tem os {v.Limite} preventivos.";

    private static readonly string[] DiasCurtos = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    /// <summary>"qui 26/11 · 3 vagas" — o botão de outro dia, em pt-BR fixo (não depende da máquina).</summary>
    public static string Dia(VagasDePreventivoNoDia v)
        => $"{DiasCurtos[(int)v.Dia.DayOfWeek]} {v.Dia.Day:00}/{v.Dia.Month:00} · {Vagas(v)}";

    /// <summary>
    /// Os próximos dias, DEPOIS de <paramref name="dia"/>, em que o profissional atende e ainda
    /// cabe coleta — o que a secretária oferece quando o dia pedido lotou.
    /// </summary>
    public static IReadOnlyList<VagasDePreventivoNoDia> ProximosComVaga(
        Profissional profissional, IEnumerable<VagasDePreventivoNoDia> dias, DateOnly dia, int quantos = 3)
        => dias.Where(v => v.Dia > dia && v.Restantes > 0 && BuscaDeVagas.AtendeNoDia(profissional, v.Dia.DayOfWeek))
               .OrderBy(v => v.Dia).Take(quantos).ToList();
}
