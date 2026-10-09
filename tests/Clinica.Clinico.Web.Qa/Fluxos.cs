using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.Web;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
static partial class Fluxos
{
 public static bool SoGestos;
 static string? ultimoPdfEntregue;
 static UsuarioSistema gestor=null!,enfermeiro=null!;
 static void Exigir(bool valor,string mensagem){if(!valor)throw new Exception(mensagem);}
 static JsonElement J(object? valor)=>JsonSerializer.SerializeToElement(valor);
 public static async Task Executar(bool web)
 {
  using var con=new SqliteConnection("Data Source=:memory:");await con.OpenAsync();
  var op=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;
  var sc=new ServiceCollection();sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
  sc.AddScoped(_=>new ClinicaDbContext(op));sc.AddSingleton<SessaoUsuario>();sc.AddSingleton<SnackbarService>();sc.AddSingleton<ISnackbarService>(s=>s.GetRequiredService<SnackbarService>());sc.AddSingleton<IDialogoService,DialogosNativosProibidos>();
  sc.AddSingleton(new ImpressaoPdf.DestinoNoEscopo(async (bytes,nome,_,__) => {
   var pasta=Path.GetFullPath("artifacts/clinico-web");Directory.CreateDirectory(pasta);
   var caminho=Path.Combine(pasta,nome);await File.WriteAllBytesAsync(caminho,bytes);ultimoPdfEntregue=caminho;return null;
  }));
  var modulo=new ModuloClinico();modulo.Registrar(sc);using var sp=sc.BuildServiceProvider();
  using(var scope=sp.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();await db.Database.EnsureCreatedAsync();var profissional=new Profissional{Nome="Profissional sintético",RegistroConselho="DEMONSTRACAO"};var u=new UsuarioSistema{Nome="Clínico demonstração",Login="clinico.qa",Perfil=PerfilAcesso.Gerente,Profissional=profissional};var p=new Paciente{Nome="Paciente fictícia de demonstração",Convenio=Convenio.UnimedIntercambio,Sexo=Sexo.Feminino};gestor=u;enfermeiro=new UsuarioSistema{Nome="Enfermagem sintética",Login="enfermagem.qa",Perfil=PerfilAcesso.Enfermagem,Profissional=profissional};db.AddRange(u,p,enfermeiro);db.CamposPersonalizadosProntuario.AddRange(
   new CampoPersonalizadoProntuario { Rotulo="Texto sintético", Tipo=TipoCampoPersonalizado.Texto, Ordem=1 },
   new CampoPersonalizadoProntuario { Rotulo="Lista sintética", Tipo=TipoCampoPersonalizado.Lista, Opcoes="Opção A\nOpção B", Ordem=2 },
   new CampoPersonalizadoProntuario { Rotulo="Confirmação sintética", Tipo=TipoCampoPersonalizado.SimNao, Ordem=3 });await db.SaveChangesAsync();sp.GetRequiredService<SessaoUsuario>().Entrar(u);sp.GetRequiredService<PacienteEmFoco>().Definir(p.Id,p.Nome);}
  var defs=ClinicoWebRegistro.CriarPaginas().ToArray();var registros=ClinicoWebRegistro.CriarDialogos().Concat(RegistroCompartilhadoWeb.Dialogos()).DistinctBy(d=>(d.Chave,d.Tipo)).ToArray();
  if(SoInfusaoModelos){await ValidarInfusaoModelosAsync(sp,modulo,defs,registros);return;}
  using var pages=new PaginasWebController(sp,defs);using var dialogs=new DialogosWebController(registros);using var contexto=DialogosDaSessao.Usar(dialogs);
  foreach(var def in defs) {await pages.NavegarAsync(def.Chave);await Task.Delay(120);var dto=pages.ObterPagina();Exigir(!dto.MensagemEhErro,def.Chave+": "+dto.Mensagem);Console.WriteLine("OK página real "+def.Chave);}
  async Task<DialogoWebDto> EsperarDialogo(){for(var i=0;i<150;i++){if(dialogs.EstadoAtual is {} d)return d;await Task.Delay(20);}throw new Exception("Diálogo não abriu");}
  await pages.NavegarAsync(ModuloClinico.ChaveMedidas);
  var abrir=pages.ExecutarAcaoAsync("Medidas.Registrar");var dlg=await EsperarDialogo();
  var peso=dlg.Pagina.Campos.Single(c=>c.Chave=="TipoNovo").Opcoes.First(o=>o.Rotulo.Contains("Peso",StringComparison.OrdinalIgnoreCase));await dialogs.AtualizarCampoAsync(dlg.Id,"TipoNovo",J(peso.Valor));await dialogs.AtualizarCampoAsync(dlg.Id,"ValorNovo",J("70"));await dialogs.ExecutarAcaoAsync(dlg.Id,"Registrar");await abrir;
  using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().MedidasClinicas.CountAsync()==1,"Medida não persistiu");
  abrir=pages.ExecutarAcaoAsync("Medidas.Registrar");dlg=await EsperarDialogo();dialogs.Fechar(dlg.Id);await abrir;
  using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().MedidasClinicas.CountAsync()==1,"Cancelar gravou medida");
  await pages.NavegarAsync(ModuloClinico.ChaveExamesDoPaciente);abrir=pages.ExecutarAcaoAsync("Anexos.RegistrarResultado");dlg=await EsperarDialogo();await dialogs.AtualizarCampoAsync(dlg.Id,"NomeNovo",J("Exame sintético"));await dialogs.AtualizarCampoAsync(dlg.Id,"ValorNovo",J("Resultado de demonstração"));await dialogs.ExecutarAcaoAsync(dlg.Id,"Registrar");await abrir;
  using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().ResultadosExame.AnyAsync(),"Resultado não persistiu");
  await pages.NavegarAsync(ModuloClinico.ChavePaciente);abrir=pages.ExecutarAcaoAsync("Capa.NovoProblema");dlg=await EsperarDialogo();await dialogs.AtualizarCampoAsync(dlg.Id,"Descricao",J("Alerta sintético para QA"));await dialogs.ExecutarAcaoAsync(dlg.Id,"Salvar");await abrir;
  using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().ProblemasPaciente.AnyAsync(),"Problema não persistiu");
  await ValidarFichaAsync(sp,pages,dialogs,EsperarDialogo);
  await pages.NavegarAsync(ModuloClinico.ChaveAvaliacoes);
  await pages.AtualizarCampoAsync("Avaliacoes.TodosOsInstrumentos",J(true));
  var instrumentos=pages.ObterPagina().Secoes.SelectMany(x=>x.Tabelas).Single(t=>t.Chave=="Avaliacoes.Chips").Linhas;
  Exigir(instrumentos.Count>0,"Escalas não disponíveis");
  await pages.ExecutarAcaoAsync("Avaliacoes.Escolher","Avaliacoes.Chips",instrumentos.First().Id);
  abrir=pages.ExecutarAcaoAsync("Avaliacoes.Aplicar");dlg=await EsperarDialogo();
  await dialogs.ExecutarAcaoAsync(dlg.Id,"Salvar");Exigir(dialogs.EstadoAtual?.Pagina.MensagemEhErro==true,"Escala sem respostas aceita");
  foreach(var linha in dialogs.EstadoAtual!.Pagina.Secoes.SelectMany(x=>x.Tabelas).Single(t=>t.Chave=="Itens").Linhas.ToArray()) {
   var opcao=linha.Campos.Single(c=>c.Chave=="Escolhida").Opcoes.First();
   await dialogs.AtualizarCampoAsync(dlg.Id,"Escolhida",J(opcao.Valor),"Itens",linha.Id);
  }
  await dialogs.ExecutarAcaoAsync(dlg.Id,"Salvar");await abrir;
  using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().AvaliacoesClinicas.AnyAsync(),"Avaliação não persistiu");
  Console.WriteLine("OK escala: incompleta bloqueada; respostas editadas nas linhas e avaliação gravada.");
  await pages.NavegarAsync(ModuloClinico.ChaveAtendimento);
  await pages.AtualizarCampoAsync("Atendimento.TextoEvolucao",J(new {texto="Evolução sintética para validação da interface.",formato=(string?)null}));
  var workspace=(PacienteWorkspaceViewModel)pages.ViewModelAtual!;
  await pages.NavegarAsync(ModuloClinico.ChaveMedidas);await pages.NavegarAsync(ModuloClinico.ChaveAtendimento);
  Exigir(ReferenceEquals(workspace,pages.ViewModelAtual)&&workspace.Atendimento.TextoEvolucao?.Contains("sintética")==true,"Troca de aba perdeu o rascunho");
  using(var scope=sp.CreateScope())Exigir(!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Evolucoes.AnyAsync(),"Editar e trocar abas gravou a evolução sem executar Salvar.");
  Console.WriteLine("OK edição sem autosave: alterar texto e navegar entre abas mantém rascunho sem criar evolução no SQLite.");
  abrir=pages.ExecutarAcaoAsync("Atendimento.AbrirMapa");dlg=await EsperarDialogo();
  await dialogs.AtualizarCampoAsync(dlg.Id,"MarcacaoWeb",J(JsonSerializer.Serialize(new{face="Frente",x=0.4,y=0.5})));
  Exigir(workspace.Atendimento.Mapa!.Pontos.Count==1,"Mapa não recebeu ponto");dialogs.Fechar(dlg.Id);await abrir;Exigir(workspace.Atendimento.Mapa.Pontos.Count==0,"Cancelar mapa não restaurou rascunho");
  abrir=pages.ExecutarAcaoAsync("Atendimento.AbrirMapa");dlg=await EsperarDialogo();await dialogs.AtualizarCampoAsync(dlg.Id,"MarcacaoWeb",J(JsonSerializer.Serialize(new{face="Frente",x=0.4,y=0.5})));await dialogs.ExecutarAcaoAsync(dlg.Id,"UsarMapaWeb");await abrir;Exigir(workspace.Atendimento.Mapa.Pontos.Count==1,"Confirmar mapa perdeu ponto");
  await ValidarCamposComplementaresAsync(pages,dialogs,EsperarDialogo);
  await pages.ExecutarAcaoAsync("Atendimento.Salvar");
  Exigir(workspace.Atendimento.MensagemEhErro==false,"Sessão: "+workspace.Atendimento.Mensagem);
  using(var scope=sp.CreateScope())Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Evolucoes.AnyAsync(e=>e.TextoEvolucao!.Contains("sintética")),"Sessão não persistiu");
  using(var scope=sp.CreateScope()) {
   var valores=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().ValoresCampoPersonalizado.OrderBy(x=>x.Rotulo).Select(x=>x.Valor).ToListAsync();
   Exigir(valores.SequenceEqual(new[]{"Sim","Opção B","Resposta sintética preservada"}),"Campos complementares do diálogo não persistiram os três tipos.");
  }
  int evolucaoId;
  using(var scope=sp.CreateScope())evolucaoId=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Evolucoes.Select(e=>e.Id).SingleAsync();
  var leitura=new SessaoDoProntuarioViewModel(sp.GetRequiredService<IServiceScopeFactory>(),evolucaoId,"Paciente fictícia de demonstração",false);
  abrir=dialogs.AbrirAsync("SessaoDoProntuario",leitura);dlg=await EsperarDialogo();
  for(var i=0;i<100&&leitura.Carregando;i++)await Task.Delay(25);
  dlg=dialogs.EstadoAtual!;var bloco=dlg.Pagina.Secoes.SelectMany(x=>x.Tabelas).Single(t=>t.Chave=="Blocos").Linhas.First(l=>l.Campos.Any(c=>JsonSerializer.Serialize(c.Valor).Contains("sint")));
  Exigir(!bloco.Campos.Single(c=>c.Chave=="Texto").Habilitado,"Leitura clínica editável");
  var negou=false;try{await dialogs.AtualizarCampoAsync(dlg.Id,"Texto",J(new{texto="alteração indevida",formato=(string?)null}),"Blocos",bloco.Id);}catch(InvalidOperationException){negou=true;}Exigir(negou,"Ponte aceitou editar registro somente leitura");dialogs.Fechar(dlg.Id);await abrir;
  var versoes=new VersoesEvolucaoViewModel(sp.GetRequiredService<IServiceScopeFactory>(),evolucaoId,"Sessão sintética");
  abrir=dialogs.AbrirAsync("VersoesEvolucao",versoes);dlg=await EsperarDialogo();await versoes.CarregarAsync();dlg=dialogs.EstadoAtual!;
  Exigir(dlg.Pagina.Secoes.SelectMany(x=>x.Tabelas).Single(t=>t.Chave=="Versoes").Linhas.Any(l=>l.Campos.Any(c=>c.Chave=="Evolucao"&&JsonSerializer.Serialize(c.Valor).Contains("sint"))),"Histórico de correções omitiu evolução");dialogs.Fechar(dlg.Id);await abrir;
  Console.WriteLine("OK leituras compartilhadas: conteúdo da sessão/versão preservado e escrita rejeitada.");
  Console.WriteLine("OK editor clínico, rascunho entre abas, mapa cancelar/confirmar e persistência de sessão.");
  Console.WriteLine("OK persistência real SQLite: medida, cancelamento, exame e alerta clínico.");
  using (var scope=sp.CreateScope()) {
   var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
   var evolucao=await db.Evolucoes.SingleAsync();
   var agendado=new Agendamento { PacienteId=evolucao.PacienteId, DataHora=evolucao.Data.ToDateTime(new TimeOnly(9,0)), ProfissionalId=gestor.ProfissionalId };
   db.Agendamentos.AddRange(agendado,new Agendamento { PacienteId=evolucao.PacienteId, DataHora=evolucao.Data.ToDateTime(new TimeOnly(10,0)), ProfissionalId=gestor.ProfissionalId });
   await db.SaveChangesAsync(); evolucao.AgendamentoId=agendado.Id; await db.SaveChangesAsync();
  }
  if(web)await Visual(sp,modulo,defs,registros);
 }
 static async Task Visual(IServiceProvider sp,ModuloClinico modulo,PaginasWebController.Pagina[] defs,DialogosWebController.RegistroDialogo[] registros)
 {
  if(SoGestos){await Capturar(sp,modulo,defs.Where(d=>d.Chave==ModuloClinico.ChaveAtendimento).ToArray(),registros);return;}
  if(SoDestinosDialogos){await ValidarDestinosDialogosAsync(sp,modulo,defs,registros);return;}
  sp.GetRequiredService<SessaoUsuario>().Entrar(gestor);
  await Capturar(sp,modulo,defs.Where(d=>d.Chave!=ModuloClinico.ChaveSessoesEnfermagem).ToArray(),registros);
  sp.GetRequiredService<SessaoUsuario>().Entrar(enfermeiro);
  await Capturar(sp,modulo,defs.Where(d=>d.Chave==ModuloClinico.ChaveSessoesEnfermagem).ToArray(),registros);
  sp.GetRequiredService<SessaoUsuario>().Entrar(gestor);
  await ValidarDestinosDialogosAsync(sp,modulo,defs,registros);
 }
 static async Task Capturar(IServiceProvider sp,ModuloClinico modulo,PaginasWebController.Pagina[] defs,DialogosWebController.RegistroDialogo[] registros)
 {
  var saida=Path.GetFullPath("artifacts/clinico-web");Directory.CreateDirectory(saida);
  using var view=new SuiteWebView(sp,defs,registros,modulo.Itens.ToArray(),"Clínico — demonstração",defs[0].Chave);
  var window=new Window{Content=view,Width=1440,Height=900,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();
  try {await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));var browser=(WebView2)view.Content;
   foreach(var tamanho in SoGestos?new[]{(1044,788)}:new[]{(1440,900),(1044,788),(900,600)}) {window.Width=tamanho.Item1;window.Height=tamanho.Item2;await Task.Delay(250);
    foreach(var p in defs){await view.NavegarAsync(p.Chave);await Task.Delay(400);var ok=await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1 && !!document.querySelector('.navegacao-topo') && !document.querySelector('.sidebar')");Exigir(ok=="true","Layout inválido "+p.Chave);using var f=File.Create(Path.Combine(saida,p.Chave+"-"+tamanho.Item1+".png"));await view.CapturarPreviewAsync(f);Console.WriteLine("OK WebView2 "+p.Chave+" "+tamanho.Item1);}
   }
   if (!SoGestos && defs.Any(d=>d.Chave==ModuloClinico.ChaveAtendimento)) { await ValidarRolagemTardiaAsync(browser); await ValidarAcoesExpostasAsync(view, browser, window, saida); }
   if (!SoGestos && defs.Any(d=>d.Chave==ModuloClinico.ChavePaciente)) await ValidarFichaVisualAsync(sp, view, browser, () => ultimoPdfEntregue);
   if(defs.Any(d=>d.Chave==ModuloClinico.ChaveAtendimento)) {
    window.Width=1044;window.Height=788;await view.NavegarAsync(ModuloClinico.ChaveAtendimento);await Task.Delay(350);
    async Task EsperarJs(string expressao){for(var i=0;i<60;i++){if(await browser.CoreWebView2.ExecuteScriptAsync(expressao)=="true")return;await Task.Delay(80);}throw new Exception("Estado web não atingido: "+expressao);}
    await AcoesVisiveisQa.ClicarExpressao(browser, "document.querySelector('[data-comando=\"Atendimento.AbrirMapa\"]')");
    await EsperarJs("!!document.querySelector('[data-mapa=\"Costas\"]')");
    Exigir(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-mapa]').getAttribute('viewBox')")=="\"0 0 220 460\"","Mapa diverge da geometria do PDF");
    await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const s=document.querySelector('[data-mapa=\"Costas\"]');const p=s.createSVGPoint();p.x=50.6;p.y=188.6;const q=p.matrixTransform(s.getScreenCTM());const evento=new MouseEvent('click',{bubbles:true,clientX:q.x,clientY:q.y});const recebido=s.createSVGPoint();recebido.x=evento.clientX;recebido.y=evento.clientY;const esperado=recebido.matrixTransform(s.getScreenCTM().inverse());window.__qaMapa=[esperado.x,esperado.y];s.dispatchEvent(evento);})()");
    await Task.Delay(800);Console.WriteLine("MAPA DOM "+await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({circulos:Array.from(document.querySelectorAll('[data-mapa] circle')).map(c=>[c.getAttribute('cx'),c.getAttribute('cy')]),erros:Array.from(document.querySelectorAll('[role=alert]')).map(e=>e.textContent)})"));
    using(var f=File.Create(Path.Combine(saida,"mapa-corporal-diagnostico.png")))await view.CapturarPreviewAsync(f);
    await EsperarJs("Array.from(document.querySelectorAll('[data-mapa=\"Costas\"] circle')).some(c=>Math.abs(Number(c.getAttribute('cx'))-window.__qaMapa[0])<.01&&Math.abs(Number(c.getAttribute('cy'))-window.__qaMapa[1])<.01)");
    await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const s=document.querySelector('[data-mapa=\"Costas\"]');s.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowRight',bubbles:true}));s.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));})()");
    await EsperarJs("Array.from(document.querySelectorAll('[data-mapa=\"Costas\"] circle')).some(c=>Math.abs(Number(c.getAttribute('cx'))-114.4)<.01&&Math.abs(Number(c.getAttribute('cy'))-230)<.01)");
    using(var f=File.Create(Path.Combine(saida,"mapa-corporal-1044.png")))await view.CapturarPreviewAsync(f);
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-fechar-dialogo]').click()");await EsperarJs("!document.querySelector('[data-dialogo]')");
    Console.WriteLine("OK gesto DOM no mapa de costas: coordenadas fiéis a220×460, cancelamento e captura.");
    if (!SoGestos) await ValidarConclusaoVisivelAsync(sp, view, browser, window, saida);
   }
  }finally{window.Close();}
 }
}
sealed class DialogosNativosProibidos:IDialogoService
{
 public bool Confirmar(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
 public bool ConfirmarPerigo(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
 public void Aviso(string t,string m)=>throw new Exception("Diálogo nativo: "+t);
 public string? PerguntarTexto(string t,string p,string? textoInicial=null,bool obrigatorio=true)=>throw new Exception("Diálogo nativo: "+t);
}
