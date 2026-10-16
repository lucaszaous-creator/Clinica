using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Formatacao;

/// <summary>O que uma célula da matriz da semana diz do profissional naquele dia.</summary>
public enum EstadoCelulaSemana
{
    /// <summary>Ele atende neste dia: número de atendimentos e barra da ocupação do horário.</summary>
    Atende,
    /// <summary>O horário dele não tem faixa neste dia (cinza).</summary>
    NaoAtende,
    /// <summary>A agenda dele está fechada o dia inteiro (hachura).</summary>
    Fechado,
    /// <summary>Sem horário cadastrado — neutro, NÃO é erro: a ocupação dele não é medida.</summary>
    SemHorario
}

/// <summary>Uma célula da matriz profissional × dia.</summary>
public sealed record CelulaSemanaOcupacao(DateOnly Dia, int? ProfissionalId, EstadoCelulaSemana Estado, int Atendimentos,
    decimal? Percentual, string Dica)
{
    public double Fracao => VisaoGeralDaAgendaNaTela.Fracao(Percentual);

    /// <summary>"3 atend." / "—" — o número grande da célula.</summary>
    public string Numero => Atendimentos.ToString(CultureInfo.InvariantCulture);

    /// <summary>A linha de baixo da célula: a ocupação, ou o porquê de não haver ocupação.</summary>
    public string Rotulo => Estado switch
    {
        EstadoCelulaSemana.NaoAtende => "não atende",
        EstadoCelulaSemana.Fechado => "agenda fechada",
        EstadoCelulaSemana.SemHorario => "sem horário",
        _ => Percentual is null ? "—" : $"{VisaoGeralDaAgendaNaTela.Percentual(Percentual)} ocupado"
    };

    /// <summary>Mostra a barra de ocupação: só onde há horário e ele atende.</summary>
    public bool TemBarra => Estado == EstadoCelulaSemana.Atende && Percentual is not null;
}

/// <summary>A cabeça de uma coluna da matriz (o dia).</summary>
public sealed record DiaDaMatriz(DateOnly Dia, string Titulo, bool EhHoje);

/// <summary>Uma linha da matriz: o profissional, o total dele na semana e as células.</summary>
public sealed record LinhaMatrizSemana(int Id, string Nome, string Horario, bool SemHorario, int Atendimentos,
    decimal? Percentual, IReadOnlyList<CelulaSemanaOcupacao> Celulas)
{
    public string PercentualTexto => Percentual is null ? (SemHorario ? "sem horário" : "—") : VisaoGeralDaAgendaNaTela.Percentual(Percentual);
    public double Fracao => VisaoGeralDaAgendaNaTela.Fracao(Percentual);
}

/// <summary>A semana inteira: os dias (colunas), uma linha por profissional e a linha de total por dia.</summary>
public sealed record MatrizSemanaOcupacao(IReadOnlyList<DiaDaMatriz> Dias, IReadOnlyList<LinhaMatrizSemana> Linhas,
    IReadOnlyList<CelulaSemanaOcupacao> Totais);

/// <summary>Um bloco na linha do tempo: uma faixa do horário (fundo) ou um atendimento (por cima).</summary>
public sealed record BlocoLinhaDoTempo(double Esquerda, double Largura, string Texto, string Dica, HorarioOcupacao? Horario)
{
    public bool Inativo => Horario is { Ocupa: false };
}

/// <summary>Um profissional no modo DIA: as faixas do horário e os atendimentos por cima.</summary>
public sealed record LinhaDoTempoProfissional(int? Id, string Nome, string Horario, string? Estado,
    IReadOnlyList<BlocoLinhaDoTempo> Faixas, IReadOnlyList<BlocoLinhaDoTempo> Atendimentos, int Total);

/// <summary>A régua das horas e uma linha por profissional.</summary>
public sealed record LinhaDoTempoDoDia(IReadOnlyList<(string Rotulo, double Posicao)> Marcas,
    IReadOnlyList<LinhaDoTempoProfissional> Linhas, double? Agora);

/// <summary>Os quatro números do topo da Visão geral.</summary>
public sealed record IndicadoresVisaoGeral(string Atendimentos, string AtendimentosNota, string Ocupacao, string OcupacaoNota,
    double? OcupacaoFracao, string HorasLivres, string HorasLivresNota, string FaltasECancelamentos, string FaltasNota);

/// <summary>
/// O DESENHO da Visão geral (out/2026, redesenho da Recepção — <c>docs/recepcao-ux-out2026.md</c> §4):
/// a matriz profissional × dia da semana, a linha do tempo do dia, o calor do mês, os quatro
/// números do topo e a linha discreta de quem está sem horário. Tudo puro e testado — o Razor só desenha.
/// </summary>
public static class VisaoGeralDaAgendaDesenho
{
    private static readonly string[] Semanas = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];
    private static readonly CultureInfo PtBr = VisaoGeralDaAgendaNaTela.PtBr;

    // ==================== topo ====================

    /// <summary>
    /// Atendimentos, ocupação "de N profissionais com horário", horas livres (com o bloqueado
    /// na nota) e faltas + cancelamentos. Sem ninguém com horário a ocupação é "—" e diz por quê.
    /// </summary>
    public static IndicadoresVisaoGeral Indicadores(OcupacaoAgenda d)
    {
        var r = d.Resumo;
        var comHorario = d.Profissionais.Count(p => !p.SemHorario);
        var total = d.Profissionais.Count;
        var ocupacaoNota = total == 0 ? "Nenhum profissional no período"
            : comHorario == 0 ? "Ninguém tem horário de atendimento cadastrado"
            : $"de {comHorario} {(comHorario == 1 ? "profissional" : "profissionais")} com horário"
              + (comHorario < total ? $" (de {total})" : "");
        var percentual = MedidaComHorario(d);
        var faltas = r.Cancelados + r.Faltas;
        return new IndicadoresVisaoGeral(
            r.Atendimentos.ToString(PtBr), "Cancelamentos e faltas ficam de fora",
            percentual.Percentual is { } p ? VisaoGeralDaAgendaNaTela.Percentual(p) : "—", ocupacaoNota,
            percentual.Percentual is null ? null : VisaoGeralDaAgendaNaTela.Fracao(percentual.Percentual),
            percentual.MinutosDisponiveis > 0 ? VisaoGeralDaAgendaNaTela.Tempo(percentual.MinutosLivres) : "—",
            percentual.MinutosDisponiveis > 0
                ? $"de {VisaoGeralDaAgendaNaTela.Tempo(percentual.MinutosDisponiveis)} de horário"
                  + (r.MinutosBloqueados > 0 ? $" · {VisaoGeralDaAgendaNaTela.Tempo(r.MinutosBloqueados)} fechado" : "")
                : "Sem horário de atendimento no período",
            faltas.ToString(PtBr), $"{r.Faltas} {(r.Faltas == 1 ? "falta" : "faltas")} · {r.Cancelados} {(r.Cancelados == 1 ? "cancelamento" : "cancelamentos")}");
    }

    /// <summary>
    /// A ocupação de QUEM TEM HORÁRIO. O resumo do serviço devolve nulo quando alguém não tem
    /// horário (não há como medir a equipe inteira) — aqui se soma só quem tem, e o "de N
    /// profissionais" diz isso. Sem profissional na lista, cai no resumo.
    /// </summary>
    public static MedidaOcupacao MedidaComHorario(OcupacaoAgenda d)
    {
        var com = d.Profissionais.Where(p => !p.SemHorario).Select(p => p.Resumo).ToList();
        if (com.Count == 0) return d.Resumo with { MinutosOcupados = 0, MinutosDisponiveis = 0, SemJornada = 1 };
        return new MedidaOcupacao(com.Sum(x => x.Atendimentos), com.Sum(x => x.Cancelados), com.Sum(x => x.Faltas),
            com.Sum(x => x.MinutosOcupados), com.Sum(x => x.MinutosDisponiveis), com.Sum(x => x.MinutosBloqueados), 0,
            com.Sum(x => x.ForaDaJornada));
    }

    /// <summary>
    /// A linha discreta que substitui os avisos: "Sem horário cadastrado: Bianca, Dr. Marcos".
    /// Nula quando todos têm horário.
    /// </summary>
    public static string? LinhaSemHorario(OcupacaoAgenda d)
    {
        var sem = d.Profissionais.Where(p => p.SemHorario).Select(p => p.Nome).ToList();
        return sem.Count == 0 ? null : $"Sem horário cadastrado: {string.Join(", ", sem)}";
    }

    /// <summary>O primeiro sem horário — quem o "cadastrar" abre no editor.</summary>
    public static int? PrimeiroSemHorario(OcupacaoAgenda d) => d.Profissionais.FirstOrDefault(p => p.SemHorario)?.Id;

    /// <summary>Atendimentos sem profissional e fora do horário — a segunda linha discreta, quando há.</summary>
    public static string? LinhaDeAtencao(OcupacaoAgenda d)
    {
        var partes = new List<string>();
        if (d.Resumo.ForaDaJornada > 0)
            partes.Add($"{d.Resumo.ForaDaJornada} {(d.Resumo.ForaDaJornada == 1 ? "atendimento" : "atendimentos")} fora do horário ou em agenda fechada");
        var semProf = d.Horarios.Count(h => h.Ocupa && h.ProfissionalId is null);
        if (semProf > 0) partes.Add($"{semProf} {(semProf == 1 ? "atendimento" : "atendimentos")} sem profissional");
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    // ==================== semana: a matriz ====================

    /// <summary>
    /// A matriz profissional × dia. O domingo só entra quando alguém atende nele ou tem algo
    /// marcado — a mesma regra das colunas antigas. A linha de total soma o dia inteiro (com
    /// os atendimentos sem profissional).
    /// </summary>
    public static MatrizSemanaOcupacao Matriz(OcupacaoAgenda d, DateOnly hoje)
    {
        var dias = d.Dias.Where(x => x.Dia.DayOfWeek != DayOfWeek.Sunday
                || x.Resumo.Atendimentos > 0
                || d.Profissionais.Any(p => p.Dias.FirstOrDefault(y => y.Dia == x.Dia)?.Faixas is { Count: > 0 }))
            .Select(x => new DiaDaMatriz(x.Dia, $"{Semanas[(int)x.Dia.DayOfWeek]} {x.Dia.Day}", x.Dia == hoje)).ToList();

        var linhas = d.Profissionais.Select(p =>
        {
            var celulas = dias.Select(dia => Celula(p, dia.Dia)).ToList();
            return new LinhaMatrizSemana(p.Id, p.Nome, p.SemHorario ? "horário não cadastrado" : p.Jornada, p.SemHorario,
                p.Resumo.Atendimentos, p.Resumo.Percentual, celulas);
        }).ToList();

        var totais = dias.Select(dia =>
        {
            var medida = d.Dias.First(x => x.Dia == dia.Dia).Resumo;
            var pct = PercentualDoDia(d, dia.Dia);
            return new CelulaSemanaOcupacao(dia.Dia, null, EstadoCelulaSemana.Atende, medida.Atendimentos, pct,
                $"{medida.Atendimentos} {(medida.Atendimentos == 1 ? "atendimento" : "atendimentos")} no dia");
        }).ToList();
        return new MatrizSemanaOcupacao(dias, linhas, totais);
    }

    private static CelulaSemanaOcupacao Celula(ProfissionalOcupacao p, DateOnly dia)
    {
        var x = p.Dias.FirstOrDefault(y => y.Dia == dia);
        var atend = x?.Resumo.Atendimentos ?? 0;
        var nomeDia = dia.ToDateTime(TimeOnly.MinValue).ToString("dddd, d/MM", PtBr);
        var qtd = $"{atend} {(atend == 1 ? "atendimento" : "atendimentos")}";
        if (x?.Fechado is { } motivo)
            return new(dia, p.Id, EstadoCelulaSemana.Fechado, atend, null, $"{p.Nome} · {nomeDia}: {motivo} · {qtd}");
        if (x?.Faixas is { Count: 0 })
            return new(dia, p.Id, EstadoCelulaSemana.NaoAtende, atend, null, $"{p.Nome} não atende {nomeDia} · {qtd}");
        if (p.SemHorario || x is null || x.Resumo.SemJornada > 0)
            return new(dia, p.Id, EstadoCelulaSemana.SemHorario, atend, null, $"{p.Nome} · {nomeDia}: {qtd} · sem horário de atendimento cadastrado");
        var faixas = x.Faixas is { Count: > 0 } f ? string.Join(" e ", f.Select(z => z.ToString())) : "";
        return new(dia, p.Id, EstadoCelulaSemana.Atende, atend, x.Resumo.Percentual,
            $"{p.Nome} · {nomeDia} {faixas}: {qtd} · {VisaoGeralDaAgendaNaTela.Percentual(x.Resumo.Percentual)} ocupado — abrir o dia");
    }

    // ==================== dia: a linha do tempo ====================

    /// <summary>
    /// Uma linha por profissional no dia: as faixas do horário como fundo e os atendimentos
    /// como blocos (em fração da régua, 0 a 1). A régua vai da abertura padrão da grade ao
    /// fechamento, esticada pelo que existe no dia — como a grade da agenda.
    /// </summary>
    public static LinhaDoTempoDoDia LinhaDoTempo(OcupacaoAgenda d, DateOnly dia, DateTime agora)
    {
        var inicioDia = dia.ToDateTime(TimeOnly.MinValue);
        var fimDia = inicioDia.AddDays(1);
        var horarios = d.Horarios.Where(h => h.Inicio < fimDia && h.Fim > inicioDia).ToList();
        var abre = Agendamento.AberturaPadraoGrade.ToTimeSpan();
        var fecha = Agendamento.FechamentoPadraoGrade.ToTimeSpan();
        foreach (var p in d.Profissionais)
            if (p.Dias.FirstOrDefault(x => x.Dia == dia)?.Faixas is { Count: > 0 } fx)
            {
                if (fx.Min(f => f.Inicio).ToTimeSpan() < abre) abre = fx.Min(f => f.Inicio).ToTimeSpan();
                if (fx.Max(f => f.Fim).ToTimeSpan() > fecha) fecha = fx.Max(f => f.Fim).ToTimeSpan();
            }
        foreach (var h in horarios)
        {
            var i = h.Inicio < inicioDia ? TimeSpan.Zero : h.Inicio.TimeOfDay;
            var f = h.Fim > fimDia ? TimeSpan.FromHours(24) : h.Fim - inicioDia;
            if (i < abre) abre = i;
            if (f > fecha) fecha = f;
        }
        abre = TimeSpan.FromHours(Math.Floor(abre.TotalHours));
        fecha = TimeSpan.FromHours(Math.Min(24, Math.Ceiling(fecha.TotalHours)));
        var total = (fecha - abre).TotalMinutes;
        double Pos(TimeSpan t) => Math.Clamp((t - abre).TotalMinutes / total, 0, 1);
        double PosDt(DateTime t) => Pos(t < inicioDia ? TimeSpan.Zero : t >= fimDia ? TimeSpan.FromHours(24) : t - inicioDia);

        var marcas = new List<(string, double)>();
        for (var h = abre; h <= fecha; h += TimeSpan.FromHours(1))
            marcas.Add(($"{(int)h.TotalHours:00}h", Pos(h)));

        BlocoLinhaDoTempo Bloco(HorarioOcupacao h)
        {
            var e = PosDt(h.Inicio);
            return new(e, Math.Max(0.004, PosDt(h.Fim) - e), $"{h.Inicio:HH:mm} {h.Paciente}",
                $"{h.Inicio:HH:mm}–{h.Fim:HH:mm} · {h.Paciente} · {h.Modalidade} · {h.Situacao}", h);
        }

        var linhas = new List<LinhaDoTempoProfissional>();
        foreach (var p in d.Profissionais)
        {
            var x = p.Dias.FirstOrDefault(y => y.Dia == dia);
            var faixas = (x?.Faixas ?? []).Select(f => new BlocoLinhaDoTempo(Pos(f.Inicio.ToTimeSpan()),
                Pos(f.Fim.ToTimeSpan()) - Pos(f.Inicio.ToTimeSpan()), f.ToString(), $"Horário de atendimento {f}", null)).ToList();
            var estado = x?.Fechado ?? (x?.Faixas is { Count: 0 } ? "não atende neste dia" : p.SemHorario ? "horário não cadastrado" : null);
            var deles = horarios.Where(h => h.ProfissionalId == p.Id).Select(Bloco).ToList();
            linhas.Add(new LinhaDoTempoProfissional(p.Id, p.Nome, p.SemHorario ? "horário não cadastrado"
                : x?.Faixas is { Count: > 0 } fx2 ? string.Join(" e ", fx2.Select(f => f.ToString())) : "não atende",
                estado, faixas, deles, deles.Count(b => !b.Inativo)));
        }
        var semProf = horarios.Where(h => h.ProfissionalId is null).Select(Bloco).ToList();
        if (semProf.Count > 0)
            linhas.Add(new LinhaDoTempoProfissional(null, "Sem profissional", "atendimentos sem dono", null, [], semProf,
                semProf.Count(b => !b.Inativo)));
        double? posAgora = DateOnly.FromDateTime(agora) == dia && agora.TimeOfDay >= abre && agora.TimeOfDay <= fecha
            ? Pos(agora.TimeOfDay) : null;
        return new LinhaDoTempoDoDia(marcas, linhas, posAgora);
    }

    // ==================== mês: o calor ====================

    /// <summary>
    /// A ocupação do DIA medida só sobre quem tem horário (o resumo do serviço fica nulo quando
    /// alguém não tem — e um profissional sem horário não pode apagar o calor do dia inteiro).
    /// </summary>
    public static decimal? PercentualDoDia(OcupacaoAgenda d, DateOnly dia)
    {
        var doDia = d.Profissionais.Select(p => p.Dias.FirstOrDefault(x => x.Dia == dia))
            .Where(x => x is not null && x.Resumo.SemJornada == 0).Select(x => x!.Resumo).ToList();
        var disp = doDia.Sum(x => x.MinutosDisponiveis);
        if (disp == 0) return d.Dias.FirstOrDefault(x => x.Dia == dia)?.Resumo.Percentual;
        return Math.Round(100m * doDia.Sum(x => x.MinutosOcupados) / disp, 1);
    }

    /// <summary>
    /// A intensidade (0 a 4) da cor de um dia no calendário: pela OCUPAÇÃO quando ela é medida;
    /// sem horário cadastrado, pelo número de atendimentos relativo ao dia mais cheio do mês.
    /// </summary>
    public static int Calor(decimal? percentual, int atendimentos, int maisCheio)
    {
        if (percentual is { } p)
            return p <= 0 ? (atendimentos > 0 ? 1 : 0) : p < 25 ? 1 : p < 50 ? 2 : p < 75 ? 3 : 4;
        if (atendimentos == 0 || maisCheio <= 0) return 0;
        var f = (double)atendimentos / maisCheio;
        return f < 0.25 ? 1 : f < 0.5 ? 2 : f < 0.75 ? 3 : 4;
    }

    /// <summary>A frase de baixo da célula do mês.</summary>
    public static string TaxaDoDia(decimal? percentual, int atendimentos)
        => percentual is not null ? $"{VisaoGeralDaAgendaNaTela.Percentual(percentual)} ocupado"
            : atendimentos > 0 ? "sem horário para medir" : "";

    /// <summary>A legenda do calor: o que a cor mede neste mês.</summary>
    public static string LegendaDoCalor(OcupacaoAgenda d)
        => d.Dias.Any(x => x.Resumo.Percentual is not null)
            ? "Cor pela ocupação do horário de atendimento (dias sem horário cadastrado, pelo número de atendimentos)."
            : "Cor pelo número de atendimentos — cadastre os horários de atendimento para ver a ocupação.";
}
