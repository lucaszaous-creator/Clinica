using System.Windows;
using Clinica.Desktop.ViewModels;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Cadastro da autorização de sessões do convênio. Retorna <c>true</c> quando gravou,
/// para a ficha do paciente recarregar os saldos.
/// </summary>
public partial class AutorizacaoWindow : Window
{
    public AutorizacaoWindow(AutorizacaoEdicaoViewModel vm, int? autorizacaoId, string? convenioPadrao)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Salvou += AoSalvar;
        Closed += (_, _) => vm.Salvou -= AoSalvar;

        Loaded += async (_, _) => await vm.CarregarAsync(autorizacaoId, convenioPadrao);
    }

    private void AoSalvar()
    {
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
