using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Markup;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Financeiro.Modulo;
using Clinica.Financeiro.ViewModels;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Usa as views, o shell com a logo e os serviços reais sobre SQLite em memória.
// Não chama SuiteApp, configuração salva, atualização, Pix externo ou produção.
static class Program
{
    static readonly string Saida = Path.GetFullPath("artifacts/design-financeiro/capturas");
    static readonly List<string> Relatorio = [];
    static bool ApenasDialogos;
    static bool ApenasCaixa;
    static bool ApenasWeb;
    [STAThread] static void Main(string[] args)
    {
        ApenasDialogos = args.Contains("--dialogos");
        ApenasCaixa = args.Contains("--caixa");
        ApenasWeb = args.Contains("--web");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));
        Directory.CreateDirectory(Saida);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        app.DispatcherUnhandledException += (_, e) => { Console.WriteLine(e.Exception); Environment.ExitCode = 1; e.Handled = true; app.Shutdown(); };
        app.Startup += async (_, _) =>
        {
            try { await Executar(app); }
            catch(Exception e) { Console.WriteLine(e); Environment.ExitCode = 1; }
            finally { if(Relatorio.Count>0) File.WriteAllLines(Path.Combine(Saida, "resultado.txt"), Relatorio); app.Shutdown(); }
        };
        app.Run();
    }
    static async Task Executar(System.Windows.Application app)
    {
        using var conexao = new SqliteConnection("Data Source=:memory:"); await conexao.OpenAsync();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        var collection = new ServiceCollection();
        collection.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        collection.AddScoped(_ => new ClinicaDbContext(options));
        collection.AddSingleton<SessaoUsuario>(); collection.AddSingleton<SnackbarService>();
        collection.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        collection.AddSingleton<IDialogoService, DialogosTeste>();
        var modulo = new ModuloFinanceiro(); modulo.Registrar(collection);
        using var services = collection.BuildServiceProvider();
        var db = services.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
        var user = new UsuarioSistema { Nome="Ana · demonstração", Login="design.local", Perfil=PerfilAcesso.Gerente };
        db.Add(user); await db.SaveChangesAsync(); services.GetRequiredService<SessaoUsuario>().Entrar(user);
        var entrada = new CategoriaFinanceira { Codigo="CONSULTAS_DEMO", Nome="Consultas particulares", Tipo=TipoLancamento.Entrada };
        var saida = new CategoriaFinanceira { Codigo="ESTRUTURA_DEMO", Nome="Estrutura e materiais", Tipo=TipoLancamento.Saida };
        db.AddRange(entrada, saida); await db.SaveChangesAsync();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        for(int mes=0; mes<6; mes++)
        for(int i=0; i<24; i++)
        {
            var data = hoje.AddMonths(-mes).AddDays(-i % 7);
            var previsto = mes==0 && i%4==0;
            db.Add(new LancamentoFinanceiro { Data=data, DataPagamento=previsto?null:data,
                DataVencimento=previsto?hoje.AddDays(i-8):null, Tipo=i%3==0?TipoLancamento.Saida:TipoLancamento.Entrada,
                Status=previsto?StatusLancamento.Previsto:StatusLancamento.Realizado, Valor=i%3==0?175m+i*11:240m+i*15,
                Descricao=i%3==0?$"Materiais e serviços · exemplo {i+1:00}":$"Consulta particular · exemplo {i+1:00}",
                Categoria=i%3==0?saida:entrada, FormaPagamento=FormaPagamento.Pix });
        }
        for(int i=0;i<12;i++) db.Add(new ItemEstoque { Nome=$"Material demonstrativo {i+1:00}", Unidade="un", EstoqueMinimo=5 });
        await db.SaveChangesAsync();
        if(ApenasWeb) { await PaginasWebQa.Executar(services); await DialogosWebQa.Executar(services); await WebQa.Executar(services, Saida); return; }
        if(ApenasDialogos) { await DialogosQa.Executar(services, Saida); return; }
        var shell = new ShellViewModel("Financeiro — Clínica SemDor · DEMONSTRAÇÃO", [modulo], services);
        var window = new ShellWindow(new Clinica.Financeiro.Views.NavegacaoFinanceira()) { DataContext=shell, ShowInTaskbar=false, Left=-30000, Top=-30000, WindowStartupLocation=WindowStartupLocation.Manual };
        app.MainWindow=window; window.Show();
        await Estabilizar(window);
        foreach(var botao in Desc<Button>(window).Where(b=>b.IsVisible && b.DataContext is ItemMenuModulo))
            if(BindingOperations.IsDataBound(botao,Button.CommandProperty) && botao.Command is null)
                throw new Exception("Navegação lateral sem comando: "+botao.ToolTip);
        var paginas = modulo.Itens.Where(i=>i.Abas.Count==0 && i.Chave!=ModuloFinanceiro.ChaveAjuda).ToArray();
        foreach(var (largura,altura) in (ApenasCaixa ? new[]{(900,600)} : new[]{(1440,900),(1100,720),(900,600)}))
        {
            window.Width=largura; window.Height=altura;
            foreach(var item in paginas)
            {
                if(ApenasCaixa && item.Chave!=ModuloFinanceiro.ChaveCaixa)continue;
                Console.WriteLine($"ABRIR {item.Chave} {largura}");
                if(!NavegacaoSuite.Ir(item.Chave)) throw new Exception("Rota inacessível: "+item.Chave);
                await Estabilizar(window);
                Console.WriteLine($"LAYOUT {item.Chave} {largura}");
                var container=(FrameworkElement)shell.TelaAtual!;
                var view=new[]{container}.Concat(Desc<FrameworkElement>(container)).First(e=>e.IsVisible && e is UserControl &&
                    (e.GetType().Assembly==typeof(Clinica.Financeiro.Views.CaixaView).Assembly || e.GetType().Name=="PacotesView"));
                foreach(var tabela in Desc<DataGrid>(view).Where(t=>t.IsVisible))
                    if(tabela.ActualHeight<120) throw new Exception($"Lista sem área útil em {item.Chave} {largura}: {tabela.ActualHeight:0}px.");
                foreach(var botao in Desc<Button>(view).Where(b=>b.IsVisible && BindingOperations.IsDataBound(b,Button.CommandProperty)))
                    if(botao.Command is null) throw new Exception($"Comando não resolvido em {item.Chave}: {botao.Content}");
                var carregar = view.DataContext?.GetType().GetMethod("CarregarAsync", Type.EmptyTypes);
                if(carregar?.Invoke(view.DataContext,null) is Task tarefa) await tarefa;
                await Estabilizar(window);
                if(view.DataContext?.GetType().GetProperty("MensagemEhErro")?.GetValue(view.DataContext) is true)
                    throw new Exception($"Falha de leitura em {item.Chave}: {view.DataContext.GetType().GetProperty("Mensagem")?.GetValue(view.DataContext)}");
                if(!Desc<Image>(window).Any(i=>i.Source?.ToString()?.Contains("logo-cor") == true)) throw new Exception("Logo da clínica ausente.");
                Capturar(window, $"{item.Chave}-{largura}");
                Console.WriteLine($"CAPTURA {item.Chave} {largura}");
                Relatorio.Add($"OK {item.Chave} {largura}x{altura}: view real carregada; logo presente.");
                foreach(var scroll in Desc<ScrollViewer>(view).Where(s=>s.IsVisible && s.ScrollableWidth>0).ToArray())
                {
                    if(!Desc<RepeatButton>(scroll).Any(b=>b.Command==ScrollBar.LineRightCommand && b.IsVisible))
                        throw new Exception("Sem seta de rolagem horizontal: "+item.Chave);
                    scroll.ScrollToRightEnd(); await Estabilizar(window);
                    if(scroll.HorizontalOffset<=0) throw new Exception("Colunas não rolam: "+item.Chave);
                    Capturar(window,$"{item.Chave}-colunas-direita-{largura}");
                    scroll.ScrollToLeftEnd(); await Estabilizar(window);
                }
                foreach(var tabs in Desc<TabControl>(view).Where(t=>t.IsVisible).ToArray())
                {
                    for(int i=1;i<tabs.Items.Count;i++)
                    {
                        tabs.SelectedIndex=i; await Estabilizar(window);
                        Capturar(window,$"{item.Chave}-aba{i}-{largura}");
                    }
                    tabs.SelectedIndex=0;
                }
                foreach(var scroll in Desc<ScrollViewer>(view).Where(s=>s.IsVisible && s.ScrollableHeight>0).ToArray())
                {
                    Console.WriteLine($"ROLAR {item.Chave} altura={scroll.ActualHeight:0} extensão={scroll.ExtentHeight:0}");
                    var seta=Desc<RepeatButton>(scroll).FirstOrDefault(b=>b.Command==ScrollBar.LineDownCommand && b.IsVisible);
                    if(seta is null) throw new Exception("Sem seta de rolagem: "+item.Chave);
                    scroll.ScrollToEnd(); await Estabilizar(window);
                    if(scroll.VerticalOffset<=0) throw new Exception("Conteúdo não rola: "+item.Chave);
                }
                if(view is Clinica.Financeiro.Views.CaixaView && view.DataContext is CaixaViewModel caixa)
                {
                    Console.WriteLine("FILTRAR CAIXA");
                    var total=caixa.Linhas.Count; caixa.FiltroTexto="Materiais"; await Estabilizar(window);
                    Console.WriteLine("FILTRO APLICADO");
                    if(caixa.Linhas.Count==0 || caixa.Linhas.Count>=total)throw new Exception("Filtro real do caixa não recortou dados.");
                    caixa.LimparFiltroCommand.Execute(null);
                    if(caixa.Linhas.Count!=total)throw new Exception("Limpar filtro não restaurou movimentos.");
                    Relatorio.Add("OK filtro e limpar do Caixa sobre lançamentos sintéticos persistidos.");
                }
                Console.WriteLine($"OK {item.Chave} {largura}");
            }
        }
        window.Close();
        if(!ApenasCaixa)
        {
            await DialogosQa.Executar(services, Saida);
            await SeriesQa.Executar(services);
        }
        Console.WriteLine(ApenasCaixa ? "Concluído: Caixa em 900x600, rolagem e filtro." : $"Concluído: {paginas.Length} rotas, três dimensões, abas, logo, rolagem, filtro, 15 diálogos e séries do caixa.");
    }
    static async Task Estabilizar(Window w)
    {
        await w.Dispatcher.InvokeAsync(()=>w.UpdateLayout(),DispatcherPriority.ApplicationIdle);
        await Task.Delay(70);
    }
    static void Capturar(Window w, string nome)
    {
        var root=(FrameworkElement)w.Content;
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(root); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(Path.Combine(Saida,nome+".png")); png.Save(file);
    }
    static IEnumerable<T> Desc<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var n in Desc<T>(child))yield return n;}
    }
}
sealed class DialogosTeste:IDialogoService
{
    public string? PerguntarTexto(string titulo,string pergunta,string? textoInicial=null,bool obrigatorio=true)=>null;
    public bool Confirmar(string titulo,string mensagem)=>false;
    public bool ConfirmarPerigo(string titulo,string mensagem)=>false;
    public void Aviso(string titulo,string mensagem)=>throw new InvalidOperationException(titulo+": "+mensagem);
}
