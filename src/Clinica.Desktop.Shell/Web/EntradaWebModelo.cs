using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Desktop.Shell.Web;

/// <summary>Porta fixa anterior à sessão: somente autenticar, criar primeiro acesso, trocar senha provisória e configurar conexão.</summary>
public sealed class EntradaWebModelo
{
 private readonly IServiceScopeFactory? _escopos;
 private UsuarioSistema? _autenticado;
 private string? _conexaoValidada;
 public string Modo {get;private set;}
 public string NomeApp {get;}
 public string Mensagem {get;private set;}="";
 public bool Erro {get;private set;}
 public bool Ocupado {get;private set;}
 public bool PodeSalvar=>_conexaoValidada is not null && !Ocupado;
 public int LimparSenhas {get;private set;}
 public UsuarioSistema? Usuario {get;private set;}
 public bool ConexaoSalva {get;private set;}
 public bool Aceitou {get;private set;}
 public event Action? Mudou;
 public event Action? Concluiu;
 public EntradaWebModelo(IServiceScopeFactory? escopos,string nomeApp,string modo,string mensagem=""){_escopos=escopos;NomeApp=nomeApp+(EdicaoDeTeste.Ativa?" — edição de teste":"");Modo=modo;Mensagem=mensagem;}
 public object Estado()=>new{tipo="estado",modo=Modo,nomeApp=NomeApp,mensagem=Mensagem,erro=Erro,ocupado=Ocupado,podeSalvar=PodeSalvar,limparSenhas=LimparSenhas};
 public async Task ExecutarAsync(string acao,JsonElement dados)
 {
  if(Ocupado)return;
  if(acao=="invalidar" && Modo=="conexao"){_conexaoValidada=null;Mensagem="";Mudou?.Invoke();return;}
  if(acao is not ("confirmar" or "testar" or "salvar"))throw new InvalidOperationException("Ação não permitida na entrada.");
  static string Texto(JsonElement d,string p,int max=4096){var texto=d.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";return texto.Length<=max?texto:throw new InvalidOperationException("Campo maior que o permitido.");}
  Ocupado=true;Mensagem="";Erro=false;Mudou?.Invoke();
  try
  {
   if(Modo is "aviso" or "pergunta"){if(acao!="confirmar")throw new InvalidOperationException("Ação não permitida.");Aceitou=true;Concluiu?.Invoke();return;}
   if(Modo=="conexao")
   {
    var entrada=Texto(dados,"conexao",8192);
    if(string.IsNullOrWhiteSpace(entrada))throw new InvalidOperationException("Informe a conexão PostgreSQL ou a URL da Neon.");
    var conexao=ConexaoStore.Normalizar(entrada);
    if(acao=="testar")
    {
     _conexaoValidada=null;var(ok,mensagem)=await ConexaoStore.TestarAsync(conexao);
     if(!ok)throw new InvalidOperationException("Não foi possível conectar: "+mensagem);
     _conexaoValidada=conexao;Mensagem="Conexão bem-sucedida. Salve para continuar.";
    }
    else if(acao=="salvar")
    {
     if(_conexaoValidada is null || !string.Equals(conexao,_conexaoValidada,StringComparison.Ordinal))throw new InvalidOperationException("Teste esta conexão antes de salvar.");
     ConexaoStore.Salvar(_conexaoValidada);ConexaoSalva=true;Concluiu?.Invoke();
    }
    else throw new InvalidOperationException("Teste a conexão antes de continuar.");
    return;
   }
   if(acao!="confirmar" || _escopos is null)throw new InvalidOperationException("Ação não permitida.");
   using var scope=_escopos.CreateScope();var acesso=scope.ServiceProvider.GetRequiredService<AcessoService>();
   var senha=Texto(dados,"senha");
   if(Modo=="entrar")
   {
    var resultado=await acesso.AutenticarAsync(Texto(dados,"login",120),senha);
    if(!resultado.Sucesso){LimparSenhas++;throw new InvalidOperationException(resultado.Erro??"Não foi possível entrar.");}
    Concluir(resultado.Usuario!);
   }
   else if(Modo=="primeiro")
   {
    if(senha!=Texto(dados,"repetida"))throw new InvalidOperationException("As duas senhas não são iguais.");
    if(await acesso.ExisteUsuarioAtivoAsync()){Modo="entrar";LimparSenhas++;Mensagem="Um usuário já foi cadastrado em outro computador. Entre com ele.";return;}
    Concluir(await acesso.CriarAsync(Texto(dados,"nome",120),Texto(dados,"login",120),senha,PerfilAcesso.Gerente,operador:"primeiro acesso"));
   }
   else if(Modo=="trocar" && _autenticado is not null)
   {
    if(senha!=Texto(dados,"repetida"))throw new InvalidOperationException("As duas senhas não são iguais.");
    await acesso.DefinirSenhaAsync(_autenticado.Id,senha,deveTrocar:false,operador:_autenticado.Login);_autenticado.DeveTrocarSenha=false;Usuario=_autenticado;LimparSenhas++;Concluiu?.Invoke();
   }
   else throw new InvalidOperationException("Estado de entrada inválido.");
  }
  catch(Exception ex){Erro=true;Mensagem=ex.GetBaseException().Message;}
  finally{Ocupado=false;Mudou?.Invoke();}
 }
 private void Concluir(UsuarioSistema usuario)
 {
  LimparSenhas++;
  if(usuario.DeveTrocarSenha){_autenticado=usuario;Modo="trocar";return;}
  Usuario=usuario;Concluiu?.Invoke();
 }
 public void Encerrar(){_autenticado=null;_conexaoValidada=null;}
}
