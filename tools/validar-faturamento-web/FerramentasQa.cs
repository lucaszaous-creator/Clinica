using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Treinamento;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Faturamento.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
static class FerramentasQa
{
 static void Exigir(bool ok,string msg){if(!ok)throw new Exception(msg);}
 public static async Task Executar(IServiceProvider sp)
 {
  var raiz=Path.GetFullPath("artifacts/ferramentas-web/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(raiz);
  var paginas=Clinica.Gerente.Web.GerenteWebRegistro.CriarPaginas().Concat(Clinica.Recepcao.Web.RecepcaoWebRegistro.CriarPaginas()).Concat(FaturamentoWebRegistro.CriarPaginas()).GroupBy(p=>p.Chave).Select(g=>g.First()).ToArray();
  var menu=new Clinica.Gerente.Modulo.ModuloGerente().Itens.Concat(new Clinica.Recepcao.Modulo.ModuloRecepcao().Itens).Concat(new Clinica.Faturamento.Modulo.ModuloFaturamento().Itens).GroupBy(i=>i.Chave).Select(g=>g.First()).ToArray();
  using(var ferramentas=new SuiteFerramentasWeb(sp,menu,paginas.Select(p=>p.Chave),dadosLocais:raiz))
  {
   var aulas=ferramentas.CatalogoAulas();Exigir(aulas.Aulas.Any(a=>a.Id=="documentos"),"Aula documentos ausente");ferramentas.SalvarProgresso("documentos",12.5,true);var progresso=ferramentas.CatalogoAulas(situacao:"concluidas").Aulas.Single(a=>a.Id=="documentos");Exigir(progresso.Posicao==12.5&&progresso.Concluida,"Progresso não gravou");bool bloqueou=false;try{await ferramentas.AbrirAulaAsync("fora-do-catalogo");}catch(InvalidOperationException){bloqueou=true;}Exigir(bloqueou,"Aula não autorizada abriu");Exigir(ferramentas.Pesquisar("Consultar guias").Any(r=>r.Rota=="faturamento-guias"),"Busca ignorou rota do faturamento");
  }
  using(var ferramentas=new SuiteFerramentasWeb(sp,menu,paginas.Select(p=>p.Chave),dadosLocais:raiz))Exigir(ferramentas.CatalogoAulas().Aulas.Single(a=>a.Id=="documentos").Posicao==12.5,"Progresso não reabriu");
  var aula=CatalogoTreinamento.Ler().Single(a=>a.Id=="documentos");MidiaTreinamentoQa.Preparar(raiz,aula);
  using var view=new SuiteWebView(sp,paginas,FaturamentoWebRegistro.CriarDialogos().Concat(Clinica.Gerente.Web.GerenteWebRegistro.CriarDialogos()).Concat(RegistroCompartilhadoWeb.Dialogos()),menu,"Gerente — demonstração","metas",dadosTreinamento:raiz);var w=new Window{Content=view,Width=1440,Height=900,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};w.Show();
  try
  {
   await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));var browser=(WebView2)view.Content;await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable","{}");browser.CoreWebView2.GetDevToolsProtocolEventReceiver("Log.entryAdded").DevToolsProtocolEventReceived+=(_,e)=>Console.WriteLine("LOG QA "+e.ParameterObjectAsJson);browser.CoreWebView2.WebResourceResponseReceived+=(_,e)=>{if(e.Request.Uri.Contains("aulas.clinica.local"))Console.WriteLine("MIDIA QA "+e.Response.StatusCode+" "+e.Response.Headers.GetHeader("Content-Type")+" "+e.Request.Uri);};
   async Task Script(string script)=>await browser.CoreWebView2.ExecuteScriptAsync(script);
   async Task Esperar(string expressao,string erro){for(var i=0;i<150;i++){if(await browser.CoreWebView2.ExecuteScriptAsync(expressao)=="true")return;await Task.Delay(50);}throw new Exception(erro+"; DOM: "+await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({video:document.querySelector('video')?.outerHTML,error:document.querySelector('video')?.error?.message,body:document.body.innerText.slice(0,650)})"));}
   async Task Key(string key,int modifiers=0,int code=0){await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type="keyDown",key,modifiers,windowsVirtualKeyCode=code}));await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type="keyUp",key,modifiers,windowsVirtualKeyCode=code}));}
   async Task Foto(string nome){using var f=File.Create(Path.Combine(raiz,nome+".png"));await view.CapturarPreviewAsync(f);}
   await Esperar("!!document.querySelector('[data-treinamento]')","Cabeçalho não abriu");
   await Script("document.querySelector('[data-grupo-botao]').focus()");await Key("ArrowDown",0,40);await Esperar("document.activeElement.matches('.submenu-topo button') && !!document.querySelector('.submenu-topo:not([hidden])')","Menu teclado não focou opção");await Key("Escape",0,27);await Esperar("document.activeElement.matches('[data-grupo-botao]') && !document.querySelector('.submenu-topo:not([hidden])')","Escape não devolveu foco");
   await Key("k",2,75);await Esperar("document.activeElement.id==='busca-tela'","CtrlK não abriu busca");await Script("document.getElementById('busca-tela').value='guias';document.getElementById('busca-tela').dispatchEvent(new Event('input',{bubbles:true}))");await Esperar("!!document.querySelector('.menu-expandido [data-rota=\"faturamento-guias\"]')","Busca não mostrou guias");await Foto("busca-topo");await Script("document.querySelector('.menu-expandido [data-rota=\"faturamento-guias\"]').click()");await Esperar("document.querySelector('main').dataset.rota==='faturamento-guias'","Busca não navegou");
   await Script("window.__acaoQa='';document.addEventListener('click',e=>{const b=e.target.closest('[data-comando]');if(b)window.__acaoQa=b.dataset.comando},true)");await Key("F5",0,116);await Esperar("window.__acaoQa==='Buscar'","F5 não chamou ação Buscar");
   sp.GetRequiredService<SnackbarService>().Info("Aviso sintético para conferir o cabeçalho");await Esperar("!!document.querySelector('[data-avisos] small')","Aviso não marcou não lido");await Script("document.querySelector('[data-avisos]').click()");await Esperar("document.querySelector('.painel-avisos')?.textContent.includes('Aviso sintético') && !document.querySelector('[data-avisos] small')","Avisos não listou/marcou lido");await Foto("avisos-topo");await Key("Escape",0,27);
   await Script("document.querySelector('[data-usuario]').click()");await Esperar("!!document.querySelector('[data-sessao=\"trocar-senha\"]') && !!document.querySelector('[data-sessao=\"trocar-usuario\"]')","Menu usuário incompleto");await Key("Escape",0,27);
   await Script("document.querySelector('[data-treinamento]').click()");await Esperar("!!document.querySelector('[data-aula=\"documentos\"]')","Catálogo não abriu");await Foto("treinamento-catalogo");
   await Script("document.getElementById('busca-aula').value='nada-para-este-filtro';document.getElementById('busca-aula').dispatchEvent(new Event('input',{bubbles:true}))");await Esperar("!document.querySelector('[data-aula]') && document.body.textContent.includes('Nenhuma aula corresponde')","Filtro de treinamento não aplicou");await Script("document.getElementById('busca-aula').value='';document.getElementById('busca-aula').dispatchEvent(new Event('input',{bubbles:true}))");
   {
    await Script("document.querySelector('[data-aula=\"documentos\"]').click()");await Esperar("document.querySelector('video')?.readyState>=1","Vídeo validado não carregou");await Esperar("document.querySelector('video')?.currentTime>=12","Vídeo não retomou progresso");await Script("document.querySelector('video').muted=true;document.querySelector('video').play()");await Esperar("document.querySelector('video')?.currentTime>13 && !document.querySelector('video').paused","Vídeo não reproduziu");await Foto("treinamento-video");await Script("document.querySelector('video').pause()");await Task.Delay(400);
    var progresso=new AcervoTreinamento(SessaoUsuario.Atual.UsuarioId,raiz).Progresso("documentos");Exigir(progresso.Posicao>12.5&&progresso.Concluida,"Progresso de playback perdeu posição ou conclusão");
    await Script("document.querySelector('[data-aula-reiniciar]').click()");await Esperar("document.querySelector('video')?.currentTime<1","Reiniciar não voltou ao começo");
   }
   await Script("window.__externoQa='pendente';fetch('https://externo.invalid/arquivo').then(()=>window.__externoQa='permitido').catch(()=>window.__externoQa='bloqueado')");await Esperar("window.__externoQa==='bloqueado'","Recurso externo não bloqueado");var origem=browser.CoreWebView2.Source;await Script("location.href='https://externo.invalid/'");await Task.Delay(200);Exigir(browser.CoreWebView2.Source==origem,"Navegação externa escapou");
   Console.WriteLine("OK ferramentas SuiteWebView: menu teclado/Escape, CtrlK/busca/navegação, F5 ação real, avisos/lidos, menu usuário, treinamento/filtro, progresso persistido, cache SHA/playback/retomada e bloqueio externo.");Console.WriteLine("Capturas ferramentas: "+raiz);
  }
  finally{w.Close();}
 }
}
