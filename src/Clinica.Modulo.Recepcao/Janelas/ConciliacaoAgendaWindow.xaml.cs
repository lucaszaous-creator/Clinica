using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// A fila de horários pendurados. Não fecha ao resolver uma linha: a recepcionista
/// percorre a lista inteira de uma vez, e fechar a cada clique a obrigaria a reabrir
/// dezenas de vezes — a mesma razão da rodada de confirmação.
/// </summary>
public partial class ConciliacaoAgendaWindow : Window
{
    public ConciliacaoAgendaWindow(ConciliacaoAgendaViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // A carga é da VIEW, no Loaded: no construtor da ViewModel ela correria antes de
        // a janela existir, e um erro nela apareceria sem tela para mostrá-lo.
        Loaded += async (_, _) => await vm.CarregarAsync();
    }
}
