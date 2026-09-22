using System.Windows;
namespace Clinica.Desktop.Shell.Componentes;

public partial class MateriaisProcedimentoWindow : Window
{
    public MateriaisProcedimentoWindow(MateriaisProcedimentoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Confirmado += () => DialogResult = true;
    }
}
