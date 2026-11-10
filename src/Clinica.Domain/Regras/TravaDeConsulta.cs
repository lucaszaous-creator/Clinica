namespace Clinica.Domain.Regras;

/// <summary>Uma consulta pelo convênio que pode acender a trava: quando foi (ou será) e o atendimento dela.</summary>
/// <param name="EspecialidadeCodigo">A especialidade da consulta (código do catálogo, ou o enum das antigas).</param>
/// <param name="ProfissionalId">Quem atende(u) a consulta.</param>
public sealed record ConsultaParaTrava(int AgendamentoId, DateTime DataHora, string? AtendimentoNumero, bool Atendida = false,
    string? EspecialidadeCodigo = null, int? ProfissionalId = null);

/// <summary>
/// A trava ACESA: a consulta que a acende, o prazo do convênio e a partir de quando ela solta.
/// </summary>
/// <param name="ConsultaEm">A consulta pelo convênio mais próxima do horário pretendido.</param>
/// <param name="LiberadaAPartirDe">
/// O primeiro dia em que o convênio aceita outra consulta DEPOIS daquela. Quando a consulta que
/// acende a trava é POSTERIOR ao horário pretendido (já havia uma marcada mais à frente), este
/// dia é contado dela — marcar antes continua travado, e a frase diz isso
/// (<see cref="TravaDeConsulta.Frase"/>).
/// </param>
/// <param name="ConsultaAtendida">A consulta que acende a trava já aconteceu (falso = só marcada).</param>
public sealed record TravaAcesa(
    string ConvenioNome, int PrazoDias, DateTime ConsultaEm, string? AtendimentoNumero, DateOnly LiberadaAPartirDe,
    bool ConsultaAtendida = false)
{
    /// <summary>
    /// A especialidade e o profissional da consulta que acende a trava (nov/2026): a trava só
    /// vale para a MESMA especialidade com o MESMO médico, e a frase precisa dizer quais.
    /// </summary>
    public string? Especialidade { get; init; }
    public string? Profissional { get; init; }
}

/// <summary>
/// A TRAVA DE CONSULTA DO CONVÊNIO (nov/2026, pedido da gerência: "se a secretária tentar fazer
/// um agendamento de uma consulta antes do prazo, sendo que já tinha realizado uma… as Unimed
/// têm, a Amil, a Petrobras, a Sul América têm — a recepção precisa respeitar essas regras").
///
/// O prazo é o que a clínica JÁ cadastrou como validade da consulta (Unimed 22 dias, Amil e
/// Petrobras 30, os personalizados no cadastro deles): dentro dele o convênio não paga outra
/// consulta. A regra é pura e mora aqui; quem a aplica é o <c>AgendaService</c>, em todas as
/// portas de marcação, e quem a atravessa é a liberação da gerência.
///
/// O que ficou decidido com a direção:
/// - vale para QUALQUER consulta do paciente pelo convênio — outra especialidade ou outro
///   médico não zeram o prazo;
/// - é só de CONSULTA: acupuntura, eletro e BSV não entram (a Sul América ADIA a guia delas,
///   decisão anterior, que continua);
/// - a consulta PARTICULAR não acende nem sofre a trava — não há guia para o convênio recusar.
/// </summary>
public static class TravaDeConsulta
{
    /// <summary>
    /// A consulta que trava o dia pretendido: outra consulta pelo convênio a MENOS de
    /// <paramref name="prazoDias"/> dias, para trás ou para a frente (já marcada adiante). A
    /// mais próxima ganha. Prazo zero ou negativo = convênio sem trava.
    /// </summary>
    public static ConsultaParaTrava? Conferir(DateOnly dia, int prazoDias, IEnumerable<ConsultaParaTrava> outras)
        => prazoDias <= 0
            ? null
            : outras
                .Select(c => (Consulta: c, Distancia: Math.Abs(DateOnly.FromDateTime(c.DataHora).DayNumber - dia.DayNumber)))
                .Where(x => x.Distancia < prazoDias)
                .OrderBy(x => x.Distancia).ThenBy(x => x.Consulta.DataHora)
                .Select(x => x.Consulta)
                .FirstOrDefault();

    /// <summary>A frase da box: o que trava, e a partir de quando solta — ou que o problema está à frente.</summary>
    public static string Frase(TravaAcesa trava, DateOnly diaPretendido)
    {
        var quando = trava.ConsultaEm.ToString("dd/MM/yyyy");
        // "consulta de Clínica da Dor com Dr. Marcos" — o que a trava compara (nov/2026).
        var oque = "uma consulta"
                   + (string.IsNullOrWhiteSpace(trava.Especialidade) ? string.Empty : $" de {trava.Especialidade}")
                   + (string.IsNullOrWhiteSpace(trava.Profissional) ? string.Empty : $" com {trava.Profissional}");
        // Três estados, três frases: com número, atendida sem número (agenda importada), só marcada.
        var atendimento = trava.AtendimentoNumero is { } n ? $" (atendimento {n})"
            : trava.ConsultaAtendida ? string.Empty : " (marcada)";
        return DateOnly.FromDateTime(trava.ConsultaEm) > diaPretendido
            ? $"{trava.ConvenioNome} aceita {oque} a cada {trava.PrazoDias} dias, e este paciente já tem "
              + $"consulta em {quando}{atendimento}. Marque até {DateOnly.FromDateTime(trava.ConsultaEm).AddDays(-trava.PrazoDias):dd/MM/yyyy} "
              + $"ou a partir de {trava.LiberadaAPartirDe:dd/MM/yyyy}."
            : $"{trava.ConvenioNome} aceita {oque} a cada {trava.PrazoDias} dias, e a última deste paciente "
              + $"foi em {quando}{atendimento}. Nova consulta pelo convênio a partir de {trava.LiberadaAPartirDe:dd/MM/yyyy}.";
    }
}

/// <summary>
/// A recusa da marcação pela trava. É exceção com DADO (a <see cref="Trava"/>) porque quem a
/// captura na Recepção oferece as três saídas — outra data, particular, pedir liberação —, e
/// quem não a conhece (a ponte do CRM, o faturamento) mostra a mensagem, que já diz o prazo.
/// </summary>
public sealed class TravaDeConsultaException : InvalidOperationException
{
    public TravaDeConsultaException(TravaAcesa trava, DateOnly diaPretendido)
        : base(TravaDeConsulta.Frase(trava, diaPretendido)
               + " Para marcar antes disso, peça a liberação à gerência — ou marque como particular.")
        => Trava = trava;

    public TravaAcesa Trava { get; }
}
