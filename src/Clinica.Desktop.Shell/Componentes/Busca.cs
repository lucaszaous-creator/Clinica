using System.Globalization;
using System.Text;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Comparação de texto para FILTRO DIGITADO, dentro de listas já carregadas.
///
/// Não substitui a busca no banco (<c>SeletorPacienteViewModel</c>, que resolve limite e
/// ordem no SQL). Serve ao caso oposto: a lista já veio, tem dezenas de linhas, e a
/// pessoa digita para achar uma.
///
/// O acento é o ponto. Quem está com o demonstrativo da operadora na mão digita
/// "jose antonio" — sem tirar o acento dos DOIS lados, "José Antônio" não aparece, e o
/// filtro passa a impressão de que a guia não está na lista. Um filtro que esconde o que
/// existe é pior do que filtro nenhum, porque a pessoa conclui e age.
/// </summary>
public static class Busca
{
    /// <summary>
    /// <paramref name="texto"/> contém <paramref name="termo"/>, ignorando caixa e acento.
    /// Termo vazio casa com tudo — quem não digitou não filtrou.
    /// </summary>
    public static bool Casa(string? texto, string? termo)
    {
        if (string.IsNullOrWhiteSpace(termo)) return true;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        return Simplificar(texto).Contains(Simplificar(termo), StringComparison.Ordinal);
    }

    /// <summary>Minúsculas, sem acento e sem espaço nas pontas.</summary>
    public static string Simplificar(string texto)
    {
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpo = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                limpo.Append(c);

        return limpo.ToString().Normalize(NormalizationForm.FormC);
    }
}
