using Clinica.Domain;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Fontes de valores de enum para colunas de DataGrid (que ficam fora da árvore
/// visual e não enxergam o DataContext). Referenciadas por {x:Static}.
/// </summary>
public static class FontesEnum
{
    public static Array Categorias { get; } = Enum.GetValues(typeof(Categoria));

    /// <summary>Famílias de regra de faturamento (os convênios embutidos).</summary>
    public static Array Familias { get; } = Enum.GetValues(typeof(Convenio));
}
