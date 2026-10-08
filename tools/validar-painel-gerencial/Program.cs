using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Gerente.Views;
using Clinica.Gerente.ViewModels;

static class Program
{
 [STAThread] static void Main()
 {
  Directory.CreateDirectory("artifacts/qa-faturamento-gerencial");
  var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
  app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
  app.Dispatcher.BeginInvoke(async ()=> {
   try {
    foreach(var (width,height) in new[]{(900,600),(1180,720)}) {
     var view=new FaturamentoGerencialView {DataContext=new Dados()};
     var window=new Window { Content=view,Width=width,Height=height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Left=-30000,Top=-30000 };
     window.Show();
     await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
     window.UpdateLayout();
     Console.WriteLine($"Tamanho {width}x{height}; view={view.ActualWidth}x{view.ActualHeight}");
     var principal=Desc<ScrollViewer>(view).Single();
     if(principal.ViewportHeight<100 || principal.ScrollableHeight<=0)throw new Exception("O painel precisa de viewport utilizável e rolagem para alcançar os convênios.");
     var descer=Desc<RepeatButton>(view).Single(b=>b.Command==ScrollBar.LineDownCommand);
     ((System.Windows.Input.RoutedCommand)descer.Command).Execute(null,descer.CommandTarget);
     await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
     if(principal.VerticalOffset<=0)throw new Exception("Seta para baixo não rolou o painel.");
     Console.WriteLine($"Seta para baixo: offset={principal.VerticalOffset}");
     foreach(var sv in Desc<ScrollViewer>(view)) {
      var pt=sv.TransformToAncestor(view).Transform(new Point(0,0));
      Console.WriteLine($"ScrollViewer x={pt.X} y={pt.Y} altura={sv.ActualHeight} viewport={sv.ViewportHeight} extent={sv.ExtentHeight} max={sv.ScrollableHeight} barra={sv.ComputedVerticalScrollBarVisibility}");
      sv.ScrollToEnd();
     }
     await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
     foreach(var panel in Desc<WrapPanel>(view))Console.WriteLine($"KPI altura={panel.ActualHeight};y={panel.TransformToAncestor(view).Transform(new Point(0,0)).Y}");
     foreach(var tb in Desc<TextBlock>(view).Where(t=>t.Text=="Convênio"||t.Text=="Sintético 01"||t.Text=="Sintético 12"))Console.WriteLine($"Texto {tb.Text}: y={tb.TransformToAncestor(view).Transform(new Point(0,0)).Y};altura={tb.ActualHeight};visivel={tb.IsVisible}");
     foreach(var b in Desc<RepeatButton>(view).Where(b=>b.Command==ScrollBar.LineDownCommand||b.Command==ScrollBar.LineUpCommand))Console.WriteLine($"Seta {b.Command}: altura={b.ActualHeight}; y={b.TransformToAncestor(view).Transform(new Point(0,0)).Y}; visivel={b.IsVisible}");
     var ultima=Desc<TextBlock>(view).Single(t=>t.Text=="Sintético 12");
     var ultimaY=ultima.TransformToAncestor(view).Transform(new Point(0,0)).Y;
     if(ultimaY<0 || ultimaY+ultima.ActualHeight>height)throw new Exception("Último convênio não ficou visível ao rolar até o fim.");
     Console.WriteLine("OK: último convênio inteiramente acessível pela rolagem.");
     var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
     using(var f=File.Create($"artifacts/qa-faturamento-gerencial/gerencial-{width}x{height}.png")){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));png.Save(f);}
     window.Close();
    }
   }catch(Exception e){Console.WriteLine(e);Environment.ExitCode=1;}
   finally{app.Shutdown();}
  });app.Run();
 }
 static IEnumerable<T> Desc<T>(DependencyObject p) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is T t)yield return t;foreach(var d in Desc<T>(c))yield return d;}}
}
public class Dados
{
 public string[] Periodos=>["Este mês"];public string PeriodoSelecionado {get;set;}="Este mês";
 public object? CarregarCommand=>null;public object? GerarFechamentoCommand=>null;
 public bool PodeGerarFechamento=>false; public bool Carregando=>false;public bool NaoVerificado=>false;
 public bool MensagemEhErro=>false;public string Mensagem=>"";
 public int TotalGuias=>100;public int PendenciasEmAberto=>20;
 public string TaxaBaixaFormatada=>"80%";public string TaxaGlosaFormatada=>"5%";public string TempoMedioFormatado=>"8 dias";
 public double TaxaBaixaFracao=>0.8;public double TaxaGlosaFracao=>0.05;public bool TemTaxaBaixa=>true;public bool TemTaxaGlosa=>true;
 public bool TemSerie=>false;public Clinica.Desktop.Controls.PontoGrafico[] SerieTaxaBaixa=>[];public object[] Meses=>[];
 public object VariacaoGuias=>new {Texto="+10%",Rotulo="vs. anterior",Leitura="Neutra",Detalhe="Comparação sintética"};public object VariacaoTaxaBaixa=>VariacaoGuias;public object VariacaoTaxaGlosa=>VariacaoGuias;public object VariacaoTempoMedio=>VariacaoGuias;
 public LinhaEnvelhecimento[] Envelhecimento=>[new("0–7 dias","10",0.5),new("8–30 dias","6",0.3),new("+30 dias","4",0.2)];
 public LinhaConvenioGerencial[] PorConvenio=>Enumerable.Range(1,12).Select(i=>new LinhaConvenioGerencial{Convenio=$"Sintético {i:00}",Total=100,Baixados=80,Pendentes=20,TaxaBaixa="80%",TaxaGlosa="5%",TempoMedio="8 dias"}).ToArray();
}
