using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Recepcao.ViewModels;
public sealed class FabricaListaPacientes(IServiceProvider servicos) : IFabricaListaPacientes
{
    public System.Windows.FrameworkElement Criar(int filtro = 0, int secao = 2)
    {
        Clinica.Domain.Entities.SessaoUsuario.Atual.ExigirAlgum(Clinica.Domain.Entities.Permissao.VerFichaPaciente | Clinica.Domain.Entities.Permissao.VerProntuario, "consultar pacientes");
        var vm = servicos.GetRequiredService<PacientesViewModel>();
        vm.SecaoInicial = secao;
        vm.Filtro = filtro;
        return new Views.PacientesView { DataContext = vm };
    }
}
