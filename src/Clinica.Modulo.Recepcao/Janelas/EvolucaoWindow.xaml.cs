using System.Windows;
using System.Windows.Input;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>Registro de uma sessão no prontuário (EVA, evolução, mapa corporal e anexos).</summary>
public partial class EvolucaoWindow : Window
{
    private readonly EvolucaoEdicaoViewModel _vm;

    public EvolucaoWindow(EvolucaoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }

    private void TelaFrente_Clique(object sender, MouseButtonEventArgs e)
        => Marcar(FaceCorpo.Frente, sender, e);

    private void TelaCostas_Clique(object sender, MouseButtonEventArgs e)
        => Marcar(FaceCorpo.Costas, sender, e);

    /// <summary>
    /// Converte o pixel clicado em fração da figura (0 a 1) e entrega ao ViewModel.
    /// A conversão mora aqui porque quem conhece o tamanho do desenho é a tela — o
    /// prontuário guarda a fração, que sobrevive a qualquer mudança de resolução.
    ///
    /// A posição é medida contra a TELA (o Canvas que recebeu o evento), nunca contra o
    /// que estiver embaixo do cursor: a silhueta é um filho dela, e medir contra o filho
    /// daria coordenadas de outro sistema.
    /// </summary>
    private void Marcar(FaceCorpo face, object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.IInputElement tela) return;

        var ponto = e.GetPosition(tela);
        _vm.Mapa.Marcar(
            face,
            ponto.X / PontoMapaItem.LarguraFigura,
            ponto.Y / PontoMapaItem.AlturaFigura);
    }
}
