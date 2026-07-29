using System.Text;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Monta CSV para o Excel em português. Duas decisões que parecem detalhe e não são:
///
/// 1. **Separador ponto e vírgula.** O Excel em pt-BR usa a vírgula como decimal, então
///    um CSV separado por vírgula abre com tudo numa coluna só. A direção conclui que a
///    exportação está quebrada — e, do ponto de vista dela, está.
/// 2. **BOM UTF-8.** Sem ele o Excel lê o arquivo como ANSI e "Ocupação" vira "OcupaÃ§Ã£o"
///    na primeira linha do relatório.
///
/// Existe aqui, no shell, porque relatório é coisa de mais de um módulo e a regra de
/// escape não pode ter duas versões.
/// </summary>
public static class ExportacaoCsv
{
    /// <summary>Monta o arquivo a partir do cabeçalho e das linhas, já em bytes.</summary>
    public static byte[] Montar(IReadOnlyList<string> cabecalho, IEnumerable<IReadOnlyList<string>> linhas)
    {
        var texto = new StringBuilder();
        texto.AppendLine(string.Join(';', cabecalho.Select(Escapar)));
        foreach (var linha in linhas)
            texto.AppendLine(string.Join(';', linha.Select(Escapar)));

        // encoderShouldEmitUTF8Identifier: true = o BOM que o Excel precisa.
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(texto.ToString());
    }

    /// <summary>
    /// Aspas quando o campo contém o separador, aspas ou quebra de linha — e as aspas de
    /// dentro viram duas, como manda o formato. A justificativa de uma não conformidade
    /// tem ponto e vírgula e quebra de linha com frequência.
    /// </summary>
    private static string Escapar(string? campo)
    {
        campo ??= string.Empty;
        if (!campo.Contains(';') && !campo.Contains('"') && !campo.Contains('\n') && !campo.Contains('\r'))
            return campo;

        return $"\"{campo.Replace("\"", "\"\"")}\"";
    }
}
