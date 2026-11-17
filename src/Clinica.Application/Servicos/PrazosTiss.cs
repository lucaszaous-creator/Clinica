namespace Clinica.Application.Servicos;

/// <summary>
/// Os PRAZOS da operadora para receber o faturamento (nov/2026, Amil): até quantos dias depois do
/// atendimento ela aceita a guia e em que janela do mês aceita o envio. É AVISO, nunca bloqueio — a
/// decisão de enviar é de quem fatura, e o sistema não conhece feriados nem acordos da clínica.
/// Os números moram no catálogo (<see cref="OperadoraTiss.PrazoDeCobrancaDias"/>,
/// <see cref="OperadoraTiss.JanelaDeEnvio"/>); a regra e as frases, só aqui.
/// </summary>
public static class PrazosTiss
{
    /// <summary>A partir de quantos dias antes do fim do prazo o aviso aparece.</summary>
    public const int AvisarComDias = 15;

    /// <summary>O aviso do prazo de cobrança de UMA guia (nulo = nada a dizer).</summary>
    public static string? PrazoDeCobranca(OperadoraTiss? operadora, DateOnly dataAtendimento, DateOnly hoje)
    {
        if (operadora?.PrazoDeCobrancaDias is not { } prazo || dataAtendimento == default) return null;
        var limite = dataAtendimento.AddDays(prazo);
        var faltam = limite.DayNumber - hoje.DayNumber;
        if (faltam < 0)
            return $"Passou o prazo de cobrança da {operadora.Nome}: {prazo} dias do atendimento, até {limite:dd/MM/yyyy}. A guia pode ser bloqueada (glosa 1701).";
        if (faltam <= AvisarComDias)
            return faltam == 0
                ? $"Hoje é o último dia do prazo de cobrança da {operadora.Nome} ({prazo} dias do atendimento)."
                : $"Faltam {faltam} dia(s) para o prazo de cobrança da {operadora.Nome}: {prazo} dias do atendimento, até {limite:dd/MM/yyyy}.";
        return null;
    }

    /// <summary>O aviso da janela de envio do mês (nulo = dentro dela, ou a operadora não tem janela).</summary>
    public static string? JanelaDeEnvio(OperadoraTiss? operadora, DateTime agora)
    {
        if (operadora?.JanelaDeEnvio is not { } janela) return null;
        var hoje = DateOnly.FromDateTime(agora);
        var diaUtil = DiaUtilDoMes(hoje);
        var hora = TimeOnly.FromDateTime(agora);
        var dentro = diaUtil is { } n && n >= janela.PrimeiroDiaUtil && n <= janela.UltimoDiaUtil
                     && hora >= janela.Abre && hora < janela.Fecha;
        if (dentro) return null;
        return $"A {operadora.Nome} só aceita o envio do {janela.PrimeiroDiaUtil}º ao {janela.UltimoDiaUtil}º dia útil do mês, "
               + $"das {janela.Abre:HH\\:mm} às {janela.Fecha:HH\\:mm}"
               + (diaUtil is { } d ? $" (hoje é o {d}º dia útil)." : " (hoje não é dia útil).")
               + " Fora dela, o envio pode ser recusado. Dia útil aqui é de segunda a sexta: confira os feriados.";
    }

    /// <summary>Os avisos de uma guia na baixa: o prazo de cobrança e a janela do mês.</summary>
    public static IReadOnlyList<string> DaGuia(OperadoraTiss? operadora, DateOnly dataAtendimento, DateTime agora)
        => new[] { PrazoDeCobranca(operadora, dataAtendimento, DateOnly.FromDateTime(agora)), JanelaDeEnvio(operadora, agora) }
            .OfType<string>().ToList();

    /// <summary>Que dia útil do mês é <paramref name="dia"/> (segunda a sexta), ou nulo no fim de semana.</summary>
    public static int? DiaUtilDoMes(DateOnly dia)
    {
        if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return null;
        var n = 0;
        for (var d = new DateOnly(dia.Year, dia.Month, 1); d <= dia; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        return n;
    }
}
