using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Gerente.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Gerente.ViewModels;

public sealed partial class ConfiguracoesViewModel
{
    [RelayCommand]
    private async Task AbrirTermosWebAsync()
    {
        using var scope=Escopos.CreateScope();
        var vm=new ModelosTermoViewModel(Escopos,scope.ServiceProvider.GetRequiredService<ISnackbarService>(),scope.ServiceProvider.GetRequiredService<IDialogoService>());
        await DialogosDaSessao.AbrirAsync("ModelosTermo",vm,()=>new ModelosTermoWindow(vm).ShowDialog());
    }
    [RelayCommand]
    private async Task AbrirCamposWebAsync()
    {
        using var scope=Escopos.CreateScope();
        var vm=new CamposPersonalizadosViewModel(Escopos,scope.ServiceProvider.GetRequiredService<IDialogoService>());
        await DialogosDaSessao.AbrirAsync("CamposPersonalizados",vm,()=>new CamposPersonalizadosWindow(vm).ShowDialog());
    }
}
