using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Contas a pagar e a receber. Responde à pergunta de toda segunda-feira — "o que vence
/// esta semana?" —, que o caixa não sabia responder porque lançamento previsto não tinha
/// data de vencimento.
/// </summary>
public partial class ContasView : UserControl
{
    public ContasView()
    {
        InitializeComponent();
    }
}
