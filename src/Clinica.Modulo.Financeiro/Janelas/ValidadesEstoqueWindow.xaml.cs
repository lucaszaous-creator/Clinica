using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>
/// Lotes vencendo e itens abaixo do mínimo — e o cadastro de item.
///
/// Recebe o MESMO ViewModel da tela de Estoque: cadastrar um item aqui tem de aparecer na
/// lista de trás, e o alerta é calculado sobre o mesmo saldo.
/// </summary>
public partial class ValidadesEstoqueWindow : Window
{
    public ValidadesEstoqueWindow(EstoqueViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
