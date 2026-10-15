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
        // O índice antigo (1 = em tratamento, 2 = cadastro incompleto) continua sendo o
        // contrato de quem chama — o Consultório abre "Pacientes da clínica" com 1.
        if (filtro != 0)
            vm.IniciarComRecorte(filtro == 1 && Clinica.Domain.Entities.SessaoUsuario.Atual.Pode(Clinica.Domain.Entities.Permissao.VerProntuario)
                ? Clinica.Application.Modelos.FiltroListaPacientes.EmTratamento
                : Clinica.Application.Modelos.FiltroListaPacientes.CadastroIncompleto);
        return new Views.PacientesView { DataContext = vm };
    }
}
