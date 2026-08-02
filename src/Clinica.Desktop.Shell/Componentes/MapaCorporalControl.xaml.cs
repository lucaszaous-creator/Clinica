using System.Windows.Controls;
using System.Windows.Input;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A figura do mapa corporal, compartilhada pelos módulos que registram sessão
/// (parcela 36). O <c>DataContext</c> esperado é um <see cref="MapaCorporalViewModel"/>.
/// </summary>
public partial class MapaCorporalControl : UserControl
{
    public MapaCorporalControl() => InitializeComponent();

    private void TelaFrente_Clique(object sender, MouseButtonEventArgs e)
        => Marcar(FaceCorpo.Frente, sender, e);

    private void TelaCostas_Clique(object sender, MouseButtonEventArgs e)
        => Marcar(FaceCorpo.Costas, sender, e);

    /// <summary>
    /// Converte o pixel clicado em fração da figura (0 a 1) e entrega ao ViewModel.
    ///
    /// A conversão mora aqui porque quem conhece o tamanho do desenho é a tela — o
    /// prontuário guarda a FRAÇÃO, que sobrevive a qualquer mudança de resolução, de
    /// escala do Windows ou de figura.
    ///
    /// A posição é medida contra a TELA (o Canvas que recebeu o evento), nunca contra o
    /// que estiver embaixo do cursor: a silhueta é um filho dela, e medir contra o filho
    /// daria coordenadas de outro sistema.
    /// </summary>
    private void Marcar(FaceCorpo face, object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.IInputElement tela) return;
        if (DataContext is not MapaCorporalViewModel mapa) return;

        var ponto = e.GetPosition(tela);
        mapa.Marcar(
            face,
            ponto.X / PontoMapaItem.LarguraFigura,
            ponto.Y / PontoMapaItem.AlturaFigura);
    }
}
