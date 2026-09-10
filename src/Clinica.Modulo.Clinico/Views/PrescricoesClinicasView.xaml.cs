using System.Windows;
using System.Windows.Controls;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Clinico.Views;

/// <summary>Prescrições do consultório: emitir e reimprimir documento clínico.</summary>
public partial class PrescricoesClinicasView : UserControl
{
    public PrescricoesClinicasView() => InitializeComponent();

    /// <summary>
    /// O "⋯" da linha: as ações que não são a principal daquele documento.
    ///
    /// O menu é o do shell (<see cref="MenuDoDocumento"/>) desde set/2026 — são TRÊS telas
    /// com os mesmos seis atos, e os rótulos deles ("Imprimir a 2ª via", "Assinar com o
    /// e-CPF…") são metade do que a consolidação entrega: três cópias chamariam o mesmo ato
    /// por nomes diferentes na primeira correção.
    /// </summary>
    private void AoAbrirMenuDoDocumento(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao) return;
        if (botao.DataContext is not LinhaDocumentoClinico linha) return;
        if (DataContext is not PrescricoesClinicasViewModel vm) return;

        MenuDoDocumento.Abrir(sender, linha.Documento, linha, new ComandosDoDocumento(
            vm.ImprimirCommand, vm.AssinarCommand, vm.EnviarCommand,
            vm.RenovarLinkCommand, vm.TirarDoArCommand, vm.CancelarCommand));
    }
}
