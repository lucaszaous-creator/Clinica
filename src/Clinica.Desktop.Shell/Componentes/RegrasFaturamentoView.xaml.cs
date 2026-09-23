namespace Clinica.Desktop.Shell.Componentes;
public partial class RegrasFaturamentoView : System.Windows.Controls.UserControl
{ public RegrasFaturamentoView() { InitializeComponent(); Loaded += async (_,_) => { if(DataContext is RegrasFaturamentoViewModel vm) await vm.CarregarAsync(); }; } }
