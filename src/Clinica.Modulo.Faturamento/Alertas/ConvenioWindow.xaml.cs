using System.Windows;
using Clinica.Desktop.ViewModels;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Edição de UM convênio do catálogo (parcela 69).
///
/// A janela edita o próprio <see cref="ConvenioEdicao"/> da lista — o MESMO objeto,
/// como manda a regra de "cadastro em botão + janela": duas cópias dariam duas
/// verdades sobre a mesma linha. Por isso não há "Cancelar": o que grava continua
/// sendo o "Salvar configurações" da tela de trás, e a janela diz isso no rodapé.
/// </summary>
public partial class ConvenioWindow : Window
{
    public ConvenioWindow(ConvenioEdicao convenio)
    {
        InitializeComponent();
        DataContext = convenio;
    }

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
