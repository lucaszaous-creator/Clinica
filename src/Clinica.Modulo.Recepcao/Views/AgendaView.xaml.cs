using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Agenda multiprofissional: uma coluna por profissional, a linha do tempo descendo pela
/// régua da esquerda e o vão livre clicável.
///
/// A View liga e desliga a releitura de fundo (parcela 62). Ela mora aqui, e não no
/// ViewModel, pela mesma razão da Fila: o shell constrói uma tela nova a cada navegação,
/// e um <c>DispatcherTimer</c> rodando manteria vivo cada ViewModel já trocado.
/// </summary>
public partial class AgendaView : UserControl
{
    public AgendaView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as AgendaViewModel)?.IniciarRelogio();
        Unloaded += (_, _) => (DataContext as AgendaViewModel)?.PararRelogio();
    }
}
