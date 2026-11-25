namespace Clinica.Domain.Regras;

/// <summary>
/// Os FERIADOS NACIONAIS (nov/2026), para a janela de envio das operadoras: o portal da Amil diz que
/// "para os dias não úteis será acrescentado o próximo dia útil", e feriado não é dia útil. O sistema não
/// tinha calendário: esta é a lista fixa, por data, das Leis 662/1949, 6.802/1980, 9.093/1995 e
/// 14.759/2023 (Consciência Negra), mais a Sexta-feira da Paixão, que cai dois dias antes da Páscoa e por
/// isso é calculada. Ficam DE FORA os feriados estaduais e municipais e os pontos facultativos (Carnaval,
/// Corpus Christi): não se sabe se a operadora os conta.
/// </summary>
public static class FeriadosNacionais
{
    /// <summary>Os de data fixa: (mês, dia, nome).</summary>
    public static readonly IReadOnlyList<(int Mes, int Dia, string Nome)> Fixos =
    [
        (1, 1, "Confraternização Universal"), (4, 21, "Tiradentes"), (5, 1, "Dia do Trabalho"),
        (9, 7, "Independência do Brasil"), (10, 12, "Nossa Senhora Aparecida"), (11, 2, "Finados"),
        (11, 15, "Proclamação da República"), (11, 20, "Dia Nacional de Zumbi e da Consciência Negra"),
        (12, 25, "Natal"),
    ];

    /// <summary>O dia é feriado nacional?</summary>
    public static bool Eh(DateOnly dia) => Nome(dia) is not null;

    /// <summary>O nome do feriado nacional, ou nulo.</summary>
    public static string? Nome(DateOnly dia)
    {
        foreach (var (mes, d, nome) in Fixos)
            if (dia.Month == mes && dia.Day == d) return nome;
        return dia == Pascoa(dia.Year).AddDays(-2) ? "Paixão de Cristo" : null;
    }

    /// <summary>O domingo de Páscoa (algoritmo de Meeus/Jones/Butcher, calendário gregoriano).</summary>
    public static DateOnly Pascoa(int ano)
    {
        int a = ano % 19, b = ano / 100, c = ano % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, mes = (h + l - 7 * m + 114) / 31, dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(ano, mes, dia);
    }
}
