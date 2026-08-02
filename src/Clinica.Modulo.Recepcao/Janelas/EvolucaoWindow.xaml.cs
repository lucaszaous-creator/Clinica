using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Registro de uma sessão no prontuário (EVA, evolução, mapa corporal e anexos).
///
/// Os manipuladores de clique na figura saíram daqui na parcela 36: a figura virou o
/// <c>MapaCorporalControl</c> do shell, e quem converte o pixel em fração é ela, que é
/// quem conhece o tamanho do desenho.
/// </summary>
public partial class EvolucaoWindow : Window
{
    public EvolucaoWindow(EvolucaoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
