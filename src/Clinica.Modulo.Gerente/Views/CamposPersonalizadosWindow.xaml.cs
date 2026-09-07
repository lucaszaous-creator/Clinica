using System.Windows;
using Clinica.Gerente.ViewModels;

namespace Clinica.Gerente.Views;

/// <summary>
/// O cadastro dos campos personalizados do prontuário. A janela não grava nada: quem grava
/// é o ViewModel, pelo botão do rodapé.
/// </summary>
public partial class CamposPersonalizadosWindow : Window
{
    public CamposPersonalizadosWindow(CamposPersonalizadosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.CarregarAsync();
    }
}
