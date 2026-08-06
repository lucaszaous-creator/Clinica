using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Emissão de receita, atestado, declaração de comparecimento e pedido de exame.</summary>
public partial class DocumentoWindow : Window
{
    public DocumentoWindow(DocumentoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // O seletor de certificado é uma janela, e o ViewModel não conhece WPF — ele
        // recebe a função e chama quando precisa, como o `Fechar` do próprio seletor.
        vm.EscolherCertificado = assunto => EscolherCertificadoWindow.Perguntar(assunto, this);
        Closed += (_, _) => vm.EscolherCertificado = null;

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
