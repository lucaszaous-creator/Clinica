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

    /// <summary>Entrada DIRETA da barra plana (sem submenu): o clique navega para o item.</summary>
    private void AoEscolherCategoria(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.OriginalSource)) return;
        if (sender is MenuItem { DataContext: Modulos.GrupoMenuModulo { Direto: { } item } } && DataContext is ShellViewModel vm
            && vm.NavegarCommand.CanExecute(item))
        {
            vm.NavegarCommand.Execute(item);
            e.Handled = true;
        }
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

    /// <summary>
    /// A camada sobre a área de trabalho onde mora o que precisa SOBREVIVER à troca de tela
    /// — hoje, o navegador único das telas Blazor. Nenhum módulo é conhecido aqui: quem põe o
    /// navegador na camada é o módulo que tem telas Blazor.
    /// </summary>
    public Panel CamadaDaJanela => CamadaPersistente;

    public ShellWindow() : this(null) { }

    public ShellWindow(FrameworkElement? navegacaoLateral)
    {
        InitializeComponent();
        NavegacaoLateral.Content = navegacaoLateral;
        SizeChanged+=(_,_)=>AjustarCabecalho();
        Loaded+=(_,_)=>AjustarCabecalho();
        Loaded+=(_,_)=>{ if (DataContext is ShellViewModel vm) vm.IniciarAvisosGlobais(); };
        Unloaded+=(_,_)=>{ if (DataContext is ShellViewModel vm) vm.PararAvisosGlobais(); };
        Closed+=(_,_)=>{ if (DataContext is ShellViewModel vm) { vm.PararAvisosGlobais(); vm.LiberarTelas(); } };
        InputBindings.Add(new KeyBinding(new RelayFoco(() =>
        {
            if (NavegacaoLateral.Content is FrameworkElement lateral)
                lateral.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            else Categorias.Focus();
        }), Key.B, ModifierKeys.Control));

        // Ctrl+F cai no campo de pesquisa. É atalho de janela e não de TextBox porque o
        // foco, na hora do atalho, está em qualquer lugar da tela ativa.
        InputBindings.Add(new KeyBinding(
            new RelayFoco(FocarPesquisa), Key.F, ModifierKeys.Control));
    }

    private void AjustarCabecalho()
    {
        if (NavegacaoLateral.Content is not null)
        {
            Cabecalho.Height = 68;
            NavegacaoSuperior.Visibility = Visibility.Collapsed;
            return;
        }
        // Menus and training move together to a second line on smaller windows,
        // preserving the search, notifications and session controls at 880 px.
        var compacto=ActualWidth<1300;
        // Altura automática no modo compacto: se as categorias dobrarem em duas linhas,
        // o cabeçalho cresce com elas em vez de decepá-las.
        Cabecalho.Height=compacto?double.NaN:68;
        Grid.SetRow(NavegacaoSuperior,compacto?1:0);
        Grid.SetColumn(NavegacaoSuperior,compacto?0:1);
        Grid.SetColumnSpan(NavegacaoSuperior,compacto?5:1);
        NavegacaoSuperior.Margin=compacto?new Thickness(0,0,0,6):new Thickness(0);
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
