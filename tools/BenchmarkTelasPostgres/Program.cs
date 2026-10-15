using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
using Npgsql;

internal static class Program
{
    static readonly bool Virtualizado = Environment.GetEnvironmentVariable("CLINICA_BENCH_VARIANTE")=="virtualizado";
    static readonly bool Capturar = Environment.GetEnvironmentVariable("CLINICA_BENCH_FOTO")=="1";
    static readonly string Saida = Path.GetFullPath("artifacts/buscas-postgres/"+(Capturar?"telas-captura":Virtualizado?"telas-virtualizadas":"telas"));
    static readonly Medidor Medidor = new();
    static readonly List<object> Resultados = [];
    static string Cadeia = "";
    static int Sequencia;
    [STAThread] static int Main()
    {
        Directory.CreateDirectory(Saida);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        int codigo = 0;
        app.Dispatcher.BeginInvoke(async () => {
            try { await Executar(); Console.WriteLine("APROVADO: cenários concluídos no fixture isolado."); }
            catch(Exception e) { Console.WriteLine(e); codigo=1; }
            finally { app.Shutdown(); }
        });
        app.Run(); return codigo;
    }
    static async Task Sql(string texto)
    {
        await using var c = new NpgsqlConnection(Cadeia); await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(texto,c) { CommandTimeout=180 }; await cmd.ExecuteNonQueryAsync();
    }
    static async Task Executar()
    {
        var entrada = Environment.GetEnvironmentVariable("CLINICA_BENCH_TELAS") ?? throw new Exception("Defina a conexão sintética CLINICA_BENCH_TELAS.");
        var cs = new NpgsqlConnectionStringBuilder(entrada);
        if(cs.Host!="127.0.0.1" || cs.Port!=55439 || cs.Username!="bench_clinica" || cs.Database is null || !System.Text.RegularExpressions.Regex.IsMatch(cs.Database,"^bench_clinica_[a-f0-9]{32}$"))
            throw new Exception("Permitido somente banco sintético do BenchmarkBuscasPostgres no cluster loopback exclusivo.");
        Cadeia=cs.ConnectionString;
        var servicos = new ServiceCollection(); servicos.AddClinica(Cadeia);
        servicos.AddScoped(_ => new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseNpgsql(Cadeia).AddInterceptors(Medidor).Options));
        servicos.AddSingleton<IDialogoService,Dialogo>(); servicos.AddSingleton<ISnackbarService,SnackbarService>();
        servicos.AddSingleton<SessaoUsuario>(); new ModuloClinico().Registrar(servicos);
        using var sp = servicos.BuildServiceProvider();
        using(var scope=sp.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            if(await db.Pacientes.CountAsync()!=50000 || await db.Pacientes.Where(p=>p.Id==1).Select(p=>p.Nome).SingleAsync()!="Zefirina Quitéria Fictícia") throw new Exception("Base não corresponde ao fixture sintético.");
            var usuario = new UsuarioSistema { Nome="Operador fictício benchmark", Login="bench."+Guid.NewGuid().ToString("N"), Perfil=PerfilAcesso.Gerente, ProfissionalId=1 };
            db.Usuarios.Add(usuario); await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        }
        // A-B-A: a reversão ajuda a distinguir efeito do índice de aquecimento/JIT/WebView.
        var fases = Capturar ? ["antes"] : Environment.GetEnvironmentVariable("CLINICA_BENCH_FASES")?.Split(',') ?? ["antes","indices","antes2"];
        if(fases.Any(f=>f is not ("antes" or "indices" or "antes2"))) throw new Exception("Fase inválida.");
        foreach(var fase in fases)
        {
            await Sql("DO $$ DECLARE i record; BEGIN FOR i IN SELECT indexname FROM pg_indexes WHERE schemaname='public' AND indexname LIKE 'bench_%' LOOP EXECUTE format('DROP INDEX %I',i.indexname); END LOOP; END $$;");
            if(fase=="indices")
            {
                await Sql(await File.ReadAllTextAsync("tools/BenchmarkBuscasPostgres/indices-texto.sql"));
                await Sql(await File.ReadAllTextAsync("tools/BenchmarkBuscasPostgres/indices-selecionados.sql"));
            }
            await Sql("ANALYZE;");
            foreach(var atraso in Capturar ? new[]{0} : new[]{0,250})
            for(int amostra=0;amostra<(Capturar?1:3);amostra++)
            {
                if(Capturar) { await Clinica(sp,fase,atraso,amostra,1,"prontuario"); continue; }
                await Pacientes(sp,fase,atraso,amostra);
                foreach(var paciente in new[]{10001,1})
                {
                    await Clinica(sp,fase,atraso,amostra,paciente,"documentos");
                    await Clinica(sp,fase,atraso,amostra,paciente,"infusoes");
                    await Clinica(sp,fase,atraso,amostra,paciente,"prontuario");
                    await Clinica(sp,fase,atraso,amostra,paciente,"ficha");
                }
            }
        }
    }
    static Window Janela(object view) => new() { Title="Benchmark — dados fictícios", Width=1366, Height=768, Left=-10000, Top=-10000, ShowActivated=false, ShowInTaskbar=false, Content=view };
    static void Iniciar(int atraso) { Medidor.Iniciar(atraso); }
    static async Task Pacientes(ServiceProvider sp,string fase,int atraso,int amostra)
    {
        var vm=new Clinica.Recepcao.ViewModels.PacientesViewModel(sp.GetRequiredService<IServiceScopeFactory>(),sp.GetRequiredService<ISnackbarService>(),sp.GetRequiredService<IDialogoService>(),sp.GetRequiredService<IFabricaFichaPaciente>());
        var view=new Clinica.Recepcao.Views.PacientesView { DataContext=vm }; var w=Janela(view); w.Show();
        try
        {
            await Task.Delay(100);
            foreach(var termo in new[]{"","zefirina","silva","00000012345"})
            {
                Iniciar(atraso); var cron=Stopwatch.StartNew();
                if(termo=="") await vm.Seletor.DesligarSugestaoCommand.ExecuteAsync(null);
                else vm.Seletor.Termo=termo; // Inclui o debounce real da digitação.
                await Esperar(()=>vm.Seletor.ResultadoAtual && !vm.Seletor.Buscando);
                if(vm.Seletor.TemErro || vm.Seletor.Resultados.Count==0) throw new Exception("Busca não entregou o resultado esperado: "+termo);
                var dados=cron.Elapsed.TotalMilliseconds;
                await Quadro(w);
                if(!Descendentes(w).OfType<ItemsControl>().Any(i=>i.ItemsSource==vm.Seletor.Resultados && i.ItemContainerGenerator.ContainerFromIndex(0) is not null)) throw new Exception("Lista WPF sem primeira linha materializada.");
                await Registrar(fase,atraso,amostra,"pacientes-"+(termo==""?"lista":termo),0,vm.Seletor.Resultados.Count,dados,cron.Elapsed.TotalMilliseconds);
            }
        }
        finally { w.Close(); await Task.Delay(100); GC.Collect(); GC.WaitForPendingFinalizers(); }
    }
    static async Task Clinica(ServiceProvider sp,string fase,int atraso,int amostra,int paciente,string tela)
    {
        var foco=new PacienteEmFoco(); foco.Definir(paciente,paciente==1?"Zefirina Quitéria Fictícia":"Paciente fictício 10001");
        var fabrica=sp.GetRequiredService<IServiceScopeFactory>(); var aviso=sp.GetRequiredService<ISnackbarService>(); var dialogo=sp.GetRequiredService<IDialogoService>();
        Iniciar(atraso); var cron=Stopwatch.StartNew();
        FrameworkElement view; Func<bool> pronto; Func<int> linhas;
        if(tela=="documentos")
        {
            var vm=new PrescricoesClinicasViewModel(fabrica,foco,aviso,dialogo);
            view=new PrescricoesClinicasView { DataContext=vm }; pronto=()=>!vm.Carregando && vm.Documentos.Count>0 && !vm.NaoVerificado; linhas=()=>vm.Documentos.Count;
        }
        else if(tela=="infusoes")
        {
            var vm=new PrescricaoInfusaoViewModel(fabrica,foco,aviso,dialogo);
            view=new PrescricaoInfusaoView { DataContext=vm }; pronto=()=>!vm.Carregando && vm.Prescricoes.Count>0 && !vm.NaoVerificado; linhas=()=>vm.Prescricoes.Count;
        }
        else if(tela=="prontuario")
        {
            var vm=new ProntuarioClinicoViewModel(fabrica,foco);
            view=new ProntuarioClinicoView { DataContext=vm }; pronto=()=>!vm.Carregando && vm.Sessoes.Count>0 && !vm.NaoVerificado; linhas=()=>vm.Sessoes.Count;
        }
        else
        {
            var vm=new PacienteWorkspaceViewModel(sp,foco);
            view=new PacienteWorkspaceView { DataContext=vm };
            pronto=()=>!vm.Atendimento.Carregando && !vm.Atendimento.NaoVerificado && vm.Cabecalho is not null && Medidor.Pendentes==0;
            linhas=()=>1;
        }
        var w=Janela(view); w.Show();
        try
        {
            await Esperar(pronto); var dados=cron.Elapsed.TotalMilliseconds;
            if(tela is "documentos" or "infusoes")
            {
                var web=Descendentes(w).OfType<WebView2>().Single();
                await Esperar(()=>web.CoreWebView2 is not null);
                var limite=Stopwatch.StartNew();
                while(await web.CoreWebView2.ExecuteScriptAsync($"document.querySelectorAll('.rx-linha').length==={linhas()}")!="true")
                { if(limite.Elapsed.TotalSeconds>60) throw new TimeoutException("DOM sem as linhas esperadas."); await Task.Delay(20); }
                await web.CoreWebView2.ExecuteScriptAsync("window.__benchQuadro=false;requestAnimationFrame(()=>requestAnimationFrame(()=>window.__benchQuadro=true));");
                while(await web.CoreWebView2.ExecuteScriptAsync("window.__benchQuadro")!="true")
                { if(limite.Elapsed.TotalSeconds>60) throw new TimeoutException("WebView sem quadro renderizado."); await Task.Delay(20); }
            }
            await Quadro(w);
            if(tela=="prontuario" && !Descendentes(w).OfType<ItemsControl>().Any(i=>i.Items.Count==linhas() && i.ItemContainerGenerator.ContainerFromIndex(0) is not null)) throw new Exception("Prontuário WPF sem linhas materializadas.");
            await Registrar(fase,atraso,amostra,tela,paciente,linhas(),dados,cron.Elapsed.TotalMilliseconds);
            if(Capturar)
            {
                var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(w);
                var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var arquivo=File.Create(Path.Combine(Saida,"prontuario-3000.png")); png.Save(arquivo);
            }
            if(Virtualizado && tela=="prontuario" && paciente==1)
            {
                // Depois do cronômetro: prova que a melhora não cortou o histórico.
                var lista=Descendentes(w).OfType<ItemsControl>().First(i=>i.Items.Count==linhas());
                int realizados=Enumerable.Range(0,lista.Items.Count).Count(i=>lista.ItemContainerGenerator.ContainerFromIndex(i) is not null);
                if(realizados>=lista.Items.Count || realizados==0) throw new Exception("Lista não virtualizou.");
                var rolagem=Descendentes(lista).OfType<ScrollViewer>().First();
                rolagem.ScrollToEnd(); await Quadro(w); await Task.Delay(50); await Quadro(w);
                if(lista.ItemContainerGenerator.ContainerFromIndex(lista.Items.Count-1) is null) throw new Exception("Última evolução inacessível.");
                var vm=(ProntuarioClinicoViewModel)view.DataContext;
                vm.TermoSessao="conteúdo fictício"; await Quadro(w);
                if(vm.Sessoes.Count!=3000) throw new Exception("Busca no histórico perdeu registros.");
                vm.TermoSessao="termo inexistente para benchmark"; await Quadro(w);
                if(vm.Sessoes.Count!=0) throw new Exception("Filtro não aplicado.");
                vm.TermoSessao=""; await Quadro(w);
                if(vm.Sessoes.Count!=3000) throw new Exception("Limpar filtro perdeu registros.");
                await File.AppendAllTextAsync(Path.Combine(Saida,"validacao-historico.log"),$"{fase}/{atraso}/{amostra}: 3000 registros, {realizados} containers, último acessível e filtros íntegros.\n");
            }
            await Esperar(()=>Medidor.Pendentes==0); await Task.Delay(100);
        }
        finally { w.Close(); await Task.Delay(100); GC.Collect(); GC.WaitForPendingFinalizers(); }
    }
    static async Task Quadro(Window w)
    {
        w.UpdateLayout();
        await w.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);
    }
    static async Task Esperar(Func<bool> condicao)
    {
        var cron=Stopwatch.StartNew();
        while(!condicao()) { if(cron.Elapsed.TotalSeconds>90) throw new TimeoutException("Tela não ficou pronta."); await Task.Delay(20); }
    }
    static IEnumerable<DependencyObject> Descendentes(DependencyObject raiz)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(raiz);i++) { var filho=VisualTreeHelper.GetChild(raiz,i); yield return filho; foreach(var d in Descendentes(filho)) yield return d; }
    }
    static async Task Registrar(string fase,int atraso,int amostra,string tela,int paciente,int linhas,double dados,double quadro)
    {
        var comandos=Medidor.Comandos.ToArray();
        Resultados.Add(new { sequencia=++Sequencia,fase,atrasoPorComandoMs=atraso,amostra,tela,paciente,linhas,dadosProntosMs=dados,telaProntaMs=quadro,aposDadosMs=quadro-dados,comandos=comandos.Length,latenciaArtificialSomadaMs=comandos.Length*atraso,comandosDetalhados=comandos });
        Console.WriteLine($"{fase} {atraso}ms {tela} p{paciente} #{amostra}: dados={dados:F1} tela={quadro:F1} ms; {comandos.Length} comandos; {linhas} linhas");
        await File.WriteAllTextAsync(Path.Combine(Saida,"resultado.json"),JsonSerializer.Serialize(new { banco=new NpgsqlConnectionStringBuilder(Cadeia).Database,limites="PostgreSQL local com dados fictícios. 250 ms artificiais por comando EF, inclusive auditoria. Não simula banda, TLS, pool remoto, servidor sobrecarregado nem posto da clínica. Tempos de comandos podem se sobrepor; não são decomposição aditiva. WPF layout/dispatcher e WebView DOM+dois quadros, sem medir apresentação física do monitor. Primeira montagem WebView inclui partida do runtime; avaliar separadamente. Três amostras por célula, insuficientes para p95 confiável.",resultados=Resultados },new JsonSerializerOptions { WriteIndented=true }));
    }
}
sealed class Medidor:DbCommandInterceptor
{
    public ConcurrentQueue<object> Comandos { get; }=new();
    readonly ConcurrentDictionary<Guid,Stopwatch> Ativos=new();
    public int Atraso; public int Pendentes=>Ativos.Count;
    public void Iniciar(int atraso) { if(Pendentes!=0) throw new Exception("Medição anterior ainda tem comandos."); Comandos.Clear(); Atraso=atraso; }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand cmd,CommandEventData e,InterceptionResult<DbDataReader> r,CancellationToken ct=default)
    { Ativos[e.CommandId]=Stopwatch.StartNew(); if(Atraso>0) await Task.Delay(Atraso,ct); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand cmd,CommandExecutedEventData e,DbDataReader r,CancellationToken ct=default)
    { Fim(cmd,e.CommandId); return ValueTask.FromResult(r); }
    void Fim(DbCommand cmd,Guid id)
    { if(Ativos.TryRemove(id,out var cron)) Comandos.Enqueue(new { sql=cmd.CommandText,parametros=cmd.Parameters.Cast<DbParameter>().ToDictionary(p=>p.ParameterName,p=>Convert.ToString(p.Value,System.Globalization.CultureInfo.InvariantCulture)),ateReaderIncluindoAtrasoMs=cron.Elapsed.TotalMilliseconds }); }
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand cmd,CommandEventData e,InterceptionResult<int> r,CancellationToken ct=default)
    { Ativos[e.CommandId]=Stopwatch.StartNew(); if(Atraso>0) await Task.Delay(Atraso,ct); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand cmd,CommandExecutedEventData e,int r,CancellationToken ct=default)
    { Fim(cmd,e.CommandId); return ValueTask.FromResult(r); }
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand cmd,CommandEventData e,InterceptionResult<object> r,CancellationToken ct=default)
    { Ativos[e.CommandId]=Stopwatch.StartNew(); if(Atraso>0) await Task.Delay(Atraso,ct); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand cmd,CommandExecutedEventData e,object? r,CancellationToken ct=default)
    { Fim(cmd,e.CommandId); return ValueTask.FromResult(r); }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand cmd,CommandEventData e,InterceptionResult<DbDataReader> r)
    { Ativos[e.CommandId]=Stopwatch.StartNew(); if(Atraso>0) Thread.Sleep(Atraso); return r; }
    public override DbDataReader ReaderExecuted(DbCommand cmd,CommandExecutedEventData e,DbDataReader r) { Fim(cmd,e.CommandId); return r; }
    public override Task CommandFailedAsync(DbCommand cmd,CommandErrorEventData e,CancellationToken ct=default) { Ativos.TryRemove(e.CommandId,out _); throw new Exception("Falha no banco sintético durante medição",e.Exception); }
}
sealed class Dialogo:IDialogoService
{
    public string? PerguntarTexto(string titulo,string pergunta,string? textoInicial=null,bool obrigatorio=true)=>null;
    public bool Confirmar(string titulo,string mensagem)=>false;
    public bool ConfirmarPerigo(string titulo,string mensagem)=>false;
    public void Aviso(string titulo,string mensagem)=>throw new Exception(titulo+": "+mensagem);
}
