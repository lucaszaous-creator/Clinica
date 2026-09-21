using System.Windows.Controls;
using Clinica.Clinico.ViewModels;
namespace Clinica.Clinico.Views;
public partial class SessoesEnfermagemView : UserControl
{
    public SessoesEnfermagemView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { if (DataContext is SessoesEnfermagemViewModel vm) await vm.CarregarAsync(); };
    }
}
