using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// A lista de espera, que era uma faixa lateral de 320 px grudada na agenda.
///
/// Recebe o MESMO <see cref="AgendaViewModel"/> da tela de trás — como o catálogo de
/// pacotes e as contas fixas do Financeiro na parcela 49. Dois ViewModels dariam duas
/// verdades sobre a mesma tabela, e o "quem chamar para as 14h" da agenda deixaria de
/// valer aqui dentro.
/// </summary>
public partial class ListaEsperaPainelWindow : Window
{
    public ListaEsperaPainelWindow(AgendaViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void AoFechar(object sender, RoutedEventArgs e) => Close();
}
