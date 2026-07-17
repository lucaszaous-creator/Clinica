using System.Windows;
using System.Windows.Input;

namespace Clinica.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += AjustarParaTela;
        PreviewKeyDown += AtalhoFocarPesquisa;
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

    // Garante que a janela nunca ultrapasse a área útil da tela.
    // Em telas menores que a largura padrão (1180) a janela era maior
    // que o monitor e a última coluna das tabelas (Ações) ficava
    // "passando da tela". Aqui limitamos o tamanho e recentralizamos.
    private void AjustarParaTela(object sender, RoutedEventArgs e)
    {
        var larguraDisponivel = SystemParameters.WorkArea.Width;
        var alturaDisponivel = SystemParameters.WorkArea.Height;

        MaxWidth = larguraDisponivel;
        MaxHeight = alturaDisponivel;

        if (Width > larguraDisponivel)
            Width = larguraDisponivel;
        if (Height > alturaDisponivel)
            Height = alturaDisponivel;

        // Recentraliza dentro da área útil.
        Left = SystemParameters.WorkArea.Left + (larguraDisponivel - Width) / 2;
        Top = SystemParameters.WorkArea.Top + (alturaDisponivel - Height) / 2;
    }
}
