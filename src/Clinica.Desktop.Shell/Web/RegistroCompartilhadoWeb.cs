using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using static Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Desktop.Shell.Web;

public static partial class RegistroCompartilhadoWeb
{
    private static Campo C(string p,string rotulo,string tipo="texto",string? opcoes=null,string? visivel=null,string? formato=null)=>new(p,p,rotulo,tipo,opcoes,Visivel:visivel,Formato:formato);
    private static Acao A(string comando,string rotulo)=>new(comando,rotulo,comando+"Command");
    public static IEnumerable<RegistroDialogo> Dialogos()
    {
        foreach(var d in DialogosExtraidos().Where(d=>d.Chave is not ("EvolucaoEnfermagem" or "ConsultaDeEnfermagem" or "MateriaisProcedimento")))yield return AprimorarDialogoClinico(d);
        foreach(var d in DialogosEnfermagemCompletos())yield return d;
        foreach(var d in DialogosPacotesCompartilhados.CriarDialogos())yield return d;
        foreach(var d in DialogosCadastro())yield return d;
        foreach(var d in DialogosAssinatura())yield return d;
        yield return new("TrocaSenha",typeof(TrocaSenhaWebViewModel),new("Trocar minha senha",null,[C("Atual","Senha atual","senha"),C("Nova","Nova senha","senha"),C("Repetida","Repita a nova senha","senha")],[A("Salvar","Trocar senha")],[]));
        yield return new("EscolherPaciente",typeof(EscolherPacienteWebViewModel),new("Escolher paciente",null,[C("Seletor.Termo","Buscar por nome ou CPF"),C("Seletor.Selecionado","Paciente","selecao","Seletor.Resultados"),C("Seletor.Erro","Erro da busca","leitura")],[A("Confirmar","Escolher")],[]),AoAbrir:vm=>((EscolherPacienteWebViewModel)vm).Seletor.BuscarAsync(imediato:true));
        yield return new("BuscaCid",typeof(BuscaCidViewModel),new("Procurar CID-10","Catálogo desta clínica; o campo original continua aceitando outros códigos.",[C("Termo","Buscar"),C("Selecionado","Código","selecao","Resultados"),C("Resumo","Resultado","leitura")],[A("Escolher","Escolher"),A("Limpar","Limpar busca")],[]));
        yield return new("EscolherCertificado",typeof(CertificadoWebViewModel),new("Assinar com certificado A1",null,[C("NomeArquivo","Arquivo","leitura"),C("Senha","Senha do A1","senha"),new("Certificado.Selecionado","Certificado.Selecionado","Certificado","selecao","Certificado.Certificados","Titular"),C("Certificado.Mensagem","Resultado","leitura")],[A("EscolherArquivo","Escolher arquivo A1"),A("Carregar","Carregar A1"),A("Certificado.Confirmar","Assinar")],[]));
        yield return new("EscolhaDeConvenio",typeof(EscolhaDeConvenioViewModel),new("Escolher convênio",null,[C("PacienteNome","Paciente","leitura"),new("Selecionado","Selecionado","Convênio","selecao","Convenios","Nome"),C("Carteirinha","Carteirinha"),C("ValidadeCarteirinha","Validade","data")],[A("Vincular","Vincular convênio")],[]));
        var documento = new RegistroDialogo("Documento",typeof(DocumentoEdicaoViewModel),new("Emitir documento",null,
        [C("Subtitulo","Paciente","leitura"),C("TipoSelecionado","Tipo de documento","selecao","Tipos"),new("Profissional","Profissional","Profissional responsável","selecao","Profissionais","Nome"),C("Data","Data","data"),C("Titulo","Título"),C("Corpo","Conteúdo","texto-rico",formato:"CorpoFormatado"),C("Observacoes","Observações","texto-rico",formato:"ObservacoesFormatadas"),C("DiasAfastamentoTexto","Dias de afastamento",visivel:"MostraAtestado"),C("Cid","CID"),C("CidAutorizado","Paciente autoriza informar CID","booleano"),C("AlergiaConferida","Conferi as colisões com alergias","booleano",visivel:"ColideComAlergia"),C("PeriodoInicio","Início","data",visivel:"MostraAtestado"),C("PeriodoFim","Fim","data",visivel:"MostraAtestado"),C("HoraChegadaTexto","Chegada",visivel:"MostraComparecimento"),C("HoraSaidaTexto","Saída",visivel:"MostraComparecimento"),new("ModeloSelecionado","ModeloSelecionado","Modelo","selecao","Modelos","Nome"),C("PreviaModelo","Prévia do modelo","leitura")],
        [new Acao("Emitir","Emitir e abrir PDF","EmitirCommand","primario"),new Acao("AdicionarItem","Adicionar item","AdicionarItemCommand",Visivel:"MostraItens"),A("BuscarCid","Procurar CID"),A("AplicarModelo","Aplicar modelo"),A("AtualizarModelo","Atualizar modelo"),A("ExcluirModelo","Excluir modelo"),A("PedirNomeModelo","Salvar como modelo")],
        [new("itens","Itens do documento","Itens",[],[A("RemoverItem","Remover item")],[C("Descricao","Descrição","texto-rico",formato:"DescricaoFormatada"),C("Quantidade","Quantidade"),C("Detalhe","Detalhe","texto-rico",formato:"DetalheFormatado")]),
         new("alertas","Alertas clínicos","AlertasClinicos",[new(".","Alerta")],[]),new("alergias","Colisões com alergias","ColisoesAlergia",[new(".","Alerta")],[]),new("exigencias","Conferência legal","ExigenciasLegais",[new(".","Exigência")],[])]));
        yield return AprimorarDialogoClinico(documento);
    }
}
