namespace Clinica.Desktop.Shell.Componentes;
public interface IRecebimento
{
    event Action? Concluido;
    bool Ocupado { get; }
}
