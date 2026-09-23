using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.ViewModels;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Os números das regras embutidas, por FAMÍLIA (parcela 69).
///
/// Sai da tela de Convênios porque responde outra pergunta: lá é "quais operadoras a
/// clínica atende", aqui é "quanto vale o número da regra que várias delas
/// compartilham". Usa o MESMO <see cref="ParametrosViewModel"/> da tela de trás — dois
/// ViewModels dariam duas verdades sobre a mesma tabela.
/// </summary>
public partial class RegrasFamiliaWindow : Window
{
    public RegrasFamiliaWindow(ParametrosViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // A célula em edição no momento do fechamento seria descartada, e a alteração
        // se perderia sem nenhum sinal — o mesmo cuidado que o Salvar da tela já toma.
        Closing += (_, _) =>
        {
            GridFamilias.CommitEdit(DataGridEditingUnit.Cell, true);
            GridFamilias.CommitEdit(DataGridEditingUnit.Row, true);
        };
    }

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
