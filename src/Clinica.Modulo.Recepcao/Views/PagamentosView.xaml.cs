using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class PagamentosView : UserControl
{
    public PagamentosView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true && DataContext is PagamentosViewModel vm) _ = vm.CarregarAsync();
        };
    }
}
