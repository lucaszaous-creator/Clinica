using System.Windows;
using System.Windows.Input;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Janela genérica da suíte: sidebar montada a partir dos módulos carregados e
/// área de conteúdo com a tela ativa. Não conhece nenhuma tela em particular.
/// </summary>
public partial class ShellWindow : Window
{
    /// <summary>Abaixo desta largura a sidebar aberta come a tela — recolhe sozinha.</summary>
    private const double LarguraRecolherMenu = 1100;

    /// <summary>Volta a abrir com folga, para não ficar piscando no limiar.</summary>
    private const double LarguraExpandirMenu = 1200;

    public ShellWindow()
    {
        InitializeComponent();

        // Ctrl+F cai no campo de pesquisa. É atalho de janela e não de TextBox porque o
        // foco, na hora do atalho, está em qualquer lugar da tela ativa.
        InputBindings.Add(new KeyBinding(
            new RelayFoco(() => PesquisaGlobal.Focus()), Key.F, ModifierKeys.Control));

        SizeChanged += AoRedimensionar;
    }

    /// <summary>
    /// Em 1366×768 (o monitor típico do balcão) a sidebar aberta deixa pouca tela para a
    /// grade da agenda. Ela recolhe sozinha, e o Ctrl+B continua mandando — quem
    /// expandir de propósito não é contrariado enquanto não mexer na janela.
    /// </summary>
    private void AoRedimensionar(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is not ShellViewModel vm) return;

        if (e.NewSize.Width < LarguraRecolherMenu && !vm.MenuRecolhido)
            vm.MenuRecolhido = true;
        else if (e.NewSize.Width >= LarguraExpandirMenu && vm.MenuRecolhido)
            vm.MenuRecolhido = false;
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
