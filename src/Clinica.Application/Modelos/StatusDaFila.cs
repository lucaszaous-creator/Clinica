using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O VOCABULÁRIO da coluna STATUS das duas listas do dia — a do médico ("Meu dia",
/// parcela 95) e a do balcão ("Agenda do dia", set/2026): a etapa da fila em uma
/// palavra, e a hora do fato embaixo.
///
/// Mora na Application, e não em cada ViewModel, pela razão de sempre: são duas telas
/// respondendo à mesma pergunta sobre o MESMO horário, e duas redações divergiriam na
/// primeira correção — o balcão diria "Na recepção" e o médico "No local" sobre a mesma
/// pessoa, e cada um acharia que o outro está falando de outra coisa. E o que decide o
/// que a tela AFIRMA precisa morar onde o <c>dotnet test</c> alcança.
///
/// Cancelado e falta têm palavra própria e detalhe VAZIO: a linha fica na lista, apagada
/// (a regra da folha do dia — quem lê às 14h precisa saber que as 15h vagaram), e não há
/// hora de fato a mostrar.
/// </summary>
public static class StatusDaFila
{
    /// <summary>Cancelado ou falta — a linha que fica APAGADA, sem ação de fila.</summary>
    public static bool ForaDaFila(StatusAgendamento status)
        => status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou;

    /// <summary>A etapa em UMA palavra. "Marcado" é quem ainda não chegou.</summary>
    public static string Palavra(StatusAgendamento status, EtapaFila etapa) => status switch
    {
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        _ => etapa switch
        {
            EtapaFila.Chegou => "No local",
            EtapaFila.Chamado => "Chamado",
            EtapaFila.EmAtendimento => "Em atendimento",
            EtapaFila.Finalizado => "Concluído",
            _ => "Marcado"
        }
    };

    /// <summary>
    /// A hora do fato sob a palavra — "chegou às 14:40 · espera 12 min", "chamado há 4
    /// min", "desde 14:52", "às 15:20". É o que faz uma linha responder "quem eu posso
    /// chamar agora" sem precisar de cinco colunas. Vazio quando não há fato.
    /// </summary>
    public static string Detalhe(
        StatusAgendamento status, EtapaFila etapa,
        DateTime? chegadaEm, int? esperaMinutos, int? chamadoHaMinutos,
        DateTime? inicioEm, DateTime? fimEm)
    {
        if (ForaDaFila(status)) return string.Empty;

        return etapa switch
        {
            EtapaFila.Chegou => Juntar(
                chegadaEm is { } c ? $"chegou às {c:HH\\:mm}" : null,
                Espera(esperaMinutos)),
            EtapaFila.Chamado => ChamadoHa(chamadoHaMinutos),
            EtapaFila.EmAtendimento => inicioEm is { } i ? $"desde {i:HH\\:mm}" : string.Empty,
            EtapaFila.Finalizado => fimEm is { } f ? $"às {f:HH\\:mm}" : string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>"espera 12 min" — vazio sem check-in.</summary>
    public static string Espera(int? minutos) => minutos is { } m ? $"espera {m} min" : string.Empty;

    /// <summary>"chamado agora" · "chamado há 4 min" — vazio para quem não foi chamado.</summary>
    public static string ChamadoHa(int? minutos) => minutos switch
    {
        null => string.Empty,
        0 => "chamado agora",
        var m => $"chamado há {m} min"
    };

    private static string Juntar(params string?[] partes)
        => string.Join(" · ", partes.Where(p => !string.IsNullOrEmpty(p)));
}
