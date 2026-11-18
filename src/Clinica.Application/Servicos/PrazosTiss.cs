namespace Clinica.Application.Servicos;

/// <summary>
/// Os PRAZOS da operadora para receber o faturamento (nov/2026, Amil): até quantos dias depois do
/// atendimento ela aceita a guia e em que janela do mês aceita o envio. É AVISO, nunca bloqueio — a
/// decisão de enviar é de quem fatura. Dia útil = segunda a sexta menos os feriados NACIONAIS
/// (<see cref="Domain.Regras.FeriadosNacionais"/>); estaduais, municipais e pontos facultativos não entram.
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
        if (DiasDeEnvio(operadora, agora) is { } dias) return dias;
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
               + " Fora dela, o envio é recusado. Dia útil aqui é de segunda a sexta, sem os feriados nacionais.";
    }

    /// <summary>
    /// O aviso dos dias do mês em que a operadora recebe lotes (Saúde Petrobras: 1 a 20). Só aviso — a
    /// fila não segura o lote. Nulo = dentro dos dias, ou a operadora não os define.
    /// </summary>
    public static string? DiasDeEnvio(OperadoraTiss? operadora, DateTime agora)
    {
        if (operadora?.DiasDeEnvioNoMes is not { } dias) return null;
        var hoje = DateOnly.FromDateTime(agora);
        if (hoje.Day >= dias.PrimeiroDia && hoje.Day <= dias.UltimoDia) return null;
        var proximo = hoje.Day < dias.PrimeiroDia
            ? new DateOnly(hoje.Year, hoje.Month, dias.PrimeiroDia)
            : new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(dias.PrimeiroDia - 1);
        return $"Pelo manual, a {operadora.Nome} recebe lotes do dia {dias.PrimeiroDia} ao dia {dias.UltimoDia} de cada mês "
               + $"(hoje é dia {hoje.Day}; o próximo período abre em {proximo:dd/MM}). O envio não é bloqueado: confira antes de mandar.";
    }

    /// <summary>Os avisos de uma guia na baixa: o prazo de cobrança e a janela do mês.</summary>
    public static IReadOnlyList<string> DaGuia(OperadoraTiss? operadora, DateOnly dataAtendimento, DateTime agora)
        => new[] { AvisoDaGuia(operadora, dataAtendimento, agora)?.Texto, JanelaDeEnvio(operadora, agora) }
            .OfType<string>().ToList();

    /// <summary>
    /// O aviso do prazo de cobrança de UMA guia, e se ele é GRAVE (vermelho): a guia já venceu, ou vence
    /// ANTES da próxima janela de envio — esperar a janela é perdê-la (nov/2026, Amil: a janela vale
    /// também para o web service, recusa 3091). Nulo = nada a dizer.
    /// </summary>
    public static (string Texto, bool Grave)? AvisoDaGuia(OperadoraTiss? operadora, DateOnly dataAtendimento, DateTime agora)
    {
        var hoje = DateOnly.FromDateTime(agora);
        if (operadora?.PrazoDeCobrancaDias is { } prazo && dataAtendimento != default && operadora.JanelaDeEnvio is { } janela
            && !DentroDaJanela(janela, agora))
        {
            var limite = dataAtendimento.AddDays(prazo);
            var abre = ProximaAbertura(janela, agora);
            if (limite >= hoje && limite < DateOnly.FromDateTime(abre))
                return ($"A guia vence em {limite:dd/MM/yyyy} ({prazo} dias do atendimento), ANTES da próxima janela de envio da {operadora.Nome} "
                        + $"({abre:dd/MM 'às' HH:mm}): ela não vai chegar a tempo pelo envio. Fale com a {operadora.Nome} ou cobre pelo portal.", true);
        }
        return PrazoDeCobranca(operadora, dataAtendimento, hoje) is { } aviso
            ? (aviso, aviso.StartsWith("Passou", StringComparison.Ordinal))
            : null;
    }

    /// <summary>
    /// A guia passou do prazo de cobrança E a operadora bloqueia por isso (catálogo: Amil, 1701/405)? Então
    /// ela não entra no lote; para as outras, o prazo é só aviso.
    /// </summary>
    public static bool BloqueadaPeloPrazo(OperadoraTiss? operadora, DateOnly dataAtendimento, DateOnly hoje)
        => operadora is { PrazoDeCobrancaBloqueia: true, PrazoDeCobrancaDias: { } prazo } && dataAtendimento != default
           && dataAtendimento.AddDays(prazo) < hoje;

    public static string FraseBloqueadaPeloPrazo(OperadoraTiss operadora, DateOnly dataAtendimento)
        => $"Passou o prazo de cobrança da {operadora.Nome}: {operadora.PrazoDeCobrancaDias} dias do atendimento, até "
           + $"{dataAtendimento.AddDays(operadora.PrazoDeCobrancaDias ?? 0):dd/MM/yyyy}. A {operadora.Nome} bloqueia a guia (glosa 1701): ela não entra no lote.";

    /// <summary>Agora está dentro da janela de envio (o dia útil e a hora)?</summary>
    public static bool DentroDaJanela(JanelaDeEnvioTiss janela, DateTime agora)
        => DiaUtilDoMes(DateOnly.FromDateTime(agora)) is { } n && n >= janela.PrimeiroDiaUtil && n <= janela.UltimoDiaUtil
           && TimeOnly.FromDateTime(agora) >= janela.Abre && TimeOnly.FromDateTime(agora) < janela.Fecha;

    /// <summary>
    /// Quando a janela de envio abre de novo. Dentro dela, é agora — a não ser que
    /// <paramref name="aPartirDeAmanha"/> (a operadora acabou de recusar por prazo: UMA ida por dia), e aí
    /// é o PRÓXIMO dia útil da janela, ainda neste mês se a janela não acabou, senão o 1º do mês seguinte.
    /// Assim um feriado no meio (que a Amil não conta como dia útil) custa um dia, não o mês. Dia útil =
    /// segunda a sexta, sem os feriados nacionais.
    /// </summary>
    public static DateTime ProximaAbertura(JanelaDeEnvioTiss janela, DateTime agora, bool aPartirDeAmanha = false)
    {
        if (!aPartirDeAmanha && DentroDaJanela(janela, agora)) return agora;
        var hoje = DateOnly.FromDateTime(agora);
        for (var d = aPartirDeAmanha ? hoje.AddDays(1) : hoje; d <= hoje.AddDays(70); d = d.AddDays(1))
        {
            if (DiaUtilDoMes(d) is not { } n || n < janela.PrimeiroDiaUtil || n > janela.UltimoDiaUtil) continue;
            var abre = d.ToDateTime(janela.Abre);
            if (d == hoje && TimeOnly.FromDateTime(agora) >= janela.Fecha) continue;
            return d == hoje && TimeOnly.FromDateTime(agora) >= janela.Abre ? agora : abre;
        }
        throw new InvalidOperationException("A janela de envio do catálogo não abre em nenhum dia dos próximos dois meses.");
    }

    /// <summary>
    /// Que dia útil do mês é <paramref name="dia"/> (segunda a sexta, menos os feriados nacionais —
    /// <see cref="Domain.Regras.FeriadosNacionais"/>), ou nulo quando ele mesmo não é dia útil.
    /// </summary>
    public static int? DiaUtilDoMes(DateOnly dia)
    {
        if (!EhDiaUtil(dia)) return null;
        var n = 0;
        for (var d = new DateOnly(dia.Year, dia.Month, 1); d <= dia; d = d.AddDays(1))
            if (EhDiaUtil(d)) n++;
        return n;
    }

    private static bool EhDiaUtil(DateOnly d)
        => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Domain.Regras.FeriadosNacionais.Eh(d);
}
