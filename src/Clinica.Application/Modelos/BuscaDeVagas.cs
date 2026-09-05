using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Um horário livre para marcar — começo e fim previstos.</summary>
public sealed record Vaga(DateTime Inicio, DateTime Fim)
{
    /// <summary>"qua 16/09 · 14:00–14:30".</summary>
    public string Rotulo => $"{Abreviar(Inicio.DayOfWeek)} {Inicio:dd/MM} · {Inicio:HH:mm}–{Fim:HH:mm}";

    private static string Abreviar(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "seg",
        DayOfWeek.Tuesday => "ter",
        DayOfWeek.Wednesday => "qua",
        DayOfWeek.Thursday => "qui",
        DayOfWeek.Friday => "sex",
        DayOfWeek.Saturday => "sáb",
        _ => "dom"
    };
}

/// <summary>
/// As próximas vagas de um profissional (set/2026) — o que a busca devolve à tela, com o
/// CRITÉRIO escrito: quem, que duração, até quando procurou, e se a jornada foi presumida.
/// Lista sem critério é lista em que a pessoa não sabe o que está faltando.
/// </summary>
public sealed record ResultadoBuscaDeVagas(
    string Profissional,
    int DuracaoMinutos,
    DateTime APartirDe,
    DateTime AteQuando,
    bool JornadaPresumida,
    string? Jornada,
    IReadOnlyList<Vaga> Vagas)
{
    public bool Vazio => Vagas.Count == 0;

    /// <summary>A frase que explica o que foi procurado.</summary>
    public string Criterio
        => $"Vagas de {DuracaoMinutos} min para {Profissional}, de {APartirDe:dd/MM} a {AteQuando:dd/MM}"
           + (JornadaPresumida
               ? $", das {Agendamento.AberturaPadraoGrade:HH:mm} às {Agendamento.FechamentoPadraoGrade:HH:mm} "
                 + "de segunda a sábado — jornada não cadastrada em Equipe."
               : $", dentro da jornada dele ({Jornada}).");
}

/// <summary>
/// A busca de horário livre (set/2026): "quando cabe?" respondida em lista, e não olhando a
/// grade dia a dia com o paciente ao telefone.
///
/// É PURA: recebe o que está marcado e fechado e devolve as vagas. As regras que decidem:
///
/// - **A jornada declarada manda.** Dias e horas do cadastro do profissional. Sem jornada,
///   a busca PRESUME a janela da grade (7h às 20h) de segunda a sábado — e DIZ que
///   presumiu, para a recepção cadastrar a jornada em vez de confiar num domingo que a
///   clínica não abre.
/// - **Colide o que OCUPA agenda** (cancelado e falta não ocupam), por intervalo, como o
///   choque da marcação — e só do profissional procurado.
/// - **Bloqueio fecha o vão** quando alcança o profissional (ou a clínica inteira).
/// - **Nada no passado**: a partir do instante pedido, e o passo é o da grade.
/// - **Teto de dias**: procurar dois meses e não achar é resposta, e a tela a escreve.
/// </summary>
public static class BuscaDeVagas
{
    public const int DiasMaximos = 60;
    public const int QuantidadePadrao = 10;

    public static IReadOnlyList<Vaga> Calcular(
        DateTime aPartirDe,
        int duracaoMinutos,
        Profissional profissional,
        IReadOnlyList<Agendamento> ocupados,
        IReadOnlyList<BloqueioAgenda> bloqueios,
        int quantidade = QuantidadePadrao,
        int diasMaximos = DiasMaximos,
        int passoMinutos = Agendamento.DuracaoPadraoMinutos)
    {
        if (duracaoMinutos <= 0) duracaoMinutos = Agendamento.DuracaoPadraoMinutos;

        var dele = ocupados
            .Where(a => a.OcupaAgenda && a.ProfissionalId == profissional.Id)
            .ToList();
        var fechamentos = bloqueios
            .Where(b => b.AlcancaRecurso(profissional.Id, null))
            .ToList();

        var abre = profissional.AtendeDas ?? Agendamento.AberturaPadraoGrade;
        var fecha = profissional.AtendeAte ?? Agendamento.FechamentoPadraoGrade;

        var vagas = new List<Vaga>();
        var ultimoDia = aPartirDe.Date.AddDays(diasMaximos);

        for (var dia = aPartirDe.Date; dia < ultimoDia && vagas.Count < quantidade; dia = dia.AddDays(1))
        {
            if (!AtendeNoDia(profissional, dia.DayOfWeek)) continue;

            var limite = dia.Add(fecha.ToTimeSpan());
            for (var inicio = dia.Add(abre.ToTimeSpan()); inicio.AddMinutes(duracaoMinutos) <= limite; inicio = inicio.AddMinutes(passoMinutos))
            {
                if (inicio < aPartirDe) continue;
                var fim = inicio.AddMinutes(duracaoMinutos);
                if (dele.Any(a => a.ColideCom(inicio, fim))) continue;
                if (fechamentos.Any(b => b.ColideCom(inicio, fim))) continue;

                vagas.Add(new Vaga(inicio, fim));
                if (vagas.Count == quantidade) break;
            }
        }

        return vagas;
    }

    /// <summary>
    /// Sem dias declarados, a busca presume segunda a sábado — domingo não se oferece a
    /// ninguém sem que alguém tenha dito que a clínica abre. Com dias declarados, são eles.
    /// </summary>
    public static bool AtendeNoDia(Profissional profissional, DayOfWeek dia)
        => profissional.DiasDeAtendimento is null
            ? dia != DayOfWeek.Sunday
            : profissional.AtendeEm(dia);

    /// <summary>A jornada foi presumida quando nada foi declarado no cadastro.</summary>
    public static bool JornadaPresumida(Profissional profissional) => !profissional.JornadaDeclarada;
}
