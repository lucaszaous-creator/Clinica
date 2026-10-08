using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
static class EntradaQa
{
 static JsonElement J(object value)=>JsonSerializer.SerializeToElement(value);
 static void Exigir(bool ok,string msg){if(!ok)throw new Exception(msg);}
 public static async Task Executar(bool web)
 {
  using var con=new SqliteConnection("Data Source=:memory:");await con.OpenAsync();var opts=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;var sc=new ServiceCollection();sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");sc.AddScoped(_=>new ClinicaDbContext(opts));using var sp=sc.BuildServiceProvider();var escopos=sp.GetRequiredService<IServiceScopeFactory>();using(var scope=sp.CreateScope())await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Database.EnsureCreatedAsync();
  const string senha="TesteSintetico!2468",nova="NovaSintetica!2468";var primeiro=new EntradaWebModelo(escopos,"Demonstração","primeiro");await primeiro.ExecutarAsync("confirmar",J(new{nome="Direção fictícia",login="direcao.qa",senha,repetida="diferente"}));Exigir(primeiro.Erro&&primeiro.Usuario is null,"Primeiro acesso aceitou repetição divergente");await primeiro.ExecutarAsync("confirmar",J(new{nome="Direção fictícia",login="direcao.qa",senha,repetida=senha}));Exigir(primeiro.Usuario?.Perfil==PerfilAcesso.Gerente,"Primeiro acesso não criou gerente");
  var corrida=new EntradaWebModelo(escopos,"Demonstração","primeiro");await corrida.ExecutarAsync("confirmar",J(new{nome="Outra direção",login="outro.qa",senha,repetida=senha}));Exigir(corrida.Modo=="entrar"&&corrida.Usuario is null,"Outro computador criou primeiro acesso depois do existente");
  var login=new EntradaWebModelo(escopos,"Demonstração","entrar");await login.ExecutarAsync("confirmar",J(new{login="direcao.qa",senha="incorreta"}));Exigir(login.Erro&&login.Usuario is null&&login.LimparSenhas>0,"Senha incorreta autenticou");await login.ExecutarAsync("confirmar",J(new{login="direcao.qa",senha}));Exigir(login.Usuario?.Login=="direcao.qa","Login válido não autenticou");
  using(var scope=sp.CreateScope())await scope.ServiceProvider.GetRequiredService<AcessoService>().CriarAsync("Provisório fictício","provisorio.qa",senha,PerfilAcesso.Gerente,deveTrocarSenha:true);
  var troca=new EntradaWebModelo(escopos,"Demonstração","entrar");await troca.ExecutarAsync("confirmar",J(new{login="provisorio.qa",senha}));Exigir(troca.Modo=="trocar"&&troca.Usuario is null,"Credencial provisória liberou sessão antes de trocar");await troca.ExecutarAsync("confirmar",J(new{senha=nova,repetida="diferente"}));Exigir(troca.Erro&&troca.Usuario is null,"Troca obrigatória aceitou repetição divergente");await troca.ExecutarAsync("confirmar",J(new{senha=nova,repetida=nova}));Exigir(troca.Usuario is {DeveTrocarSenha:false},"Troca obrigatória não concluiu");using(var scope=sp.CreateScope())Exigir((await scope.ServiceProvider.GetRequiredService<AcessoService>().AutenticarAsync("provisorio.qa",nova)).Sucesso,"Nova senha não persistiu");
  var config=new EntradaWebModelo(null,"Demonstração","conexao");await config.ExecutarAsync("salvar",J(new{conexao="Host=127.0.0.1;Port=1;Timeout=1"}));Exigir(config.Erro&&!config.ConexaoSalva&&!config.PodeSalvar,"Configuração salvou sem testar");await config.ExecutarAsync("testar",J(new{conexao="Host=127.0.0.1;Port=1;Username=NAO_USAR;Database=NAO_USAR;Timeout=1"}));Exigir(config.Erro&&!config.PodeSalvar,"Conexão indisponível habilitou salvar");
  Exigir(!JsonSerializer.Serialize(troca.Estado()).Contains(nova),"Senha presente no estado público");Console.WriteLine("OK entrada: primeiro acesso, corrida entre postos, login inválido/válido, troca obrigatória e conexão sem gravação não validada.");
  if(!web)return;
  foreach(var modo in new[]{"entrar","primeiro","trocar","conexao","aviso","pergunta"})
  {
   var vm=new EntradaWebModelo(escopos,"Recepção — demonstração",modo,modo is "aviso" or "pergunta"?"Mensagem de demonstração para conferir a apresentação.":"");
   if(modo=="trocar"){using var scope=sp.CreateScope();await scope.ServiceProvider.GetRequiredService<AcessoService>().DefinirTrocaObrigatoriaAsync(troca.Usuario!.Id,true);vm=new EntradaWebModelo(escopos,"Demonstração","entrar");await vm.ExecutarAsync("confirmar",J(new{login="provisorio.qa",senha=nova}));}
   var w=new EntradaWebWindow(vm){Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};var tarefa=w.MostrarAsync();var browser=(WebView2)w.Content;
   try
   {
    for(var i=0;i<200;i++){if(browser.CoreWebView2 is not null&&await browser.CoreWebView2.ExecuteScriptAsync("document.body?.dataset.modo === "+JsonSerializer.Serialize(modo))=="true")break;await Task.Delay(50);}
    Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.body?.dataset.modo === "+JsonSerializer.Serialize(modo))=="true","Entrada não carregou "+modo);
    foreach(var tamanho in new[]{(1040,760),(800,600)}){w.Width=tamanho.Item1;w.Height=tamanho.Item2;await Task.Delay(180);Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1")=="true","Entrada com overflow "+modo);Directory.CreateDirectory("artifacts/faturamento-web");using var f=File.Create($"artifacts/faturamento-web/entrada-{modo}-{tamanho.Item1}.png");await browser.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);}
    if(modo=="entrar")
    {
     await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('login').value='direcao.qa';document.getElementById('senha').value='incorreta';document.getElementById('form').requestSubmit()");for(var i=0;i<100&&!vm.Erro;i++)await Task.Delay(50);Exigir(vm.Erro&&vm.Usuario is null,"Ponte não validou login");await Task.Delay(100);Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('senha').value==='' && !document.getElementById('mensagem').hidden")=="true","Senha inválida não limpou/exibiu erro");
     await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('senha').value="+JsonSerializer.Serialize(senha)+";document.getElementById('form').requestSubmit()");Exigir(await tarefa.WaitAsync(TimeSpan.FromSeconds(10))&&vm.Usuario is not null,"Login WebView2 não concluiu");
    }
    else {await browser.CoreWebView2.ExecuteScriptAsync("chrome.webview.postMessage({acao:'fechar'})");Exigir(!await tarefa.WaitAsync(TimeSpan.FromSeconds(5)),"Cancelar entrada confirmou");}
    Console.WriteLine("OK entrada WebView2 "+modo+" 1040/800");
   }
   finally{if(w.IsVisible)w.Close();}
  }
 }
}
