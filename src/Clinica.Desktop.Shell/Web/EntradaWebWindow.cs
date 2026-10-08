using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace Clinica.Desktop.Shell.Web;

public sealed class EntradaWebWindow : Window
{
 private readonly EntradaWebModelo _modelo;
 private readonly WebView2 _browser=new();
 private readonly TaskCompletionSource<bool> _resposta=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private bool _fechada,_pronto,_sucesso;
 public EntradaWebWindow(EntradaWebModelo modelo)
 {
  _modelo=modelo;Title=modelo.NomeApp+" — Clínica SemDor"+(Configuracao.EdicaoDeTeste.Ativa?" — teste PR 245":"");Width=1040;Height=700;MinWidth=800;MinHeight=550;WindowStartupLocation=WindowStartupLocation.CenterScreen;Content=_browser;
  modelo.Mudou+=EnviarEstado;modelo.Concluiu+=Concluir;Loaded+=Carregar;
  Closing+=(_,e)=>{if(modelo.Ocupado&&!_sucesso)e.Cancel=true;};
  Closed+=(_,_)=>{_fechada=true;modelo.Mudou-=EnviarEstado;modelo.Concluiu-=Concluir;modelo.Encerrar();_browser.Dispose();_resposta.TrySetResult(_sucesso);};
 }
 public async Task<bool> MostrarAsync(){Show();return await _resposta.Task;}
 private void Concluir(){_sucesso=true;Close();}
 public static async Task<UsuarioSistema?> AutenticarAsync(IServiceScopeFactory escopos,string app,bool primeiro)
 {var modelo=new EntradaWebModelo(escopos,app,primeiro?"primeiro":"entrar");return await new EntradaWebWindow(modelo).MostrarAsync()?modelo.Usuario:null;}
 public static async Task<bool> ConfigurarAsync(string app)=>await new EntradaWebWindow(new(null,app,"conexao")).MostrarAsync();
 public static async Task<bool> AvisarAsync(string app,string mensagem,bool perguntar=false)=>await new EntradaWebWindow(new(null,app,perguntar?"pergunta":"aviso",mensagem)).MostrarAsync();
 private async void Carregar(object sender,RoutedEventArgs args)
 {
  try
  {
   var perfil=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Clinica.Desktop.Shell.Configuracao.EdicaoDeTeste.NomePasta,"Suite","WebView2");await _browser.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder:perfil));if(_fechada)return;
   var core=_browser.CoreWebView2;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
   core.SetVirtualHostNameToFolderMapping("suite.clinica.local",Path.Combine(AppContext.BaseDirectory,"WebSuite","wwwroot"),CoreWebView2HostResourceAccessKind.Deny);
   const string origem="https://suite.clinica.local/entrada.html";
   core.NavigationStarting+=(_,e)=>{if(e.Uri!=origem)e.Cancel=true;};core.FrameNavigationStarting+=(_,e)=>e.Cancel=true;core.NewWindowRequested+=(_,e)=>e.Handled=true;core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;core.DownloadStarting+=(_,e)=>e.Cancel=true;
   core.WebMessageReceived+=async(_,e)=>
   {
    if(_fechada||e.Source!=origem)return;
    try
    {
     var raw=e.WebMessageAsJson;if(raw.Length>16000)return;using var json=JsonDocument.Parse(raw);var r=json.RootElement;if(!r.TryGetProperty("acao",out var a))return;
     var acao=a.GetString();if(acao=="pronto"){_pronto=true;EnviarEstado();return;}if(acao=="fechar"){if(!_modelo.Ocupado)Close();return;}
     if(acao is "confirmar" or "testar" or "salvar" or "invalidar")await _modelo.ExecutarAsync(acao,r);
    }
    catch(Exception ex){Configuracao.LogSuite.Registrar("Entrada web — mensagem recusada",ex);}
   };
   core.Navigate(origem);
  }
  catch(Exception ex){_resposta.TrySetException(ex);Close();}
 }
 private void EnviarEstado(){if(_pronto&&!_fechada)_browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(_modelo.Estado()));}
}
