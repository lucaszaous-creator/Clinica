using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A legenda das FAMÍLIAS de modalidade (set/2026 — a agenda com cor, mockup aprovado em
/// docs/mockups/agenda-com-cor.html). Um XAML para as três telas que pintam a família: a
/// lista do dia do balcão, a grade e o Meu dia do médico. Três cópias divergiriam na
/// primeira família nova — e a legenda que ficasse para trás mentiria sobre a cor.
///
/// Ela fica SEMPRE (decisão da direção): quem chega novo ao balcão lê a cor no primeiro
/// dia, e uma linha de 12 px no pé da tela não custa altura a ninguém.
/// </summary>
public partial class LegendaFamiliasView : UserControl
{
    public LegendaFamiliasView() => InitializeComponent();

    /// <summary>Acrescenta o "✓ confirmou na rodada" — as duas LISTAS, onde o ✓ aparece.</summary>
    public static readonly DependencyProperty ComConfirmacaoProperty =
        DependencyProperty.Register(nameof(ComConfirmacao), typeof(bool), typeof(LegendaFamiliasView),
            new PropertyMetadata(false));

    public bool ComConfirmacao
    {
        get => (bool)GetValue(ComConfirmacaoProperty);
        set => SetValue(ComConfirmacaoProperty, value);
    }

    /// <summary>Acrescenta o encaixe e a agenda fechada — só a GRADE os desenha.</summary>
    public static readonly DependencyProperty ComGradeProperty =
        DependencyProperty.Register(nameof(ComGrade), typeof(bool), typeof(LegendaFamiliasView),
            new PropertyMetadata(false));

    public bool ComGrade
    {
        get => (bool)GetValue(ComGradeProperty);
        set => SetValue(ComGradeProperty, value);
    }
}
