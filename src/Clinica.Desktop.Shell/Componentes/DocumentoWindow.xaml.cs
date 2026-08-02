using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Emissão de receita, atestado, declaração de comparecimento e pedido de exame.</summary>
public partial class DocumentoWindow : Window
{
    public DocumentoWindow(DocumentoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
