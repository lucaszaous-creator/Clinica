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
    // Nov/2026: a montagem subiu para a Application (CsvParaExcel), que a gestão web também usa
    // — e ganhou a neutralização de fórmula (CSV injection). Este nome fica para os chamadores do shell.
    public static byte[] Montar(IReadOnlyList<string> cabecalho, IEnumerable<IReadOnlyList<string>> linhas)
        => Clinica.Application.Formatacao.CsvParaExcel.Montar(cabecalho, linhas);
}
