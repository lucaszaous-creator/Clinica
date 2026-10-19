using System.Globalization;

namespace Clinica.Application.Modelos;

/// <summary>O cabeçalho de uma coluna da grade da semana: o dia, com a contagem dele.</summary>
public sealed record CabecalhoDiaDaSemana(string Titulo, string Subtitulo, bool EhHoje, string Resumo);

/// <summary>
/// A "MINHA SEMANA" — o que a tela afirma (out/2026; era o <c>MinhaSemanaViewModel</c> WPF).
/// A GRADE em si continua sendo <see cref="GradeSemana"/> (já pura); aqui moram os cabeçalhos
/// dos sete dias, o período, o resumo da semana e os itens do filtro.
/// </summary>
public static class MinhaSemanaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string FraseSemPermissao =
        "Abrir o paciente mostra o prontuário, e o seu acesso não tem essa permissão. A direção libera em Acessos.";

    public static string NomeDoDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "Segunda",
        DayOfWeek.Tuesday => "Terça",
        DayOfWeek.Wednesday => "Quarta",
        DayOfWeek.Thursday => "Quinta",
        DayOfWeek.Friday => "Sexta",
        DayOfWeek.Saturday => "Sábado",
        _ => "Domingo"
    };

    /// <summary>"13/10 a 19/10/2026".</summary>
    public static string Periodo(SemanaDoProfissional semana)
        => $"{semana.Inicio.ToString("dd/MM", PtBr)} a {semana.Fim.ToString("dd/MM/yyyy", PtBr)}";

    /// <summary>Os sete cabeçalhos — o dia vazio continua tendo coluna (com "—").</summary>
    public static IReadOnlyList<CabecalhoDiaDaSemana> Cabecalhos(SemanaDoProfissional semana, DateOnly hoje)
        => semana.Dias.Select(d => new CabecalhoDiaDaSemana(
            NomeDoDia(d.Dia.DayOfWeek),
            d.Dia.ToString("dd/MM", PtBr),
            d.Dia == hoje,
            d.Sessoes.Count == 0 ? "—" : $"{d.Sessoes.Count} sessão(ões)")).ToList();

    /// <summary>
    /// O resumo diz o que as colunas não dizem: quantas PESSOAS são e qual o dia mais cheio. A
    /// dívida é a "NESTA semana" — o botão do Meu dia conta a fila de trabalho, e dois números
    /// com a mesma frase se leem como o mesmo número errado.
    /// </summary>
    public static string Resumo(SemanaDoProfissional semana)
    {
        if (semana.Sessoes == 0) return "Nenhum horário marcado nesta semana.";

        var partes = new List<string>
        {
            $"{semana.Sessoes} sessão(ões)",
            $"{semana.PacientesDistintos} paciente(s)"
        };

        if (semana.DiaMaisCheio is { } cheio && cheio.Sessoes.Count > 0)
            partes.Add($"dia mais cheio: {NomeDoDia(cheio.Dia.DayOfWeek).ToLowerInvariant()} ({cheio.Sessoes.Count})");

        if (semana.RegistrosPendentes > 0)
            partes.Add($"{semana.RegistrosPendentes} sem evolução nesta semana");

        return string.Join(" · ", partes);
    }

    /// <summary>
    /// Os horários da semana como o filtro os enxerga. A sessão de uma hora aparece em duas
    /// faixas da grade: conta-se UMA vez.
    /// </summary>
    public static IReadOnlyList<ItemDaAgendaDoPosto> ItensDoFiltro(SemanaDoProfissional semana)
        => semana.Dias.SelectMany(d => d.Sessoes)
            .DistinctBy(s => s.AgendamentoId)
            .Select(s => new ItemDaAgendaDoPosto(s.PacienteNome, s.Modalidade, s.GrupoVisual))
            .ToList();
}
