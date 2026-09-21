using Clinica.Domain.Entities;

namespace Clinica.Domain;

/// <summary>Converte modelos da clínica em texto editável, sem interpretar medicamentos.</summary>
public static class TextoReceituario
{
    public static (string Texto, string? Formato) ConteudoDoModelo(ModeloDocumento modelo)
    {
        var partes = new List<(string?, string?)> { (modelo.Corpo, modelo.CorpoFormatado) };
        foreach (var item in modelo.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
        {
            var linha = TextoFormatado.Juntar([(item.Descricao, item.DescricaoFormatada),
                (item.Quantidade, null), (item.Detalhe, item.DetalheFormatado)], "\n");
            partes.Add(linha);
        }
        return TextoFormatado.Juntar(partes);
    }

    public static string DoModelo(ModeloDocumento modelo)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(modelo.Corpo)) partes.Add(modelo.Corpo.Trim());
        foreach (var item in modelo.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
        {
            var linhas = new[] { item.Descricao, item.Quantidade, item.Detalhe }
                .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim());
            var texto = string.Join(Environment.NewLine, linhas);
            if (texto.Length > 0) partes.Add(texto);
        }
        return string.Join(Environment.NewLine + Environment.NewLine, partes);
    }

    /// <summary>Acrescentar uma sugestão preserva exatamente o texto já digitado.</summary>
    public static string Acrescentar(string? atual, string sugestao)
    {
        if (string.IsNullOrWhiteSpace(sugestao)) return atual ?? string.Empty;
        if (string.IsNullOrEmpty(atual)) return sugestao;
        return atual + Environment.NewLine + Environment.NewLine + sugestao;
    }
}
