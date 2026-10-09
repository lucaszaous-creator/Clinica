using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Domain;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

internal static class GerenteFluxos
{
    internal static async Task Executar(bool somenteBuscas=false,bool somenteContraste=false)
    {
        using var banco=new SqliteConnection("Data Source=:memory:");await banco.OpenAsync();
        var op=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(banco).Options;
        var sc=new ServiceCollection();sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        sc.AddScoped(_=>new ClinicaDbContext(op));sc.AddSingleton<SessaoUsuario>();sc.AddSingleton<PacienteEmFoco>();sc.AddSingleton<SnackbarService>();sc.AddSingleton<ISnackbarService>(sp=>sp.GetRequiredService<SnackbarService>());sc.AddSingleton<IDialogoService,DialogosProibidos>();
        var modulos=Program.Modulos();foreach(var m in modulos)m.Registrar(sc);using var sp=sc.BuildServiceProvider();
        using(var escopo=sp.CreateScope())
        {
            var db=escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();await db.Database.EnsureCreatedAsync();
            var usuario=new UsuarioSistema{Nome="Direção — demonstração",Login="gerente.qa",Perfil=PerfilAcesso.Gerente};
            var paciente=new Paciente{Nome="Paciente fictícia — teste visual",Convenio=Convenio.UnimedIntercambio,Sexo=Sexo.Feminino};db.AddRange(usuario,paciente);await db.SaveChangesAsync();
            sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);sp.GetRequiredService<PacienteEmFoco>().Definir(paciente.Id,paciente.Nome);
            await escopo.ServiceProvider.GetRequiredService<AtendimentoService>().LancarAsync(paciente.Id,DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),ModalidadeAtendimento.AcupunturaComEletro);
        }
        var pasta=Path.GetFullPath("artifacts/gerente-web");Directory.CreateDirectory(pasta);
        var janela=new SuiteWebWindow(sp,modulos,"Gerente Geral — demonstração"){Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};janela.Show();
        try
        {
            var view=janela.WebView;await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));var browser=(WebView2)view.Content;

            if(somenteContraste){await ContrasteTodosQa.Executar(sp);return;}

            async Task EsperarBusca(string expressao)
            {
                for(var n=0;n<180;n++){if(await browser.CoreWebView2.ExecuteScriptAsync(expressao)=="true")return;await Task.Delay(50);}
                throw new Exception("Gerente: busca não atingiu o estado esperado: "+expressao);
            }
            async Task DigitarPaciente(string prefixo)
            {
                await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('"+prefixo+"input[data-campo=\"Seletor.Termo\"]');e.focus();e.value='Paciente fictícia';e.dispatchEvent(new Event('input',{bubbles:true}));})()");
                await EsperarBusca("!!document.querySelector('"+prefixo+"[data-sugestao-paciente]:not(:disabled)')");
                await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('"+prefixo+"[data-sugestao-paciente]').scrollIntoView({block:'center',behavior:'instant'})");
            }
            janela.Width=1100;janela.Height=720;
            await view.NavegarAsync("marcar-horario");
            await EsperarBusca("!!document.querySelector('.busca-paciente-unificada')");
            await DigitarPaciente("");
            using(var arquivo=File.Create(Path.Combine(pasta,"gerente-agendamento-busca.png")))await view.CapturarPreviewAsync(arquivo);
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-sugestao-paciente]').click()");
            await EsperarBusca("document.querySelector('.paciente-confirmado')?.textContent.includes('Paciente fictícia')===true");
            await view.NavegarAsync(Clinica.Clinico.Modulo.ModuloClinico.ChaveProntuarios);
            await EsperarBusca("!!document.querySelector('[data-comando=\"NovoProntuario\"]')");
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-comando=\"NovoProntuario\"]').click()");
            await EsperarBusca("!!document.querySelector('.dialogo-web input[data-campo=\"Seletor.Termo\"]')");
            await DigitarPaciente(".dialogo-web ");
            using(var arquivo=File.Create(Path.Combine(pasta,"gerente-dialogo-busca.png")))await view.CapturarPreviewAsync(arquivo);
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web [data-sugestao-paciente]').click()");
            await EsperarBusca("document.querySelector('.dialogo-web .paciente-confirmado')?.textContent.includes('Paciente fictícia')===true");
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web [data-fechar-dialogo]').click()");
            await EsperarBusca("!document.querySelector('.dialogo-web')");
            using(var escopo=sp.CreateScope())
                if(await escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.AnyAsync())throw new Exception("Gerente: buscar e cancelar criou agendamento");
            Console.WriteLine("OK Gerente busca real: mesma SuiteWebWindow do executável completo; agendamento com busca e seleção; Novo prontuário com busca, seleção e cancelamento sem gravar.");
            if(somenteBuscas)return;
            foreach(var tamanho in new[]{(1440,900),(1100,720),(900,600)})
            {
                janela.Width=tamanho.Item1;janela.Height=tamanho.Item2;await Task.Delay(150);
                foreach(var p in Clinica.Gerente.Web.GerenteWebRegistro.CriarPaginas().Concat(Clinica.Financeiro.Web.FinanceiroSuiteRegistro.CriarPaginas()))
                {
                    await view.NavegarAsync(p.Chave);await Task.Delay(650);
                    var resultado=await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({overflow:document.documentElement.scrollWidth>innerWidth+1,ferramentas:[...document.querySelectorAll('.topbar .busca-global')].every(b=>{const r=b.getBoundingClientRect();return r.width>0&&b.contains(document.elementFromPoint(r.x+r.width/2,r.y+r.height/2))}),topo:!!document.querySelector('.navegacao-topo'),erro:[...document.querySelectorAll('.erro')].map(e=>e.textContent).filter(Boolean),titulo:document.querySelector('h1')?.textContent})");
                    var json=JsonDocument.Parse(JsonSerializer.Deserialize<string>(resultado)!);var r=json.RootElement;
                    if(r.GetProperty("overflow").GetBoolean()||!r.GetProperty("topo").GetBoolean()||!r.GetProperty("ferramentas").GetBoolean()||r.GetProperty("erro").GetArrayLength()>0)throw new Exception(p.Chave+": "+resultado);
                    using var arquivo=File.Create(Path.Combine(pasta,p.Chave+"-"+tamanho.Item1+".png"));await view.CapturarPreviewAsync(arquivo);
                    Console.WriteLine("OK WebView2 Gerente "+p.Chave+" "+tamanho.Item1);
                }
            }
            await GerenteOperacoesUiQa.Executar(sp, view, browser);
        }
        finally{janela.Close();}
    }
    private sealed class DialogosProibidos:IDialogoService
    {
        public bool Confirmar(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
        public bool ConfirmarPerigo(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
        public void Aviso(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
        public string? PerguntarTexto(string t,string p,string? textoInicial=null,bool obrigatorio=true)=>throw new Exception("Diálogo nativo: "+t);
    }
}
