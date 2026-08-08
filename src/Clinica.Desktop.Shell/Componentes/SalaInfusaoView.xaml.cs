using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A fila da sala de infusão.
///
/// A View liga e desliga a releitura periódica, como o quadro do "Meu dia" (parcela 38):
/// quem assina a prescrição está noutra máquina, e sem ela a folha nova só apareceria no
/// próximo clique de Atualizar. Ligar aqui, e não no ViewModel, é o que impede um
/// <c>DispatcherTimer</c> de manter viva cada tela já trocada — o shell constrói uma nova
/// a cada navegação.
/// </summary>
public partial class SalaInfusaoView : UserControl
{
    public SalaInfusaoView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as SalaInfusaoViewModel)?.Acompanhar(true);
        Unloaded += (_, _) => (DataContext as SalaInfusaoViewModel)?.Acompanhar(false);
    }
}
