using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Gerente.ViewModels;
using Clinica.Gerente.Web;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
static class GerenteGravacoesQa
{
 static JsonElement J(object? value)=>JsonSerializer.SerializeToElement(value);
 static void Exigir(bool ok,string erro){if(!ok)throw new Exception(erro);}
 public static async Task Executar(IServiceProvider sp)
 {
  var escopos=sp.GetRequiredService<IServiceScopeFactory>();var sessao=SessaoUsuario.Atual;UsuarioSistema original;using(var scope=sp.CreateScope())original=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<UsuarioSistema>().AsNoTracking().SingleAsync(u=>u.Id==sessao.UsuarioId);
  using var ctl=new DialogosWebController(GerenteWebRegistro.CriarDialogos());
  var u=new UsuarioEdicaoViewModel(escopos,sessao);await u.Inicializacao;var tarefa=ctl.AbrirAsync("UsuarioWindow",u);var d=ctl.EstadoAtual!;
  await ctl.ExecutarAcaoAsync(d.Id,"salvar");Exigir(ctl.EstadoAtual is not null&&u.MensagemEhErro,"Usuário vazio não foi validado");
  await ctl.AtualizarCampoAsync(d.Id,"Nome",J("Usuário fictício por formulário"));await ctl.AtualizarCampoAsync(d.Id,"Login",J("gerente.form.qa"));await ctl.AtualizarCampoAsync(d.Id,"Senha",J("SenhaSintetica!2468"));await ctl.AtualizarCampoAsync(d.Id,"DeveTrocarSenha",J(true));
  var permissao=u.Permissoes.First(x=>!x.Marcada);var nomePermissao=permissao.Rotulo;var bit=permissao.Valor;var linha=ctl.EstadoAtual!.Pagina.Secoes.SelectMany(s=>s.Tabelas).Single(t=>t.Chave=="permissoes").Linhas.Single(l=>l.Celulas["Rotulo"]==nomePermissao);await ctl.AtualizarCampoAsync(d.Id,"Marcada",J(true),"permissoes",linha.Id);
  await ctl.ExecutarAcaoAsync(d.Id,"salvar");Exigir(await tarefa.WaitAsync(TimeSpan.FromSeconds(5))==true,"Usuário não confirmou");int usuarioId;
  using(var scope=sp.CreateScope()){var salvo=await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().Set<UsuarioSistema>().SingleAsync(x=>x.Login=="gerente.form.qa");usuarioId=salvo.Id;Exigir(salvo.Nome=="Usuário fictício por formulário"&&salvo.DeveTrocarSenha&&salvo.Pode(bit),"Usuário/permissão não persistiu");}
  var editar=new UsuarioEdicaoViewModel(escopos,sessao,usuarioId);await editar.Inicializacao;tarefa=ctl.AbrirAsync("UsuarioWindow",editar);d=ctl.EstadoAtual!;Exigir(!d.Pagina.Campos.Single(c=>c.Chave=="Login").Habilitado,"Login de edição habilitado");await ctl.AtualizarCampoAsync(d.Id,"Nome",J("Edição cancelada"));ctl.Fechar(d.Id);Exigir(await tarefa==false,"Cancelar usuário confirmou");using(var scope=sp.CreateScope())Exigir((await scope.ServiceProvider.GetRequiredService<AcessoService>().ObterAsync(usuarioId))!.Nome!="Edição cancelada","Cancelar edição alterou usuário");
  var m=new MetaEdicaoViewModel(escopos,2030,null);await m.Inicializacao;tarefa=ctl.AbrirAsync("MetaWindow",m);d=ctl.EstadoAtual!;await ctl.ExecutarAcaoAsync(d.Id,"salvar");Exigir(m.MensagemEhErro&&ctl.EstadoAtual is not null,"Meta vazia aceita");await ctl.AtualizarCampoAsync(d.Id,"Valor",J("1.250,50"));await ctl.AtualizarCampoAsync(d.Id,"Observacoes",J("Planejamento sintético pelo formulário"));var mes=d.Pagina.Campos.Single(c=>c.Chave=="Mes").Opcoes.Last();await ctl.AtualizarCampoAsync(d.Id,"Mes",J(mes.Valor));await ctl.ExecutarAcaoAsync(d.Id,"salvar");Exigir(await tarefa.WaitAsync(TimeSpan.FromSeconds(5))==true,"Meta não confirmou");using(var scope=sp.CreateScope())Exigir((await scope.ServiceProvider.GetRequiredService<MetaService>().DoAnoAsync(2030)).Any(x=>x.Mes==12&&x.Valor==1250.50m&&x.Observacoes=="Planejamento sintético pelo formulário"),"Meta não persistiu campos");
  var p=new PrecoEdicaoViewModel(escopos,0);await p.Inicializacao;tarefa=ctl.AbrirAsync("PrecoConvenioWindow",p);d=ctl.EstadoAtual!;await ctl.ExecutarAcaoAsync(d.Id,"salvar");Exigir(p.MensagemEhErro&&ctl.EstadoAtual is not null,"Preço sem convênio aceito");var convenio=d.Pagina.Campos.Single(c=>c.Chave=="Convenio").Opcoes.First(o=>!string.IsNullOrEmpty(o.Valor));await ctl.AtualizarCampoAsync(d.Id,"Convenio",J(convenio.Valor));await ctl.AtualizarCampoAsync(d.Id,"Valor",J("145,50"));await ctl.AtualizarCampoAsync(d.Id,"VigenteDe",J("2030-01-01"));await ctl.AtualizarCampoAsync(d.Id,"VigenteAte",J("2030-12-31"));await ctl.ExecutarAcaoAsync(d.Id,"salvar");Exigir(await tarefa.WaitAsync(TimeSpan.FromSeconds(5))==true,"Preço não confirmou: "+p.Mensagem);using(var scope=sp.CreateScope())Exigir((await scope.ServiceProvider.GetRequiredService<PrecoConvenioService>().CatalogoAsync()).Any(x=>x.ConvenioCodigo==p.Convenio!.Codigo&&x.Valor==145.50m&&x.VigenteDe==new DateOnly(2030,1,1)&&x.VigenteAte==new DateOnly(2030,12,31)),"Preço não persistiu vigência");
  try
  {
   sessao.Entrar(new UsuarioSistema{Id=usuarioId,Nome="Acesso restrito fictício",Login="restrito.qa",Perfil=PerfilAcesso.Recepcao,PermissoesNegadas=PerfisAcesso.Todas});
   using var restrito=new DialogosWebController(GerenteWebRegistro.CriarDialogos());foreach(var item in new (string,object)[]{("UsuarioWindow",u),("MetaWindow",m),("PrecoConvenioWindow",p),("SenhaProvisoria",new SenhaProvisoriaWebViewModel("Fictício"))}){bool negou=false;try{await restrito.AbrirAsync(item.Item1,item.Item2).WaitAsync(TimeSpan.FromSeconds(1));}catch(InvalidOperationException){negou=true;}Exigir(negou,"Permissão não bloqueou "+item.Item1);}
  }
  finally{sessao.Entrar(original);}
  Console.WriteLine("OK Gerente gravação real: usuário+permissão+troca obrigatória, cancelamento de edição, meta decimal/mês/observação e preço/vigência; quatro formulários negam acesso sem permissão.");
 }
}
