using Clinica.Desktop.Shell.Web;
namespace Clinica.Gerente.Web;

public sealed class RegistroGerenteWeb : IRegistroModuloWeb
{
    public IEnumerable<PaginasWebController.Pagina> Paginas()=>GerenteWebRegistro.CriarPaginas();
    public IEnumerable<DialogosWebController.RegistroDialogo> Dialogos()=>GerenteWebRegistro.CriarDialogos().Concat(GerenteWebRegistro.DialogosConfiguracao());
}
