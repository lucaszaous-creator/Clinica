using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Clinica.Financeiro.Views;

public partial class CaixaView : UserControl
{
    public CaixaView()
    {
        InitializeComponent();
    }

    private void AoMudarTamanho(object sender, SizeChangedEventArgs e)
    {
        if (ResumoCaixa is null) return;
        var empilhar = ActualWidth < 760;
        ColunaMovimentos.Width = empilhar ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(MovimentosPeriodo, empilhar ? 0 : 1);
        Grid.SetRow(MovimentosPeriodo, empilhar ? 1 : 0);
        ResultadoPeriodo.Margin = empilhar ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 16, 0);
    }
}

/// <summary>Reserva espaço para a lista enquanto cabeçalhos e filtros continuam roláveis.</summary>
public sealed class AlturaCabecalhoFinanceiroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var altura = value is double atual && double.IsFinite(atual) ? atual : 0;
        if (parameter is string texto && texto.StartsWith("reservar:", StringComparison.Ordinal)
            && double.TryParse(texto[9..], NumberStyles.Float, CultureInfo.InvariantCulture, out var reserva))
            return Math.Max(48, altura - reserva);
        var proporcao = double.TryParse(parameter?.ToString(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var informada) ? Math.Clamp(informada, 0.1, 0.6) : 0.4;
        return Math.Max(48, altura * proporcao);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
