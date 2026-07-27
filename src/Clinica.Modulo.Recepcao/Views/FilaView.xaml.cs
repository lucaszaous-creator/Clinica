using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Fila de hoje em kanban.
///
/// A View liga e desliga o relógio que envelhece os tempos de espera: o shell constrói
/// uma tela nova a cada navegação, e um <c>DispatcherTimer</c> rodando manteria vivo
/// cada ViewModel já trocado (e faria vários recalcularem a mesma coisa).
/// </summary>
public partial class FilaView : UserControl
{
    public FilaView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as FilaViewModel)?.IniciarRelogio();
        Unloaded += (_, _) => (DataContext as FilaViewModel)?.PararRelogio();
    }
}
