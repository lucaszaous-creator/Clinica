using Clinica.Domain.Entities;
using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Desktop.Shell.Componentes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace Clinica.Desktop.Shell.Web;
/// <summary>Somente texto do termo, respostas e traço do paciente; a equipe confirma na janela principal.</summary>
public sealed class PainelAssinaturaWeb : Window
{
 private readonly AssinaturaPacienteViewModel _vm;
 private readonly WebView2 _browser=new();
 private bool _pronto,_fechado;
 private readonly int _usuario=SessaoUsuario.Atual.UsuarioId;
 private readonly TaskCompletionSource _carregado=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public PainelAssinaturaWeb(AssinaturaPacienteViewModel vm)
 {
  _vm=vm;Title="Assinatura — Clínica SemDor";Content=_browser;Width=1100;Height=720;Owner=System.Windows.Application.Current?.MainWindow;
  vm.DeclaracaoRespondida+=RespostaMudou;vm.PropertyChanged+=EstadoMudou;
  Loaded+=Carregar;Closed+=(_,_)=>{_fechado=true;vm.DeclaracaoRespondida-=RespostaMudou;vm.PropertyChanged-=EstadoMudou;_browser.Dispose();vm.PainelWebFechado();_carregado.TrySetCanceled();};
 }
 public async Task AbrirAsync(TelaDoSistema tela){Show();TelasDoSistema.Posicionar(this,tela);await _carregado.Task.WaitAsync(TimeSpan.FromSeconds(30));}
 private async void Carregar(object sender,RoutedEventArgs args)
 {
  try
  {
   var perfil=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Clinica.Desktop.Shell.Configuracao.EdicaoDeTeste.NomePasta,"Suite","WebView2");await _browser.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder:perfil));if(_fechado)return;
   var core=_browser.CoreWebView2;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
   core.SetVirtualHostNameToFolderMapping("suite.clinica.local",Path.Combine(AppContext.BaseDirectory,"WebSuite","wwwroot"),CoreWebView2HostResourceAccessKind.Deny);
   const string origem="https://suite.clinica.local/assinatura-paciente.html";
   core.NavigationStarting+=(_,e)=>{if(e.Uri!=origem)e.Cancel=true;};core.FrameNavigationStarting+=(_,e)=>e.Cancel=true;core.NewWindowRequested+=(_,e)=>e.Handled=true;core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;core.DownloadStarting+=(_,e)=>e.Cancel=true;
   core.WebMessageReceived+=(_,e)=>
   {
    if(_fechado||e.Source!=origem||!SessaoUsuario.Atual.Autenticado||SessaoUsuario.Atual.UsuarioId!=_usuario)return;
    try
    {
     var texto=e.TryGetWebMessageAsString();if(texto.Length>2800000)return;
     if(texto=="pronto"){_pronto=true;EnviarEstado();_carregado.TrySetResult();return;}
     _vm.ReceberTracoWeb(texto);EnviarEstado();
    }
    catch(Exception ex){_vm.MensagemEhErro=true;_vm.Mensagem="Não foi possível receber a assinatura: "+ex.GetBaseException().Message;}
   };
   core.Navigate(origem);
  }
  catch(Exception ex){_carregado.TrySetException(ex);Close();}
 }
 private void RespostaMudou(int ordem,string? valor)=>EnviarEstado();
 private void EstadoMudou(object? sender,System.ComponentModel.PropertyChangedEventArgs e)=>EnviarEstado();
 private void EnviarEstado()
 {
  if(!_pronto||_fechado)return;
  _browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{tipo="estado",titulo=_vm.Titulo,paciente=_vm.PacienteNome,numero=_vm.Numero,corpo=_vm.Corpo,declaracoes=_vm.Declaracoes.Select(d=>new{d.Descricao,d.Detalhe,d.Resposta}),bloqueado=_vm.Carregando||_vm.AguardandoCelular||_vm.TemTracoRemoto,mensagem=_vm.SituacaoDaColeta},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
 }
 public void Limpar(){if(_pronto&&!_fechado)_browser.CoreWebView2.PostWebMessageAsJson("{\"tipo\":\"limpar\"}");}
}
