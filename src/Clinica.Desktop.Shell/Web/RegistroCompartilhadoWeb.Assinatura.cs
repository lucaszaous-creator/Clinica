using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using static Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Desktop.Shell.Web;
public static partial class RegistroCompartilhadoWeb
{
 public static IEnumerable<RegistroDialogo> DialogosAssinatura()
 {
  yield return new("EscolherTermo",typeof(EscolherTermoViewModel),new("Qual termo o paciente vai assinar?",null,[new("PacienteNome","PacienteNome","Paciente","leitura"),new("Selecionado","Selecionado","Termo","selecao","Modelos","Nome")],[new("escolher","Escolher termo","EscolherCommand","primario","PodeEscolher")],[]),Permissao.ColherAssinaturaPaciente);
  yield return new("EscolherSessaoDoTermo",typeof(EscolherSessaoDoTermoViewModel),new("A qual sessão este termo pertence?",null,[new("PacienteNome","PacienteNome","Paciente","leitura"),new("NomeDoTermo","NomeDoTermo","Termo","leitura"),new("Selecionada","Selecionada","Sessão ou termo avulso","selecao","Opcoes"),new("Selecionada.Detalhe","Selecionada.Detalhe","Detalhes da sessão","leitura")],[new("confirmar","Confirmar sessão","ConfirmarCommand","primario","PodeConfirmar")],[]),Permissao.ColherAssinaturaPaciente);
  yield return new("AssinaturaPaciente",typeof(AssinaturaPacienteViewModel),new("Assinatura do paciente",null,[
   new("PacienteNome","PacienteNome","Paciente","leitura"),new("Numero","Numero","Documento","leitura"),new("SessaoDoTermo","SessaoDoTermo","Sessão deste termo","leitura"),new("Corpo","Corpo","Leia o termo com o paciente","leitura"),
   new("AlergiasResumo","AlergiasResumo","Alergias — confirme com o paciente","leitura",Visivel:"PodeVerAlergias"),new("NovaAlergia","NovaAlergia","Alergia relatada agora",Visivel:"PodeRegistrarAlergia"),
   new("DocumentoConferido","DocumentoConferido","Documento de identidade conferido",Ajuda:"Registre o documento apresentado pelo paciente."),new("Testemunha","Testemunha","Testemunha da clínica","leitura"),new("SituacaoDaColeta","SituacaoDaColeta","Coleta","leitura"),
   new("TracoWeb","TracoWeb","Assinatura do paciente","assinatura",Visivel:"PodeAssinarLocal",Habilitado:"!Carregando",Maximo:2800000),new("ImagemRemota","ImagemRemota","Assinatura recebida do celular","imagem-leitura",Visivel:"TemTracoRemoto")
  ],[new("alergia","Registrar alergia","RegistrarAlergiaCommand",Habilitado:"PodeRegistrarAlergia",Visivel:"PodeRegistrarAlergia"),new("todas","Confirmar todas as declarações","ConfirmarTodasCommand"),new("celular","Enviar pelo celular","EnviarPeloCelularCommand",Habilitado:"!AguardandoCelular&!TemTracoRemoto",Visivel:"EnvioRemotoDisponivel"),new("cancelar-celular","Cancelar envio pelo celular","CancelarEnvioCelularCommand",Visivel:"AguardandoCelular"),new("limpar","Limpar assinatura","LimparTracoWebCommand",Habilitado:"!TemTracoRemoto&!AguardandoCelular"),new("recusar","Paciente recusou assinar","RecusarCommand",Habilitado:"PodeColher"),new("confirmar","Confirmar assinatura","ConfirmarWebCommand","primario","PodeConfirmar")],
  [new("declaracoes","Declarações do paciente","Declaracoes",[new("Ordem","Ordem"),new("Descricao","Declaração"),new("Detalhe","Orientação")],[],[new("Resposta","Resposta","Resposta do paciente","selecao","OpcoesResposta")])]),Permissao.ColherAssinaturaPaciente,vm=>((AssinaturaPacienteViewModel)vm).PrepararWebAsync());
 }
}
