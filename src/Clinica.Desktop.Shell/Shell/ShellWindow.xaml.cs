using System.Windows;
using System.Windows.Input;

namespace Clinica.Desktop.Shell;

/// <summary>Janela da suíte com abas superiores sempre visíveis e área de conteúdo.</summary>
public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new RelayFoco(() => Categorias.Focus()), Key.B, ModifierKeys.Control));

        // Ctrl+F cai no campo de pesquisa. É atalho de janela e não de TextBox porque o
        // foco, na hora do atalho, está em qualquer lugar da tela ativa.
        InputBindings.Add(new KeyBinding(
            new RelayFoco(FocarPesquisa), Key.F, ModifierKeys.Control));
    }

    /// <summary>Ctrl+F põe o cursor na pesquisa global.</summary>
    private void FocarPesquisa()
    {
        PesquisaGlobal.Focus();
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
