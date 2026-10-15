using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Recepcao.Modulo;
using Clinica.Recepcao.Views;
using Clinica.Recepcao.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    private static bool MedirAbertura;
    [STAThread] private static int Main(string[] args)
    {
        MedirAbertura = args.Contains("--medir-abertura");
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        var resultado = 0;
        app.Startup += async (_, _) =>
        {
            try { await Executar(); Console.WriteLine("APROVADO: agenda dia/semana/mês, filtros, grade, erros, permissões e notificações fora da agenda em WebView2 real."); }
            catch (Exception ex) { Console.WriteLine(ex); resultado = 1; }
            finally { app.Shutdown(); }
        };
        app.Run(); return resultado;
    }
    private static void Conferir(bool v, string erro) { if (!v) throw new Exception(erro); }
    private static async Task Esperar(Func<Task<bool>> condicao, string erro)
    {
        var limite = DateTime.UtcNow.AddSeconds(35);
        while (DateTime.UtcNow < limite) { if (await condicao()) return; await Task.Delay(120); }
        throw new Exception(erro);
    }
    private static Task Esperar(WebView2 web, string js) => Esperar(async () => web.CoreWebView2 != null && await web.CoreWebView2.ExecuteScriptAsync(js) == "true", js);
    private static WebView2? Navegador(DependencyObject raiz)
    {
        if (raiz is WebView2 web) return web;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
            if (Navegador(VisualTreeHelper.GetChild(raiz, i)) is { } filho) return filho;
        return null;
    }
    private static async Task<WebView2> AbrirWeb(Window janela, string chave)
    {
        Conferir(NavegacaoSuite.Ir(chave), "Rota inacessível: " + chave); janela.UpdateLayout();
        WebView2? web = null;
        await Esperar(() => Task.FromResult((web = Navegador(janela)) != null), "Navegador não apareceu");
        await Esperar(web!, "!!document.querySelector('main') && document.querySelector('main').getAttribute('aria-busy')==='false'");
        return web!;
    }
    private static async Task Clicar(WebView2 web, string texto)
    {
        Conferir(await web.CoreWebView2.ExecuteScriptAsync($"(()=>{{let b=[...document.querySelectorAll('button')].find(b=>b.textContent==={System.Text.Json.JsonSerializer.Serialize(texto)});if(!b||b.disabled)return false;b.click();return true;}})()") == "true", "Botão ausente ou desabilitado: " + texto);
        await Task.Delay(350);
        await Esperar(web, "document.querySelector('main')?.getAttribute('aria-busy')==='false'");
    }
    private static async Task Capturar(WebView2 web, string nome)
    {
        Directory.CreateDirectory("artifacts/ocupacao-agenda");
        using var png = File.Create($"artifacts/ocupacao-agenda/{nome}.png");
        await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
    }
    private static async Task Executar()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:"); conexao.Open();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        var servicos = new ServiceCollection(); servicos.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        servicos.AddScoped(_ => new ClinicaDbContext(opcoes));
        servicos.AddSingleton<SessaoUsuario>(); servicos.AddSingleton<SnackbarService>();
        servicos.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        servicos.AddSingleton<IDialogoService, Dialogos>();
        var modulo = new ModuloDeTeste(); modulo.Registrar(servicos);
        using var provedor = servicos.BuildServiceProvider(); using var escopo = provedor.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
        var recepcao = new UsuarioSistema { Nome = "Recepção demonstração", Login = "recepcao.qa", Perfil = PerfilAcesso.Recepcao };
        var medicos = new[] { "Ana Martins", "Bruno Oliveira", "Clara Souza" }.Select(nome => new Profissional { Nome = nome, Ativo = true,
            DiasDeAtendimento = 62, AtendeDas = new(8, 0), AtendeAte = new(17, 0) }).ToArray();
        var tecnica = new UsuarioSistema { Nome = "Enfermagem demonstração", Login = "enfermagem.qa", Perfil = PerfilAcesso.Enfermagem, Profissional = medicos[2] };
        var pacientes = new[] { "Marina Alves", "João Pereira", "Camila Ribeiro", "Pedro Santos", "Luciana Rocha", "Rafael Costa" }.Select(n => new Paciente { Nome = n }).ToArray();
        db.AddRange(recepcao, tecnica); db.Profissionais.AddRange(medicos); db.Pacientes.AddRange(pacientes); await db.SaveChangesAsync();
        var segunda = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        for (var dia = 0; dia < 5; dia++)
            for (var h = 0; h < 4 + dia; h++) db.Agendamentos.Add(new() { PacienteId = pacientes[h % 6].Id, ProfissionalId = medicos[h % 3].Id,
                DataHora = segunda.AddDays(dia).AddHours(8 + h), DuracaoMinutos = 60, ModalidadePrevista = h % 2 == 0 ? ModalidadeAtendimento.BsvApenas : ModalidadeAtendimento.Consulta });
        for (var dia = new DateTime(segunda.Year, segunda.Month, 1); dia.Month == segunda.Month; dia = dia.AddDays(1))
        {
            if (dia >= segunda && dia < segunda.AddDays(7) || dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            for (var h = 0; h < 2 + dia.Day % 7; h++) db.Agendamentos.Add(new() { PacienteId = pacientes[h % 6].Id, ProfissionalId = medicos[h % 3].Id,
                DataHora = dia.AddHours(8 + h), DuracaoMinutos = 60, ModalidadePrevista = ModalidadeAtendimento.BsvApenas });
        }
        await db.SaveChangesAsync();
        var inicio = DateTime.SpecifyKind(DateTime.Today.AddDays(2).AddHours(14), DateTimeKind.Unspecified);
        var svc = escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>();
        var resumo = await svc.PeriodoAsync(tecnica.Id, new(inicio, inicio.AddHours(2)));
        var pedido = await svc.CriarAsync(tecnica.Id, new(Guid.NewGuid(), inicio, inicio.AddHours(2), inicio, ModalidadeAtendimento.BsvApenas, 4, "Podemos receber mais quatro atendimentos neste período.", resumo.Referencia));
        var sessao = provedor.GetRequiredService<SessaoUsuario>(); sessao.Entrar(recepcao);
        var shell = new ShellViewModel("Recepção · demonstração", [modulo], provedor);
        var janela = new ShellWindow { DataContext = shell, Width = 1280, Height = 920, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
        janela.Show();
        if (MedirAbertura)
        {
            var medicoes = new List<object>();
            var navegadores = new HashSet<WebView2>();
            var paineis = new HashSet<PainelClinicoWeb>();
            for (var i = 0; i < 16; i++)
            {
                var chave = i % 2 == 0 ? ChavesSuite.OcupacaoAgenda : ChavesSuite.PedidosEnfermagem;
                var marcador = "Atualização sintética " + i;
                if (i % 2 == 0) medicos[0].Nome = marcador;
                else (await db.Set<SolicitacaoEnfermagem>().SingleAsync(p => p.Id == pedido)).Observacao = marcador;
                await db.SaveChangesAsync();
                var relogio = Stopwatch.StartNew();
                Conferir(NavegacaoSuite.Ir(chave), "Rota indisponível");
                janela.UpdateLayout();
                var montarMs = relogio.ElapsedMilliseconds;
                var navegador = Navegador(janela)!;
                paineis.Add(Encontrar<PainelClinicoWeb>(janela)!);
                var novo = navegadores.Add(navegador);
                await Esperar(() => Task.FromResult(navegador.CoreWebView2 != null), "Inicialização não terminou");
                var inicializarMs = relogio.ElapsedMilliseconds;
                await Esperar(navegador, "!!document.querySelector('main') && document.querySelector('main').getAttribute('aria-busy')==='false'");
                await Esperar(navegador, $"document.body.textContent.includes({JsonSerializer.Serialize(marcador)})");
                medicoes.Add(new { rodada = i, chave, novo, montarMs, inicializarMs, prontoMs = relogio.ElapsedMilliseconds });
            }
            Directory.CreateDirectory("artifacts/ocupacao-agenda");
            File.WriteAllText("artifacts/ocupacao-agenda/abertura.json", JsonSerializer.Serialize(new { navegadoresCriados = navegadores.Count, medicoes }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(File.ReadAllText("artifacts/ocupacao-agenda/abertura.json"));
            Conferir(navegadores.Count == 2, "Cada retorno recriou o navegador da agenda ou dos pedidos.");
            for (var i = 0; i < 10; i++)
            {
                Conferir(NavegacaoSuite.Ir("qa-descarte-" + i), "Rota sintética de descarte ausente");
                janela.UpdateLayout();
            }
            Conferir(paineis.All(p => !p.PodeReutilizar), "Um navegador removido do histórico não foi descartado.");
            await AbrirWeb(janela, ChavesSuite.PedidosEnfermagem);
            var ultimoPainel = Encontrar<PainelClinicoWeb>(janela)!;
            janela.Close();
            Conferir(!ultimoPainel.PodeReutilizar, "O navegador atual ficou vivo após fechar a janela.");
            return;
        }
        await Esperar(() => Task.FromResult(shell.PedidosNovos == 1), "Pedido não chegou ao cabeçalho fora da agenda");
        Conferir(shell.TituloTela == "Início", "Teste deve começar fora da agenda");
        var atalho = (Button)janela.FindName("PedidosEnfermagemAtalho");
        var cabecalho = (Border)janela.FindName("Cabecalho");
        Directory.CreateDirectory("artifacts/ocupacao-agenda");
        foreach (var largura in new[] { 880, 1280, 1600 })
        {
            janela.Width = largura; janela.UpdateLayout(); await Task.Delay(200);
            var ponto = atalho.TransformToAncestor(cabecalho).Transform(new Point());
            Conferir(atalho.IsVisible && ponto.X >= 0 && ponto.X + atalho.ActualWidth <= cabecalho.ActualWidth, "Atalho de pedidos cortado no cabeçalho");
            var captura = new RenderTargetBitmap((int)cabecalho.ActualWidth, (int)cabecalho.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            captura.Render(cabecalho);
            var codificador = new PngBitmapEncoder(); codificador.Frames.Add(BitmapFrame.Create(captura));
            using var arquivo = File.Create($"artifacts/ocupacao-agenda/cabecalho-{largura}.png"); codificador.Save(arquivo);
        }
        janela.Width = 1280;
        atalho.Command.Execute(null);
        await Esperar(() => Task.FromResult(shell.TituloTela == "Pedidos da enfermagem"), "Atalho do cabeçalho não abriu a caixa");
        Conferir((await svc.DetalheAsync(recepcao.Id, pedido)).Pedido.Situacao == "Novo", "Atalho marcou pedido sem abrir o detalhe");
        Conferir(NavegacaoSuite.Ir(ModuloRecepcao.ChavePainel), "Não voltou à abertura");
        shell.AbrirAvisosCommand.Execute(null); await Task.Delay(200);
        Conferir((await svc.DetalheAsync(recepcao.Id, pedido)).Pedido.Situacao == "Novo", "Abrir sino marcou aviso como lido");
        shell.AvisosAbertos = false;
        var web = await AbrirWeb(janela, ChavesSuite.OcupacaoAgenda);
        await Esperar(web, "document.querySelectorAll('.oa-dia').length===7 && document.querySelector('.oa-indicadores article strong')?.textContent==='30'");
        await Capturar(web, "semana");
        foreach (var largura in new[] { 620, 900, 1100 })
        {
            // O conteúdo é exercitado também abaixo da largura mínima da janela principal.
            janela.MinWidth = 0; janela.Width = largura; janela.UpdateLayout(); await Task.Delay(200);
            await Esperar(web, "document.documentElement.scrollWidth<=innerWidth+1");
            await Capturar(web, "semana-" + largura);
        }
        janela.Width = 1280;
        await Clicar(web, "Mês");
        await Esperar(web, "document.querySelectorAll('.oa-dia').length>=28"); await Capturar(web, "mes");
        await Clicar(web, "Dia"); await Esperar(web, "!!document.querySelector('.oa-lista-dia')"); await Capturar(web, "dia");
        // Clique contextual da semana deve selecionar o dia, sem depender da data anterior.
        await Clicar(web, "Semana");
        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.oa-dia').click()");
        await Esperar(web, $"document.querySelector('.oa-data input')?.value==='{segunda:yyyy-MM-dd}' && !!document.querySelector('.oa-lista-dia')");
        await Clicar(web, "Hoje");
        // Filtrar no host; depois a grade deve abrir com o mesmo dia e profissional.
        await web.CoreWebView2.ExecuteScriptAsync($"(()=>{{let s=document.querySelector('.oa-profissional select');s.value='{medicos[0].Id}';s.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
        await Esperar(web, "document.querySelectorAll('.oa-profissional-linha').length===1 && document.querySelector('main').getAttribute('aria-busy')==='false'");
        await Clicar(web, "+ Agendar atendimento");
        await Esperar(() => Task.FromResult(Encontrar<NovoAtendimentoView>(janela)?.DataContext is NovoAtendimentoViewModel novo
            && novo.Data.Date == DateTime.Today && novo.Profissional?.Id == medicos[0].Id), "Agendar perdeu dia ou profissional");
        Conferir(provedor.GetRequiredService<PreenchimentoNovoAtendimento>().Espiar(true) is null, "Agendar deixou pedido pendente");
        web = await AbrirWeb(janela, ChavesSuite.OcupacaoAgenda);
        await web.CoreWebView2.ExecuteScriptAsync($"(()=>{{let s=document.querySelector('.oa-profissional select');s.value='{medicos[0].Id}';s.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
        await Esperar(web, "document.querySelectorAll('.oa-profissional-linha').length===1 && document.querySelector('main').getAttribute('aria-busy')==='false'");
        await Esperar(web, "!document.body.textContent.toLocaleLowerCase().includes('jornada') && !document.body.textContent.includes('Abrir grade')");
        Conferir(shell.Grupos.SelectMany(g => g.Itens).All(i => i.Abas.All(a => a.Chave != ChavesSuite.AgendaRecepcao)), "A aba Grade continua no menu");
        provedor.GetRequiredService<PedidoAgenda>().AbrirComFiltro(DateOnly.FromDateTime(DateTime.Today.AddDays(2)), medicos[0].Id);
        Conferir(NavegacaoSuite.Ir(ChavesSuite.AgendaRecepcao), "Atalho antigo ficou sem destino");
        await Esperar(web, $"document.querySelector('.oa-data input')?.value==='{DateTime.Today.AddDays(2):yyyy-MM-dd}' && document.querySelector('.oa-profissional select')?.value==='{medicos[0].Id}' && !!document.querySelector('.oa-lista-dia')");
        Conferir(provedor.GetRequiredService<PedidoAgenda>().Consumir() is null, "Data do atalho não foi consumida");
        web = await AbrirWeb(janela, ChavesSuite.PedidosEnfermagem);
        await Esperar(web, "document.querySelectorAll('.pedidos-lista li').length===1"); await Capturar(web, "pedidos");
        await Clicar(web, "Ver pedido"); await Esperar(web, "document.querySelector('.pedido-status')?.textContent==='Visto pela recepção'");
        await Esperar(() => Task.FromResult(shell.PedidosNovos == 0 && shell.ResumoPedidos.Contains("1 em aberto")), "Contador global não refletiu leitura sem atualização manual");
        await Clicar(web, "Ver este dia na agenda"); await Task.Delay(700);
        web = Navegador(janela)!;
        await Esperar(web, $"document.querySelector('.oa-data input')?.value==='{inicio:yyyy-MM-dd}' && document.querySelector('.oa-profissional select')?.value==='' && !!document.querySelector('.oa-lista-dia')");
        web = await AbrirWeb(janela, ChavesSuite.OcupacaoAgenda);
        recepcao.Ativo = false; await db.SaveChangesAsync();
        await Clicar(web, "Atualizar"); await Esperar(web, "!!document.querySelector('.oa-erro') && !document.querySelector('.oa-indicadores')");
        await shell.AtualizarPedidosGlobaisAsync(); Conferir(shell.ErroPedidos, "Falha global sumiu como zero avisos");
        recepcao.Ativo = true; await db.SaveChangesAsync();
        await Clicar(web, "Tentar novamente"); await Esperar(web, "!!document.querySelector('.oa-indicadores')");
        await shell.AtualizarPedidosGlobaisAsync(); Conferir(!shell.ErroPedidos, "Falha global não recuperou");
        // Voltar pelo menu mantém a interface e o filtro escolhido. A conferência de
        // jornada de outro profissional precisa solicitar explicitamente a lista completa.
        await Esperar(web, "document.querySelector('.oa-profissional select')?.value===''");
        await web.CoreWebView2.ExecuteScriptAsync("(()=>{let s=document.querySelector('.oa-profissional select');s.value='';s.dispatchEvent(new Event('change',{bubbles:true}));})()");
        await Esperar(web, "document.querySelectorAll('.oa-profissional-linha').length===3 && document.querySelector('main').getAttribute('aria-busy')==='false'");
        medicos[1].AtendeAte = null; await db.SaveChangesAsync();
        await Clicar(web, "Atualizar");
        await Esperar(web, "!document.body.textContent.toLocaleLowerCase().includes('jornada') && document.querySelectorAll('.oa-indicadores article').length===4");
        await Capturar(web, "visao-geral-sem-jornada");
        await web.CoreWebView2.ExecuteScriptAsync("(()=>{let s=document.querySelector('.oa-data input');let set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;set.call(s,'2027-02-01');s.dispatchEvent(new Event('change',{bubbles:true}));s.dispatchEvent(new Event('input',{bubbles:true}));})()");
        await Esperar(web, "document.querySelector('.oa-data input')?.value==='2027-02-01' && document.querySelector('.oa-indicadores article strong')?.textContent==='0' && document.querySelector('main').getAttribute('aria-busy')==='false'");
        await Clicar(web, "Dia"); await Esperar(web, "!!document.querySelector('.oa-lista-dia .oa-vazio') && !document.querySelector('.oa-erro')");
        janela.Close(); sessao.Entrar(tecnica);
        var restrito = new ShellViewModel("Enfermagem", [modulo], provedor);
        Conferir(!restrito.PodeVerPedidos && !restrito.Itens.Any(i => i.Chave == ChavesSuite.PedidosEnfermagem), "Enfermagem ganhou caixa da recepção");
        Conferir(!sessao.Pode(Permissao.EditarAgenda), "Enfermagem ganhou permissão para agendar");
    }
    private static T? Encontrar<T>(DependencyObject raiz) where T : DependencyObject
    {
        if (raiz is T t) return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++) if (Encontrar<T>(VisualTreeHelper.GetChild(raiz, i)) is { } filho) return filho;
        return null;
    }
    // Apenas a abertura neutra é substituída. Agenda, navegação, notificações e serviços são reais.
    private sealed class ModuloDeTeste : IModuloApp
    {
        private readonly ModuloRecepcao real = new();
        public string Nome => real.Nome;
        public IReadOnlyList<ItemMenuModulo> Itens => MedirAbertura
            ? real.Itens.Concat(Enumerable.Range(0, 10).Select(i => new ItemMenuModulo
                { Chave = "qa-descarte-" + i, Rotulo = "Página sintética " + i, Glifo = "", Grupo = GrupoSidebar.Gestao })).ToArray()
            : real.Itens;
        public void Registrar(IServiceCollection s) => real.Registrar(s);
        public object? CriarTela(string chave, IServiceProvider s) => chave == ModuloRecepcao.ChavePainel || chave.StartsWith("qa-descarte-", StringComparison.Ordinal) ? new TextBlock { Text = "Início · dados de demonstração", Margin = new Thickness(30) } : real.CriarTela(chave, s);
    }
    private sealed class Dialogos : IDialogoService
    {
        public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null, bool obrigatorio = true) => null;
        public bool Confirmar(string titulo, string mensagem) => false;
        public bool ConfirmarPerigo(string titulo, string mensagem) => false;
        public void Aviso(string titulo, string mensagem) { }
    }
}
