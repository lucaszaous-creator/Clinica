using System.Globalization;

namespace Clinica.Domain;

/// <summary>
/// Uma faixa de atendimento num dia: [início, fim). "08:00–12:00".
/// </summary>
public readonly record struct FaixaDeAtendimento(TimeOnly Inicio, TimeOnly Fim)
{
    public int Minutos => (int)(Fim - Inicio).TotalMinutes;

    public override string ToString() => $"{Inicio:HH\\:mm}–{Fim:HH\\:mm}";
}

/// <summary>
/// O texto compacto dos horários por dia (out/2026): <c>1=08:00-12:00,14:00-18:00;4=14:00-19:00</c>.
/// Cultura INVARIANTE nas duas direções — o texto é gravado e lido por postos com culturas
/// diferentes. Ler é tolerante (faixa ilegível some, nunca derruba a agenda); escrever é
/// estrito (<see cref="Validar"/> recusa antes).
/// </summary>
public static class HorariosDeAtendimento
{
    public static IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>> Ler(string? texto)
    {
        var dias = new Dictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>();
        if (string.IsNullOrWhiteSpace(texto)) return dias;
        foreach (var bloco in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = bloco.Split('=', 2);
            if (partes.Length != 2 || !int.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                || n is < 0 or > 6) continue;
            var faixas = new List<FaixaDeAtendimento>();
            foreach (var f in partes[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var hs = f.Split('-', 2);
                if (hs.Length == 2
                    && TimeOnly.TryParseExact(hs[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var de)
                    && TimeOnly.TryParseExact(hs[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ate)
                    && ate > de)
                    faixas.Add(new FaixaDeAtendimento(de, ate));
            }
            if (faixas.Count > 0) dias[(DayOfWeek)n] = faixas.OrderBy(x => x.Inicio).ToList();
        }
        return dias;
    }

    public static string Escrever(IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>> semana)
        => string.Join(";", semana.Where(d => d.Value.Count > 0).OrderBy(d => (int)d.Key).Select(d =>
            $"{(int)d.Key}=" + string.Join(",", d.Value.OrderBy(f => f.Inicio).Select(f =>
                f.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture) + "-" + f.Fim.ToString("HH:mm", CultureInfo.InvariantCulture)))));

    /// <summary>
    /// Recusa o que não é horário: fim antes do início e faixas que se sobrepõem no mesmo dia
    /// (sobreposição contaria a mesma hora duas vezes na ocupação). Devolve a frase, ou nulo.
    /// </summary>
    public static string? Validar(IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>> semana)
    {
        foreach (var (dia, faixas) in semana)
        {
            var nome = Entities.Profissional.AbreviarDia(dia);
            if (faixas.Any(f => f.Fim <= f.Inicio))
                return $"Em {nome}, o fim do horário precisa ser depois do início.";
            var ordem = faixas.OrderBy(f => f.Inicio).ToList();
            for (var i = 1; i < ordem.Count; i++)
                if (ordem[i].Inicio < ordem[i - 1].Fim)
                    return $"Em {nome}, as faixas {ordem[i - 1]} e {ordem[i]} se sobrepõem.";
        }
        return null;
    }
}
