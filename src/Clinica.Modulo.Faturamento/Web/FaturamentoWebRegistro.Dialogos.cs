using Clinica.Desktop.ViewModels;
using Clinica.Domain.Entities;
using D = Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Faturamento.Web;
public static partial class FaturamentoWebRegistro
{
    private static D.Campo C(string p,string rotulo,string tipo="texto",string? opcoes=null,string? visivel=null,string? guarda=null) => new(p,p,rotulo,tipo,opcoes,Visivel:visivel,Habilitado:guarda);
    private static D.Acao Confirmar(string rotulo="Confirmar",string? guarda=null) => new("confirmar",rotulo,"ConfirmarCommand","primario",guarda);
    private static D.Coluna[] Cols(string colunas) => colunas.Split(';').Select(c=>{var a=c.Split('|');return new D.Coluna(a[0],a[1]);}).ToArray();
    private static D.RegistroDialogo R<T>(string titulo,D.Campo[] campos,D.Acao[]? acoes=null,D.Tabela[]? tabelas=null,Permissao permissao=Permissao.VerFaturamento,string? chave=null,Func<object,Task>? abrir=null) => new(chave??typeof(T).Name,typeof(T),new(titulo,null,campos,acoes??[Confirmar()],tabelas??[]),permissao,abrir);
    public static IEnumerable<D.RegistroDialogo> CriarDialogos()
    {
        yield return R<AvisoPendenciasFaturamento>("Aviso de pendências",[],[new("painel","Ver painel de pendências","VerPainelCommand","primario")],[new("pendencias","Guias pendentes","Linhas",Cols("PacienteNome|Paciente;ConvenioNome|Convênio;Tipo|Código;Ordem|Ordem;DataPrevista|Prevista;DiasEmAtraso|Dias em atraso;ObservacaoPendencia|Observação"),[])]);
        yield return R<ObservacaoFaturamento>("Observação da pendência",[C("Texto","Observação","textarea")],[Confirmar("Salvar observação")]);
        yield return R<NaoConformidadeFaturamento>("Não conformidade",[C("Justificativa","Justificativa obrigatória","textarea")],permissao:Permissao.MarcarNaoConformidade);
        yield return R<BaixaViewModel>("Dar baixa na guia",[C("PacienteNome","Paciente","leitura"),C("Codigo.Tipo","Código","leitura"),C("Codigo.Ordem","Ordem","leitura"),C("Codigo.DataPrevistaFaturamento","Data prevista","leitura"),C("Codigo.FormaObtencao","Como obter","leitura"),C("ObservacaoPendencia","Observação da pendência","leitura"),C("DataBaixa","Data da baixa","data"),C("NumeroGuia","Número da guia"),C("DicaNumeroGuia","Formato aceito","leitura"),C("CriticaNumeroGuia","Validação do número","leitura"),C("Observacao","Observação da baixa","textarea")],[Confirmar("Confirmar baixa","PodeConfirmar")],permissao:Permissao.BaixarGuia,chave:"FaturamentoBaixa");
        yield return R<BaixaLoteFaturamento>("Baixa em lote",[C("Data","Data da baixa","data")],tabelas:[new("guias","Guias selecionadas","Linhas",Cols("Descricao|Guia;Situacao|Situação;Dica|Formato"),[],[C("NumeroGuia","Número da guia")])],permissao:Permissao.BaixarGuia);
        yield return R<RodadaFaturamento>("Rodar pendências",[C("Data","Data da baixa","data")],tabelas:[new("guias","Decisão por guia","Linhas",Cols("Descricao|Guia;Situacao|Situação;Dica|Formato"),[],[C("NumeroGuia","Número para baixa"),C("Justificativa","Justificativa de não conformidade","textarea")])]);
        yield return R<EnvioFaturamento>("Marcar lote enviado",[C("Data","Data do envio","data"),C("Protocolo","Protocolo da operadora")],permissao:Permissao.GerenciarLotesTiss);
        yield return R<GlosaFaturamento>("Registrar glosa",[C("Data","Data da glosa","data"),C("Selecionado","Motivo ANS","selecao","Motivos"),C("Motivo","Complemento do motivo","textarea"),C("Significado","O que significa","leitura"),C("ComoEvitar","Como evitar","leitura"),C("Lastro","Documentação para recurso","leitura")],[new("guia","Entenda os motivos","AbrirGuiaCommand"),Confirmar("Registrar glosa")],permissao:Permissao.RegistrarGlosa);
        yield return R<GuiaGlosasFaturamento>("Por que as guias são glosadas",[C("Busca","Buscar motivo"),C("Selecionado","Motivo ANS","selecao","Motivos"),C("Significado","O que significa","leitura"),C("ComoEvitar","Como evitar","leitura"),C("Lastro","Documentação para recurso","leitura"),C("Recurso","Viabilidade do recurso","leitura")],[]);
        yield return R<RetornoFaturamento>("Registrar retorno do lote",[C("Data","Data do retorno","data"),C("Observacao","Observação","textarea")],tabelas:[new("guias","Retorno por guia","Linhas",Cols("Paciente|Paciente;Tipo|Código;NumeroGuia|Número da guia"),[],[C("Glosada","Glosada","booleano"),C("Motivo","Motivo ANS","selecao","Motivos",guarda:"Glosada"),C("Complemento","Complemento","texto",guarda:"Glosada")])],permissao:Permissao.GerenciarLotesTiss);
        yield return R<HistoricoFaturamento>("Histórico da guia",[],[],[new("historico","Movimentações","Linhas",Cols("Quando|Data e hora;Operador|Operador;Acao|Ação;Detalhe|Detalhe"),[])],Permissao.VerAuditoria,abrir:vm=>((HistoricoFaturamento)vm).CarregarAsync());
        yield return R<ConvenioEdicao>("Configurar convênio",[
            C("Nome","Nome"),C("Familia","Família","selecao","Familias",guarda:"FamiliaEditavel"),C("Ativo","Ativo","booleano"),C("GeraGuia","Gera guia para faturar","booleano"),C("FormatoNumeroGuia","Formato do número","selecao","Formatos"),C("RegistroAnsOperadora","Registro ANS da operadora"),
            C("FazEletro","Faz eletro","booleano",visivel:"EhPersonalizado"),C("TemSegundoCodigo","Tem segundo código","booleano",visivel:"EhPersonalizado"),C("FormaSegundoCodigo","Obtenção do segundo código","selecao","Formas","EhPersonalizado"),C("SegundoCodigoDependeApp","Segundo código depende do aplicativo","booleano",visivel:"EhPersonalizado"),C("DiasSegundoCodigo","Dias para o segundo código","numero",visivel:"EhPersonalizado"),C("FaturaBsv","Fatura BSV","booleano",visivel:"EhPersonalizado"),C("InverteDatasBsv","Inverter datas BSV","booleano",visivel:"EhPersonalizado"),C("ValidadeConsultaDias","Validade da consulta (dias; vazio sem prazo)","numero",visivel:"EhPersonalizado"),C("CategoriaComApp","Categoria com aplicativo","selecao","Categorias","EhPersonalizado"),C("CategoriaSemApp","Categoria sem aplicativo","selecao","Categorias","EhPersonalizado")
        ],[],permissao:Permissao.ConfigurarFaturamento,chave:"FaturamentoConvenio");
        yield return R<ParametrosViewModel>("Regras por família — salve na página de configurações",[],[],[new("familias","Regras de faturamento","Itens",Cols("Convenio|Família"),[],[C("ValidadeConsultaDias","Validade da consulta (dias)","numero"),C("DiasSegundoCodigo","Dias para o segundo código","numero"),C("CategoriaComApp","Categoria com aplicativo","selecao"),C("CategoriaSemApp","Categoria sem aplicativo","selecao")])],Permissao.ConfigurarFaturamento,chave:"FaturamentoRegrasFamilia");
    }
}
public sealed class RegistroFaturamentoWeb : Clinica.Desktop.Shell.Web.IRegistroModuloWeb
{
    public IEnumerable<Clinica.Desktop.Shell.Web.PaginasWebController.Pagina> Paginas()=>FaturamentoWebRegistro.CriarPaginas();
    public IEnumerable<D.RegistroDialogo> Dialogos()=>FaturamentoWebRegistro.CriarDialogos();
}
