using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Treinamento;

static class TrainingQa
{
    public static async Task Run(Window window,IModuloApp[] modules,IServiceProvider services,string output)
    {
        var plans=modules.SelectMany(m=>m.Itens).Where(i=>i.Abas.Count==0).DistinctBy(i=>i.Chave).Select(i=>new AulaTreinamento{Id=i.Chave,Telas=[i.Chave]}).ToArray();
        var modes=new Dictionary<string,IModuloApp[]>{["Recepção"]=[modules[0],new ModuloContextual(modules[1])],["Consultório"]=[modules[1],new ModuloContextual(modules[0])],["Financeiro"]=[modules[2],new ModuloContextual(modules[0]),new ModuloContextual(modules[1])],["Faturamento"]=[new Clinica.Desktop.ModuloFaturamentoAplicativo()],["Gerente"]=modules};
        var reports=new List<object>();
        foreach(var (name,scope) in modes)
        {
            var vm=new ShellViewModel(name+" — Clínica SemDor · DEMONSTRAÇÃO",scope,services);window.DataContext=vm;
            var allowed=(HashSet<string>)typeof(ShellViewModel).GetField("_telasTreinamento",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
            var lessons=CatalogoTreinamento.Filtrar(plans,allowed);
            if(name is "Financeiro" or "Faturamento" && lessons.Any(a=>a.Id.StartsWith("consultorio-")))throw new Exception("Clinical lesson leaked into "+name);
            if(lessons.Select(a=>a.Id).Distinct().Count()!=lessons.Count)throw new Exception("Duplicate lessons");
            vm.AbrirTreinamentoCommand.Execute(null);await Task.Delay(500);
            reports.Add(new{module=name,lessonCount=lessons.Count,lessons=lessons.Select(a=>a.Id),trainingView=vm.TelaAtual!.GetType().Name});
        }
        File.WriteAllText(Path.Combine(output,"acesso-modulos.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        var manager=(ShellViewModel)window.DataContext;
        var managerAllowed=(HashSet<string>)typeof(ShellViewModel).GetField("_telasTreinamento",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(manager)!;
        var training=new TreinamentoView(CatalogoTreinamento.Filtrar(CatalogoTreinamento.Ler(),managerAllowed),1,Path.Combine(output,"progresso-isolado"));
        manager.TelaAtual=training;
        window.Width=1600;window.Height=950;await Shot(window,output,"treinamento-biblioteca");
        window.Width=880;window.Height=690;await Shot(window,output,"treinamento-880");
        window.Width=1600;window.Height=950;
        var search=(TextBox)training.FindName("Busca");search.Text="CONFIRMACOES";await Task.Delay(200);
        if(((ItemsControl)training.FindName("Aulas")).Items.Count!=1)throw new Exception("Accent-insensitive search failed");
        search.Text="";await Task.Delay(100);
        var cards=Desc(training).OfType<Button>().Where(b=>b.Tag is not null&&b.Tag.GetType().Name=="Cartao").ToArray();
        if(cards.Length==0)throw new Exception("No ready lesson");
        cards[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Task.Delay(300);
        var media=Desc(training).OfType<MediaElement>().Single();media.Volume=0;
        var timeout=DateTime.UtcNow.AddSeconds(20);
        while(!media.NaturalDuration.HasTimeSpan&&DateTime.UtcNow<timeout)await Task.Delay(200);
        if(!media.NaturalDuration.HasTimeSpan)throw new Exception("Media did not open");
        await Task.Delay(1600);if(media.Position.TotalSeconds<.5)throw new Exception("Media did not advance");
        var full=Desc(training).OfType<Button>().Single(b=>Equals(b.Content,"Tela cheia"));full.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));await Task.Delay(400);
        var fullscreen=System.Windows.Application.Current.Windows.OfType<Window>().Single(w=>w!=window);fullscreen.Close();await Task.Delay(300);
        var chapterButton=Desc(training).OfType<Button>().FirstOrDefault(b=>b.Tag is double);
        var player=Desc(training).OfType<TreinamentoPlayer>().Single();player.Buscar(8);await Task.Delay(500);
        if(media.Position.TotalSeconds<7)throw new Exception("Chapter seek failed");
        await Shot(window,output,"treinamento-player");
        player.Parar();await Task.Delay(300);if(media.Position.TotalSeconds>.1)throw new Exception("Media did not stop");
        File.WriteAllText(Path.Combine(output,"verificado.txt"),"Five module scopes; no clinical videos in finance/billing; no duplicates; accent-insensitive search; media opened and advanced; fullscreen returned; seek worked; leaving stopped playback.");
    }
    static IEnumerable<DependencyObject> Desc(DependencyObject root){for(var n=0;n<VisualTreeHelper.GetChildrenCount(root);n++){var c=VisualTreeHelper.GetChild(root,n);yield return c;foreach(var d in Desc(c))yield return d;}}
    static async Task Shot(Window window,string output,string name)
    {
        window.UpdateLayout();await Task.Delay(500);var root=(FrameworkElement)window.Content;
        var bmp=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(root);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(Path.Combine(output,name+".png"));png.Save(file);
    }
}
