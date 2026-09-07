using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que está FECHADO num dia, em uma linha — "Ana Souza · Sala 2", "Clínica fechada"
/// (set/2026).
///
/// Nasceu para o cabeçalho da coluna da visão de SEMANA do balcão, onde as férias eram
/// invisíveis: a coluna do dia é de TODOS, então pintar os vãos seria mentira (os outros
/// atendem) e, sem uma palavra escrita, o dia de férias aparecia apenas com menos
/// horários — que se lê como "ninguém marcou". É a lição do "Agenda fechada neste dia" do
/// Meu dia (parcela 69), na tela de quem marca.
///
/// Mora na Application, e não na ViewModel, pela regra de sempre: <b>o que decide o que a
/// tela AFIRMA precisa morar onde o <c>dotnet test</c> alcança</b> — a `GradeSemana` da
/// parcela 69 e o `ResumoSessaoAnterior` da 77 seguiram o mesmo caminho.
/// </summary>
public static class FechamentosDaAgenda
{
    /// <summary>
    /// Descreve os fechamentos que alcançam <paramref name="dia"/>.
    ///
    /// Com <paramref name="donoId"/> (o recorte "só a minha agenda"), só o que alcança
    /// ELE: o fechamento da sala 2 não é notícia para quem atende noutra sala, e escrevê-lo
    /// na coluna de quem não é afetado é o alerta que dispara para todo mundo — o que
    /// ensina a ignorar a linha. Sem dono, a coluna é de todos e cada recurso fechado é
    /// nomeado.
    ///
    /// Repetido é dito UMA vez: três bloqueios de tarde da mesma pessoa na mesma semana
    /// escreveriam o nome dela três vezes, e a linha deixaria de se ler.
    /// </summary>
    public static string Descrever(
        IReadOnlyList<BloqueioAgenda> bloqueios, DateTime dia, int? donoId = null)
    {
        if (bloqueios.Count == 0) return string.Empty;

        var inicio = dia.Date;
        var fim = inicio.AddDays(1);

        var alvos = bloqueios
            .Where(b => b.ColideCom(inicio, fim))
            .Where(b => donoId is null || b.AlcancaRecurso(donoId, null))
            .Select(Nomear)
            .Distinct()
            .ToList();

        return string.Join(" · ", alvos);
    }

    /// <summary>
    /// O nome do recurso fechado. O da clínica diz "Clínica fechada" — dizer o motivo aqui
    /// gastaria a linha inteira com um texto livre que a clínica escreve como quiser, e o
    /// motivo continua na dica do vão bloqueado da grade.
    /// </summary>
    private static string Nomear(BloqueioAgenda b)
        => b.DaClinica
            ? "Clínica fechada"
            : b.Profissional?.Rotulo
              ?? (b.Sala?.Nome is { } sala ? $"Sala {sala}" : "Recurso fechado");
}
