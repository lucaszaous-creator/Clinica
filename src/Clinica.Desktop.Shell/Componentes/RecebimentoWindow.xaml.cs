namespace Clinica.Desktop.Shell.Componentes;
public partial class RecebimentoWindow : System.Windows.Window
{
 public RecebimentoWindow(IRecebimento vm)
 {
  InitializeComponent(); DataContext = vm;
  void Concluir() => DialogResult = true;
  vm.Concluido += Concluir; Closed += (_,_) => vm.Concluido -= Concluir;
 }
}
