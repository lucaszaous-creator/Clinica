using System.IO;
using System.Net;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace Clinica.Faturamento.Web;

/// <summary>Contingência anterior ao login: somente a cópia local, sem ponte para serviços ou gravação.</summary>
public static class AvisoPendenciasOfflineWeb
{
 public static async Task MostrarAsync(IReadOnlyList<PendenciaCodigo> pendencias,DateTime geradoEm)
 {
  var pronto=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  var browser=new WebView2();var janela=new Window{Title="Pendências — modo contingência",Width=1100,Height=720,MinWidth=880,MinHeight=600,Content=browser,WindowStartupLocation=WindowStartupLocation.CenterScreen};
  janela.Closed+=(_,_)=>{browser.Dispose();pronto.TrySetResult();};
  janela.Loaded+=async(_,_)=>
  {
   try
   {
    var perfil=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Clinica.Desktop.Shell.Configuracao.EdicaoDeTeste.NomePasta,"Suite","WebView2");
    await browser.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder:perfil));
    var core=browser.CoreWebView2;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
    core.SetVirtualHostNameToFolderMapping("suite.clinica.local",Path.Combine(AppContext.BaseDirectory,"WebSuite","wwwroot"),CoreWebView2HostResourceAccessKind.DenyCors);
    var primeiraNavegacao=true;core.NavigationStarting+=(_,e)=>{if(primeiraNavegacao&&!e.IsUserInitiated){primeiraNavegacao=false;return;}e.Cancel=true;};core.FrameNavigationStarting+=(_,e)=>e.Cancel=true;core.NewWindowRequested+=(_,e)=>e.Handled=true;core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;core.DownloadStarting+=(_,e)=>e.Cancel=true;
    core.WebMessageReceived+=(_,e)=>{if(e.Source=="about:blank"&&e.TryGetWebMessageAsString()=="fechar")janela.Close();};
    var nonce=Guid.NewGuid().ToString("N");static string H(object? v)=>WebUtility.HtmlEncode(v?.ToString()??"");
    var linhas=string.Join("",pendencias.Select(p=>$"<tr><td>{H(p.PacienteNome)}</td><td>{H(p.ConvenioNome)}</td><td>{H(RotulosEnum.De(p.Tipo))} · {H(RotulosEnum.De(p.Ordem))}</td><td>{p.DataPrevista:dd/MM/yyyy}</td><td>{p.DiasEmAtraso}</td><td>{H(p.ObservacaoPendencia)}</td></tr>"));
    core.NavigateToString($$"""
    <!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src https://suite.clinica.local; style-src 'unsafe-inline'; font-src https://suite.clinica.local; script-src 'nonce-{{nonce}}'"><title>Pendências — contingência</title><style>@font-face{font-family:Inter;src:url(https://suite.clinica.local/assets/inter-latin-400-normal-C38fXH4l.woff2)}body{margin:0;background:#f7f9fc;color:#142b4e;font:16px Inter,system-ui,sans-serif}header{padding:20px 32px;background:white;border-bottom:1px solid #e3e9f2;display:flex;align-items:center;justify-content:space-between}img{width:120px}main{padding:32px}h1{font-size:26px;margin:0 0 14px}.aviso{padding:18px;border:1px solid #e7cc81;background:#fff9e7;border-radius:12px;line-height:1.5}.tabela{overflow:auto;background:white;border:1px solid #e3e9f2;border-radius:12px;margin-top:24px}table{width:100%;border-collapse:collapse;min-width:800px}th,td{text-align:left;padding:14px;border-bottom:1px solid #e3e9f2}th{background:#f6f8fc;font-size:14px}button{padding:12px 22px;border:0;border-radius:9px;background:#1e40af;color:white;font:inherit;cursor:pointer}button:focus-visible{outline:3px solid #688afd;outline-offset:3px}</style></head><body><header><img src="https://suite.clinica.local/logo-clinica.png" alt="Clínica SemDor"><button id="fechar">Fechar</button></header><main><h1>Pendências — modo contingência</h1><div class="aviso">Sem conexão com o banco. Última sincronização: <strong>{{geradoEm:dd/MM/yyyy HH:mm}}</strong>.<br>Esta cópia permite somente leitura. Registre as baixas quando a conexão voltar.</div><h2>{{pendencias.Count}} guia(s) na última sincronização</h2><div class="tabela" tabindex="0" aria-label="Pendências salvas"><table><thead><tr><th>Paciente</th><th>Convênio</th><th>Código</th><th>Prevista</th><th>Atraso na sincronização</th><th>Observação</th></tr></thead><tbody>{{linhas}}</tbody></table></div></main><script nonce="{{nonce}}">document.getElementById('fechar').onclick=()=>chrome.webview.postMessage('fechar');</script></body></html>
    """);
   }
   catch(Exception ex){pronto.TrySetException(ex);janela.Close();}
  };
  janela.Show();await pronto.Task;
 }
}
