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
            var vm=new ShellViewModel(name+" — Clínica SemDor · DEMONSTRAÇÃO",scope,services);((JanelaDoApp)window).MostrarMoldura(vm);
            var allowed=(HashSet<string>)typeof(ShellViewModel).GetField("_telasTreinamento",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vm)!;
            var lessons=CatalogoTreinamento.Filtrar(plans,allowed);
            if(name is "Financeiro" or "Faturamento" && lessons.Any(a=>a.Id.StartsWith("consultorio-")))throw new Exception("Clinical lesson leaked into "+name);
            if(lessons.Select(a=>a.Id).Distinct().Count()!=lessons.Count)throw new Exception("Duplicate lessons");
            vm.AbrirTreinamentoCommand.Execute(null);await Task.Delay(500);
            // Blazor desde out/2026: o treinamento é a tela Treinamento no navegador da janela.
            if(vm.TelaAtual is not ITelaDoNavegador{Componente.Name:"Treinamento"})throw new Exception("Training screen not opened in "+name);
            reports.Add(new{module=name,lessonCount=lessons.Count,lessons=lessons.Select(a=>a.Id),trainingView="Treinamento (Blazor)"});
        }
        File.WriteAllText(Path.Combine(output,"acesso-modulos.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        // O catálogo que a tela recebe é o recorte do shell (TreinamentoDaSuite): a busca sem acento,
        // o player e a tela cheia são do navegador agora, e saem conferidos na tela, não aqui.
        var acervo=services.GetService(typeof(TreinamentoDaSuite)) as TreinamentoDaSuite;
        var aulas=acervo?.Aulas()??[];
        File.WriteAllText(Path.Combine(output,"verificado.txt"),$"Five module scopes; no clinical videos in finance/billing; no duplicates; the training screen opens in each scope ({aulas.Count} lessons for the last scope).");
    }
    static IEnumerable<DependencyObject> Desc(DependencyObject root){for(var n=0;n<VisualTreeHelper.GetChildrenCount(root);n++){var c=VisualTreeHelper.GetChild(root,n);yield return c;foreach(var d in Desc(c))yield return d;}}
    static async Task Shot(Window window,string output,string name)
    {
        window.UpdateLayout();await Task.Delay(500);var root=(FrameworkElement)window.Content;
        var bmp=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(root);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(Path.Combine(output,name+".png"));png.Save(file);
    }
}
