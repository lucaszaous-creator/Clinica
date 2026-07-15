using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Shell de navegação: troca de telas, destaca a seção atual e mantém o contador de pendências.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private int _pendenciasBadge;

    /// <summary>Seção ativa (para destacar o item do menu).</summary>
    [ObservableProperty]
    private string _secaoAtual = "Pendencias";

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
        SecaoAtual = "Pendencias";
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarPacientes()
    {
        var vm = _sp.GetRequiredService<PacientesViewModel>();
        vm.FichaSolicitada += AbrirFicha;
        SecaoAtual = "Pacientes";
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarNovoAtendimento()
    {
        var vm = _sp.GetRequiredService<NovoAtendimentoViewModel>();
        SecaoAtual = "Atendimento";
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarFaturados()
    {
        var vm = _sp.GetRequiredService<FaturadosViewModel>();
        SecaoAtual = "Faturados";
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarRelatorios()
    {
        var vm = _sp.GetRequiredService<RelatoriosViewModel>();
        SecaoAtual = "Relatorios";
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

    private void AbrirFicha(int pacienteId)
    {
        var vm = _sp.GetRequiredService<FichaPacienteViewModel>();
        vm.Voltar += MostrarPacientes;
        CurrentViewModel = vm;
        _ = vm.CarregarAsync(pacienteId);
    }
}
