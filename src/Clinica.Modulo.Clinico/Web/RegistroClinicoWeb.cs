using Clinica.Desktop.Shell.Web;
namespace Clinica.Clinico.Web;
public sealed class RegistroClinicoWeb(IEnumerable<IRegistroSecoesAdministrativasWeb> administrativos) : IRegistroModuloWeb
{
    public IEnumerable<PaginasWebController.Pagina> Paginas() => ClinicoWebRegistro.CriarPaginas(administrativos.SelectMany(a=>a.Secoes()));
    public IEnumerable<DialogosWebController.RegistroDialogo> Dialogos() => ClinicoWebRegistro.CriarDialogos();
}
