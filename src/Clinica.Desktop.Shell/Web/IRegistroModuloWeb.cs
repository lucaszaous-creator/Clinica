namespace Clinica.Desktop.Shell.Web;

/// <summary>Contratos explícitos de apresentação registrados pelo módulo carregado neste executável.</summary>
public interface IRegistroModuloWeb
{
    IEnumerable<PaginasWebController.Pagina> Paginas();
    IEnumerable<DialogosWebController.RegistroDialogo> Dialogos();
}

public interface IRegistroSecoesAdministrativasWeb
{
    IEnumerable<PaginasWebController.Secao> Secoes();
}
