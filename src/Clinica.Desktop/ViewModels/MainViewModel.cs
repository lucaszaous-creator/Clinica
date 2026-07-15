using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Shell de navegação: troca de telas e mantém o contador de pendências (badge) do topo.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private int _pendenciasBadge;

    public MainViewModel(IServiceProvider sp)
    {
        _sp = sp;
        MostrarDashboard();
    }

    [RelayCommand]
    private void MostrarDashboard()
    {
        var vm = _sp.GetRequiredService<DashboardViewModel>();
        vm.PendenciasAtualizadas += total => PendenciasBadge = total;
        vm.AbrirBaixaSolicitado += AbrirBaixa;
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarPacientes()
    {
        var vm = _sp.GetRequiredService<PacientesViewModel>();
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarNovoAtendimento()
    {
        var vm = _sp.GetRequiredService<NovoAtendimentoViewModel>();
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    private void AbrirBaixa(int codigoId)
    {
        var vm = _sp.GetRequiredService<BaixaViewModel>();
        vm.BaixaConcluida += MostrarDashboard;
        vm.Cancelado += MostrarDashboard;
        CurrentViewModel = vm;
        _ = vm.CarregarAsync(codigoId);
    }
}
