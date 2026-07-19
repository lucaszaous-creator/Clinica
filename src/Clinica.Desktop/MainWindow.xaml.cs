using System.Windows;
using System.Windows.Input;

namespace Clinica.Desktop;

public partial class MainWindow : Window
{
    // Histerese do auto-recolhimento da sidebar: recolhe abaixo de 1180px e só
    // reexpande acima de 1320px, para não "piscar" perto do limiar.
    private const double LarguraRecolherMenu = 1180;
    private const double LarguraExpandirMenu = 1320;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += AjustarParaTela;
        PreviewKeyDown += AtalhoFocarPesquisa;
        SizeChanged += AjustarMenuPelaLargura;
    }

    // Em janelas estreitas as últimas colunas das tabelas (Ações) ficavam cortadas
    // na borda direita até o usuário recolher o menu na mão. Agora a sidebar
    // recolhe/expande sozinha conforme a largura; Ctrl+B continua funcionando
    // normalmente entre os limiares.
    private void AjustarMenuPelaLargura(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || DataContext is not ViewModels.MainViewModel vm)
            return;

        if (e.NewSize.Width < LarguraRecolherMenu && !vm.MenuRecolhido)
            vm.MenuRecolhido = true;
        else if (e.NewSize.Width >= LarguraExpandirMenu && vm.MenuRecolhido)
            vm.MenuRecolhido = false;
    }

    // Ctrl+F foca a pesquisa global (foco é responsabilidade da View, não do VM).
    private void AtalhoFocarPesquisa(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
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

        // Estado inicial da sidebar conforme a largura (SizeChanged pode não disparar de novo).
        if (DataContext is ViewModels.MainViewModel vm && Width < LarguraRecolherMenu)
            vm.MenuRecolhido = true;
    }
}
