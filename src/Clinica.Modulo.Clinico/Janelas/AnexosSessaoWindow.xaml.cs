using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Janela dos anexos de uma sessão.
///
/// Não fecha sozinha: diferente da aplicação de escala, aqui não há "a" gravação — o
/// profissional anexa o laudo, salva outro em disco, remove o que veio errado e sai quando
/// terminou. Fechar no primeiro sucesso o obrigaria a reabrir a cada arquivo.
/// </summary>
public partial class AnexosSessaoWindow : Window
{
    public AnexosSessaoWindow(AnexosSessaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        Loaded += async (_, _) => await vm.CarregarAsync();
    }
}
