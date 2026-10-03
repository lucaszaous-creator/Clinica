using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Emissão de receita, atestado, declaração de comparecimento e pedido de exame.</summary>
public partial class DocumentoWindow : Window
{
    internal void AtualizarFolha()
    {
        Content = null;
        if (RolagemFolha.Parent is System.Windows.Controls.Border superficie) superficie.Child = null;
        Content = new DocumentoFolha((DocumentoEdicaoViewModel)DataContext, this, RolagemFolha);
    }

    public DocumentoWindow(DocumentoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        AtualizarFolha();

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
