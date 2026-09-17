using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace Clinica.Desktop.Shell;

/// <summary>Janela da suíte com navegação no cabeçalho e área de trabalho integral.</summary>
public partial class ShellWindow : Window
{
    private void AoPassarPelaCategoria(object sender, MouseEventArgs e)
    {
        if (sender is MenuItem categoria && categoria.HasItems)
            categoria.IsSubmenuOpen = true;
    }

    private void AoEscolherTela(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: Modulos.ItemMenuModulo item } && DataContext is ShellViewModel vm
            && vm.NavegarCommand.CanExecute(item))
        {
            vm.NavegarCommand.Execute(item);
            e.Handled = true;
        }
    }

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
