using Clinica.Recepcao.Web;
using Clinica.Recepcao.ViewModels;
using Clinica.Desktop.Shell.Web;
using System.Collections;
static class Program
{
    [STAThread] static int Main(string[] args)
    {
        if(!args.Contains("--fluxos") && !args.Contains("--buscas")) return Contratos();
        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => { try { if(Contratos()!=0) throw new Exception("Contrato inválido"); if(args.Contains("--buscas")) await BuscasPacientesQa.Executar(); else await FluxosRecepcao.Executar(args.Contains("--web")); } catch(Exception ex) { Console.WriteLine(ex); Environment.ExitCode=1; } finally { app.Shutdown(); } };
        app.Run(); return Environment.ExitCode;
    }
    static int Contratos()
    {
var pages = RecepcaoWebRegistro.CriarPaginas().ToArray();
var errors = PaginasWebController.ValidarRegistro(pages).ToList();
errors.AddRange(DialogosWebController.ValidarRegistro(RegistroCompartilhadoWeb.DialogosCadastro()));
var admin = new PaginasWebController.Pagina("administrativo-qa", "Administrativo", typeof(FichaPacienteViewModel), [], [], RecepcaoWebRegistro.SecoesAdministrativas(""), []);
errors.AddRange(PaginasWebController.ValidarRegistro([admin]));
foreach(var p in pages.Append(admin)) {
    void Check(Type t, string? name, string context) { if(name is null) return; foreach(var x in name.Split('&')) { var path=x.Replace("vm:","").Replace("presente:","").TrimStart('!'); var type=x.StartsWith("vm:") ? p.Tipo : t; foreach(var part in path.Split('.')) { var prop=type.GetProperty(part); if(prop is null) {errors.Add($"{p.Chave} {context}: {type.Name}.{path} ausente");break;} type=prop.PropertyType;} } }
    foreach(var a in p.Acoes) Check(p.Tipo,a.Visivel,"visibilidade ação");
    foreach(var s in p.Secoes) foreach(var tab in s.Tabelas) { Type t=p.Tipo; foreach(var part in tab.Origens[0].Split('.')) t=t.GetProperty(part)?.PropertyType??typeof(object); var row=tab.TipoLinha??t.GetInterfaces().Append(t).FirstOrDefault(x=>x.IsGenericType&&x.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GenericTypeArguments[0]??typeof(object); foreach(var a in tab.Acoes) Check(row,a.Visivel,"visibilidade linha"); }
}
foreach(var d in RecepcaoWebRegistro.CriarDialogos()) {
 void Check(Type t,string? path,string context) { if(path is null||path==".")return; foreach(var x in path.Split('&')) {var target=x.StartsWith("vm:")?d.Tipo:t; foreach(var part in x.Replace("vm:","").Replace("presente:","").TrimStart('!').Split('.')) {var prop=target.GetProperty(part); if(prop is null) {errors.Add($"{d.Chave} {context}: {target.Name}.{x} ausente");break;} target=prop.PropertyType;} } }
 foreach(var f in d.Definicao.Campos) { Check(d.Tipo,f.Caminho,"campo");Check(d.Tipo,f.Opcoes,"opções");Check(d.Tipo,f.Visivel,"visibilidade");Check(d.Tipo,f.Habilitado,"guarda"); }
 foreach(var a in d.Definicao.Acoes) {Check(d.Tipo,a.Comando,"ação");Check(d.Tipo,a.Habilitado,"guarda ação");}
 foreach(var tab in d.Definicao.Tabelas) {Check(d.Tipo,tab.Colecao,"tabela");Type? t=d.Tipo;foreach(var part in tab.Colecao.Split('.'))t=t?.GetProperty(part)?.PropertyType;var row=t?.GetInterfaces().Append(t).FirstOrDefault(x=>x.IsGenericType&&x.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GenericTypeArguments[0];if(row is null)continue;foreach(var c in tab.Colunas)Check(row,c.Caminho,"coluna");foreach(var f in tab.Campos??[]){Check(row,f.Caminho,"campo linha");Check(row,f.Opcoes,"opções linha");Check(row,f.Habilitado,"guarda linha");}foreach(var a in tab.Acoes){Check(d.Tipo,a.Comando,"ação linha");Check(d.Tipo,a.Habilitado,"guarda ação tabela");Check(row,a.HabilitadoLinha,"guarda ação linha");}}
}
foreach(var e in errors.Distinct()) Console.WriteLine(e);
Console.WriteLine($"{pages.Length} páginas, {RecepcaoWebRegistro.CriarDialogos().Count()} diálogos; {errors.Distinct().Count()} erros de contrato.");
return errors.Count==0?0:1;

    }
}
