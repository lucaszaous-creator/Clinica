using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using D=Clinica.Desktop.Shell.Web.DialogosWebController;
using P=Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Desktop.Shell.Web;
public static partial class RegistroCompartilhadoWeb
{
    private static string RotuloEnfermagem(string texto)=>texto switch {
        "Sistolica"=>"Pressão sistólica","Diastolica"=>"Pressão diastólica","Cardiaca"=>"Frequência cardíaca","Respiratoria"=>"Frequência respiratória","Saturacao"=>"Saturação de oxigênio","Data Do Atendimento"=>"Data do atendimento","Consulta Completa"=>"Consulta completa","Intercorrencia"=>"Intercorrência","Alergia Observada"=>"Alergia observada","Texto"=>"Evolução de enfermagem","Historico"=>"Histórico e antecedentes","Acesso Local"=>"Local do acesso","Acesso Calibre"=>"Calibre do acesso","Acesso Puncionado Em"=>"Data da punção","Exame Fisico"=>"Exame físico","Avaliacao"=>"Avaliação","Diagnosticos"=>"Diagnósticos de enfermagem","Titulo"=>"Descrição","Relacionado A"=>"Relacionado a","Evidenciado Por"=>"Evidenciado por","Resultado Esperado"=>"Resultado esperado","Descricao"=>"Descrição","Frequencia"=>"Frequência","Se Necessario"=>"Se necessário (SOS)","Etapas Em Falta"=>"Etapas pendentes","Campos Personalizados"=>"Campos complementares da clínica","Rotulo"=>"Campo","Eva Antes"=>"EVA antes","Eva Depois"=>"EVA depois","Retorno Sugerido Em"=>"Retorno sugerido","Dica Rodape"=>"Orientação","Selo Detalhe"=>"Campos complementares","Nome Arquivo"=>"Arquivo","Abrir Detalhe"=>"Campos complementares","Aviso Modalidade Enfermagem"=>"Modalidade da sessão","Sinais Vitais"=>"Sinais vitais",_=>texto};
    private static D.Definicao DefinicaoEnfermagem(string titulo,P.Secao s)
    {
        D.Campo Campo(P.Campo c)=>new(c.Propriedade,c.Propriedade,RotuloEnfermagem(c.Rotulo),c.Tipo,c.Opcoes,c.RotuloOpcao??"Rotulo",c.Visivel,c.Guarda,Formato:c.Formato,ValorOpcao:c.ValorOpcao);
        D.Acao Acao(P.Acao a)=>new(a.Comando,RotuloEnfermagem(a.Rotulo),a.Comando+"Command",a.Estilo,a.Guarda?.Replace("vm:",""));
        return new(titulo,null,s.Campos.Select(Campo).Concat(s.Indicadores.Select(i=>{var p=i.Split('|');return new D.Campo(p[0],p[0],RotuloEnfermagem(p.Length>1?p[1]:p[0]),"leitura");})).DistinctBy(c=>c.Chave).ToArray(),s.Acoes.Select(Acao).ToArray(),
            s.Tabelas.Select(t=>new D.Tabela(t.Chave,RotuloEnfermagem(t.Titulo),t.Origens[0],t.Colunas.Select(c=>new D.Coluna(c.Propriedade,RotuloEnfermagem(c.Rotulo))).ToArray(),t.Acoes.Select(Acao).ToArray(),t.Campos.Select(Campo).ToArray())).ToArray());
    }
    public static IEnumerable<D.RegistroDialogo> DialogosEnfermagemCompletos()
    {
        yield return new("LeituraEvolucaoEnfermagem", typeof(Clinica.Application.Modelos.LeituraEvolucaoEnfermagem),
            new("Evolução de enfermagem", null,
                [new("Paciente", "Paciente", "Paciente", "leitura"), new("Texto", "Texto", "Registro completo", "texto-rico-leitura")], [], []), Permissao.VerProntuario);
        var escrever=EscreverSessaoCompleta();
        escrever=escrever with {Tabelas=escrever.Tabelas.Select(t=>t.Chave=="CamposPersonalizados"?t with {Campos=[
            new("RespostaTextoWeb","Resposta","texto",Visivel:"EhCaixaDeTexto"),new("RespostaListaWeb","Resposta","selecao",Opcoes:"Opcoes",Visivel:"EhLista"),new("RespostaSimNaoWeb","Resposta","selecao",Opcoes:"OpcoesSimNaoWeb",Visivel:"EhSimNao")]}:t).ToArray()};
        yield return new("EscreverSessao",typeof(EscreverSessaoViewModel),DefinicaoEnfermagem("Escrever sessão",escrever),Permissao.EditarProntuario);
        var passagem=PassagemCompleta();passagem=passagem with {Acoes=[..passagem.Acoes,new("AbrirConsultaWeb","Abrir consulta de enfermagem",Guarda:"PodeRegistrar")]};
        yield return new("EvolucaoEnfermagem",typeof(EvolucaoEnfermagemViewModel),DefinicaoEnfermagem("Evolução de enfermagem",passagem),Permissao.RegistrarEvolucaoEnfermagem);
        var consulta=ConsultaCompleta();consulta=consulta with {Acoes=[..consulta.Acoes,new("CatalogoDiagnosticosWeb","Catálogo de diagnósticos"),new("CatalogoCuidadosWeb","Catálogo de cuidados")]};
        yield return new("ConsultaDeEnfermagem",typeof(EvolucaoEnfermagemViewModel),DefinicaoEnfermagem("Consulta de enfermagem",consulta) with {Descricao="As alterações entram no rascunho da passagem. Feche este formulário e use Registrar na passagem para gravar."},Permissao.RegistrarEvolucaoEnfermagem);
        yield return new("EscolherSessaoEnfermagem",typeof(EscolherSessaoEnfermagemWebViewModel),new("Vincular à sessão original",null,[new("Paciente","Paciente","Paciente","leitura"),new("Selecionada","Selecionada","Sessão original","selecao","Opcoes")],[new("Confirmar","Vincular à sessão","ConfirmarCommand","primario")],[]),Permissao.RegistrarEvolucaoEnfermagem);
        yield return new("CatalogoEnfermagem",typeof(CatalogoDeEnfermagem),new("Catálogo de enfermagem",null,[new("Titulo","Titulo","Catálogo","leitura"),new("Explicacao","Explicacao","Orientação","leitura"),new("Busca","Busca","Buscar no catálogo"),new("Resumo","Resumo","Resultados","leitura")],[],[new("Itens","Itens do catálogo","Itens",[new("Codigo","Código"),new("Titulo","Diagnóstico ou cuidado"),new("Detalhe","Detalhes")],[new("Adicionar","Adicionar ao plano","AdicionarCommand")])]),Permissao.RegistrarEvolucaoEnfermagem);
        yield return new("DetalheSessao",typeof(EscreverSessaoViewModel),new("Campos complementares da sessão",null,
            new[]{("QueixaPrincipal","Queixa principal"),("HistoriaDoencaAtual","História da doença atual"),("ExameFisico","Exame físico"),("HipoteseDiagnostica","Hipótese diagnóstica"),("CidSessao","CID"),("Conduta","Conduta"),("Orientacoes","Orientações"),("PlanoTerapeutico","Plano terapêutico"),("RetornoSugeridoNota","Retorno sugerido — observações"),("Encaminhamento","Encaminhamento")}.Select(c=>new D.Campo(c.Item1,c.Item1,c.Item2,"textarea")).ToArray(),[new("BuscarCid","Buscar CID","BuscarCidCommand")],[]),Permissao.EditarProntuario);
    }
}
