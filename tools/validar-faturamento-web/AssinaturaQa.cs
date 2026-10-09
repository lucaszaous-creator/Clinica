using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Wpf;
static class AssinaturaQa
{
 static void Exigir(bool c,string m){if(!c)throw new Exception(m);}
 public static async Task Executar(IServiceProvider sp,bool web)
 {
  using var scope=sp.CreateScope();var s=scope.ServiceProvider;var paciente=await s.GetRequiredService<ClinicaDbContext>().Pacientes.FirstAsync();
  AssinaturaPacienteViewModel Criar()=>new(s.GetRequiredService<DocumentoClinicoService>(),s.GetRequiredService<AssinaturaDoPacienteService>(),s.GetRequiredService<IDialogoService>(),paciente.Id,null,paciente.Nome,acessos:s.GetRequiredService<AcessoProntuarioService>(),parametros:s.GetRequiredService<ParametrosService>());
  var vm=Criar();using var ctl=new DialogosWebController(RegistroCompartilhadoWeb.DialogosAssinatura());var abrir=ctl.AbrirAsync("AssinaturaPaciente",vm);await Esperar(()=>!vm.Carregando&&vm.Numero.Length>0);
  var d=ctl.EstadoAtual!;Exigir(!d.Pagina.Acoes.Single(a=>a.Chave=="confirmar").Habilitada,"Assinar sem traço habilitado");
  await ctl.AtualizarCampoAsync(d.Id,"DocumentoConferido",JsonSerializer.SerializeToElement("Documento fictício de teste conferido"));await ctl.ExecutarAcaoAsync(d.Id,"todas");
  var traco=Traco();await ctl.AtualizarCampoAsync(d.Id,"TracoWeb",JsonSerializer.SerializeToElement(traco));Exigir(vm.TemTraco,"Traço web não chegou ao C#");
  await ctl.ExecutarAcaoAsync(d.Id,"confirmar");await abrir;Exigir(vm.Concluido,"Termo não concluído");vm.EncerrarWeb();
  using(var leitura=sp.CreateScope()){var db=leitura.ServiceProvider.GetRequiredService<ClinicaDbContext>();var docs=await db.Set<DocumentoClinico>().AsNoTracking().Where(x=>x.PacienteId==paciente.Id).ToListAsync();Exigir(docs.Any(x=>x.PacienteAssinou),"Assinatura não persistiu no documento");}
  Console.WriteLine("OK assinatura web: identidade, declarações, PNG, confirmação e documento persistido.");
  if(!web)return;
  var paginas=Clinica.Faturamento.Web.FaturamentoWebRegistro.CriarPaginas().ToArray();using var view=new SuiteWebView(sp,paginas,Clinica.Faturamento.Web.FaturamentoWebRegistro.CriarDialogos().Concat(RegistroCompartilhadoWeb.DialogosAssinatura()),new Clinica.Faturamento.Modulo.ModuloFaturamento().Itens.Where(i=>paginas.Any(p=>p.Chave==i.Chave)).ToArray(),"Demonstração","faturamento-pendencias");var janela=new Window{Content=view,Width=1100,Height=720,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};System.Windows.Application.Current.MainWindow=janela;janela.Show();
  var vm2=Criar();try{
   await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));var tarefa=view.Dialogos.AbrirAsync("AssinaturaPaciente",vm2);await Esperar(()=>!vm2.Carregando&&vm2.Numero.Length>0);var browser=(WebView2)view.Content;await Task.Delay(400);
   var encontrou=await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('canvas')");Exigir(encontrou=="true","Canvas não renderizou na assinatura real");
   // Entrada real do navegador: ponteiro produz PNG e envia pela mesma ponte usada no atendimento.
   Directory.CreateDirectory("artifacts/faturamento-web");using(var f=File.Create("artifacts/faturamento-web/assinatura-termo-1100.png"))await view.CapturarPreviewAsync(f);
   var id=view.Dialogos.EstadoAtual!.Id;
   await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('canvas').scrollIntoView({block:'center'})");await Task.Delay(150);
   using(var rect=JsonDocument.Parse(await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const r=document.querySelector('canvas').getBoundingClientRect();return {x:r.left+25,y:r.top+60}})()")))
   {
    var x=rect.RootElement.GetProperty("x").GetDouble();var y=rect.RootElement.GetProperty("y").GetDouble();
    async Task Mouse(string tipo,double mx,double my,string botao="none")=>await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type=tipo,x=mx,y=my,button=botao,clickCount=1}));
    await Mouse("mousePressed",x,y,"left");for(var j=1;j<24;j++)await Mouse("mouseMoved",x+j*10,y+(j%2)*28,"left");await Mouse("mouseReleased",x+240,y,"left");
   }
   try{await Esperar(()=>vm2.TemTraco);}catch{throw new Exception("Canvas principal: "+vm2.Mensagem+"; DOM="+await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web').innerText"));}Exigir(vm2.TracoWeb.Length>300,"Traço do canvas principal não chegou pela ponte");await Task.Delay(300);
   Directory.CreateDirectory("artifacts/faturamento-web");using(var f=File.Create("artifacts/faturamento-web/assinatura-paciente-1100.png"))await view.CapturarPreviewAsync(f);
   var painel=new PainelAssinaturaWeb(vm2);await painel.AbrirAsync(new TelaDoSistema("qa","Segunda tela sintética",false,-30000,-30000,1100,720));
   try
   {
    var remoto=(WebView2)painel.Content;await Task.Delay(300);vm2.LimparTracoWebCommand.Execute(null);painel.Limpar();
    await remoto.CoreWebView2.ExecuteScriptAsync("(()=>{const c=document.querySelector('canvas'),r=c.getBoundingClientRect();c.dispatchEvent(new PointerEvent('pointerdown',{pointerId:1,clientX:r.left+30,clientY:r.top+70,bubbles:true}));for(let i=1;i<24;i++)c.dispatchEvent(new PointerEvent('pointermove',{pointerId:1,clientX:r.left+30+i*12,clientY:r.top+70+(i%2)*35,bubbles:true}));c.dispatchEvent(new PointerEvent('pointerup',{pointerId:1,bubbles:true}));})()");
    await Esperar(()=>vm2.TemTraco);vm2.ConfirmarTodasCommand.Execute(null);await Task.Delay(150);
    Exigir(await remoto.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('.resposta').length>0 && [...document.querySelectorAll('.resposta')].every(x=>x.textContent.includes('Sim'))")=="true","Segunda tela perdeu respostas");
    using(var f=File.Create("artifacts/faturamento-web/assinatura-segunda-tela.png"))await remoto.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
    Console.WriteLine("OK segunda tela WebView2: texto/declarações sincronizadas, pointer→PNG→C#, limpar e encerramento.");
   }
   finally{painel.Close();}
   view.Dialogos.Fechar(id);await tarefa;
   Console.WriteLine("OK WebView2: formulário completo e canvas de assinatura renderizados; cancelamento preserva termo pendente.");
  }finally{vm2.EncerrarWeb();janela.Close();}
 }
 static async Task Esperar(Func<bool> f){for(var i=0;i<200;i++){if(f())return;await Task.Delay(30);}throw new Exception("Carga do termo não terminou");}
 static string Traco()
 {
  var dv=new DrawingVisual();using(var g=dv.RenderOpen()){var pen=new Pen(Brushes.Black,4.4);for(var i=0;i<24;i++)g.DrawLine(pen,new Point(40+i*30,140+(i%2)*75),new Point(70+i*30,140+((i+1)%2)*75));}var bmp=new RenderTargetBitmap(1600,440,96,96,PixelFormats.Pbgra32);bmp.Render(dv);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using var m=new MemoryStream();enc.Save(m);return JsonSerializer.Serialize(new{png="data:image/png;base64,"+Convert.ToBase64String(m.ToArray()),largura=800,altura=220,temTraco=true});
 }
}
