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
using Clinica.Recepcao.ViewModels;
using Clinica.Recepcao.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;

/// <summary>Reproduz a busca pelo DOM, sem chamar BuscarAsync nem setters C# no teste.</summary>
static class BuscasPacientesQa
{
    static void Exigir(bool ok, string texto) { if (!ok) throw new Exception(texto); }
    static async Task Esperar(WebView2 browser, string js, string descricao)
    {
        for (var n = 0; n < 180; n++)
        {
            if (await browser.CoreWebView2.ExecuteScriptAsync(js) == "true") return;
            await Task.Delay(50);
        }
        var estado = await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({erro:document.querySelector('.erro-ponte')?.textContent,campos:[...document.querySelectorAll('[data-campo]')].map(e=>[e.dataset.campo,e.value]),texto:document.body.innerText.slice(0,4000)})");
        throw new Exception("Tempo excedido: " + descricao + " " + estado);
    }
    public static async Task Executar()
    {
        using var con = new SqliteConnection("Data Source=:memory:"); await con.OpenAsync();
        var op = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;
        var sc = new ServiceCollection(); sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
        sc.AddScoped(_ => new ClinicaDbContext(op)); sc.AddSingleton<SessaoUsuario>(); sc.AddSingleton<SnackbarService>();
        sc.AddSingleton<ISnackbarService>(s => s.GetRequiredService<SnackbarService>()); sc.AddSingleton<IDialogoService,DialogosNativosProibidos>();
        var recepcao = new ModuloRecepcao(); var clinico = new ModuloClinico(); recepcao.Registrar(sc); clinico.Registrar(sc);
        using var sp = sc.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>(); await db.Database.EnsureCreatedAsync();
            var profissional = new Profissional { Nome = "Profissional sintético de busca" };
            var usuario = new UsuarioSistema { Nome = "Gestora sintética", Login = "buscas.qa", Perfil = PerfilAcesso.Gerente };
            db.Add(profissional);db.Add(usuario);
            // O alvo fica além das primeiras 50 linhas, como uma clínica com cadastro importado.
            db.AddRange(Enumerable.Range(1,230).Select(n => new Paciente { Nome = $"AAA sintético {n:000}", Documento = $"DEMO-{n:000}" }));
            db.AddRange(new Paciente { Nome = "Zuleica Sintética Busca", Documento = "52998224725", Telefone = "00000000000", Convenio = Convenio.UnimedIntercambio },
                new Paciente { Nome = "Zoraide Outra Sintética", Documento = "11144477735", Convenio = Convenio.UnimedIntercambio });
            await db.SaveChangesAsync(); sp.GetRequiredService<SessaoUsuario>().Entrar(usuario);
            await scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync();
            await scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync();
        }
        var defs = RecepcaoWebRegistro.CriarPaginas().Concat(ClinicoWebRegistro.CriarPaginas()).DistinctBy(p=>p.Chave).Select(p=>p.Chave!="documentos"?p:p with {
            Fabrica=servicos=> {
                var vm=(DocumentosViewModel)p.Fabrica!(servicos);
                vm.Seletor.ConsultaPersonalizada=async(termo,_)=>{
                    // Resposta antiga ignora cancelamento na consulta simulada; o seletor deve descartá-la.
                    await Task.Delay(termo?.StartsWith("Zuleica")==true?800:50);
                    using var escopo=servicos.CreateScope();
                    return await escopo.ServiceProvider.GetRequiredService<PacienteService>().BuscarAsync(termo,50);
                };
                return vm;
            }
        }).ToArray();
        var registros = RecepcaoWebRegistro.CriarDialogos().Concat(ClinicoWebRegistro.CriarDialogos()).Concat(RegistroCompartilhadoWeb.Dialogos()).DistinctBy(d=>(d.Chave,d.Tipo)).ToArray();
        var menu = recepcao.Itens.Concat(clinico.Itens).DistinctBy(i=>i.Chave).ToArray();
        var pasta = Path.GetFullPath("artifacts/recepcao-buscas"); Directory.CreateDirectory(pasta);
        using var view = new SuiteWebView(sp, defs, registros, menu, "Buscas — demonstração sintética", "documentos");
        var janela = new Window { Content = view, Width = 1100, Height = 740, Left = -30000, Top = -30000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual }; janela.Show();
        try
        {
            await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45)); var browser = (WebView2)view.Content;
            async Task Digitar(string texto, bool enter=false)
            {
                foreach(var parcial in Enumerable.Range(1,texto.Length).Select(n=>texto[..n]))
                {
                    var valor = JsonSerializer.Serialize(parcial);
                    await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('input[data-campo=\"Seletor.Termo\"]');if(!e)throw Error('Busca ausente');e.focus();e.value={valor};e.dispatchEvent(new Event('input',{{bubbles:true}}));return true}})()");
                    await Task.Delay(25);
                }
                if(enter) await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('input[data-campo=\"Seletor.Termo\"]').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}))");
                await Esperar(browser,"[...document.querySelectorAll('select[data-campo=\"Seletor.Selecionado\"] option')].some(o=>o.textContent.includes('Zuleica'))", "resultado da busca " + texto);
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"document.querySelector('input[data-campo=\"Seletor.Termo\"]').value==={JsonSerializer.Serialize(texto)}") == "true", "Rerender apagou texto digitado");
            }
            async Task Selecionar(string nome)
            {
                await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('select[data-campo=\"Seletor.Selecionado\"]');const o=[...e.options].find(o=>o.textContent.includes({JsonSerializer.Serialize(nome)}));if(!o)throw Error('Paciente ausente');e.value=o.value;e.dispatchEvent(new Event('change',{{bubbles:true}}));return true}})()");
                await Task.Delay(800);
            }
            foreach(var rota in new[] { "documentos", "marcar-horario", "pacientes-recepcao", "prontuario", ModuloClinico.ChavePrescricoes, ModuloClinico.ChavePrescricaoInfusao })
            {
                await view.NavegarAsync(rota); await Task.Delay(600);
                var buscaAoDigitar = new[] { "documentos", "marcar-horario", "prontuario" }.Contains(rota);
                await Digitar(buscaAoDigitar ? "Zule" : "Zuleica", enter: !buscaAoDigitar);
                if (buscaAoDigitar) Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.activeElement?.dataset.campo===\"Seletor.Termo\"") == "true", "Busca exigiu sair do campo " + rota);
                if(rota=="documentos")
                {
                    await Esperar(browser,"document.querySelector('[data-tabela-container=\"Seletor.Resultados\"]')?.textContent.includes('Zuleica')===true", "nome visível em resultados Documentos");
                    await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"EscolherPaciente\"]')");
                    await Esperar(browser,"document.body.innerText.includes('Zuleica Sintética Busca')", "paciente do documento escolhido");
                }
                else await Selecionar("Zuleica");
                using(var imagem=File.Create(Path.Combine(pasta,rota+"-selecao.png"))) await view.CapturarPreviewAsync(imagem);
                Console.WriteLine("OK UI real: " + (buscaAoDigitar ? "nome parcial durante digitação, sem Enter/blur, identidade e seleção " : "digitação + Enter + seleção ") + rota);
            }
            await view.NavegarAsync("documentos"); await Task.Delay(500);
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"TrocarPaciente\"]')", opcional: true); await Task.Delay(300);
            await Digitar("529.982.247-25"); await Selecionar("Zuleica");
            Console.WriteLine("OK UI real: CPF formatado encontra alvo fora das primeiras 50 fichas.");
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"TrocarPaciente\"]')", opcional: true); await Task.Delay(300);
            await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('input[data-campo=\"Seletor.Termo\"]');e.value='Zuleica';e.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Task.Delay(650);
            await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('input[data-campo=\"Seletor.Termo\"]');e.value='Zoraide';e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Esperar(browser,"[...document.querySelectorAll('select[data-campo=\"Seletor.Selecionado\"] option')].some(o=>o.textContent.includes('Zoraide'))", "resposta mais recente da busca lenta");
            await Task.Delay(1100);
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('select[data-campo=\"Seletor.Selecionado\"]');return e&&[...e.options].some(o=>o.textContent.includes('Zoraide'))&&![...e.options].some(o=>o.textContent.includes('Zuleica'))})()") == "true", "Resposta atrasada substituiu paciente da busca mais recente");
            Console.WriteLine("OK UI real: resposta lenta fora de ordem descartada; texto e resultados mais novos preservados.");
            await view.NavegarAsync("prontuario"); await Task.Delay(500);
            await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('input[data-campo=\"Seletor.Termo\"]');e.value='Zoraide';e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Esperar(browser,"[...document.querySelectorAll('select[data-campo=\"Seletor.Selecionado\"] option')].some(o=>o.textContent.includes('Zoraide'))", "busca do novo prontuário");
            await Selecionar("Zoraide");
            await Esperar(browser,"document.body.innerText.includes('Zoraide Outra Sintética')", "novo paciente do prontuário");
            Exigir(sp.GetRequiredService<PacienteEmFoco>().Nome=="Zoraide Outra Sintética", "Seleção não trocou o paciente em foco");
            Console.WriteLine("OK UI real: novo prontuário muda paciente em foco.");
            var foco=sp.GetRequiredService<PacienteEmFoco>();
            foco.Definir(foco.PacienteId!.Value,foco.Nome,agendamentoId:999,atendimentoId:888,dataDoHorario:DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
            await view.NavegarAsync(ModuloClinico.ChaveProntuarios); await Task.Delay(500);
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"NovoProntuario\"]')");
            await Esperar(browser,"!!document.querySelector('.dialogo-web input[data-campo=\"Seletor.Termo\"]')", "Novo prontuário pergunta quem será atendido");
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web [data-fechar-dialogo]').click()");
            await Esperar(browser,"!document.querySelector('.dialogo-web')", "escolha de prontuário cancelada");
            Exigir(foco.Nome=="Zoraide Outra Sintética"&&foco.AgendamentoId==999&&foco.AtendimentoId==888,"Cancelar novo prontuário alterou foco ou vínculo anterior");
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"NovoProntuario\"]')");
            await Esperar(browser,"!!document.querySelector('.dialogo-web input[data-campo=\"Seletor.Termo\"]')", "nova escolha de prontuário");
            await Digitar("Zuleica",enter:true); await Selecionar("Zuleica");
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('.dialogo-web [data-comando=\"Confirmar\"]')");
            await Esperar(browser,"!document.querySelector('.dialogo-web')&&document.body.innerText.includes('Zuleica Sintética Busca')&&!!document.querySelector('[data-comando=\"IniciarSessao\"]')", "atendimento do novo prontuário");
            Exigir(foco.Nome=="Zuleica Sintética Busca"&&foco.AgendamentoId is null&&foco.AtendimentoId is null&&foco.DataDoHorario is null,"Novo prontuário reutilizou paciente ou sessão anterior");
            using(var imagem=File.Create(Path.Combine(pasta,"novo-prontuario-outro-paciente.png"))) await view.CapturarPreviewAsync(imagem);
            Console.WriteLine("OK UI real: Novo prontuário cancela mantendo foco e vínculos; confirmar outro paciente troca contexto e remove vínculos anteriores.");
            await view.NavegarAsync("agenda-recepcao"); await Task.Delay(700);
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"NovoHorario\"]')");
            await Esperar(browser,"!!document.querySelector('input[data-campo=\"Seletor.Termo\"]') && document.body.innerText.includes('Marcar horário')", "formulário de agendamento");
            async Task Campo(string chave, string valor, bool dialogo=false)
            {
                await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('{(dialogo?".dialogo-web ":"")}[data-campo=\"{chave}\"]');if(!e)throw Error('Campo ausente');e.value={JsonSerializer.Serialize(valor)};e.dispatchEvent(new Event('input',{{bubbles:true}}));e.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
                await Task.Delay(150);
            }
            await Campo("Seletor.Termo", "Zuleica");
            await Esperar(browser,"[...document.querySelectorAll('select[data-campo=\"Seletor.Selecionado\"] option')].some(o=>o.textContent.includes('Zuleica'))", "busca no formulário de horário");
            await Selecionar("Zuleica");
            await Campo("Data", DateTime.Today.AddDays(2).ToString("yyyy-MM-dd")); await Campo("Hora", "10:00"); await Campo("Duracao", "30");
            await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const e=document.querySelector('select[data-campo=\"Profissional\"]');e.value=[...e.options].find(o=>o.textContent.includes('sintético')).value;e.dispatchEvent(new Event('change',{bubbles:true}));})()"); await Task.Delay(300);
            await AcoesVisiveisQa.ClicarExpressao(browser, "(()=>{const linhas=[...document.querySelectorAll('[data-tabela-container=\"Cartoes\"] [data-linha-id]')];const linha=linhas.find(e=>e.textContent.includes('Acupuntura (apenas)'));if(!linha)throw Error('Modalidade ausente');return linha.querySelector('[data-comando=\"EscolherModalidade\"]');})()"); await Task.Delay(500);
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"Lancar\"]')");
            await Esperar(browser,"document.body.innerText.includes('Horário marcado')", "agendamento salvo");
            await Esperar(browser,"!!document.querySelector('.dialogo-web [data-comando=\"fechar\"]')", "pergunta de impressão do comprovante");
            Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web').innerText.includes('Imprimir o comprovante de agendamento para Zuleica')") == "true","Confirmação de impressão omitiu paciente, pergunta e data");
            using(var imagem=File.Create(Path.Combine(pasta,"comprovante-pergunta.png"))) await view.CapturarPreviewAsync(imagem);
            await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('.dialogo-web [data-comando=\"fechar\"]')");
            await Esperar(browser,"!document.querySelector('.dialogo-web')", "comprovante dispensado");
            using(var scope=sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.CountAsync()==1, "UI não persistiu agendamento");
            var popup=view.ExecutarComDialogosAsync(async()=>{await view.Dialogos.AbrirAsync("RecepcaoAgendamento",new AgendamentoEdicaoViewModel(sp.GetRequiredService<IServiceScopeFactory>()));});
            await Esperar(browser,"!!document.querySelector('.dialogo-web input[data-campo=\"Hora\"]')", "segundo formulário de horário");
            await Campo("Hora", "12:00",dialogo:true);
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.dialogo-web [data-fechar-dialogo]').click()");
            await Esperar(browser,"!document.querySelector('.dialogo-web')", "agendamento cancelado");
            await popup;
            using(var scope=sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Agendamentos.CountAsync()==1, "Cancelar UI criou outro horário");
            Console.WriteLine("OK UI real: navegar a Marcar, buscar paciente, preencher, salvar horário e cancelar popout sem gravar.");
            using(var scope=sp.CreateScope()) Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Pacientes.CountAsync()==232, "Busca/seleção gravou paciente indevidamente");
            await ConferirDensidade(sp, view, browser, janela, pasta);
        }
        finally { janela.Close(); }
        view.Dispose();
        sp.GetRequiredService<SessaoUsuario>().Entrar(new UsuarioSistema { Id=999, Nome="Agendamento sem faturamento", Login="agenda.restrita.qa", Perfil=PerfilAcesso.Gerente, PermissoesNegadas=Permissao.LancarAtendimento });
        var marcar=new NovoAtendimentoViewModel(sp.GetRequiredService<IServiceScopeFactory>());
        marcar.FixarModo(true);
        Exigir(marcar.PodeLancar, "Somente EditarAgenda não habilita confirmação de horário sem guia");
        marcar.GuiaNaMarcacao=true;
        Exigir(!marcar.PodeLancar, "Agendamento com guia habilitado sem LancarAtendimento");
        marcar.FixarModo(false);
        Exigir(!marcar.PodeLancar, "Lançamento imediato habilitado sem LancarAtendimento");
        Console.WriteLine("OK permissões: marcar sem guia exige EditarAgenda; com guia e lançamento imediato exigem LancarAtendimento.");
    }
    static async Task ConferirDensidade(IServiceProvider sp, SuiteWebView view, WebView2 browser, Window janela, string pasta)
    {
        using(var scope=sp.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            db.Agendamentos.RemoveRange(await db.Agendamentos.ToListAsync()); await db.SaveChangesAsync();
            var pacientes=await db.Pacientes.OrderBy(p=>p.Id).ToListAsync();
            var profissional=await db.Profissionais.FirstAsync();
            var segundo=new Profissional {Nome="Profissional sintético segundo"};
            db.Profissionais.Add(segundo);await db.SaveChangesAsync();
            var segunda=DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek+6)%7);
            var dias=new[]{58,50,45,34,10,0,0};
            // Mantém hoje com 34 itens, qualquer que seja o dia em que o cenário for executado.
            var hoje=(int)(DateTime.Today-segunda).TotalDays;
            (dias[3],dias[hoje])=(dias[hoje],dias[3]);
            for(var dia=0;dia<7;dia++) for(var n=0;n<dias[dia];n++)
                db.Agendamentos.Add(new Agendamento {PacienteId=pacientes[n%60].Id, ProfissionalId=n%2==0?profissional.Id:segundo.Id, DataHora=segunda.AddDays(dia).AddHours(9).AddMinutes(n%20*15), DuracaoMinutos=30, ModalidadePrevista=ModalidadeAtendimento.AcupunturaSimples, Observacoes="Recado sintético para demonstrar a leitura de uma agenda densa sem ocultar as ações."});
            foreach(var p in pacientes.Skip(60).Take(170))
                db.Acompanhamentos.Add(new AcompanhamentoPaciente {PacienteId=p.Id,Tipo=TipoAcompanhamento.Recall,Modalidade=ModalidadeAtendimento.AcupunturaSimples,ReferenciaEm=DateTime.Today.AddDays(-90),ProximoContato=DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),CriadoEm=DateTime.Today.AddDays(-10),CriadoPor="QA sintética",Etapa=EtapaAcompanhamento.AContatar});
            await db.SaveChangesAsync();
        }
        foreach(var largura in new[]{1366,900})
        {
            janela.Width=largura;janela.Height=largura==900?660:768;await Task.Delay(200);
            foreach(var rota in new[]{"fila","retorno-pacientes",ModuloClinico.ChaveMinhaSemana})
            {
                await view.NavegarAsync(rota);
                var esperado=rota=="fila"?34:rota=="retorno-pacientes"?170:197;
                var seletor=rota=="fila"?".registro-cartao":rota=="retorno-pacientes"?".registro-cartao":".agenda-sessao";
                await Esperar(browser,$"document.querySelectorAll('{seletor}').length==={esperado}","densidade "+rota+" "+esperado);
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1") == "true","Página transborda horizontalmente "+rota);
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"[...document.querySelectorAll('{seletor}')].every(e=>e.querySelector('strong,h4')?.textContent.trim().length>3)") == "true","Nome de paciente ausente "+rota);
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"[...document.querySelectorAll('{seletor}')].every(e=>e.getBoundingClientRect().width>=220)") == "true","Cartões ilegíveis ou estreitos "+rota);
                var acao=rota=="fila"?"AbrirFicha":rota=="retorno-pacientes"?"Abrir":"Abrir";
                Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"document.querySelectorAll('{seletor} [data-comando=\"{acao}\"]').length==={esperado}") == "true","Ação de abrir paciente perdida "+rota);
                using var imagem=File.Create(Path.Combine(pasta,rota+"-denso-"+largura+".png"));await view.CapturarPreviewAsync(imagem);
                Console.WriteLine($"OK WebView2 denso: {rota}, {esperado} cartões legíveis e ações, {largura}px.");
                if(rota=="fila")
                {
                    const string linhas="[data-tabela-container=\"Profissionais\"] .profissional-filtro[data-linha-id]";
                    await Esperar(browser,$"document.querySelectorAll('{linhas}').length===3", "Todos mais dois profissionais identificados");
                    var dados=await browser.CoreWebView2.ExecuteScriptAsync($"[...document.querySelectorAll('{linhas}')].map(l=>['Nome','Quantidade','Ativo'].map(c=>l.querySelector('[data-coluna='+c+']').textContent.trim()))");
                    Console.WriteLine("Profissionais DOM "+dados);
                    Exigir(dados.Contains("Todos")&&dados.Contains("Profissional sintético de busca")&&dados.Contains("Profissional sintético segundo"),"Nomes de profissionais ocultos");
                    Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const linhas=[...document.querySelectorAll('{linhas}')];return parseInt(linhas[0].querySelector('[data-coluna=Quantidade]').textContent)===34&&parseInt(linhas[1].querySelector('[data-coluna=Quantidade]').textContent)===17&&parseInt(linhas[2].querySelector('[data-coluna=Quantidade]').textContent)===17&&linhas[0].querySelector('[data-comando=Filtrar]').getAttribute('aria-pressed')==='true'&&linhas.slice(1).every(l=>l.querySelector('[data-comando=Filtrar]').getAttribute('aria-pressed')==='false')}})()") == "true","Contagens ou filtro ativo incorretos antes de filtrar");
                    await AcoesVisiveisQa.ClicarExpressao(browser, $"[...document.querySelectorAll('{linhas}')].find(l=>l.querySelector('[data-coluna=Nome]').textContent.includes('sintético segundo')).querySelector('[data-comando=\"Filtrar\"]')");
                    await Esperar(browser,"document.querySelectorAll('.registro-cartao').length===17", "filtro do segundo profissional");
                    Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"(()=>{{const linhas=[...document.querySelectorAll('{linhas}')];return linhas.find(l=>l.querySelector('[data-coluna=Nome]').textContent.includes('sintético segundo')).querySelector('[data-comando=Filtrar]').getAttribute('aria-pressed')==='true'&&linhas.filter(l=>!l.querySelector('[data-coluna=Nome]').textContent.includes('sintético segundo')).every(l=>l.querySelector('[data-comando=Filtrar]').getAttribute('aria-pressed')==='false')}})()") == "true","Filtro ativo não acompanha profissional escolhido");
                    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-tabela-container=\"Profissionais\"]').scrollIntoView({block:'center'})");await Task.Delay(200);
                    using(var filtrado=File.Create(Path.Combine(pasta,"fila-profissionais-filtrada-"+largura+".png")))await view.CapturarPreviewAsync(filtrado);
                    await AcoesVisiveisQa.ClicarExpressao(browser, $"[...document.querySelectorAll('{linhas}')].find(l=>l.querySelector('[data-coluna=Nome]').textContent.trim()==='Todos').querySelector('[data-comando=\"Filtrar\"]')");
                    await Esperar(browser,"document.querySelectorAll('.registro-cartao').length===34", "Todos restaura os 34 horários");
                    Exigir(await browser.CoreWebView2.ExecuteScriptAsync($"[...document.querySelectorAll('{linhas}')].find(l=>l.querySelector('[data-coluna=Nome]').textContent.trim()==='Todos').querySelector('[data-comando=Filtrar]').getAttribute('aria-pressed')==='true'") == "true","Retorno a Todos não ativou estado");
                    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-tabela-container=\"Profissionais\"]').scrollIntoView({block:'center'})");await Task.Delay(200);
                    using(var todos=File.Create(Path.Combine(pasta,"fila-profissionais-todos-"+largura+".png")))await view.CapturarPreviewAsync(todos);
                    Console.WriteLine($"OK profissionais UI: Todos 34, nomes 17+17, filtro segundo 17 e Todos restaura 34; estado ativo acompanha clique, {largura}px.");
                }
            }
        }
    }
}
