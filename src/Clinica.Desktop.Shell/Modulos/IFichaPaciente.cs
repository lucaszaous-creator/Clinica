namespace Clinica.Desktop.Shell.Modulos;

public interface IContextoPaciente
{
    bool Corresponde(PacienteEmFoco foco);
}
