using System.Windows;
using Clinica.Application.Modelos;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Lista as próximas vagas de um profissional e devolve a escolhida em
/// <see cref="Escolhida"/> (set/2026). Não marca nada: é o formulário que abriu a janela
/// que preenche data e hora e salva pelo caminho de sempre.
/// </summary>
public partial class ProximasVagasWindow : Window
{
    public ProximasVagasWindow(ResultadoBuscaDeVagas resultado)
    {
        InitializeComponent();
        DataContext = resultado;
    }

    /// <summary>A vaga clicada, ou nula quando a pessoa fechou sem escolher.</summary>
    public Vaga? Escolhida { get; private set; }

    private void Vaga_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Vaga vaga }) return;
        Escolhida = vaga;
        DialogResult = true;
    }
}
