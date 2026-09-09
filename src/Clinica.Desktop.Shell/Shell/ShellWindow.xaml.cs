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
            new RelayFoco(FocarPesquisa), Key.F, ModifierKeys.Control));
    }

    /// <summary>
    /// Ctrl+F põe o cursor na pesquisa global — e, no modo IMERSIVO, primeiro devolve a
    /// barra de cima, que é onde o campo mora.
    ///
    /// ⚠️ Sem esta metade o atalho ficaria MUDO na tela do paciente, que é onde se passa
    /// o dia: <c>Focus()</c> sobre elemento <c>Collapsed</c> devolve false e não faz nada,
    /// sem erro e sem log — o botão que não faz nada da parcela 41, vestido de teclado.
    ///
    /// ⚠️ E o foco espera uma passagem de leiaute: elemento que acabou de ficar visível
    /// ainda não foi medido no mesmo instante, e focá-lo ali falha calado do mesmo jeito.
    /// </summary>
    private void FocarPesquisa()
    {
        if (DataContext is ShellViewModel { Imersivo: true } vm)
        {
            vm.Imersivo = false;
            Dispatcher.BeginInvoke(
                new Action(() => PesquisaGlobal.Focus()),
                System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

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
