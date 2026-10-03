using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

public partial class DocumentoOpcoesWindow : Window
{
    public DocumentoOpcoesWindow(DocumentoEdicaoViewModel vm, int aba = 0)
    {
        InitializeComponent();
        DataContext = vm;
        Abas.SelectedIndex = aba;
    }
}
