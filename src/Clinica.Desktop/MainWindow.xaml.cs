using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace Clinica.Desktop;

public partial class MainWindow : Window
{
    private void AoPassarPelaCategoria(object sender, MouseEventArgs e)
    {
        if (sender is MenuItem categoria && categoria.HasItems)
            categoria.IsSubmenuOpen = true;
    }

    private void AoEscolherTela(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: ViewModels.ItemMenu item } && DataContext is ViewModels.MainViewModel vm
            && vm.NavegarCommand.CanExecute(item.Secao))
        {
            vm.NavegarCommand.Execute(item.Secao);
            e.Handled = true;
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        Loaded += AjustarParaTela;
        PreviewKeyDown += AtalhoFocarPesquisa;
    }

    // Ctrl+F foca a pesquisa global (foco é responsabilidade da View, não do VM).
    private void AtalhoFocarPesquisa(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Categorias.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PesquisaGlobal.Focus();
            PesquisaGlobal.SelectAll();
            e.Handled = true;
        }
    }

    // Garante que a janela RESTAURADA nunca ultrapasse a área útil da tela.
    // Em telas menores que a largura padrão (1280) a janela era maior
    // que o monitor e a última coluna das tabelas (Ações) ficava
    // "passando da tela". Aqui limitamos o tamanho e recentralizamos.
    //
    // IMPORTANTE: não fixar MaxWidth/MaxHeight — com eles definidos, ao MAXIMIZAR
    // a janela o WPF a mantinha menor que o quadro maximizado do Windows e o
    // restante da tela aparecia como faixas/margens PRETAS.
    private void AjustarParaTela(object sender, RoutedEventArgs e)
    {
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
        if (WindowState != WindowState.Normal)
            return; // maximizada: o Windows cuida do tamanho

        var larguraDisponivel = SystemParameters.WorkArea.Width;
        var alturaDisponivel = SystemParameters.WorkArea.Height;

        if (Width > larguraDisponivel)
            Width = larguraDisponivel;
        if (Height > alturaDisponivel)
            Height = alturaDisponivel;

        // Recentraliza dentro da área útil.
        Left = SystemParameters.WorkArea.Left + (larguraDisponivel - Width) / 2;
        Top = SystemParameters.WorkArea.Top + (alturaDisponivel - Height) / 2;

    }
}
