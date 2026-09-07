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
    /// <summary>
    /// Cancelado, falta ou substituído — a linha que fica APAGADA, sem ação de fila. A lista
    /// é a NEGAÇÃO de <c>Agendamento.OcupaAgenda</c>: o que não ocupa a agenda não está na
    /// fila, e a próxima situação nova cai do lado certo sem ninguém lembrar.
    /// </summary>
    public static bool ForaDaFila(StatusAgendamento status)
        => status is not (StatusAgendamento.Agendado or StatusAgendamento.Realizado);

    /// <summary>A etapa em UMA palavra. "Marcado" é quem ainda não chegou.</summary>
    public static string Palavra(StatusAgendamento status, EtapaFila etapa) => status switch
    {
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        // A sessão aconteceu, noutro lançamento: nem falta nem cancelamento.
        StatusAgendamento.Substituido => "Substituído",
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
    /// A situação como o CARTÃO DA GRADE a mostra — vazia enquanto o horário só está
    /// marcado (set/2026).
    ///
    /// A grade e a lista respondem à mesma pergunta com pesos diferentes. A lista tem uma
    /// coluna STATUS e ali "Marcado" é resposta: a coluna existe, e deixá-la em branco
    /// pareceria dado faltando. No cartão da grade não há coluna nenhuma — o que se
    /// escreve ali disputa espaço com o nome do paciente e a modalidade —, e a regra da
    /// casa é a que já está escrita no template: <i>só o que muda a conduta ganha selo;
    /// estado normal não leva selo nenhum</i>. Escrever "Marcado" em quarenta cartões de
    /// um dia que ainda não começou é a linha que ninguém lê, e é ela que faria ninguém
    /// ler o "Em atendimento" do cartão ao lado.
    ///
    /// A etapa AGUARDANDO é o corte exato: ela é derivada dos carimbos
    /// (<c>Agendamento.Etapa</c>), então "aguardando" quer dizer que nada aconteceu ainda
    /// — nem chegada, nem chamada, nem cancelamento.
    /// </summary>
    public static string SituacaoNaGrade(StatusAgendamento status, EtapaFila etapa)
        => etapa == EtapaFila.Aguardando ? string.Empty : Palavra(status, etapa);

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
