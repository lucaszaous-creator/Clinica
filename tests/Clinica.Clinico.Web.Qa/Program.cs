using System.IO;
using System.Text.Json;
using System.Reflection;
using Clinica.Clinico.ViewModels;
internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  var app=new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
  app.Startup+=async(_,_)=>{try{Contratos();Fluxos.SoInfusaoModelos=args.Contains("--infusao-modelos");Fluxos.SoGestos=args.Contains("--gestos");Fluxos.SoDestinosDialogos=args.Contains("--destinos-dialogos");if(Fluxos.SoInfusaoModelos||args.Contains("--fluxos")||args.Contains("--web")||Fluxos.SoGestos||Fluxos.SoDestinosDialogos)await Fluxos.Executar(args.Contains("--web")||Fluxos.SoGestos||Fluxos.SoDestinosDialogos);}catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}finally{app.Shutdown();}};
  app.Run();
 }
 static void Contratos()
 {
var vistos = new HashSet<Type>();
object Tipo(Type t) {
 vistos.Add(t);
 return new { nome=t.FullName, propriedades=t.GetProperties().Select(p=>new {nome=p.Name,tipo=p.PropertyType.FullName,editavel=p.SetMethod?.IsPublic==true,colecao=p.PropertyType.GetInterfaces().Append(p.PropertyType).FirstOrDefault(x=>x.IsGenericType && x.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GetGenericArguments()[0].FullName}).ToArray() };
}
var assemblies = new[]{typeof(AtendimentoViewModel).Assembly,typeof(Clinica.Desktop.Shell.Componentes.FolhaDaSessaoViewModel).Assembly,typeof(Clinica.Domain.Entities.Paciente).Assembly,typeof(Clinica.Application.Servicos.ProntuarioService).Assembly,typeof(Clinica.Infrastructure.SessaoEnfermagemItem).Assembly};
File.WriteAllText("tests/Clinica.Clinico.Web.Qa/tipos.json",JsonSerializer.Serialize(assemblies.SelectMany(a=>a.GetTypes()).Where(t=>t.IsPublic).Select(Tipo).ToArray()));
Console.WriteLine("Metadados públicos exportados sem instanciar serviços ou acessar banco.");


var paginas=Clinica.Clinico.Web.ClinicoWebRegistro.CriarPaginas().ToArray();
var dialogos=Clinica.Clinico.Web.ClinicoWebRegistro.CriarDialogos().Concat(Clinica.Desktop.Shell.Web.RegistroCompartilhadoWeb.Dialogos()).DistinctBy(d=>(d.Chave,d.Tipo)).ToArray();
var erros=Clinica.Desktop.Shell.Web.PaginasWebController.ValidarRegistro(paginas).Concat(Clinica.Desktop.Shell.Web.DialogosWebController.ValidarRegistro(dialogos)).ToArray();
foreach(var erro in erros) Console.WriteLine(erro);
Console.WriteLine($"{paginas.Length} páginas, {dialogos.Length} diálogos, {erros.Length} erros contratuais.");
if(erros.Length>0)throw new Exception("Contratos inválidos");

 }
}
