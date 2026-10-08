using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Desktop.Shell.Web;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Gerente.ViewModels;
using Clinica.Gerente.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
static class FormulariosExtrasQa
{
 public static async Task Executar(IServiceProvider sp,bool web)
 {
  using var ctl=new DialogosWebController(GerenteWebRegistro.CriarDialogos());var escopos=sp.GetRequiredService<IServiceScopeFactory>();var usuario=new UsuarioEdicaoViewModel(escopos,SessaoUsuario.Atual);await usuario.Inicializacao;var meta=new MetaEdicaoViewModel(escopos,DateTime.Today.Year,null);await meta.Inicializacao;var preco=new PrecoEdicaoViewModel(escopos,0);await preco.Inicializacao;
  foreach(var item in new (string,object)[]{("SenhaProvisoria",new SenhaProvisoriaWebViewModel("Usuário fictício")),("UsuarioWindow",usuario),("MetaWindow",meta),("PrecoConvenioWindow",preco)})
  {var abriu=ctl.AbrirAsync(item.Item1,item.Item2);var d=ctl.EstadoAtual??throw new Exception("Diálogo Gerente não abriu");if(d.Pagina.Campos.Count==0)throw new Exception("Formulário vazio");ctl.Fechar(d.Id);await abriu;}
  Console.WriteLine("OK Gerente: usuário/permissões, senha provisória, metas e preços carregam e cancelam na ponte web.");
  await GerenteGravacoesQa.Executar(sp);
  if(!web)return;
  var antes=System.Windows.Application.Current.Windows.Cast<Window>().ToHashSet();var pendencia=new Clinica.Application.Modelos.PendenciaCodigo(1,1,"Paciente fictício de contingência",Convenio.UnimedIntercambio,TipoCodigo.Acupuntura,OrdemCodigo.Segundo,DateOnly.FromDateTime(DateTime.Today),FormaObtencao.App,2,Clinica.Application.Modelos.NivelUrgencia.Vermelho,"Pendência de demonstração");
  var tarefa=Clinica.Faturamento.Web.AvisoPendenciasOfflineWeb.MostrarAsync([pendencia],DateTime.Now);var janela=System.Windows.Application.Current.Windows.Cast<Window>().Single(w=>!antes.Contains(w));janela.Left=-30000;janela.Top=-30000;janela.ShowInTaskbar=false;var browser=(WebView2)janela.Content;
  try
  {
   var pronto=false;for(var i=0;i<150;i++){if(browser.CoreWebView2 is not null && await browser.CoreWebView2.ExecuteScriptAsync("!!document.getElementById('fechar')")=="true"){pronto=true;break;}await Task.Delay(50);}if(!pronto){if(tarefa.IsFaulted)await tarefa;throw new Exception("Contingência web não abriu: "+browser.Source+"; "+(browser.CoreWebView2 is null?"sem engine":await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML.slice(0,500)")));}
   Directory.CreateDirectory("artifacts/faturamento-web");using(var f=File.Create("artifacts/faturamento-web/contingencia-offline.png"))await browser.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
   await browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('fechar').click()");await tarefa.WaitAsync(TimeSpan.FromSeconds(5));Console.WriteLine("OK contingência offline WebView2: cópia local somente leitura, indicação de sincronização e fechar.");
  }
  finally{if(janela.IsVisible)janela.Close();}
 }
}
