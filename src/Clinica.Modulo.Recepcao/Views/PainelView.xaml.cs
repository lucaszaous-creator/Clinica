using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Painel próprio da Recepção — o dia visto do balcão.
///
/// A View liga e desliga a releitura de fundo (parcela 62): é a tela de abertura, a que
/// fica no monitor a manhã inteira sem ninguém tocar, e números parados ali se leem como
/// "não chegou ninguém".
/// </summary>
public partial class PainelView : UserControl
{
    public PainelView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as PainelViewModel)?.IniciarRelogio();
        Unloaded += (_, _) => (DataContext as PainelViewModel)?.PararRelogio();
    }
}
