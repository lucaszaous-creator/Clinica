using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;

namespace Clinica.Clinico.Modulo;

public sealed class FabricaFichaPaciente(IServiceProvider servicos) : IFabricaFichaPaciente
{
    public System.Windows.FrameworkElement Criar(PacienteEmFoco paciente, Action? voltar = null, int secao = 2)
    {
        SessaoUsuario.Atual.ExigirAlgum(Permissao.VerFichaPaciente | Permissao.VerProntuario, "abrir ficha do paciente");
        return new Views.PacienteWorkspaceView
        {
            DataContext = new ViewModels.PacienteWorkspaceViewModel(servicos, paciente,
                secao) { VoltarParaOrigem = voltar }
        };
    }
}
