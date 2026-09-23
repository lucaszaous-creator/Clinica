using System.Windows.Input;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>Única fábrica da ficha, usada por listas, busca, agenda e consultas contextuais.</summary>
public interface IFabricaFichaPaciente
{
    System.Windows.FrameworkElement Criar(PacienteEmFoco paciente, Action? voltar = null, int secao = 2);
}

/// <summary>Funções administrativas incorporadas à mesma ficha sem dependência entre módulos.</summary>
public interface IFichaAdministrativaPaciente
{
    bool PodeEditar { get; }
    object Resumo { get; }
    object Convenio { get; }
    object Relacionamento { get; }
    object Privacidade { get; }
    object Termos { get; }
    ICommand EditarCommand { get; }
    ICommand WhatsAppCommand { get; }
    void DefinirPaciente(int pacienteId);
}

public interface IFabricaListaPacientes
{
    System.Windows.FrameworkElement Criar(int filtro = 0, int secao = 2);
}

public interface IContextoPaciente
{
    bool Corresponde(PacienteEmFoco foco);
}
