using System.Windows;
using System.Windows.Input;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Janela genérica da suíte: sidebar fixa montada a partir dos módulos carregados
/// (grupos temáticos + itens), topbar e área de conteúdo com a tela ativa. Não conhece
/// nenhuma tela em particular. O comportamento de menu (recolher/expandir, item ativo)
/// vive no ShellViewModel; aqui só mora o que exige a janela — o atalho de foco.
/// </summary>
public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();

        // Ctrl+F cai no campo de pesquisa. É atalho de janela e não de TextBox porque o
        // foco, na hora do atalho, está em qualquer lugar da tela ativa.
        InputBindings.Add(new KeyBinding(
            new RelayFoco(() => PesquisaGlobal.Focus()), Key.F, ModifierKeys.Control));
    }

    /// <summary>Comando mínimo para ligar uma tecla a uma ação da própria janela.</summary>
    private sealed class RelayFoco(Action acao) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => acao();
    }
}
