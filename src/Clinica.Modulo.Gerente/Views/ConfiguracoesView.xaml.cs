using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Clinica.Desktop.Controls;
using Clinica.Gerente.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.Views;

/// <summary>
/// Configurações da clínica. Item de primeiro nível da proposta que a suíte não tinha:
/// as chaves existiam desde a parcela 0, mas só o app congelado sabia editá-las.
/// </summary>
public partial class ConfiguracoesView : UserControl
{
    public ConfiguracoesView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Abre a escrita dos termos que o paciente assina (parcela 66).
    ///
    /// A janela é montada aqui, e não no ViewModel, pela razão de sempre nesta suíte: a
    /// tela é criada pelo shell e o VM de Configurações não conhece janela nenhuma. O
    /// escopo é próprio, como nos demais formulários.
    /// </summary>
    private void AoAbrirTermos(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ConfiguracoesViewModel vm) return;

        var escopos = vm.Escopos;
        using var scope = escopos.CreateScope();

        var janela = new ModelosTermoWindow(
            new ModelosTermoViewModel(
                escopos,
                scope.ServiceProvider.GetRequiredService<ISnackbarService>(),
                scope.ServiceProvider.GetRequiredService<IDialogoService>()))
        {
            // A janela ATIVA, não a principal: com um modal aberto, esta nasceria atrás
            // dele e quem clicou concluiria que o botão não fez nada (parcela 58).
            Owner = System.Windows.Application.Current?.Windows.OfType<Window>()
                        .FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current?.MainWindow
        };

        janela.ShowDialog();
    }
}
