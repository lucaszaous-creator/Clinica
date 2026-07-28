using System.Windows.Controls;

namespace Clinica.Gerente.Views;

/// <summary>
/// Configurações da clínica. Item de primeiro nível da proposta que a suíte não tinha:
/// as chaves existiam desde a parcela 0, mas só o app congelado sabia editá-las.
/// </summary>
public partial class ConfiguracoesView : UserControl
{
    public ConfiguracoesView()
    {
        InitializeComponent();
    }
}
