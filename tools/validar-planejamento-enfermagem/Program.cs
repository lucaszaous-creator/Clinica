using System.IO;
using System.Windows;
using System.Windows.Media;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Recepcao.ViewModels;
using Clinica.Recepcao.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    [STAThread] private static int Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        var resultado = 0;
        app.Startup += async (_, _) =>
        {
            try { await Executar(); Console.WriteLine("APROVADO: recepção real WebView2, banco sintético, leitura compartilhada, encerramento e atalho para agenda."); }
            catch (Exception e) { Console.WriteLine(e); resultado = 1; }
            finally { app.Shutdown(); }
        };
        app.Run(); return resultado;
    }
    private static Task<string> Script(WebView2 web, string js) => web.CoreWebView2.ExecuteScriptAsync(js);
    private static async Task Esperar(WebView2 web, string js)
    {
        var limite = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < limite)
        {
            if (web.CoreWebView2 is not null && await Script(web, js) == "true") return;
            await Task.Delay(100);
        }
        throw new Exception("Condição não confirmada: " + js);
    }
    private static WebView2? Navegador(DependencyObject raiz)
    {
        if (raiz is WebView2 web) return web;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
            if (Navegador(VisualTreeHelper.GetChild(raiz, i)) is { } filho) return filho;
        return null;
    }
    private static void Conferir(bool valor, string mensagem) { if (!valor) throw new Exception(mensagem); }
    private static async Task Executar()
    {
        const string saida = "artifacts/planejamento-enfermagem"; Directory.CreateDirectory(saida);
        using var conexao = new SqliteConnection("Data Source=:memory:"); conexao.Open();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        servicos.AddScoped(_ => new ClinicaDbContext(opcoes));
        servicos.AddSingleton<SessaoUsuario>(); servicos.AddSingleton<SnackbarService>();
        servicos.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        servicos.AddSingleton<IDialogoService, Dialogos>();
        new Clinica.Recepcao.Modulo.ModuloRecepcao().Registrar(servicos);
        using var provedor = servicos.BuildServiceProvider(); using var escopo = provedor.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
        var tecnica = new UsuarioSistema { Nome = "Técnica fictícia", Login = "tecnica.qa", Perfil = PerfilAcesso.Enfermagem, Profissional = new() { Nome = "Enfermagem fictícia", Ativo = true } };
        var recepcao = new UsuarioSistema { Nome = "Recepção fictícia", Login = "recepcao.qa", Perfil = PerfilAcesso.Recepcao };
        var paciente = new Paciente { Nome = "Paciente sintético" };
        db.AddRange(tecnica, recepcao, paciente); await db.SaveChangesAsync();
        SessaoUsuario.Atual.Entrar(recepcao);
        var inicio = DateTime.SpecifyKind(DateTime.Today.AddDays(2).AddHours(8), DateTimeKind.Unspecified);
        var svc = escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>();
        var resumo = await svc.PeriodoAsync(tecnica.Id, new(inicio, inicio.AddHours(4)));
        var id = await svc.CriarAsync(tecnica.Id, new(Guid.NewGuid(), inicio, inicio.AddHours(4), inicio, ModalidadeAtendimento.BsvApenas, 4, "Distribuir ao longo da manhã. <script>teste</script>", resumo.Referencia));
        var outroDia = inicio.AddDays(1);
        resumo = await svc.PeriodoAsync(tecnica.Id, new(outroDia, outroDia.AddHours(4)));
        await svc.CriarAsync(tecnica.Id, new(Guid.NewGuid(), outroDia, outroDia.AddHours(4), outroDia, ModalidadeAtendimento.BsvComAcupuntura, 2, "Segundo dia fictício", resumo.Referencia));
        // Mesmo após o envio, a agenda pode mudar em qualquer canal.
        db.Agendamentos.Add(new() { PacienteId = paciente.Id, DataHora = inicio, ModalidadePrevista = ModalidadeAtendimento.BsvApenas });
        await db.SaveChangesAsync();
        var vm = new AgendaViewModel(provedor.GetRequiredService<IServiceScopeFactory>(), provedor.GetRequiredService<ISnackbarService>(), new Dialogos());
        var tela = new AgendaView { DataContext = vm };
        var janela = new Window { Content = tela, Width = 1180, Height = 850, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
        janela.Show(); await Task.Delay(600);
        var agenda = Navegador(tela) ?? throw new Exception("Agenda sem navegador");
        await Esperar(agenda, "document.querySelector('.avisos-enfermagem')?.textContent.includes('2 novos')===true");
        // Postagem sem contexto deve ser ignorada pelo adaptador real.
        await Script(agenda, "window.chrome.webview.postMessage({acao:'pedidos-enfermagem',contexto:'invalido'})");
        await Task.Delay(150);
        Conferir(System.Windows.Application.Current.Windows.Count == 1, "Mensagem sem contexto abriu janela");
        await Script(agenda, "document.querySelector('.avisos-enfermagem button').click()");
        Window? caixa = null;
        for (var i = 0; i < 200 && caixa is null; i++) { caixa = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pedidos da enfermagem"); await Task.Delay(100); }
        if (caixa is null) throw new Exception("Caixa de avisos não abriu");
        caixa.Left = -30000; caixa.Top = -30000;
        var web = Navegador(caixa) ?? throw new Exception("Caixa sem navegador");
        await Esperar(web, "document.querySelectorAll('.pedidos-lista li').length===2");
        foreach (var largura in new[] { 620, 900, 1100 })
        {
            caixa.Width = largura; caixa.UpdateLayout(); await Task.Delay(200);
            await Esperar(web, "document.documentElement.scrollWidth<=innerWidth+1");
            using var png = File.Create($"{saida}/recepcao-{largura}.png");
            await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        }
        await Script(web, "document.querySelector('.pedidos-lista li button').click()");
        await Esperar(web, "document.querySelector('.pedido-status')?.textContent==='Visto pela recepção' && !!document.querySelector('.pedido-mudanca')");
        Conferir((await svc.DetalheAsync(tecnica.Id, id)).Pedido.Situacao == "Visto", "Leitura não chegou ao portal");
        Conferir(await Script(web, "!!document.querySelector('script:not([src])')") == "false", "Observação virou script");
        using (var png = File.Create($"{saida}/detalhe.png")) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        await Script(web, "[...document.querySelectorAll('button')].find(b=>b.textContent==='Encerrar aviso').click()");
        await Esperar(web, "!!document.querySelector('#pedido-retorno')");
        await Script(web, "[...document.querySelectorAll('button')].find(b=>b.textContent==='Continuar com o aviso aberto').click()");
        Conferir((await svc.DetalheAsync(tecnica.Id, id)).Pedido.Situacao == "Visto", "Cancelar fechou o aviso");
        await Script(web, "[...document.querySelectorAll('button')].find(b=>b.textContent==='Encerrar aviso').click()");
        await Script(web, "[...document.querySelectorAll('button')].find(b=>b.textContent==='Confirmar encerramento').click()");
        await Esperar(web, "document.querySelector('.pedido-status')?.textContent==='Aviso encerrado'");
        Conferir((await svc.DetalheAsync(tecnica.Id, id)).Pedido.Situacao == "Encerrado", "Encerramento não persistiu");
        Conferir(await db.Agendamentos.CountAsync() == 1, "Aviso criou ou removeu agendamento");
        await Script(web, "[...document.querySelectorAll('button')].find(b=>b.textContent==='Ver este dia na agenda').click()");
        await Task.Delay(300);
        Conferir(vm.Dia.Date == inicio.Date && vm.FiltroProfissional?.Id is null && vm.FiltroSala?.Id is null, "Atalho não removeu filtros do dia");
        await Esperar(agenda, "document.querySelector('.avisos-enfermagem')?.textContent.includes('1 novos')===true");
        janela.Close();
    }
    private sealed class Dialogos : IDialogoService
    {
        public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
        public bool Confirmar(string titulo, string mensagem) => false;
        public bool ConfirmarPerigo(string titulo, string mensagem) => false;
        public void Aviso(string titulo, string mensagem) { }
    }
}
