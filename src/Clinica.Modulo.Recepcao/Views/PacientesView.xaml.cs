using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class PacientesView : UserControl
{
    public PacientesView() => InitializeComponent();

    /// <summary>
    /// O "⋯" da linha de documento: as ações que não são a principal daquele papel.
    ///
    /// O menu é o do shell (<see cref="MenuDoDocumento"/>) — são três telas com os mesmos
    /// seis atos, e três cópias dos rótulos ("Imprimir a 2ª via", "Assinar com o e-CPF…")
    /// divergiriam na primeira correção: a que ficasse para trás chamaria o mesmo ato por
    /// outro nome.
    /// </summary>
    private void AoAbrirMenuDoDocumento(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao) return;
        if (botao.DataContext is not LinhaDocumento linha) return;
        if (DataContext is not FichaPacienteViewModel vm) return;

        MenuDoDocumento.Abrir(sender, linha.Documento, linha, new ComandosDoDocumento(
            vm.ImprimirDocumentoCommand, vm.AssinarDocumentoCommand, vm.EnviarDocumentoCommand,
            vm.RenovarLinkDocumentoCommand, vm.TirarDoArDocumentoCommand,
            vm.CancelarDocumentoCommand));
    }
}
