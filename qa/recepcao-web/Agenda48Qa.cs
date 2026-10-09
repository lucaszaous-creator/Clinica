using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.Web;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Recepcao.Modulo;
using Clinica.Recepcao.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

/// <summary>Dia sintético com 48 pacientes distintos. Nenhuma conexão ou mensagem real.</summary>
static class Agenda48Qa
{
    static void Exigir(bool condicao, string mensagem) { if (!condicao) throw new Exception(mensagem); }
    static async Task Esperar(WebView2 browser, string js, string descricao)
    {
        for (var n = 0; n < 180; n++)
        {
            if (await browser.CoreWebView2.ExecuteScriptAsync(js) == "true") return;
            await Task.Delay(50);
        }
        throw new Exception(descricao + ": " + await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText.slice(0,3500)"));
    }

    public static async Task Executar()
    {
        using var con = new SqliteConnection("Data Source=:memory:"); await con.OpenAsync();
        var op = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;
        var sc = new ServiceCollection();
        sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        sc.AddScoped(_ => new ClinicaDbContext(op)); sc.AddSingleton<SessaoUsuario>();
        sc.AddSingleton<SnackbarService>(); sc.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>());
        sc.AddSingleton<IDialogoService, DialogosNativosProibidos>();
        var recepcao = new ModuloRecepcao(); var clinico = new ModuloClinico(); recepcao.Registrar(sc); clinico.Registrar(sc);
        using var sp = sc.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
            var secretária = new UsuarioSistema { Nome="Secretária QA48", Login="secretaria.qa48", Perfil=PerfilAcesso.Recepcao };
            var profissionais = new[] { new Profissional { Nome="Profissional QA48 A" }, new Profissional { Nome="Profissional QA48 B" } };
            db.Add(secretária); db.AddRange(profissionais);
            var pacientes = Enumerable.Range(1,48).Select(n=>new Paciente { Nome=$"Paciente fictício QA48 {n:00}", Documento=$"QA48-{n:00}", Convenio=Convenio.UnimedIntercambio }).ToArray();
            db.AddRange(pacientes); await db.SaveChangesAsync();
            for (var n=0; n<48; n++)
            {
                var inicio=DateTime.Today.AddHours(8).AddMinutes(n/2*15);
                db.Add(new Agendamento { PacienteId=pacientes[n].Id, ProfissionalId=profissionais[n%2].Id,
                    DataHora=inicio, DuracaoMinutos=15, ModalidadePrevista=ModalidadeAtendimento.AcupunturaSimples,
                    Status=n>=44?StatusAgendamento.Faltou:n>=40?StatusAgendamento.Cancelado:n>=32?StatusAgendamento.Realizado:StatusAgendamento.Agendado,
                    ChegadaEm=n>=8&&n<40?inicio.AddMinutes(-5):null,
                    InicioAtendimentoEm=n>=16&&n<40?inicio:null, FimAtendimentoEm=n>=24&&n<40?inicio.AddMinutes(10):null });
            }
            await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(secretária);
            await scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync();
            Exigir(await db.Pacientes.CountAsync()==48 && await db.Agendamentos.CountAsync()==48,"Cenário deve ter exatamente 48 pacientes e horários");
        }
        var defs=RecepcaoWebRegistro.CriarPaginas().Concat(ClinicoWebRegistro.CriarPaginas()).DistinctBy(p=>p.Chave).ToArray();
        var dialogos=RecepcaoWebRegistro.CriarDialogos().Concat(ClinicoWebRegistro.CriarDialogos()).Concat(RegistroCompartilhadoWeb.Dialogos()).DistinctBy(d=>(d.Chave,d.Tipo)).ToArray();
        var menu=recepcao.Itens.Concat(clinico.Itens).DistinctBy(i=>i.Chave).ToArray();
        var pasta=Path.GetFullPath("artifacts/agenda48"); Directory.CreateDirectory(pasta);
        using var view=new SuiteWebView(sp,defs,dialogos,menu,"Agenda — 48 pacientes fictícios","fila");
        var janela=new Window { Content=view,Width=1366,Height=768,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual }; janela.Show();
        try
        {
            await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45)); var browser=(WebView2)view.Content;
            const string linhas=".recepcao-linha[data-linha-id]";
            string Linha(int n)=>$"[...document.querySelectorAll('{linhas}')].find(l=>l.querySelector('h4').textContent.includes('QA48 {n:00}'))";
            async Task Clicar(string expressao)=>await AcoesVisiveisQa.ClicarExpressao(browser,expressao);
            async Task Filtro(string nome,int quantidade)
            {
                await Clicar($"document.querySelector('[data-agenda-situacao=\"{nome}\"]')");
                await Esperar(browser,$"document.querySelectorAll('{linhas}').length==={quantidade}","Filtro "+nome);
            }
            await Esperar(browser,$"document.querySelectorAll('{linhas}').length===48","48 horários visíveis à secretária");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('[data-tabela-container=Profissionais] [data-coluna=Quantidade]')].map(e=>parseInt(e.textContent)).join(',')==='48,24,24'")=="true","Filtro profissional deve contar também faltas e cancelados exibidos");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('.recepcao-resumo-dia .campo-web')].find(e=>e.querySelector('label').textContent==='Em Sala')?.querySelector('output').textContent==='8'")=="true","Em sala contou atendimento com fim já registrado");
            foreach(var largura in new[]{1366,900})
            {
                janela.Width=largura; await Task.Delay(200);
                await Filtro("A atender",16); await Filtro("Em atendimento",8); await Filtro("Conclusão pendente",8);
                await Filtro("Concluídos",8); await Filtro("Cancelados",4); await Filtro("Faltas",4); await Filtro("Todos",48);
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1")=="true","Página vazou no notebook");
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"['Avancar','MarcarFalta','Cancelar'].every(c=>{{const b={Linha(1)}.querySelector('[data-comando=\"'+c+'\"]');return b&&!b.disabled&&!b.closest('[popover]')}})")=="true","Chegada/falta/cancelar devem ser diretos e autorizados");
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"!{Linha(33)}.querySelector('[data-comando=MarcarFalta]')&&!{Linha(33)}.querySelector('[data-comando=Cancelar]')")=="true","Concluído expôs ação incompatível");
                if(largura==900) Console.WriteLine(await browser.CoreWebView2.ExecuteScriptAsync($"JSON.stringify([...{Linha(1)}.querySelectorAll('.recepcao-linha-acoes>button,.recepcao-linha-acoes>.acoes-menu-grupo>button')].map(b=>({{texto:b.textContent,r:b.getBoundingClientRect().toJSON()}})))"));
                if(largura==900) Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const botoes=[...{Linha(1)}.querySelectorAll('.recepcao-linha-acoes>button,.recepcao-linha-acoes>.acoes-menu-grupo>button')];return botoes.length===4&&Math.max(...botoes.map(b=>(b.getBoundingClientRect().top+b.getBoundingClientRect().height/2)))-Math.min(...botoes.map(b=>(b.getBoundingClientRect().top+b.getBoundingClientRect().height/2)))<3}})()")=="true","Ações devem ficar lado a lado no notebook");
                await Task.Delay(200);
                using var foto=File.Create(Path.Combine(pasta,$"fila-48-{largura}.png")); await view.CapturarPreviewAsync(foto);
            }
            await Clicar($"{Linha(1)}.querySelector('[data-comando=Avancar]')");
            await Esperar(browser,$"{Linha(1)}?.innerText.includes('No local')===true","Chegada atualizada");
            async Task Excecao(int paciente,string comando,StatusAgendamento estado)
            {
                await Clicar($"{Linha(paciente)}.querySelector('[data-comando={comando}]')");
                await Esperar(browser,"!!document.querySelector('.dialogo-web [data-comando=confirmar]')","Confirmação de "+comando);
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"document.querySelector('.dialogo-web').innerText.includes('QA48 {paciente:00}')")=="true","Confirmação omitiu paciente");
                await Clicar("document.querySelector('.dialogo-web [data-comando=fechar]')");
                await Esperar(browser,"!document.querySelector('.dialogo-web')","Cancelar confirmação");
                using(var scope=sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.AnyAsync(a=>a.Paciente!.Nome==$"Paciente fictício QA48 {paciente:00}"&&a.Status==StatusAgendamento.Agendado),"Cancelar diálogo alterou horário");
                await Clicar($"{Linha(paciente)}.querySelector('[data-comando={comando}]')");
                await Esperar(browser,"!!document.querySelector('.dialogo-web [data-comando=confirmar]')","Nova confirmação");
                await Clicar("document.querySelector('.dialogo-web [data-comando=confirmar]')");
                await Esperar(browser,$"!document.querySelector('.dialogo-web')&&!{Linha(paciente)}?.querySelector('[data-comando={comando}]')","Estado aplicado sem ação duplicada");
                using(var scope=sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.AnyAsync(a=>a.Paciente!.Nome==$"Paciente fictício QA48 {paciente:00}"&&a.Status==estado),"Estado não persistiu: "+estado);
            }
            await Excecao(2,"MarcarFalta",StatusAgendamento.Faltou); await Excecao(3,"Cancelar",StatusAgendamento.Cancelado);
            await Filtro("Faltas",5); await Filtro("Cancelados",5); await Filtro("A atender",14); await Filtro("Todos",48);
            // Reentrada confirma leitura persistida, não apenas a atualização otimista do navegador.
            await view.NavegarAsync("agenda-recepcao"); await Task.Delay(300); await view.NavegarAsync("fila");
            await Esperar(browser,$"document.querySelectorAll('{linhas}').length===48&&{Linha(1)}?.innerText.includes('No local')===true","Reentrada preserva 48 horários e chegada");
            using(var scope=sp.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                Exigir(await db.Pacientes.CountAsync()==48&&await db.Agendamentos.CountAsync()==48,"Ações duplicaram pacientes/horários");
                Exigir(await db.Agendamentos.CountAsync(a=>a.ChegadaEm!=null)==33,"Chegada alterou outros registros");
            }
            await view.NavegarAsync(ModuloClinico.ChaveMeuDia);
            await Esperar(browser,"document.querySelector('[data-agenda-situacao=Todos] .agenda-filtro-contagem')?.textContent==='48'","Meu dia deve refletir os mesmos 48 horários");
            foreach(var grupo in new[]{("Concluídos",8),("Conclusão pendente",8),("Faltas",5),("Cancelados",5)})
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"document.querySelector('[data-agenda-situacao=\"{grupo.Item1}\"] .agenda-filtro-contagem')?.textContent==='{grupo.Item2}'")=="true","Meu dia diverge da recepção: "+grupo.Item1);
            using(var foto=File.Create(Path.Combine(pasta,"meu-dia-48.png")))await view.CapturarPreviewAsync(foto);
            Console.WriteLine("OK QA48 WebView2 + SQLite: 48 pacientes/48 horários; secretaria, 7 filtros em1366/900px, chegada, falta/cancelar com desistência sem gravação, persistência e paridade Meu dia.");
        }
        finally { janela.Close(); }
    }
}
