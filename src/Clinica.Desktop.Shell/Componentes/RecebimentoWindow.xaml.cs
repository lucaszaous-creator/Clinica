namespace Clinica.Desktop.Shell.Componentes;
public partial class RecebimentoWindow : System.Windows.Window
{
 public RecebimentoWindow(IRecebimento vm)
 {
  InitializeComponent(); DataContext = vm;
  var concluido = false;
  void Concluir() { concluido = true; DialogResult = true; }
  Closing += (_, e) => { if (vm.Ocupado && !concluido) e.Cancel = true; };
  vm.Concluido += Concluir; Closed += (_,_) => vm.Concluido -= Concluir;
 }
}
