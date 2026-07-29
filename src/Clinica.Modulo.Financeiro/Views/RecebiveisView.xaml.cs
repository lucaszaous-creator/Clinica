using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Recebíveis de cartão. A previsão de recebimento era gravada desde a parcela 9 e nenhuma
/// tela a lia — dinheiro que a adquirente não depositou não aparecia em lugar nenhum.
/// </summary>
public partial class RecebiveisView : UserControl
{
    public RecebiveisView()
    {
        InitializeComponent();
    }
}
