using System.Globalization;
using System.Windows.Data;
using Clinica.Domain;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Traduz um valor de enum para o rótulo da tela (parcela 41).
///
/// Por que um conversor, e não <c>DisplayMemberPath</c>
/// ---------------------------------------------------
/// <c>DisplayMemberPath</c> lê uma PROPRIEDADE do item, e enum não tem propriedade. As
/// alternativas eram embrulhar cada enum num objeto só para a tela — dezesseis listas
/// novas, uma por seletor — ou converter na hora. O conversor ganha porque a lista
/// continua sendo a do enum: o <c>SelectedItem</c> segue amarrado ao valor de verdade, e
/// nada no ViewModel muda.
///
/// Uso
/// ---
/// <code>
/// &lt;ComboBox ItemsSource="{Binding Tipos}" SelectedItem="{Binding TipoSelecionado}"
///           ItemTemplate="{StaticResource ItemRotuloEnum}" /&gt;
/// </code>
///
/// O template <c>ItemRotuloEnum</c> (em Componentes/Campos.xaml) já aplica este conversor
/// — é ele que as telas usam, e não o conversor solto.
///
/// A conversão de VOLTA não existe de propósito: o item escolhido é o próprio valor do
/// enum, e o rótulo nunca precisa virar enum outra vez. Um <c>ConvertBack</c> que
/// procurasse o enum pelo texto passaria a depender da grafia do rótulo — e um acento
/// corrigido quebraria a seleção.
/// </summary>
public sealed class RotuloEnum : IValueConverter
{
    /// <summary>Instância pronta, para quem precisa do conversor fora de um dicionário.</summary>
    public static RotuloEnum Instancia { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => RotulosEnum.De(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
