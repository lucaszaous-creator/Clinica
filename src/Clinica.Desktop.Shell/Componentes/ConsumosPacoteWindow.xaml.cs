using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Lista as sessões já debitadas do pacote e devolve ao saldo a que foi debitada por
/// engano. Não fecha ao devolver: quem corrige uma sessão costuma conferir as outras
/// logo em seguida. Quem pergunta se o saldo mudou é a tela de Pacotes, lendo
/// <see cref="ConsumosPacoteViewModel.Mudou"/> depois do <c>ShowDialog</c>.
/// </summary>
public partial class ConsumosPacoteWindow : Window
{
    public ConsumosPacoteWindow(ConsumosPacoteViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.CarregarAsync();
    }
}
