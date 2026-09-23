namespace Clinica.Desktop.Shell.Componentes;
public partial class DadosClinicaView : System.Windows.Controls.UserControl
{
    public DadosClinicaView() { InitializeComponent(); Loaded += async (_, _) => { if (DataContext is DadosClinicaViewModel vm) await vm.CarregarAsync(); }; }
}
