using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Desfazer uma sessão lançada por engano. Ver <see cref="EstornoAtendimentoViewModel"/>
/// para as decisões — em especial por que a consulta renovada não é desfeita aqui.
/// </summary>
public partial class EstornoAtendimentoWindow : Window
{
    private readonly EstornoAtendimentoViewModel _vm;

    public EstornoAtendimentoWindow(EstornoAtendimentoViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = vm;

        vm.Concluiu += AoConcluir;
        Closed += (_, _) => vm.Concluiu -= AoConcluir;

        // A carga é da VIEW: no construtor da ViewModel ela correria antes de a janela
        // existir, e um erro nela apareceria sem tela para mostrá-lo.
        Loaded += async (_, _) => await vm.CarregarAsync();
    }

    private void AoConcluir()
    {
        DialogResult = true;
        Close();
    }
}
