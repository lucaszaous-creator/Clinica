using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;

static class AllScreens
{
    public static async Task CaptureAsync(Window window,IModuloApp[] modules,IServiceProvider services,string output)
    {
        var done=new HashSet<string>(); var report=new List<object>();
        foreach(var module in modules)
        {
            var scope=modules.Select(m=>m==module?m:(IModuloApp)new ModuloContextual(m)).ToArray();
            var shell=new ShellViewModel(module.Nome+" — Clínica SemDor · DEMONSTRAÇÃO",scope,services);
            window.DataContext=shell;
            foreach(var item in module.Itens.Select(OrganizacaoNavegacao.Aplicar).Where(i=>i.Abas.Count==0))
            {
                if(!done.Add(item.Chave))continue;
                try
                {
                    var view=module.CriarTela(item.Chave,services) as FrameworkElement;
                    if(view==null)throw new InvalidOperationException("Tela não construída");
                    shell.TelaAtual=view;
                    if(view.DataContext is ICarregarAoAbrir load)await load.CarregarAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    await Task.Delay(1300);
                    var scenes=new List<object>();
                    async Task Shot(string step)
                    {
                        await window.Dispatcher.InvokeAsync(()=>window.UpdateLayout(),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        await Task.Delay(450);
                        var root=(FrameworkElement)window.Content;
                        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);
                        var background=new DrawingVisual();using(var drawing=background.RenderOpen())drawing.DrawRectangle(window.Background,null,new Rect(0,0,root.ActualWidth,root.ActualHeight));
                        bitmap.Render(background);bitmap.Render(root);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                        var filename=item.Chave+"-"+scenes.Count+".png";
                        using(var file=File.Create(Path.Combine(output,filename)))png.Save(file);
                        var labels=Descendants(view).OfType<TextBlock>().Where(t=>t.IsVisible&&!string.IsNullOrWhiteSpace(t.Text)).Select(t=>t.Text).Distinct().ToArray();
                        var buttons=Descendants(view).OfType<ButtonBase>().Where(b=>b.IsVisible).Select(b=>new{label=b.Content?.ToString(),enabled=b.IsEnabled,tip=b.ToolTip?.ToString()}).Where(b=>!string.IsNullOrWhiteSpace(b.label)).ToArray();
                        scenes.Add(new{step,file=filename,labels,buttons});
                    }
                    await Shot("Visão geral");
                    // Capture the actual tab content for each section, without invoking any
                    // save, send, signing or destructive command.
                    foreach(var tabs in Descendants(view).OfType<TabControl>().Where(t=>t.IsVisible).Take(2).ToArray())
                    {
                        var original=tabs.SelectedIndex;
                        for(int n=0;n<tabs.Items.Count;n++)
                        {
                            if(n==original)continue;
                            tabs.SelectedIndex=n;await Task.Delay(600);
                            await Shot((tabs.Items[n] as TabItem)?.Header?.ToString()??"Seção "+(n+1));
                        }
                        tabs.SelectedIndex=original;
                    }
                    report.Add(new{id=item.Chave,title=item.Rotulo,module=module.Nome,view=view.GetType().Name,model=view.DataContext?.GetType().Name,scenes});
                }
                catch(Exception e){report.Add(new{id=item.Chave,title=item.Rotulo,module=module.Nome,error=e.ToString()});}
                File.WriteAllText(Path.Combine(output,"capturas.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            }
        }
        File.WriteAllText(Path.Combine(output,"concluido.txt"),report.Count.ToString());
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
        {var child=VisualTreeHelper.GetChild(node,i);yield return child;foreach(var d in Descendants(child))yield return d;}
    }
}
