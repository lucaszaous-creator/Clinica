using System.IO;
using System.Text.Json;
using System.Windows;
using Clinica.Faturamento.Web;
using Clinica.Faturamento.Modulo;
using Clinica.Desktop.ViewModels;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
static class Fluxos
{
 static void Exigir(bool condicao,string texto){if(!condicao)throw new Exception(texto);}
 static JsonElement J(object? v)=>JsonSerializer.SerializeToElement(v);
 public static async Task Executar(bool web,bool ferramentasSomente=false)
 {
  using var con=new SqliteConnection("Data Source=:memory:");await con.OpenAsync();
  var op=new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(con).Options;
  var sc=new ServiceCollection();sc.AddClinica("Host=127.0.0.1;Port=1;Database=NAO_USAR;Username=NAO_USAR;Timeout=1");
  sc.AddScoped(_=>new ClinicaDbContext(op));sc.AddSingleton<SessaoUsuario>();sc.AddSingleton<SnackbarService>();sc.AddSingleton<ISnackbarService>(s=>s.GetRequiredService<SnackbarService>());sc.AddSingleton<IDialogoService,DialogosNativosProibidos>();
  new Clinica.Gerente.Modulo.ModuloGerente().Registrar(sc);new Clinica.Recepcao.Modulo.ModuloRecepcao().Registrar(sc);var modulo=new ModuloFaturamento();modulo.Registrar(sc);using var sp=sc.BuildServiceProvider();
  using(var scope=sp.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();await db.Database.EnsureCreatedAsync();var u=new UsuarioSistema{Nome="Faturamento demonstração",Login="faturamento.qa",Perfil=PerfilAcesso.Gerente};db.Add(u);var p=new Paciente{Nome="Paciente fictícia de demonstração",Convenio=Convenio.UnimedIntercambio,Sexo=Sexo.Feminino};db.Add(p);await db.SaveChangesAsync();sp.GetRequiredService<SessaoUsuario>().Entrar(u);await scope.ServiceProvider.GetRequiredService<AtendimentoService>().LancarAsync(p.Id,DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),ModalidadeAtendimento.AcupunturaComEletro);}
  if(ferramentasSomente){await FerramentasQa.Executar(sp);return;}
  var defs=FaturamentoWebRegistro.CriarPaginas().ToArray();using var pages=new PaginasWebController(sp,defs);using var dialogs=new DialogosWebController(FaturamentoWebRegistro.CriarDialogos());using var contexto=DialogosDaSessao.Usar(dialogs);
  foreach(var def in defs){await pages.NavegarAsync(def.Chave);var dto=pages.ObterPagina();Exigir(!dto.MensagemEhErro,def.Chave+": "+dto.Mensagem);Console.WriteLine("OK página real "+def.Chave);}
  await pages.NavegarAsync("faturamento-pendencias");
  LinhaWebDto Linha(string tabela)=>pages.ObterPagina().Secoes.SelectMany(s=>s.Tabelas).Single(t=>t.Chave==tabela).Linhas.First();
  async Task<DialogoWebDto> EsperarDialogo(string? titulo=null){for(var i=0;i<100;i++){if(dialogs.EstadoAtual is {} d && (titulo is null||d.Pagina.Titulo.Contains(titulo)))return d;await Task.Delay(30);}throw new Exception("Diálogo não abriu: "+titulo);}
  var linha=Linha("Codigos");var anotar=pages.ExecutarAcaoAsync("Anotar","Codigos",linha.Id);var dlg=await EsperarDialogo();await dialogs.AtualizarCampoAsync(dlg.Id,"Texto",J("Aguardando envio pela operadora — teste sintético"));await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await anotar;
  Exigir(Linha("Codigos").Celulas["ObservacaoPendencia"].Contains("teste sintético"),"Anotação não persistiu");
  linha=Linha("Codigos");var cancelar=pages.ExecutarAcaoAsync("DarBaixa","Codigos",linha.Id);dlg=await EsperarDialogo();dialogs.Fechar(dlg.Id);await cancelar;Exigir(Linha("Codigos").Id!=null,"Cancelar retirou pendência");
  linha=Linha("Codigos");var baixar=pages.ExecutarAcaoAsync("DarBaixa","Codigos",linha.Id);dlg=await EsperarDialogo();Exigir(!dlg.Pagina.Acoes.Single(a=>a.Chave=="confirmar").Habilitada,"Baixa vazia habilitada");await dialogs.AtualizarCampoAsync(dlg.Id,"NumeroGuia",J("123456789"));var confirmarBaixa=dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");dlg=await EsperarDialogo("Confirmar baixa");await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await confirmarBaixa;await baixar;
  using(var scope=sp.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();Exigir(await db.Codigos.AnyAsync(c=>c.NumeroGuiaReal=="123456789"&&c.Status==StatusCodigo.Baixado),"Baixa não gravou");}
  await pages.NavegarAsync("faturamento-faturados");var faturada=Linha("Baixados");var glosar=pages.ExecutarAcaoAsync("Glosar","Baixados",faturada.Id);dlg=await EsperarDialogo();var motivo=dlg.Pagina.Campos.Single(c=>c.Chave=="Selecionado").Opcoes.First();await dialogs.AtualizarCampoAsync(dlg.Id,"Selecionado",J(motivo.Valor));await dialogs.AtualizarCampoAsync(dlg.Id,"Motivo",J("Complemento de demonstração"));await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await glosar;
  await pages.NavegarAsync("faturamento-glosas");Exigir(Linha("Glosas").Celulas["Codigo.MotivoGlosa"].Contains("demonstração"),"Glosa não persistiu");
  await pages.NavegarAsync("faturamento-pendencias");linha=Linha("Codigos");var nc=pages.ExecutarAcaoAsync("NaoConformidade","Codigos",linha.Id);dlg=await EsperarDialogo();await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");dlg=await EsperarDialogo("Não conformidade —");await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");Exigir(dialogs.EstadoAtual is {Pagina.MensagemEhErro:true},"NC sem justificativa passou");await dialogs.AtualizarCampoAsync(dlg.Id,"Justificativa",J("Decisão sintética para teste"));await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await nc;
  await pages.NavegarAsync("faturamento-nc");Exigir(Linha("Itens").Celulas["Justificativa"].Contains("sintética"),"NC não persistiu");
  var reabrir=pages.ExecutarAcaoAsync("Reabrir","Itens",Linha("Itens").Id);dlg=await EsperarDialogo();await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await reabrir;
  await pages.NavegarAsync("faturamento-parametros");await pages.AtualizarCampoAsync("TussAcupuntura",J("31601014"));await pages.ExecutarAcaoAsync("Salvar");Exigir(!pages.ObterPagina().MensagemEhErro,"Salvar parâmetros falhou");
  using(var scope=sp.CreateScope()){var par=scope.ServiceProvider.GetRequiredService<ParametrosService>();Exigir((await par.ObterPrestadorAsync()).CodigoTuss(TipoCodigo.Acupuntura)=="31601014","TUSS não persistiu");var lotes=scope.ServiceProvider.GetRequiredService<LoteTissService>();await lotes.CriarAsync(DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),DateOnly.FromDateTime(DateTime.Today),"123456","qa");}
  await pages.NavegarAsync("faturamento-tiss");var envio=pages.ExecutarAcaoAsync("MarcarEnviado","Lotes",Linha("Lotes").Id);dlg=await EsperarDialogo();await dialogs.AtualizarCampoAsync(dlg.Id,"Protocolo",J("PROTOCOLO-SINTETICO"));await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await envio;Exigir(Linha("Lotes").Celulas["Protocolo"]=="PROTOCOLO-SINTETICO","Envio não persistiu");
  var retorno=pages.ExecutarAcaoAsync("RegistrarRetorno","Lotes",Linha("Lotes").Id);dlg=await EsperarDialogo();dialogs.Fechar(dlg.Id);dlg=await EsperarDialogo("Registrar retorno do lote");
  var retornoLinha=dlg.Pagina.Secoes.SelectMany(x=>x.Tabelas).Single().Linhas.First();await dialogs.AtualizarCampoAsync(dlg.Id,"Glosada",J(true),"guias",retornoLinha.Id);var motivoRetorno=retornoLinha.Campos.Single(c=>c.Chave=="Motivo").Opcoes.First();await dialogs.AtualizarCampoAsync(dlg.Id,"Motivo",J(motivoRetorno.Valor),"guias",retornoLinha.Id);await dialogs.AtualizarCampoAsync(dlg.Id,"Complemento",J("Retorno sintético conferido"),"guias",retornoLinha.Id);await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await retorno;Exigir(!string.IsNullOrEmpty(Linha("Lotes").Celulas["DataRetorno"]),"Retorno TISS não persistiu");
  await pages.NavegarAsync("faturamento-pendencias");var pendencia=((DashboardViewModel)pages.ViewModelAtual!).Codigos.First();
  var rodadaVm=new RodadaFaturamento([pendencia],new(10,1,1,false,0,false,0),true);var rodada=dialogs.AbrirAsync("RodadaFaturamento",rodadaVm);dlg=await EsperarDialogo();var bloqueou=false;try{dialogs.Fechar(dlg.Id);}catch(InvalidOperationException){bloqueou=true;}Exigir(bloqueou,"Rodada bloqueante permitiu cancelar");await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");Exigir(dialogs.EstadoAtual!.Pagina.MensagemEhErro,"Rodada aceitou guia sem decisão");var linhaRodada=dialogs.EstadoAtual.Pagina.Secoes.SelectMany(x=>x.Tabelas).Single().Linhas.First();await dialogs.AtualizarCampoAsync(dlg.Id,"Justificativa",J("Decisão sintética"),"guias",linhaRodada.Id);await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await rodada;
  linha=Linha("Codigos");await pages.ExecutarAcaoAsync("AlternarSelecao","Codigos",linha.Id);Exigir(Linha("Codigos").Celulas["Selecionada"]=="Selecionada","Seleção web não refletiu estado");var loteBaixa=pages.ExecutarAcaoAsync("DarBaixaSelecionadas");dlg=await EsperarDialogo();var selecionada=dlg.Pagina.Secoes.SelectMany(x=>x.Tabelas).Single().Linhas.First();await dialogs.AtualizarCampoAsync(dlg.Id,"NumeroGuia",J("987654321"),"guias",selecionada.Id);await dialogs.ExecutarAcaoAsync(dlg.Id,"confirmar");await loteBaixa;
  Console.WriteLine("OK persistência: anotação, cancelar baixa, validação, baixa, glosa, NC, reabrir, TUSS, envio, retorno e baixa em lote. Rodada bloqueante exige decisão.");
  await EntradaQa.Executar(web);
  await FormulariosExtrasQa.Executar(sp,web);
  await AssinaturaQa.Executar(sp,web);
  if(web)await Visual(sp,modulo,defs);
 }
 static async Task Visual(IServiceProvider sp,ModuloFaturamento modulo,PaginasWebController.Pagina[] defs)
 {
  var saida=Path.GetFullPath("artifacts/faturamento-web");Directory.CreateDirectory(saida);
  using var view=new SuiteWebView(sp,defs,FaturamentoWebRegistro.CriarDialogos(),modulo.Itens.Where(i=>defs.Any(d=>d.Chave==i.Chave)).ToArray(),"Faturamento — demonstração","faturamento-pendencias");
  var window=new Window{Content=view,Width=1440,Height=900,Left=-30000,Top=-30000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();
  try{await view.QuandoPronto.WaitAsync(TimeSpan.FromSeconds(45));var browser=(WebView2)view.Content;
   foreach(var tamanho in new[]{(1440,900),(1100,720),(900,600)}) {window.Width=tamanho.Item1;window.Height=tamanho.Item2;await Task.Delay(250);
    foreach(var p in defs){await view.NavegarAsync(p.Chave);await Task.Delay(400);var ok=await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollWidth<=innerWidth+1 && !!document.querySelector('.navegacao-topo') && !document.querySelector('.sidebar')");Exigir(ok=="true","Layout inválido "+p.Chave);if(p.Chave=="faturamento-pendencias")Exigir(await browser.CoreWebView2.ExecuteScriptAsync("!document.querySelector('[data-secao=Carteirinhas]')&&!!document.querySelector('[data-secao=Codigos]')&&!!document.querySelector('[data-secao=Recursos]')&&!!document.querySelector('[data-secao=Consultas]')")=="true","Pendências perdeu seção mantida ou ainda mostra carteirinhas");using var f=File.Create(Path.Combine(saida,p.Chave+"-"+tamanho.Item1+".png"));await view.CapturarPreviewAsync(f);Console.WriteLine("OK WebView2 "+p.Chave+" "+tamanho.Item1);}
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
