using Clinica.Gerente.ViewModels;
using Clinica.Domain.Entities;
using D=Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Gerente.Web;
public static partial class GerenteWebRegistro
{
 public static IEnumerable<D.RegistroDialogo> CriarDialogos()
 {
  yield return new("SenhaProvisoria",typeof(SenhaProvisoriaWebViewModel),new("Redefinir senha",null,[new("Senha","Senha","Senha provisória","senha")],[new("confirmar","Definir senha","ConfirmarCommand","primario")],[]),Permissao.GerenciarUsuarios);
  yield return new("UsuarioWindow",typeof(UsuarioEdicaoViewModel),new("Usuário",null,[
   new("Nome","Nome","Nome",Maximo:120),new("Login","Login","Login",Habilitado:"EhNovo",Maximo:60),
   new("PerfilSelecionado","PerfilSelecionado","Perfil","selecao","Perfis"),new("Profissional","Profissional","Profissional vinculado","selecao","Profissionais","Nome"),
   new("CpfProfissional","CpfProfissional","CPF do profissional",Habilitado:"PodeEditarCpf",Maximo:14),new("CpfDica","CpfDica","Orientação do CPF","leitura"),
   new("Senha","Senha","Senha (em branco mantém a atual)","senha"),new("DeveTrocarSenha","DeveTrocarSenha","Exigir troca de senha no próximo acesso","booleano"),new("Ativo","Ativo","Ativo","booleano"),new("ResumoExcecoes","ResumoExcecoes","Exceções ao perfil","leitura")
  ],[new("salvar","Salvar usuário","SalvarCommand","primario")],[new("permissoes","Permissões individuais","Permissoes",[new("Assunto","Assunto"),new("Rotulo","Permissão"),new("Explicacao","O que permite"),new("Procedencia","Origem da decisão")],[],[new("Marcada","Marcada","Conceder","booleano")])]),Permissao.GerenciarUsuarios,vm=>((UsuarioEdicaoViewModel)vm).Inicializacao);
  yield return new("MetaWindow",typeof(MetaEdicaoViewModel),new("Definir meta",null,[
   new("Mes","Mes","Mês","selecao","Meses"),new("Ano","Ano","Ano","numero"),new("Indicador","Indicador","Indicador","selecao","Indicadores"),new("Valor","Valor","Meta"),new("UnidadeAtual","UnidadeAtual","Unidade","leitura"),new("Profissional","Profissional","Profissional (clínica inteira quando não definido)","selecao","Profissionais","Nome"),new("Observacoes","Observacoes","Observações","textarea")
  ],[new("salvar","Salvar meta","SalvarCommand","primario")],[]),Permissao.DefinirMetas,vm=>((MetaEdicaoViewModel)vm).Inicializacao);
  yield return new("PrecoConvenioWindow",typeof(PrecoEdicaoViewModel),new("Preço de convênio",null,[
   new("Convenio","Convenio","Convênio","selecao","Convenios","Nome"),new("Tipo","Tipo","Procedimento","selecao","Tipos"),new("Especialidade","Especialidade","Especialidade (vazio: qualquer)","selecao","Especialidades"),new("Valor","Valor","Valor por guia",Maximo:14),new("VigenteDe","VigenteDe","Vigente desde","data"),new("VigenteAte","VigenteAte","Vigente até (opcional)","data"),new("Ativo","Ativo","Ativo","booleano")
  ],[new("salvar","Salvar preço","SalvarCommand","primario")],[]),Permissao.EditarFinanceiro,vm=>((PrecoEdicaoViewModel)vm).Inicializacao);
 }
}
