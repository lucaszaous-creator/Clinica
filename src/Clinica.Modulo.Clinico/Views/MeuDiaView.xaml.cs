using System.Windows.Controls;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Views;

/// <summary>
/// Abertura do consultório: a agenda do profissional como LISTA (parcela 95) e o que
/// ficou sem registro.
///
/// A View liga e desliga a releitura periódica (parcela 38; 30 s desde set/2026 — o
/// porquê do intervalo está no <c>_relogio</c> da ViewModel). Quem marca a chegada,
/// cancela e corrige o que a sessão é continua sendo o balcão, na outra máquina, e sem
/// ela a lista só mudaria por clique em Atualizar. Ligar aqui, e não no ViewModel, é o
/// que impede um <c>DispatcherTimer</c> de manter vivo cada tela já trocada — o shell
/// constrói uma nova a cada navegação.
///
/// O arrasto entre raias morreu com as raias: a lista não tem para onde arrastar, e as
/// transições continuam nos botões da linha.
/// </summary>
public partial class MeuDiaView : UserControl
{
    public MeuDiaView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as MeuDiaViewModel)?.AoEntrarEmCena();
        Unloaded += (_, _) => (DataContext as MeuDiaViewModel)?.AoSairDeCena();
    }
}
