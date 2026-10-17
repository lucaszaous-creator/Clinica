using Clinica.Domain;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Um trecho livre na jornada de um profissional — o "+ 14:00 livre" da semana.</summary>
public sealed record VaoLivre(DateOnly Dia, DateTime Inicio, DateTime Ate)
{
    public string Rotulo => $"+ {Inicio:HH:mm} livre";
    public string Dica => $"Livre das {Inicio:HH:mm} às {Ate:HH:mm} — clique para marcar";
}

/// <summary>Um dia em que a agenda está fechada (férias, feriado, folga), com o motivo escrito.</summary>
public sealed record DiaFechado(DateOnly Dia, string Motivo);

/// <summary>
/// Os vãos livres e os dias fechados da Visão geral da agenda (out/2026, mockup aprovado
/// "2 · Agenda — Visão geral"): a semana por profissional, com o horário livre CLICÁVEL.
/// A grade saiu da navegação, e era nela que o vazio tinha tamanho; aqui o vazio vira um
/// botão que diz a hora.
///
/// É PURA, pela regra da casa: o que a tela afirma mora onde o <c>dotnet test</c> alcança.
/// As regras que decidem:
///
/// - **Só com jornada DECLARADA (horas).** Sem ela não há como dizer o que está livre, e
///   presumir 7h–20h ofereceria vaga às 19h a quem sai às 17h. A tela diz que falta a jornada.
/// - **Trechos contíguos viram UM vão** (a hora em que ele começa): uma tarde livre não é
///   oito botões de meia hora — é "a tarde está livre a partir das 14:00".
/// - **Colide o que OCUPA agenda** (cancelado e falta devolvem o horário), e o bloqueio
///   que alcança o profissional fecha o trecho.
/// - **Nada no passado**: dia anterior a hoje não tem vão, e hoje começa agora.
/// - **Dia fechado é o dia cuja jornada inteira está bloqueada** — o motivo vai escrito.
/// </summary>
public static class VaosDaAgenda
{
    public static IReadOnlyList<VaoLivre> Calcular(
        Profissional profissional,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyList<HorarioOcupacao> horarios,
        IReadOnlyList<BloqueioAgenda> bloqueios,
        DateTime agora,
        int passoMinutos = Agendamento.DuracaoPadraoMinutos)
    {
        if (!profissional.JornadaDeclarada) return [];
        if (passoMinutos <= 0) passoMinutos = Agendamento.DuracaoPadraoMinutos;

        var dele = horarios.Where(h => h.Ocupa && h.ProfissionalId == profissional.Id).ToList();
        var fechamentos = bloqueios.Where(b => b.AlcancaRecurso(profissional.Id, null)).ToList();
        var vaos = new List<VaoLivre>();

        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
        {
            if (dia < DateOnly.FromDateTime(agora)) continue;
            // Vão livre só existe dentro de horário DECLARADO: sem faixa no dia não há o que medir.
            foreach (var faixa in profissional.FaixasEm(dia.DayOfWeek) ?? [])
            {
                var abre = dia.ToDateTime(faixa.Inicio);
                var fecha = dia.ToDateTime(faixa.Fim);
                DateTime? comeco = null;
                var fimLivre = abre;
                for (var t = abre; t.AddMinutes(passoMinutos) <= fecha; t = t.AddMinutes(passoMinutos))
                {
                    var fimTrecho = t.AddMinutes(passoMinutos);
                    var livre = fimTrecho > agora
                                && !dele.Any(h => h.Inicio < fimTrecho && h.Fim > t)
                                && !fechamentos.Any(b => b.ColideCom(t, fimTrecho));
                    if (livre) { comeco ??= t; fimLivre = fimTrecho; }
                    else if (comeco is { } c) { vaos.Add(new VaoLivre(dia, c, t)); comeco = null; }
                }
                if (comeco is { } aberto) vaos.Add(new VaoLivre(dia, aberto, fimLivre));
            }
        }
        return vaos;
    }

    /// <summary>
    /// Os dias inteiramente fechados. Com profissional, é a jornada DELE que precisa estar
    /// toda bloqueada; sem profissional (toda a equipe), só conta o fechamento da CLÍNICA —
    /// as férias de um não fecham a agenda dos outros.
    /// </summary>
    public static IReadOnlyList<DiaFechado> Fechados(
        Profissional? profissional,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyList<BloqueioAgenda> bloqueios)
    {
        var dias = new List<DiaFechado>();
        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
        {
            var limites = profissional?.LimitesEm(dia.DayOfWeek);
            var abre = dia.ToDateTime(limites?.Abre ?? Agendamento.AberturaPadraoGrade);
            var fecha = dia.ToDateTime(limites?.Fecha ?? Agendamento.FechamentoPadraoGrade);
            if (fecha <= abre) continue;
            var que = profissional is null
                ? bloqueios.Where(b => b.DaClinica)
                : bloqueios.Where(b => b.AlcancaRecurso(profissional.Id, null));
            var cobre = que.FirstOrDefault(b => b.Inicio <= abre && b.Fim >= fecha);
            if (cobre is not null)
                dias.Add(new DiaFechado(dia, string.IsNullOrWhiteSpace(cobre.Motivo) ? "Agenda fechada" : $"Agenda fechada · {cobre.Motivo.Trim()}"));
        }
        return dias;
    }
}
