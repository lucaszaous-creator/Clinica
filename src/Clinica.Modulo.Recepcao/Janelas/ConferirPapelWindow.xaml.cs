using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Conferir um papel pelo código do rodapé.
///
/// Recebe o MESMO <see cref="DocumentosViewModel"/> da tela de trás — o padrão que a
/// parcela 49 fixou ao tirar os cadastros das faixas laterais do Financeiro. Um ViewModel
/// próprio daria duas verdades sobre a mesma conferência: `Codigo`, `Conferido` e a
/// segunda via já moram lá, e a janela é só a porta.
/// </summary>
public partial class ConferirPapelWindow : Window
{
    public ConferirPapelWindow(DocumentosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
