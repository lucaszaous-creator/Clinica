using Clinica.Domain;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Fontes de valores de enum para colunas de DataGrid (que ficam fora da árvore
/// visual e não enxergam o DataContext). Referenciadas por {x:Static}.
/// </summary>
public static class FontesEnum
{
    public static Array Categorias { get; } = Enum.GetValues(typeof(Categoria));

    /// <summary>Famílias de regra de faturamento (os convênios embutidos + Personalizado).</summary>
    public static Array Familias { get; } = Enum.GetValues(typeof(Convenio));

    /// <summary>Bases de comportamento das modalidades (as modalidades embutidas do motor de regras).</summary>
    public static Array ModalidadesBase { get; } = Enum.GetValues(typeof(ModalidadeAtendimento));

    /// <summary>Formas de obtenção do 2º código (para a regra personalizada).</summary>
    public static Array FormasObtencao { get; } = new[]
    {
        FormaObtencao.Sistema, FormaObtencao.App, FormaObtencao.Ligacao
    };
}
