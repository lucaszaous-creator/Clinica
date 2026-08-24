using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clinica.Domain.Entities;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A figura do mapa corporal, compartilhada pelos módulos que registram sessão
/// (parcela 36). O <c>DataContext</c> esperado é um <see cref="MapaCorporalViewModel"/>.
/// </summary>
public partial class MapaCorporalControl : UserControl
{
    public MapaCorporalControl()
    {
        InitializeComponent();

        // A figura é montada da lista do DOMÍNIO (parcela 79), a mesma que o PDF desenha.
        // Enquanto ela era um GeometryGroup no XAML, pôr o mapa no papel significava
        // desenhar o corpo uma segunda vez — e a segunda correção de silhueta já sairia
        // divergente, com a agulha num lugar na tela e noutro no papel do paciente.
        CorpoFrente.Data = Geometria();
        CorpoCostas.Data = Geometria();

        Coluna.X1 = Coluna.X2 = SilhuetaCorporal.ColunaX;
        Coluna.Y1 = SilhuetaCorporal.ColunaTopo;
        Coluna.Y2 = SilhuetaCorporal.ColunaBase;

        foreach (var tela in new FrameworkElement[]
                 { TelaFrente, TelaCostas, PontosFrente, PontosCostas })
        {
            tela.Width = SilhuetaCorporal.Largura;
            tela.Height = SilhuetaCorporal.Altura;
        }
    }

    /// <summary>
    /// A silhueta do domínio virada em <see cref="Geometry"/> do WPF.
    ///
    /// ⚠️ A tradução mora AQUI e não no domínio porque <c>Clinica.Domain</c> não referencia
    /// WPF — e não deve: é a mesma camada que o Consultório, a Recepção e o PDF leem. O que
    /// atravessa é a LISTA de formas; cada lado a desenha com o que tem (aqui um
    /// <c>GeometryGroup</c>, no papel um SVG).
    ///
    /// Uma <c>Geometry</c> por Path, e não uma compartilhada: geometria congelada dava para
    /// dividir, mas as duas figuras são independentes e o custo de montar doze primitivas
    /// é nenhum perto de acoplá-las.
    /// </summary>
    private static Geometry Geometria()
    {
        var grupo = new GeometryGroup { FillRule = FillRule.Nonzero };

        foreach (var forma in SilhuetaCorporal.Formas)
            grupo.Children.Add(forma switch
            {
                ElipseSilhueta e => new EllipseGeometry(new Point(e.Cx, e.Cy), e.Rx, e.Ry),
                RetanguloSilhueta r => new RectangleGeometry(
                    new Rect(r.X, r.Y, r.Largura, r.Altura), r.Raio, r.Raio),
                _ => (Geometry)Geometry.Empty
            });

        return grupo;
    }

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
