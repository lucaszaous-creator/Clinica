using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O CRONÔMETRO da tela de atendimento (set/2026, mockup aprovado em
/// <c>docs/mockups/temporizador-cronometro-digital.html</c> — o "visor neutro").
///
/// O pedido da direção foi curto: <i>"um temporizador dentro da consulta quando o
/// médico/enfermeiro clica em atender, só para ele saber há quanto tempo já está atendendo
/// aquele paciente naquela consulta"</i> — e a referência que veio junto foi um painel de
/// cronômetro, com horas, minutos e segundos correndo.
///
/// Mora aqui, e não na ViewModel, pela regra da <c>GradeSemana</c> (parcela 69) e do
/// <c>ResumoSessaoAnterior</c> (77): <b>o que decide o que a tela AFIRMA precisa morar onde
/// o <c>dotnet test</c> alcança</b> — os projetos WPF não compilam no projeto de teste.
///
/// As decisões que o desenho fixou
/// -------------------------------
/// <list type="bullet">
/// <item><b>Sem consulta em curso, não há visor.</b> Prontuário aberto pela carteira, sessão
/// de outro dia, horário cancelado ou falta: <see cref="De"/> devolve <c>null</c> e a tela
/// não desenha nada. Um cronômetro zerado convidaria a "iniciar" um atendimento que não
/// existe na agenda de ninguém — e zero não é o mesmo que "não começou".</item>
/// <item><b>Encerrado PARA o visor.</b> Depois do Finalizar quem fala é a pílula de
/// situação ("Encerrado às 15h02 · durou 24 min"): manter o visor ali, parado, seria dois
/// leitores dizendo a mesma coisa com pesos diferentes.</item>
/// <item><b>As horas aparecem sempre</b> (<c>00:12:35</c>), como no painel da referência. O
/// preço está medido e aceito: numa consulta os dois primeiros dígitos ficam em zero. A
/// alternativa — escondê-las até virar a hora — faz o visor mudar de LARGURA no meio do
/// atendimento, e o que está ao lado dá um pulo.</item>
/// </list>
///
/// ⚠️ A conta vem do CARIMBO (<c>Agendamento.InicioAtendimentoEm</c>), nunca de um relógio
/// que a tela liga ao abrir: é isso que faz o número continuar certo quando o profissional
/// troca de aba, sai da tela ou fecha o programa — e é o mesmo carimbo que o balcão lê.
/// </summary>
public static class CronometroDaSessao
{
    /// <summary>
    /// O texto do visor, ou <c>null</c> quando não há o que cronometrar.
    /// </summary>
    public static string? De(Agendamento? horario, DateTime agora)
    {
        if (horario is null) return null;

        // Só corre o atendimento EM CURSO: entrou na sala, não foi encerrado e o horário
        // continua em aberto (cancelado, falta e substituído saem por aqui).
        if (horario.InicioAtendimentoEm is null
            || horario.FimAtendimentoEm is not null
            || horario.Status != StatusAgendamento.Agendado) return null;

        return Formatar(horario.TempoDeAtendimento(agora));
    }

    /// <summary>
    /// O horário do paciente que está EM CURSO agora, entre os do dia — ou <c>null</c>.
    ///
    /// Existe para as telas que abrem um paciente <b>sem</b> vir da agenda: a tela da
    /// Enfermagem do shell é a porta da passagem fora de horário (curativo, triagem,
    /// observação) e não carrega horário nenhum, então sem isto ela nunca teria cronômetro
    /// — e eu teria prometido no mockup um visor que ela não mostra.
    ///
    /// A regra é a MESMA do <see cref="De"/>, e é por isso que ela mora aqui e não numa
    /// ViewModel: duas definições de "atendimento em curso" divergiriam na primeira
    /// correção, e a tela passaria a cronometrar uma sessão que já foi encerrada.
    ///
    /// Dois em curso não deveriam existir — mas se existirem, vence o que começou por
    /// ÚLTIMO: é o paciente que está na sala agora.
    /// </summary>
    public static Agendamento? EmCurso(IEnumerable<Agendamento>? doDia)
        => doDia?
            .Where(a => a.InicioAtendimentoEm is not null
                        && a.FimAtendimentoEm is null
                        && a.Status == StatusAgendamento.Agendado)
            .OrderByDescending(a => a.InicioAtendimentoEm)
            .FirstOrDefault();

    /// <summary>
    /// <c>00:12:35</c> — horas, minutos e segundos, sempre com as três casas.
    ///
    /// Público e separado do <see cref="De"/> porque é o que o teste mede sem precisar
    /// montar um agendamento: o formato é a metade que a clínica olha o dia inteiro.
    /// </summary>
    public static string? Formatar(TimeSpan? corrido)
    {
        if (corrido is not { } t) return null;

        // Atendimento de mais de um dia não existe, e mesmo assim o visor não pode virar
        // outra coisa: TotalHours mantém a contagem crescendo em vez de dar a volta.
        var horas = (int)t.TotalHours;
        return $"{horas:00}:{t.Minutes:00}:{t.Seconds:00}";
    }
}
