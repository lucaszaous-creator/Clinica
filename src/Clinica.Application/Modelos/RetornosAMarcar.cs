using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma sessão em que quem atendeu pediu retorno — a projeção que o repositório devolve
/// (set/2026). Só o que a fila do balcão precisa: quem, quando foi a sessão, para quando
/// é o retorno, por quê, e quem pediu. Nunca o texto da evolução.
/// </summary>
public sealed record RetornoSugerido(
    int EvolucaoId,
    int PacienteId,
    string Paciente,
    string? Telefone,
    DateOnly Sessao,
    DateOnly RetornoEm,
    string? Nota,
    int? ProfissionalId,
    string? ProfissionalNomeCurto,
    string? ProfissionalNome);

/// <summary>
/// Um horário ATIVO (nem cancelado, nem falta) de um paciente — a projeção mínima com
/// que se responde "depois desta sessão, ele já tem hora marcada?".
/// </summary>
public sealed record HorarioPosterior(int PacienteId, DateTime DataHora);

/// <summary>Uma linha da fila "Retornos a marcar".</summary>
public sealed record RetornoAMarcar(
    int PacienteId,
    string Paciente,
    string? Telefone,
    DateOnly Sessao,
    DateOnly RetornoEm,
    string? Nota,
    int? ProfissionalId,
    string Profissional,
    int DiasDeAtraso)
{
    /// <summary>A data sugerida já passou e ninguém marcou.</summary>
    public bool Atrasado => DiasDeAtraso > 0;

    /// <summary>"atrasado há 5 dias" · "é hoje" · "em 3 dias".</summary>
    public string Situacao => DiasDeAtraso switch
    {
        > 1 => $"atrasado há {DiasDeAtraso} dias",
        1 => "atrasado há 1 dia",
        0 => "é hoje",
        -1 => "amanhã",
        _ => $"em {-DiasDeAtraso} dias"
    };

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
    public bool TemNota => !string.IsNullOrWhiteSpace(Nota);
}

/// <summary>
/// A fila do balcão "quem saiu daqui com pedido de retorno e ainda não tem horário"
/// (set/2026).
///
/// O médico grava <see cref="Evolucao.RetornoSugeridoEm"/> desde a parcela 77, e a única
/// leitura fora do Consultório era a janela de edição da evolução: a recepcionista não
/// tinha como saber que alguém devia voltar. A regra da casa está certa em NÃO
/// transformar a sugestão em agendamento (parcela 58 — a pendência não vira horário de
/// quem atende); o que faltava era a FILA DE TRABALHO, como a conciliação da agenda.
///
/// A montagem é PURA: recebe as sugestões e os horários e devolve as linhas. É o que
/// permite testá-la sem banco, e é onde mora a única decisão da tela:
///
/// - **Por paciente vale a sessão MAIS RECENTE.** Quem foi visto de novo e recebeu um novo
///   pedido tem um retorno só — o último. A sugestão anterior foi cumprida ou superada.
/// - **"Coberto" é ter horário ATIVO depois do dia da sessão.** Não "depois de hoje": um
///   retorno pedido em 20/08 para 27/08, marcado e realizado em 27/08, já foi atendido —
///   e "futuro a partir de hoje" o daria como pendente para sempre. Cancelado e falta
///   não cobrem: o horário voltou a não existir.
/// - **O atraso se conta da data sugerida**, e a lista sai dos mais atrasados para os
///   que ainda vão vencer — o de 15 dias atrás é o que a clínica já perdeu; o de daqui
///   a 3 dias é o que ainda dá para marcar.
///
/// O que ela nunca faz: marcar, dispensar ou gravar qualquer coisa. Marcar é o Marcar da
/// Recepção, pré-preenchido; a linha some sozinha quando o horário passa a existir.
/// </summary>
public static class RetornosAMarcar
{
    /// <summary>
    /// Retorno sugerido para antes disto não entra na fila: passou de dois meses, o
    /// paciente não é mais "um retorno a marcar", é assunto de quem parou de vir
    /// (retenção/recall). Pendência que nunca some é pendência que ninguém lê.
    /// </summary>
    public const int JanelaDiasParaTras = 60;

    public static IReadOnlyList<RetornoAMarcar> Montar(
        IReadOnlyList<RetornoSugerido> sugestoes,
        IReadOnlyList<HorarioPosterior> horarios,
        DateOnly hoje)
    {
        var porPaciente = horarios
            .GroupBy(h => h.PacienteId)
            .ToDictionary(g => g.Key, g => g.Select(h => DateOnly.FromDateTime(h.DataHora)).ToList());

        return sugestoes
            .GroupBy(s => s.PacienteId)
            // A sessão mais recente de cada paciente; empate de data desempata pelo id.
            .Select(g => g.OrderByDescending(s => s.Sessao).ThenByDescending(s => s.EvolucaoId).First())
            .Where(s => !porPaciente.TryGetValue(s.PacienteId, out var dias) || dias.All(d => d <= s.Sessao))
            .Select(s => new RetornoAMarcar(
                s.PacienteId,
                s.Paciente,
                s.Telefone,
                s.Sessao,
                s.RetornoEm,
                string.IsNullOrWhiteSpace(s.Nota) ? null : s.Nota.Trim(),
                s.ProfissionalId,
                Rotulo(s.ProfissionalNomeCurto, s.ProfissionalNome),
                hoje.DayNumber - s.RetornoEm.DayNumber))
            .OrderBy(r => r.RetornoEm)
            .ThenBy(r => r.Paciente)
            .ToList();
    }

    /// <summary>
    /// A mesma regra de <see cref="Profissional.Rotulo"/> — repetida aqui porque a
    /// projeção do repositório não carrega a entidade, e um nome curto em branco tem de
    /// cair para o nome inteiro como em toda tela.
    /// </summary>
    private static string Rotulo(string? nomeCurto, string? nome)
        => string.IsNullOrWhiteSpace(nomeCurto)
            ? (string.IsNullOrWhiteSpace(nome) ? "quem atendeu" : nome!)
            : nomeCurto!;
}
