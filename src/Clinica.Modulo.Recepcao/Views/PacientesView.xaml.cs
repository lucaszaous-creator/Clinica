using System.Windows.Controls;
using System.Windows.Input;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class PacientesView : UserControl
{
    public PacientesView() => InitializeComponent();

    /// <summary>Enter abre a ficha quando sobra um só — a promessa do rodapé.</summary>
    private void AoTeclarNaBusca(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not PacientesViewModel vm) return;
        vm.AbrirSeUnico();
        e.Handled = true;
    }
}
