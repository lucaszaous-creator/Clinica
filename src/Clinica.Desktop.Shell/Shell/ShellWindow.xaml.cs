using System.Windows;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Janela genérica da suíte: sidebar montada a partir dos módulos carregados e
/// área de conteúdo com a tela ativa. Não conhece nenhuma tela em particular.
/// </summary>
public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
    }
}
