using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.Views;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    private static readonly string Saida = Path.GetFullPath("artifacts/prescricoes");
    [STAThread] private static void Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Clinica.Desktop.Shell;component/Styles/Suite.xaml") });
        app.Startup += async (_, _) =>
        {
            try { Directory.CreateDirectory(Saida); await Executar(); Console.WriteLine("APROVADO: prescrições reais no WebView2 com SQLite sintético."); }
            catch (Exception e) { Console.WriteLine(e); Environment.ExitCode = 1; }
            finally { app.Shutdown(); }
        };
        app.Run();
    }
    private static async Task Executar()
    {
        using var conexao = new SqliteConnection("Data Source=:memory:"); await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options;
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        servicos.AddScoped(_ => new ClinicaDbContext(opcoes)); servicos.AddSingleton<SessaoUsuario>();
        using var sp = servicos.BuildServiceProvider(); using var escopo = sp.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
        var ana = new Paciente { Nome = "Ana Beatriz Martins", Documento = "12345678909", DataNascimento = new DateOnly(1986, 4, 18), Convenio = Convenio.UnimedIntercambio };
        var bruno = new Paciente { Nome = "Bruno Almeida Costa", Documento = "52998224725", Convenio = Convenio.UnimedIntercambio };
        var medico = new Profissional { Nome = "Dra. Helena Duarte — demonstração", RegistroConselho = "CRM-RJ 123456", Ativo = true };
        db.AddRange(ana, bruno, medico); await db.SaveChangesAsync();
        var usuario = new UsuarioSistema { Nome = "QA sintético", Login = "qa.prescricoes", Perfil = PerfilAcesso.Gerente, ProfissionalId = medico.Id, Profissional = medico };
        db.Add(usuario); await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
        var tipos = new[] { TipoDocumentoClinico.Receita, TipoDocumentoClinico.Atestado, TipoDocumentoClinico.Comparecimento, TipoDocumentoClinico.PedidoExame };
        for (var i = 0; i < 12; i++) db.Add(new DocumentoClinico { PacienteId = ana.Id, ProfissionalId = medico.Id, Tipo = tipos[i % 4], Numero = $"2026/{174-i:0000}", CodigoVerificacao = $"DEMO-{i:0000}", Data = new DateOnly(2026,10,9).AddDays(-i), CanceladoEm = i == 3 ? DateTime.Now : null });
        db.Add(new DocumentoClinico { PacienteId = bruno.Id, ProfissionalId = medico.Id, Tipo = TipoDocumentoClinico.Atestado, Numero = "2026/0200", CodigoVerificacao = "DEMO-BRUNO", Data = new DateOnly(2026,10,9) });
        for (var i = 0; i < 5; i++) db.Add(new PrescricaoInterna { PacienteId = ana.Id, ProfissionalId = medico.Id, Numero = $"2026/{85-i:0000}", CodigoVerificacao = $"INF-DEMO-{i}", Data = new DateOnly(2026,10,9).AddDays(-i), Hora = new TimeOnly(9,30), Situacao = (SituacaoPrescricao)i });
        await db.SaveChangesAsync();
        var foco = new PacienteEmFoco(); var snackbar = new SnackbarService(); var dialogos = new Dialogos(); var factory = sp.GetRequiredService<IServiceScopeFactory>();
        await TestarRespostaAtrasada(factory,ana,bruno);
        var vm = new PrescricoesClinicasViewModel(factory,foco,snackbar,dialogos);
        var view = new PrescricoesClinicasView { DataContext = vm };
        var janela = new Window { Title = "QA — dados fictícios", Content = view, Width = 1420, Height = 950, Left = -30000, Top = -30000, ShowActivated = false, ShowInTaskbar = false };
        janela.Show();
        try
        {
            var web = Navegador(janela);
            await Esperar(web,"!!document.querySelector('.rx-escolha')");
            if (!vm.Seletor.Ocioso || vm.Seletor.Resultados.Count != 0) throw new Exception("Abertura consultou pacientes sem solicitação.");
            await Capturar(web,"01-selecionar-paciente.png");
            await Campo(web,".rx-escolha input","Ana Beatriz"); await Esperar(web,"document.querySelectorAll('.rx-resultado').length===1");
            await Capturar(web,"02-buscar-paciente.png");
            await Script(web,"document.querySelector('.rx-resultado').click()");
            await Esperar(web,"document.querySelectorAll('.rx-linha').length===12");
            if (vm.PacienteAtualId != ana.Id || foco.PacienteId != ana.Id) throw new Exception("Seleção não chegou ao contexto clínico.");
            await Capturar(web,"03-documentos-1420.png");
            await Campo(web,".rx-filtros input","Atestado"); await Esperar(web,"document.querySelectorAll('.rx-linha').length===3");
            await Clicar(web,"Limpar filtros"); await Esperar(web,"document.querySelectorAll('.rx-linha').length===12");
            await Script(web,"document.querySelector('.rx-menu summary').click()");
            await Esperar(web,"document.querySelector('.rx-menu[open]')!==null");
            await Capturar(web,"04-acoes-documento.png");
            await Script(web,"document.querySelector('.rx-menu[open]').open=false");
            foreach(var largura in new[]{1100,900,650})
            {
                janela.Width=largura; janela.Height=760; await Task.Delay(250);
                await Esperar(web,"document.documentElement.scrollWidth<=innerWidth+1");
                await Script(web,"scrollTo(0,0)"); await Capturar(web,$"documentos-{largura}.png");
            }
            janela.Width=1420; janela.Height=950;
            await Clicar(web,"Trocar paciente"); await Esperar(web,"!!document.querySelector('.rx-escolha')");
            await Campo(web,".rx-escolha input","Bruno"); await Esperar(web,"document.querySelectorAll('.rx-resultado').length===1");
            await Clicar(web,"Manter paciente"); await Esperar(web,"document.querySelectorAll('.rx-linha').length===12");
            if (foco.PacienteId != ana.Id) throw new Exception("Cancelar troca alterou paciente.");
            await Clicar(web,"Trocar paciente"); await Esperar(web,"!!document.querySelector('.rx-escolha')");
            await Campo(web,".rx-escolha input","Bruno"); await Esperar(web,"document.querySelectorAll('.rx-resultado').length===1");
            await Script(web,"document.querySelector('.rx-resultado').click()");
            await Esperar(web,"document.querySelectorAll('.rx-linha').length===1 && document.querySelector('.rx-paciente h2')?.textContent==='Bruno Almeida Costa'");
            if (vm.Documentos.Single().PacienteId != bruno.Id) throw new Exception("Histórico pertence ao paciente anterior.");
            Console.WriteLine("OK: busca real → seleção → filtro → menu → cancelar troca → trocar paciente e histórico correto; 1420/1100/900/650 px sem vazamento.");
            await Clicar(web,"Trocar paciente"); await Esperar(web,"!!document.querySelector('.rx-escolha')");
            await Campo(web,".rx-escolha input","nome inexistente"); await Esperar(web,"document.querySelector('.rx-busca-status')?.textContent.includes('Nenhum paciente')===true");
            await Clicar(web,"Todos os pacientes"); await Esperar(web,"document.querySelectorAll('.rx-resultado').length===2 && document.querySelector('.rx-escolha input').value===''");
            await Script(web,"document.querySelector('.rx-resultado').click()"); await Esperar(web,"document.querySelectorAll('.rx-linha').length===12");
            // A mesma tela embutida na ficha não pode trocar o contexto por uma mensagem forjada.
            var adapter = new PrescricoesWebAdapter(vm);
            var contexto = vm.ContextoDaLista;
            await Rejeitar(adapter,new {acao="imprimir",contexto,id=vm.Documentos.Max(d=>d.DocumentoId)+100});
            await Rejeitar(adapter,new {acao="cancelar",contexto="outro-paciente",id=vm.Documentos[0].DocumentoId});
            vm.MostrarCabecalho=false; await Rejeitar(adapter,new {acao="trocar-paciente",contexto}); vm.MostrarCabecalho=true;
            await adapter.ExecutarAsync(JsonSerializer.SerializeToElement(new {acao="trocar-paciente",contexto}));
            vm.Seletor.Termo="Ana"; await vm.Seletor.BuscarAsync(imediato:true);
            var estado=JsonSerializer.SerializeToElement(adapter.ObterEstado()); var revisao=estado.GetProperty("busca").GetProperty("revisao").GetInt32();
            vm.Seletor.Termo="Bruno";
            await Rejeitar(adapter,new {acao="selecionar-paciente",contexto,id=ana.Id,termo="Bruno",revisao});
            await vm.Seletor.BuscarAsync(imediato:true);
            await adapter.ExecutarAsync(JsonSerializer.SerializeToElement(new {acao="voltar-paciente",contexto}));
            Console.WriteLine("OK: IDs/contextos obsoletos, troca embutida e resultado antigo durante debounce rejeitados.");
            // Uma falha real do serviço nunca é anunciada como histórico vazio.
            var tabela = db.Model.FindEntityType(typeof(DocumentoClinico))!.GetTableName()!;
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tabela}\" RENAME TO \"QA_DocumentosIndisponiveis\"");
            await vm.CarregarCommand.ExecuteAsync(null);
            await Esperar(web,"document.querySelector('.rx-vazio h3')?.textContent==='Não foi possível carregar o histórico'");
            await Capturar(web,"05-erro-historico.png");
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"QA_DocumentosIndisponiveis\" RENAME TO \"{tabela}\"");
            await Clicar(web,"Atualizar"); await Esperar(web,"document.querySelectorAll('.rx-linha').length===12");
            Console.WriteLine("OK: falha real de leitura, mensagem de erro e recuperação por Atualizar.");

            // Troca de view exercita descarte do primeiro host e montagem do segundo.
            var inf = new PrescricaoInfusaoViewModel(factory,foco,snackbar,dialogos);
            janela.Content=new PrescricaoInfusaoView { DataContext=inf };
            await Task.Delay(200); janela.UpdateLayout();
            web=Navegador(janela); await Esperar(web,"document.querySelectorAll('.rx-linha').length===5");
            await Script(web,"scrollTo(0,0)"); await Capturar(web,"06-infusoes-1420.png");
            await Clicar(web,"Trocar paciente"); await Esperar(web,"!!document.querySelector('.rx-escolha')");
            await Campo(web,".rx-escolha input","Bruno"); await Esperar(web,"document.querySelectorAll('.rx-resultado').length===1");
            await Script(web,"document.querySelector('.rx-resultado').click()");
            await Esperar(web,"document.querySelector('.rx-vazio h3')?.textContent==='Nenhuma infusão prescrita'");
            if(inf.PacienteAtualId!=bruno.Id)throw new Exception("Infusão não trocou o paciente.");
            await Capturar(web,"07-infusao-vazia.png");
            await TestarNovaInfusao(web,janela,bruno.Id);
            var ai=new PrescricoesWebAdapter(inf);
            sp.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id=9999, Nome="QA sem permissão", Login="qa.negado", Perfil=PerfilAcesso.Enfermagem });
            var negou=false;try { ai.ExigirAcesso(); } catch(InvalidOperationException) { negou=true; }
            if(!negou)throw new Exception("Acesso sem Prescrever aceito.");
            sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            Console.WriteLine("OK: infusão com histórico/estados, troca, vazio, editor real no paciente correto e acesso negado.");
        }
        finally { janela.Close(); }
    }
    private static async Task TestarNovaInfusao(WebView2 web,Window principal,int pacienteId)
    {
        var concluiu=new TaskCompletionSource();
        var observar=System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(async()=>
        {
            try
            {
                Window? editor=null;
                for(var i=0;i<100&&editor is null;i++){await Task.Delay(100);editor=System.Windows.Application.Current.Windows.Cast<Window>().FirstOrDefault(w=>w!=principal&&w.DataContext is PrescricaoInternaEdicaoViewModel);}
                if(editor is null)throw new Exception("Nova prescrição não abriu o editor.");
                editor.Left=-30000;editor.Top=-30000;
                var vm=(PrescricaoInternaEdicaoViewModel)editor.DataContext;
                await vm.Inicializacao;
                var navegador=Navegador(editor);await Esperar(navegador,"!!document.querySelector('.infusao-pagina')");
                if(!await Script(navegador,"document.body.textContent.includes('Bruno Almeida Costa')").ContinueWith(t=>t.Result=="true"))throw new Exception("Editor abriu com outro paciente.");
                editor.Close();concluiu.SetResult();
            }
            catch(Exception e){concluiu.SetException(e);foreach(var w in System.Windows.Application.Current.Windows.Cast<Window>().Where(w=>w!=principal).ToArray())w.Close();}
        });
        await Clicar(web,"Nova prescrição");await concluiu.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }
    private static async Task TestarRespostaAtrasada(IServiceScopeFactory factory,Paciente ana,Paciente bruno)
    {
        var antiga=new TaskCompletionSource<IReadOnlyList<Paciente>>();
        var seletor=new SeletorPacienteViewModel(factory){SemBuscaInicial=true,ConsultaPersonalizada=(termo,_)=>termo=="Ana"?antiga.Task:Task.FromResult<IReadOnlyList<Paciente>>([bruno])};
        seletor.Termo="Ana";var primeira=seletor.BuscarAsync(imediato:true);
        seletor.Termo="Bruno";await seletor.BuscarAsync(imediato:true);
        antiga.SetException(new InvalidOperationException("Falha da busca antiga"));await primeira;
        if(!seletor.ResultadoAtual||seletor.TemErro||seletor.Resultados.Single().Id!=bruno.Id)throw new Exception("Erro atrasado substituiu busca nova.");
        Console.WriteLine("OK: consulta antiga que falha depois da nova não altera resultados, erro ou disponibilidade.");
    }
    private static async Task Rejeitar(PrescricoesWebAdapter adapter,object comando)
    { try { await adapter.ExecutarAsync(JsonSerializer.SerializeToElement(comando)); } catch(InvalidOperationException) { return; } throw new Exception("Comando indevido aceito: "+JsonSerializer.Serialize(comando)); }
    private static Task<string> Script(WebView2 web,string js)=>web.CoreWebView2.ExecuteScriptAsync(js);
    private static async Task Esperar(WebView2 web,string expressao)
    { var limite=DateTime.UtcNow.AddSeconds(25);while(DateTime.UtcNow<limite){if(web.CoreWebView2 is not null&&await Script(web,expressao)=="true")return;await Task.Delay(100);}throw new Exception("WebView2 não confirmou: "+expressao); }
    private static async Task Clicar(WebView2 web,string texto)
    {var seletor="[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==="+JsonSerializer.Serialize(texto)+")";await Esperar(web,seletor+" && !"+seletor+".disabled");await Script(web,seletor+".click()");}
    private static Task Campo(WebView2 web,string seletor,string texto)=>Script(web,"(()=>{const e=document.querySelector("+JsonSerializer.Serialize(seletor)+");e.focus();Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,"+JsonSerializer.Serialize(texto)+");e.dispatchEvent(new Event('input',{bubbles:true}));})()");
    private static async Task Capturar(WebView2 web,string nome){using var arquivo=File.Create(Path.Combine(Saida,nome));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,arquivo);}
    private static WebView2 Navegador(DependencyObject raiz)
    {if(raiz is WebView2 w)return w;for(var i=0;i<VisualTreeHelper.GetChildrenCount(raiz);i++){try{return Navegador(VisualTreeHelper.GetChild(raiz,i));}catch(InvalidOperationException){}}throw new InvalidOperationException("WebView não montado.");}
    private sealed class Dialogos:IDialogoService
    {public bool Confirmar(string titulo,string mensagem)=>false;public bool ConfirmarPerigo(string titulo,string mensagem)=>false;public string? PerguntarTexto(string titulo,string pergunta,string? textoInicial=null,bool obrigatorio=true)=>null;public void Aviso(string titulo,string mensagem){} }
}
