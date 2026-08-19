using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>O extrato do item de estoque — que movimentos produziram este saldo.</summary>
public partial class ExtratoEstoqueWindow : Window
{
    public ExtratoEstoqueWindow(ExtratoEstoqueViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
