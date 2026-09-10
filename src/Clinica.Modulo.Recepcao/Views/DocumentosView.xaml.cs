using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// A central de documentos: as nove folhas do mockup num lugar só, e a lista do que já
/// saiu — clínico e financeiro na mesma lista, com os seis atos do documento.
/// </summary>
public partial class DocumentosView : UserControl
{
    public DocumentosView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// O "⋯" da linha: as ações que não são a principal daquele papel. O menu é o do shell
    /// (<see cref="MenuDoDocumento"/>) — são três telas com os mesmos seis atos, e três
    /// cópias dos rótulos divergiriam na primeira correção.
    ///
    /// ⚠️ Na folha FINANCEIRA (recibo, orçamento) não há documento clínico: ali o menu não
    /// se aplica — não há assinatura, link nem e-CPF —, e os dois atos que ela tem (2ª via e
    /// cancelar) continuam nos botões da linha.
    /// </summary>
    private void AoAbrirMenuDoDocumento(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement botao) return;
        if (botao.DataContext is not LinhaFolhaEmitida linha) return;
        if (DataContext is not DocumentosViewModel vm) return;

        if (linha.Documento is not { } doc)
        {
            // A folha financeira tem DOIS atos, e o menu os oferece pelos mesmos rótulos —
            // menu que abre vazio é o botão que não faz nada da parcela 41.
            MenuDaFolhaFinanceira(sender, linha, vm);
            return;
        }

        MenuDoDocumento.Abrir(sender, doc, linha, new ComandosDoDocumento(
            vm.ReimprimirCommand, vm.AssinarCommand, vm.EnviarCommand,
            vm.RenovarLinkCommand, vm.TirarDoArCommand, vm.CancelarCommand));
    }

    /// <summary>
    /// Recibo e orçamento: 2ª via e cancelar com motivo. Não se assinam com e-CPF (não é
    /// documento de saúde) e não se publicam — oferecer os outros quatro atos aqui seria
    /// prometer o que o serviço financeiro não faz.
    /// </summary>
    private static void MenuDaFolhaFinanceira(
        object remetente, LinhaFolhaEmitida linha, DocumentosViewModel vm)
    {
        if (remetente is not FrameworkElement botao) return;

        var menu = new ContextMenu
        {
            PlacementTarget = botao,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        menu.Items.Add(new MenuItem
        {
            Header = "Imprimir a 2ª via",
            Command = vm.ReimprimirCommand,
            CommandParameter = linha
        });

        if (linha.PodeCancelar)
            menu.Items.Add(new MenuItem
            {
                Header = "Cancelar o documento…",
                Command = vm.CancelarCommand,
                CommandParameter = linha
            });

        menu.IsOpen = true;
    }
}
