using System;
using System.Windows;
using Clinica.Desktop.ViewModels;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Novo agendamento em janela própria. Abre com data e hora já preenchidas quando a
/// secretária clica numa faixa livre da agenda. Retorna <c>true</c> quando agendou,
/// para a agenda se recarregar.
/// </summary>
public partial class AgendamentoWindow : Window
{
    public AgendamentoWindow(AgendamentoEdicaoViewModel vm, DateTime? inicio)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Agendou += AoAgendar;
        Closed += (_, _) => vm.Agendou -= AoAgendar;

        Loaded += async (_, _) => await vm.CarregarAsync(inicio);
    }

    private void AoAgendar()
    {
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
