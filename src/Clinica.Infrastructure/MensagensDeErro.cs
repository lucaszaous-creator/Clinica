namespace Clinica.Infrastructure;

/// <summary>
/// Traduz falha de banco para o que se pode fazer a respeito.
///
/// O balcão via o texto cru do Postgres — <c>23505: duplicate key value violates unique
/// constraint "IX_Metas_Ano_Mes_Indicador_ProfissionalId"</c> — e para quem está com o
/// paciente na frente isso não é informação, é susto: a pessoa conclui que quebrou o
/// sistema e para de trabalhar, quando o que houve foi cadastrar duas vezes a mesma
/// coisa.
///
/// A tradução é por **intenção do índice**, não por tabela. O mesmo `23505` significa
/// "esta meta já existe" num lugar e "já existe usuário com este login" em outro, e uma
/// frase genérica para os dois não diria a nenhum deles o que corrigir.
///
/// Mora fora do repositório para poder ser testada sozinha: a ordem das regras é o que
/// mais erra aqui — um `Contains` genérico colocado cedo demais engole os específicos
/// que vêm depois, e o resultado é uma mensagem plausível e errada, que é pior do que o
/// texto cru porque ninguém desconfia dela.
/// </summary>
public static class MensagensDeErro
{
    /// <summary>Mensagem para violação de índice único, a partir do nome do índice.</summary>
    /// <remarks>
    /// As regras vão da MAIS específica para a mais genérica, e é isso que faz
    /// `IX_Lotes_Numero` cair em "lote" e não em "documento" — os dois contêm "Numero".
    /// </remarks>
    public static string Duplicidade(string? indice)
    {
        if (string.IsNullOrWhiteSpace(indice))
            return "Este registro já existe — a gravação foi recusada para não duplicá-lo.";

        // --- específicos por tabela ---
        if (Casa(indice, "Usuarios", "Login"))
            return "Já existe um usuário com este login.";

        if (Casa(indice, "Salas", "Nome"))
            return "Já existe uma sala com este nome.";

        if (Casa(indice, "Lotes", "Numero"))
            return "Já existe um lote com este número.";

        if (indice.Contains("Metas", StringComparison.Ordinal))
            return "Já existe uma meta para este mês, indicador e profissional. "
                   + "Edite a que existe em vez de criar outra.";

        if (indice.Contains("Orcamentos", StringComparison.Ordinal))
            return "Já existe um teto de gasto para esta categoria neste mês.";

        if (indice.Contains("OrigemRecorrencia", StringComparison.Ordinal))
            return "Esta conta já foi gerada por esta recorrência — ela não entra duas vezes.";

        if (indice.Contains("Contatos", StringComparison.Ordinal))
            return "Esta mensagem já foi registrada para este paciente nesta rodada.";

        // --- genéricos por coluna, só depois dos específicos ---
        if (indice.Contains("CodigoVerificacao", StringComparison.Ordinal))
            return "Já existe um documento com este código de conferência.";

        if (indice.Contains("Numero", StringComparison.Ordinal))
            return "Já existe um documento com este número. Emita novamente para receber "
                   + "o próximo da sequência.";

        return "Este registro já existe — a gravação foi recusada para não duplicá-lo. "
               + $"(índice: {indice})";
    }

    /// <summary>
    /// Mensagem para queda de conexão.
    ///
    /// O `EnableRetryOnFailure` já tentou três vezes antes de chegar aqui, então pedir
    /// "tente de novo" sem mais nada mandaria a pessoa repetir o que já falhou quatro
    /// vezes. O que resolve é olhar a internet — o banco é remoto.
    /// </summary>
    public const string SemConexao =
        "Não foi possível falar com o banco de dados. Verifique a conexão com a internet "
        + "e repita a operação — nada foi gravado.";

    /// <summary>Mensagem para chave estrangeira apontando para registro que sumiu.</summary>
    public const string VinculoQuebrado =
        "Este registro está ligado a outro que não existe mais. "
        + "Atualize a tela (F5) e repita a operação.";

    private static bool Casa(string indice, string tabela, string coluna)
        => indice.Contains(tabela, StringComparison.Ordinal)
           && indice.Contains(coluna, StringComparison.Ordinal);
}
