using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Os horários de um profissional que passaram do prazo SEM chegada — uma linha do sino.</summary>
/// <param name="Chave">A chave do visto/lido no sino: muda quando entra horário novo ou sai algum.</param>
public sealed record HorariosSemChegada(int ProfissionalId, string Profissional, int Quantos, DateTime MaisAntigo, string Chave)
{
    /// <summary>"3 horários sem chegada na agenda de Dra. Ana: confirme falta ou presença."</summary>
    public string Frase => ConclusaoPorProfissional.FraseSemChegada(Profissional, Quantos);
}

/// <summary>
/// A CONCLUSÃO AUTOMÁTICA POR PROFISSIONAL (nov/2026 — pedido da gerência: a médica não usa o sistema,
/// ninguém conclui as sessões dela e as guias não nascem). A regra mora aqui, com teste; quem roda é o
/// <c>ConclusaoAutomaticaService</c> (Infrastructure), no mesmo relógio da conclusão por prazo — a API
/// do portal na VPS, 24 h, e os apps abertos.
/// <list type="bullet">
/// <item>Liga por PROFISSIONAL (<see cref="Profissional.ConcluirAutomaticamenteHoras"/>), nunca pelo nome.</item>
/// <item>Só conclui o horário com CHEGADA registrada (decisão do dono): sem chegada, ninguém sabe se a
/// consulta aconteceu, e cobrar o que não aconteceu é pior do que esperar a recepção.</item>
/// <item>Conclui pelo MESMO núcleo do "Concluir" (<c>AgendaService.ConcluirAtendimentoClinicoAsync</c>):
/// atendimento, códigos (as guias pendentes de baixa), particular e pacote como no clique.</item>
/// <item>Não escreve prontuário: sem evolução, e a sessão não entra em "sessões sem evolução".</item>
/// </list>
/// </summary>
public static class ConclusaoPorProfissional
{
    /// <summary>O prazo que vale: 1 a <see cref="Profissional.ConcluirAutomaticamenteHorasMaximo"/> horas; o resto é desligado.</summary>
    public static int? Horas(Profissional? profissional)
        => profissional?.ConcluirAutomaticamenteHoras is { } h and > 0 and <= Profissional.ConcluirAutomaticamenteHorasMaximo ? h : null;

    /// <summary>
    /// O horário está no alcance da regra? Só a partir da data do cadastro (<see cref="Profissional.ConcluirAutomaticamenteDesde"/>);
    /// sem data, nada — a regra nunca anda para trás.
    /// </summary>
    public static bool NoAlcance(Profissional profissional, DateTime horario)
        => profissional.ConcluirAutomaticamenteDesde is { } desde && horario >= desde.Date;

    /// <summary>O prazo venceu? Consulta às 09:00 com 10 horas → às 19:00 (inclusive).</summary>
    public static bool Venceu(DateTime horario, int horas, DateTime agora) => horario.AddHours(horas) <= agora;

    /// <summary>
    /// Quem assina na trilha: o SISTEMA, com a regra que agiu — "Conclusão automática (Dra. Ana, 10h)".
    /// Nunca um usuário humano: ninguém clicou. Cabe nas colunas de operador (a da trilha tem 80).
    /// </summary>
    public static string Operador(Profissional profissional, int horas)
    {
        var nome = string.IsNullOrWhiteSpace(profissional.NomeCurto) ? profissional.Nome : profissional.NomeCurto!;
        nome = nome.Trim();
        if (nome.Length > 40) nome = nome[..40].TrimEnd();
        return $"Conclusão automática ({nome}, {horas}h)";
    }

    /// <summary>O detalhe da trilha: o horário, o prazo e a hora em que o sistema concluiu.</summary>
    public static string Detalhe(Agendamento horario, int horas, DateTime agora)
        => $"Sessão de {horario.DataHora:dd/MM/yyyy HH:mm} concluída às {agora:dd/MM/yyyy HH:mm}: chegada registrada às "
           + $"{horario.ChegadaEm:HH:mm} e ninguém concluiu em {horas} horas (regra do profissional). Ato do sistema; "
           + "não representa evolução, avaliação ou assinatura médica.";

    /// <summary>A frase do sino para os horários sem chegada.</summary>
    public static string FraseSemChegada(string profissional, int quantos)
        => quantos == 1
            ? $"1 horário sem chegada na agenda de {profissional}: confirme falta ou presença."
            : $"{quantos} horários sem chegada na agenda de {profissional}: confirme falta ou presença.";

    /// <summary>
    /// Quantos horários o relógio conclui por volta (1 por minuto): cada um é uma transação curta; 50 por
    /// minuto dão conta de um mês retroativo de uma agenda cheia em poucos minutos sem segurar o portal.
    /// </summary>
    public const int MaximoPorVolta = 50;

    /// <summary>Até quantos dias para trás o sino cobra: horário mais velho que isso já saiu do dia a dia da recepção.</summary>
    public const int DiasDoAvisoSemChegada = 30;

    /// <summary>A chave do sino: o profissional, o horário mais novo e a quantidade — notícia nova muda a chave.</summary>
    public static string ChaveSemChegada(int profissionalId, int ultimoAgendamentoId, int quantos)
        => $"SC:{profissionalId}:{ultimoAgendamentoId}:{quantos}";
}
