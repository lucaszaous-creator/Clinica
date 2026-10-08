using Clinica.Desktop.Shell.Web;
namespace Clinica.Financeiro.Web;
public sealed class RegistroFinanceiroSuiteWeb : IRegistroModuloWeb
{
    public IEnumerable<PaginasWebController.Pagina> Paginas()=>FinanceiroSuiteRegistro.CriarPaginas();
    public IEnumerable<DialogosWebController.RegistroDialogo> Dialogos()=>FinanceiroSuiteDialogos.CriarDialogos();
}
