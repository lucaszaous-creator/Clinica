using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>
/// Achar um papel que já saiu (out/2026 — a clínica: "os laudos precisam ser fáceis de
/// LOCALIZAR e IMPRIMIR"). A aba "O que já saiu" da central só recortava por período e
/// por paciente escolhido; quem tinha o número "2026/0001" ou o código de conferência do
/// rodapé na mão rolava a lista do mês procurando a linha.
///
/// ⚠️ Mora na Application, e não na ViewModel: decidir que linha APARECE é o que a tela
/// afirma, e afirmação que só existe dentro de um projeto WPF é afirmação que o
/// <c>dotnet test</c> não alcança (a regra da <c>GradeSemana</c>).
/// </summary>
public static class BuscaNasFolhas
{
    /// <summary>A opção "sem filtro" do combo de tipo — a primeira da lista.</summary>
    public const string TodasAsFolhas = "Todas as folhas";

    private static readonly CompareInfo Comparador = new CultureInfo("pt-BR").CompareInfo;
    private const CompareOptions SemCaixaNemAcento =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// A linha bate com o termo E com o tipo escolhidos.
    ///
    /// O termo casa por TRECHO, sem caixa e sem acento, no número, no código de
    /// conferência, na folha, no paciente e em quem assinou. O número casa também SEM a
    /// barra ("20260001" acha "2026/0001"): é como ele sai digitado por quem lê o papel.
    ///
    /// ⚠️ Tipo nulo ou vazio é "todas" — o <c>ComboBox</c> cuja lista é refeita escreve
    /// NULL no item escolhido por um instante, e nulo não pode esconder a lista inteira
    /// (a lição dos filtros dos Lançamentos).
    /// </summary>
    public static bool Casa(FolhaEmitida folha, string? termo, string? tipo)
    {
        if (!string.IsNullOrWhiteSpace(tipo) && tipo != TodasAsFolhas
            && !string.Equals(folha.FolhaRotulo, tipo, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrWhiteSpace(termo)) return true;
        var t = termo.Trim();

        if (Contem(folha.Numero, t) || Contem(folha.CodigoVerificacao, t)
            || Contem(folha.FolhaRotulo, t) || Contem(folha.Paciente, t)
            || Contem(folha.Profissional, t))
            return true;

        var soAlfanumerico = SoAlfanumerico(t);
        return soAlfanumerico.Length > 0
               && (SoAlfanumerico(folha.Numero).Contains(soAlfanumerico, StringComparison.OrdinalIgnoreCase)
                   || SoAlfanumerico(folha.CodigoVerificacao).Contains(soAlfanumerico, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// As opções do combo de tipo: "Todas as folhas", depois as folhas do CATÁLOGO que esta
    /// pessoa alcança, na ordem dele, e por fim as que aparecem na lista sem estar no
    /// catálogo (um tipo gravado por uma versão mais nova). Oferecer só o que foi emitido no
    /// período faria o tipo sumir do combo justamente quando a pessoa o procura e ele não
    /// está lá — e "não há laudo neste período" é uma resposta, não um combo sem a opção.
    /// </summary>
    public static IReadOnlyList<string> Tipos(
        IEnumerable<string> catalogo, IEnumerable<FolhaEmitida> emitidas)
    {
        var lista = new List<string> { TodasAsFolhas };
        foreach (var rotulo in catalogo.Concat(emitidas.Select(e => e.FolhaRotulo)))
            if (!string.IsNullOrWhiteSpace(rotulo) && !lista.Contains(rotulo))
                lista.Add(rotulo);
        return lista;
    }

    /// <summary>
    /// O resumo quando o filtro está ligado: "3 de 30 batem com a busca". Vazio quando nada
    /// filtra — e então quem fala é o resumo do período, sozinho.
    /// </summary>
    public static string Recorte(int mostradas, int total, string? termo, string? tipo)
    {
        var temTermo = !string.IsNullOrWhiteSpace(termo);
        var temTipo = !string.IsNullOrWhiteSpace(tipo) && tipo != TodasAsFolhas;
        if (!temTermo && !temTipo) return string.Empty;

        var criterio = (temTermo, temTipo) switch
        {
            (true, true) => $"com “{termo!.Trim()}” em {tipo}",
            (true, false) => $"com “{termo!.Trim()}”",
            _ => $"de {tipo}"
        };
        return $"{mostradas} de {total} {criterio}";
    }

    private static bool Contem(string? onde, string termo)
        => !string.IsNullOrEmpty(onde) && Comparador.IndexOf(onde, termo, SemCaixaNemAcento) >= 0;

    private static string SoAlfanumerico(string? texto)
        => texto is null ? string.Empty : new string(texto.Where(char.IsLetterOrDigit).ToArray());
}
