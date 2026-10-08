using Clinica.Faturamento.Web;
using Clinica.Desktop.Shell.Web;
using System.Collections;
static class Program
{
[STAThread] static void Main(string[] args)
{
var app=new System.Windows.Application {ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
app.Startup+=async(_,_)=>{try{Contratos();if(args.Contains("--entrada-only"))await EntradaQa.Executar(true);else await Fluxos.Executar(args.Contains("--web"),args.Contains("--ferramentas-only"));}catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}finally{app.Shutdown();}};
app.Run();
}
static void Contratos()
{
var paginas=FaturamentoWebRegistro.CriarPaginas().ToArray();
var erros=PaginasWebController.ValidarRegistro(paginas).Concat(DialogosWebController.ValidarRegistro(FaturamentoWebRegistro.CriarDialogos())).ToList();
foreach(var p in paginas) foreach(var t in p.Secoes.SelectMany(s=>s.Tabelas)) {
 var prop=p.Tipo.GetProperty(t.Origens[0]);var tipo=prop?.PropertyType.GetInterfaces().Append(prop.PropertyType).FirstOrDefault(t=>t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GetGenericArguments()[0];
 if(tipo is null)continue;
 foreach(var c in t.Colunas.Where(c=>c.Propriedade is not ("SituacaoGuia" or "Selecionada"))) {var atual=tipo;foreach(var parte in c.Propriedade.Split('.')) {var x=atual.GetProperty(parte);if(x is null){erros.Add(p.Chave+": coluna "+tipo.Name+"."+c.Propriedade+" ausente");break;}atual=x.PropertyType;}}
}
erros.AddRange(DialogosWebController.ValidarRegistro(Clinica.Gerente.Web.GerenteWebRegistro.CriarDialogos()));
erros.AddRange(DialogosWebController.ValidarRegistro(RegistroCompartilhadoWeb.DialogosAssinatura()));
foreach(var e in erros)Console.WriteLine(e);
Console.WriteLine($"{paginas.Length} páginas, {FaturamentoWebRegistro.CriarDialogos().Count()} formulários, {erros.Count} erro(s).");
if(erros.Count>0)throw new Exception("Contratos inválidos");
}
}
