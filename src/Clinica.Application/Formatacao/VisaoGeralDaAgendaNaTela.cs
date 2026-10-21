using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;

namespace Clinica.Application.Formatacao;

/// <summary>O período da Visão geral da agenda.</summary>
public enum ModoVisaoAgenda { Dia, Semana, Mes }

/// <summary>Um horário na lista do modo DIA. Cancelado e falta continuam, apagados — a regra da folha do dia.</summary>
public sealed record LinhaHorarioOcupacao(HorarioOcupacao Horario, string Inicio, string Ate)
{
    /// <summary>"No local · chegou às 14:40", "Em atendimento", "Marcado".</summary>
    public string Situacao => string.IsNullOrEmpty(Horario.SituacaoDoDia)
        ? Horario.Situacao
        : Horario.ChegadaEm is { } c && Horario.Etapa == Clinica.Domain.Entities.EtapaFila.Chegou
            ? $"{Horario.SituacaoDoDia} · chegou às {c:HH\\:mm}"
            : Horario.SituacaoDoDia;

    public bool Inativo => !Horario.Ocupa;
}

/// <summary>A prévia de um horário dentro do cartão do dia, na semana.</summary>
public sealed record PreviaHorarioOcupacao(string Hora, string Paciente, string Profissional);

/// <summary>Um dia no calendário do mês (ou célula vazia antes do dia 1).</summary>
public sealed record CartaoDiaOcupacao(DateOnly Dia, MedidaOcupacao Resumo, string Rotulo, bool Selecionado, bool Vazio,
    IReadOnlyList<PreviaHorarioOcupacao> Previas)
{
    public int Atendimentos => Resumo.Atendimentos;
    public string TotalTexto => Resumo.Atendimentos == 1 ? "atendimento" : "atendimentos";
    public double Fracao => VisaoGeralDaAgendaNaTela.Fracao(Resumo.Percentual);
    public bool SemBase => Resumo.Percentual is null;
    public string Taxa => Resumo.Percentual is not null ? $"{VisaoGeralDaAgendaNaTela.Percentual(Resumo.Percentual)} ocupado"
        : Resumo.SemJornada > 0 ? "Sem horário cadastrado" : "Ninguém atende neste dia";
    public bool TemMais => Resumo.Atendimentos > 3;
    public string Mais => TemMais ? $"+{Resumo.Atendimentos - 3} atendimentos" : "";
    public string Acessivel => $"{Dia.ToDateTime(TimeOnly.MinValue).ToString("dddd, d 'de' MMMM", VisaoGeralDaAgendaNaTela.PtBr)}, "
        + $"{Resumo.Atendimentos} atendimentos, {VisaoGeralDaAgendaNaTela.Percentual(Resumo.Percentual)} de ocupação";
}

/// <summary>Um item da coluna do dia na SEMANA: um atendimento ou um VÃO livre que marca ali.</summary>
public sealed record ItemSemanaOcupacao(DateTime Inicio, HorarioOcupacao? Horario, VaoLivre? Vao)
{
    public string Hora => Inicio.ToString("HH:mm", VisaoGeralDaAgendaNaTela.PtBr);
    public bool EhVao => Vao is not null;
    public bool Inativo => Horario is { Ocupa: false };
    public string Dica => Vao?.Dica ?? $"{Horario?.Paciente} · {Horario?.Modalidade} · {Horario?.Profissional} · {Horario?.Situacao}";
}

/// <summary>Uma coluna da semana — o dia com os atendimentos e os vãos em ordem de hora.</summary>
public sealed record ColunaSemanaOcupacao(DateOnly Dia, string Titulo, string Subtitulo, bool EhHoje, string? Fechado,
    IReadOnlyList<ItemSemanaOcupacao> Itens);

/// <summary>Uma linha da ocupação por profissional.</summary>
public sealed record LinhaProfissionalOcupacao(ProfissionalOcupacao Profissional)
{
    public int Id => Profissional.Id;
    public string Nome => Profissional.Nome;
    public string Jornada => Profissional.Jornada;
    public int Atendimentos => Profissional.Resumo.Atendimentos;
    public string Percentual => Profissional.Resumo.Percentual is null ? "—" : VisaoGeralDaAgendaNaTela.Percentual(Profissional.Resumo.Percentual);
    public double Fracao => VisaoGeralDaAgendaNaTela.Fracao(Profissional.Resumo.Percentual);
    public string Livres => Profissional.Resumo.SemJornada > 0 ? "Sem horário cadastrado"
        : Profissional.Resumo.MinutosDisponiveis > 0 ? $"{VisaoGeralDaAgendaNaTela.Tempo(Profissional.Resumo.MinutosLivres)} livres"
        : "Não atende no período";
}

/// <summary>
/// O que a "Visão geral da agenda" AFIRMA (out/2026, migrado do <c>OcupacaoAgendaViewModel</c>
/// WPF — onde estas decisões moravam fora do alcance do <c>dotnet test</c>). Os números vêm do
/// <see cref="IOcupacaoAgendaService"/>; aqui ficam o período, as frases dos indicadores, o
/// calendário do mês e as colunas da semana.
/// </summary>
public static class VisaoGeralDaAgendaNaTela
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Semanas = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    // ==================== medidas ====================

    public static string Tempo(int minutos) => $"{minutos / 60}h{(minutos % 60 != 0 ? $" {minutos % 60}min" : "")}";

    public static string Percentual(decimal? v) => v is null ? "Sem cálculo" : $"{v.Value.ToString(PtBr)}%";

    public static double Fracao(decimal? v) => v is null ? 0 : Math.Clamp((double)v.Value / 100d, 0, 1);

    // ==================== período ====================

    /// <summary>Dia = o dia; semana = de segunda a domingo; mês = do dia 1 ao último.</summary>
    public static (DateOnly Inicio, DateOnly Fim) Periodo(ModoVisaoAgenda modo, DateOnly dia)
    {
        if (modo == ModoVisaoAgenda.Dia) return (dia, dia);
        if (modo == ModoVisaoAgenda.Semana)
        {
            var segunda = dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
            return (segunda, segunda.AddDays(6));
        }
        var primeiro = new DateOnly(dia.Year, dia.Month, 1);
        return (primeiro, primeiro.AddMonths(1).AddDays(-1));
    }

    /// <summary>‹ / › — um período para trás ou para a frente. Nulo fora de 1901–2199.</summary>
    public static DateOnly? Andar(ModoVisaoAgenda modo, DateOnly dia, int passo)
    {
        var destino = modo == ModoVisaoAgenda.Mes ? dia.AddMonths(passo)
            : dia.AddDays(passo * (modo == ModoVisaoAgenda.Semana ? 7 : 1));
        return destino.Year is < 1901 or > 2199 ? null : destino;
    }

    /// <summary>"12 a 17 de outubro" — o período do navegador.</summary>
    public static string PeriodoCurto(ModoVisaoAgenda modo, DateOnly dia, OcupacaoAgenda? d, DateOnly? ultimoDiaDaSemana)
    {
        if (d is null) return TituloPeriodo(modo, dia, null);
        var ini = d.Inicio.ToDateTime(TimeOnly.MinValue);
        if (modo == ModoVisaoAgenda.Dia) return ini.ToString("ddd, d 'de' MMMM", PtBr);
        if (modo == ModoVisaoAgenda.Mes) return Capitalizar(ini.ToString("MMMM 'de' yyyy", PtBr));
        var ultimo = ultimoDiaDaSemana ?? d.Fim;
        var fim = ultimo.ToDateTime(TimeOnly.MinValue);
        return d.Inicio.Month == ultimo.Month
            ? $"{d.Inicio.Day} a {ultimo.Day} de {fim.ToString("MMMM", PtBr)}"
            : $"{ini.ToString("d MMM", PtBr)} a {fim.ToString("d MMM", PtBr)}";
    }

    public static string TituloPeriodo(ModoVisaoAgenda modo, DateOnly dia, OcupacaoAgenda? d)
    {
        if (d is null) return dia.ToDateTime(TimeOnly.MinValue).ToString("d 'de' MMMM", PtBr);
        var ini = d.Inicio.ToDateTime(TimeOnly.MinValue);
        var fim = d.Fim.ToDateTime(TimeOnly.MinValue);
        return modo switch
        {
            ModoVisaoAgenda.Mes => Capitalizar(ini.ToString("MMMM 'de' yyyy", PtBr)),
            ModoVisaoAgenda.Dia => Capitalizar(ini.ToString("dddd, d 'de' MMMM 'de' yyyy", PtBr)),
            _ => $"{ini.ToString("d MMM", PtBr)} — {fim.ToString("d MMM yyyy", PtBr)}"
        };
    }

    public static string TituloCalendario(ModoVisaoAgenda modo) => modo switch
    {
        ModoVisaoAgenda.Dia => "Atendimentos do dia",
        ModoVisaoAgenda.Semana => "Distribuição na semana",
        _ => "Movimento do mês"
    };

    public static string SubtituloCalendario(ModoVisaoAgenda modo) => modo == ModoVisaoAgenda.Dia
        ? "Horários em ordem de atendimento."
        : "Selecione um dia para consultar todos os horários.";

    // ==================== indicadores ====================

    public static string OcupacaoTexto(MedidaOcupacao? r) => r?.Percentual is null ? "—" : Percentual(r.Percentual);

    public static string OcupacaoDetalhe(MedidaOcupacao? r) => r is null ? ""
        : r.SemJornada > 0 ? $"{r.SemJornada} sem horário de atendimento cadastrado"
        : r.MinutosDisponiveis > 0 ? $"{Tempo(r.MinutosOcupados)} de {Tempo(r.MinutosDisponiveis)} disponíveis"
        : "Ninguém atende neste período";

    public static string LivreTexto(MedidaOcupacao? r) => r is null ? "—" : r.SemJornada > 0 ? "—" : Tempo(r.MinutosLivres);

    public static string LivreDetalhe(MedidaOcupacao? r) => r?.SemJornada > 0 ? "Cadastre os horários de atendimento para calcular" : "Somado entre os profissionais";

    public static string BloqueadoTexto(MedidaOcupacao? r) => r is null ? "—" : Tempo(r.MinutosBloqueados);

    public static string BloqueadoDetalhe(MedidaOcupacao? r) => r is null ? "" : $"{r.Cancelados} cancelados · {r.Faltas} faltas";

    /// <summary>Quem não tem jornada completa e os atendimentos sem profissional — nulo quando não há o que dizer.</summary>
    public static string? TextoSemJornada(OcupacaoAgenda? d)
    {
        if (d is null || d.Resumo.SemJornada == 0) return null;
        var incompletos = d.Profissionais.Where(p => p.Resumo.SemJornada > 0).Select(p => p.Nome).ToList();
        var texto = incompletos.Count > 0 ? $"Sem horário de atendimento cadastrado: {string.Join(", ", incompletos)}. " : "";
        if (d.Horarios.Any(h => h.Ocupa && h.ProfissionalId is null)) texto += "Há atendimentos sem profissional vinculado. ";
        return texto + "Os atendimentos continuam visíveis.";
    }

    public static string? TextoForaDaJornada(MedidaOcupacao? r) => r is not { ForaDaJornada: > 0 } ? null
        : $"{r.ForaDaJornada} atendimento(s) fora do horário de atendimento ou sobre um bloqueio. Confira a grade antes de agendar.";

    /// <summary>A dica da semana: sem profissional não há vaga a mostrar; sem jornada, também não.</summary>
    public static string DicaDaSemana(bool filtrando, OcupacaoAgenda? d) => !filtrando
        ? "Escolha um profissional para ver os horários livres"
        : d?.SemJornadaParaVagas == true ? "Cadastre o horário de atendimento do profissional para ver os horários livres"
        : "Clique numa vaga para marcar ali";

    // ==================== listas ====================

    public static IReadOnlyList<LinhaHorarioOcupacao> HorariosDoDia(OcupacaoAgenda d)
        => d.Horarios.Select(h => new LinhaHorarioOcupacao(h, h.Inicio.ToString("HH:mm", PtBr),
            $"até {h.Fim.ToString("HH:mm", PtBr)}" + (h.Fim.Date != h.Inicio.Date ? " · dia seguinte" : ""))).ToList();


    /// <summary>
    /// O calendário do MÊS: células vazias antes do dia 1 (para ele cair na coluna da semana
    /// certa — a semana começa na segunda) e um cartão por dia.
    /// </summary>
    public static IReadOnlyList<CartaoDiaOcupacao> Calendario(OcupacaoAgenda d, DateOnly selecionado)
    {
        var cartoes = new List<CartaoDiaOcupacao>();
        if (d.Dias.Count == 0) return cartoes;
        var vazios = ((int)d.Dias[0].Dia.DayOfWeek + 6) % 7;
        for (var i = 0; i < vazios; i++)
            cartoes.Add(new CartaoDiaOcupacao(d.Dias[0].Dia.AddDays(i - vazios), d.Dias[0].Resumo, "", false, true, []));
        foreach (var dia in d.Dias)
        {
            var inicioDia = dia.Dia.ToDateTime(TimeOnly.MinValue);
            // O horário que atravessa a meia-noite aparece nos dois dias.
            var previas = d.Horarios.Where(h => DateOnly.FromDateTime(h.Inicio) <= dia.Dia && h.Fim > inicioDia && h.Ocupa)
                .Take(3).Select(h => new PreviaHorarioOcupacao(h.Inicio.ToString("HH:mm", PtBr), h.Paciente, h.Profissional)).ToList();
            cartoes.Add(new CartaoDiaOcupacao(dia.Dia, dia.Resumo, inicioDia.ToString("dd", PtBr), dia.Dia == selecionado, false, previas));
        }
        return cartoes;
    }

    /// <summary>
    /// A semana em colunas — cada dia com TODOS os atendimentos e os vãos livres, em ordem de
    /// hora. O domingo só entra quando tem alguma coisa: a clínica não abre, e uma coluna vazia
    /// toda semana gastaria um sétimo da largura para dizer isso.
    /// </summary>
    public static IReadOnlyList<ColunaSemanaOcupacao> Semana(OcupacaoAgenda d, bool filtrando, DateOnly hoje)
    {
        var colunas = new List<ColunaSemanaOcupacao>();
        foreach (var dia in d.Dias)
        {
            var inicioDia = dia.Dia.ToDateTime(TimeOnly.MinValue);
            var fimDia = inicioDia.AddDays(1);
            var horarios = d.Horarios.Where(h => h.Inicio >= inicioDia && h.Inicio < fimDia)
                .Select(h => new ItemSemanaOcupacao(h.Inicio, h, null));
            var vaos = d.Vagas.Where(v => v.Dia == dia.Dia).Select(v => new ItemSemanaOcupacao(v.Inicio, null, v));
            var itens = horarios.Concat(vaos).OrderBy(i => i.Inicio).ThenBy(i => i.EhVao).ToList();
            var fechado = d.Fechados.FirstOrDefault(f => f.Dia == dia.Dia)?.Motivo;
            if (dia.Dia.DayOfWeek == DayOfWeek.Sunday && itens.Count == 0 && fechado is null) continue;

            var nVagas = itens.Count(i => i.EhVao);
            var atend = dia.Resumo.Atendimentos;
            var sub = fechado is not null ? fechado.Replace("Agenda fechada · ", "", StringComparison.Ordinal)
                : $"{atend} {(atend == 1 ? "atendimento" : "atendimentos")}"
                  + (filtrando && !d.SemJornadaParaVagas ? (nVagas == 0 ? " · sem vaga" : $" · {nVagas} {(nVagas == 1 ? "vaga" : "vagas")}") : "");
            colunas.Add(new ColunaSemanaOcupacao(dia.Dia, $"{Semanas[(int)dia.Dia.DayOfWeek]} {dia.Dia.Day}",
                dia.Dia == hoje ? $"hoje · {sub}" : sub, dia.Dia == hoje, fechado, itens));
        }
        return colunas;
    }

    public static IReadOnlyList<LinhaProfissionalOcupacao> Equipe(OcupacaoAgenda d)
        => d.Profissionais.Select(p => new LinhaProfissionalOcupacao(p)).ToList();

    private static string Capitalizar(string t) => t.Length == 0 ? t : char.ToUpper(t[0], PtBr) + t[1..];
}
