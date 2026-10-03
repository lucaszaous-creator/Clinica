using System.Windows.Controls;
using System.Windows.Input;
using Clinica.Clinico.ViewModels;
namespace Clinica.Clinico.Views;
public partial class HistoricoConsultaView : UserControl
{
    public HistoricoConsultaView()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || DataContext is not HistoricoConsultaViewModel vm) return;
            vm.FecharCommand.Execute(null);
            e.Handled = true;
        };
    }
    public void FocarFechar()
    {
        FecharHistorico.Focus();
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(FecharHistorico), FecharHistorico);
    }
}
