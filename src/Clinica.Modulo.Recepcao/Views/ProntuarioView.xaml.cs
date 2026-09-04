using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Prontuário como tela própria. A porta de menu que faltava: até aqui só se chegava à
/// evolução por dentro da ficha do paciente.
/// </summary>
public partial class ProntuarioView : UserControl
{
    public ProntuarioView()
    {
        InitializeComponent();
        Loaded += (_, _) => FocarBuscaSeEstiverEscolhendo();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) FocarBuscaSeEstiverEscolhendo();
        };
    }

    /// <summary>
    /// O cursor vai para a busca quando a tela abre SEM paciente — que é o estado normal
    /// dela desde que a busca virou a tela (set/2026).
    ///
    /// ⚠️ Só quando a busca EXISTE na tela: com o paciente já escolhido o componente está
    /// colapsado, e mandar foco para um elemento invisível é um no-op que deixaria o
    /// teclado no lugar errado. Foco é assunto da VIEW — o ViewModel não o dá.
    /// </summary>
    private void FocarBuscaSeEstiverEscolhendo()
    {
        if (DataContext is ProntuarioViewModel { MostrandoLista: true }) Busca.Focar();
    }
}
