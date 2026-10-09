using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using static Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Desktop.Shell.Web;
public static partial class RegistroCompartilhadoWeb
{
    private static RegistroDialogo AprimorarDialogoClinico(RegistroDialogo registro)
    {
        var d=registro.Definicao;
        if(registro.Chave=="InfusaoExterna")
            return registro with {Definicao=d with {Titulo="Registro de infusão externa",Campos=d.Campos.Select(c=>c with {Maximo=c.Caminho switch {"Texto"=>20000,"Orientacao"=>2000,"Diluente"=>120,"Volume" or "Tempo"=>60,_=>c.Maximo}}).ToArray()}};
        if(registro.Chave=="SessaoDoProntuario")
            return registro with {Permissao=Permissao.VerProntuario,Definicao=d with {Titulo="Sessão do prontuário",Acoes=[..d.Acoes,new("CopiarWeb","Copiar registro","CopiarWebCommand",Habilitado:"PodeCopiar")],Tabelas=d.Tabelas.Select(t=>t.Chave=="Blocos"?t with {Campos=[new("Texto","Texto","Registro clínico","texto-rico-leitura",Formato:"Formato")]}:t).ToArray()}};
        if(registro.Chave=="VersoesEvolucao")
            return registro with {Permissao=Permissao.VerProntuario,Definicao=d with {Titulo="Histórico de correções",Tabelas=d.Tabelas.Select(t=>t.Chave=="Versoes"?t with {Colunas=[..t.Colunas,new("Vigente","Versão em vigor")],Campos=[new("Evolucao","Evolucao","Evolução registrada","texto-rico-leitura",Formato:"EvolucaoFormatada")]}:t).ToArray()}};
        if(registro.Chave=="FolhaExecucao")
            return registro with {Definicao=d with {Titulo="Execução da infusão",Tabelas=d.Tabelas.Select(t=>t.Chave=="Alertas"?t with {Colunas=[new(".","Alerta clínico")]}:t).ToArray()}};
        if(registro.Chave=="DocumentoOpcoes")
            return registro with {Definicao=d with {Titulo="Dados e modelos do documento",Campos=[..d.Campos.Where(c=>c.Caminho is not ("PreviaModelo" or "Profissional")),new("Profissional","Profissional","Profissional responsável","selecao","Profissionais","Nome"),new("Observacoes","Observacoes","Observações impressas","texto-rico",Formato:"ObservacoesFormatadas"),new("BuscaModeloWeb","BuscaModeloWeb","Buscar modelo pelo nome"),new("ModeloSelecionado","ModeloSelecionado","Modelo","selecao","ModelosDisponiveisWeb","Nome"),new("PreviaModelo","PreviaModelo","Prévia do modelo","leitura")]}};
        if(registro.Chave=="Documento")
            return registro with {Definicao=d with {Campos=[..d.Campos.Select(c=>c.Caminho=="ModeloSelecionado"?c with {Opcoes="ModelosDisponiveisWeb"}:c),new("BuscaModeloWeb","BuscaModeloWeb","Buscar modelo pelo nome"),new("EnderecoDaReceita","EnderecoDaReceita","Endereço na receita","leitura"),new("ResponsavelDocumento","ResponsavelDocumento","Responsável pelo documento","leitura")]}};
        return registro;
    }
}
