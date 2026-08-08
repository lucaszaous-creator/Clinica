using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>
/// O catálogo de pacotes — o que está à venda.
///
/// Recebe o MESMO ViewModel da tela de Pacotes, e não uma cópia: o catálogo e os pacotes
/// vendidos são a mesma leitura de banco, e dois ViewModels dariam duas verdades sobre a
/// mesma tabela — cadastrar um pacote aqui e a tela de trás continuar mostrando a lista
/// velha é o defeito que isso evita.
/// </summary>
public partial class CatalogoPacotesWindow : Window
{
    public CatalogoPacotesWindow(PacotesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
