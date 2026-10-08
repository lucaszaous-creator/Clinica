using Clinica.Clinico.ViewModels;
using Clinica.Domain.Entities;
using D = Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Clinico.Web;

/// <summary>Contrato fechado dos formulários clínicos; gravações continuam nas ViewModels.</summary>
public static partial class ClinicoWebRegistro
{
    private static D.Campo F(string caminho,string rotulo,string tipo="texto",string? opcoes=null,string rotuloOpcao="Rotulo",string? visivel=null,string? formato=null)
        => new(caminho,caminho,rotulo,tipo,opcoes,rotuloOpcao,Visivel:visivel,Formato:formato);
    private static D.Acao A(string comando,string rotulo,string estilo="secundario",string? habilitado=null)
        => new(comando,rotulo,comando+"Command",estilo,habilitado);
    private static D.Coluna C(string caminho,string rotulo)=>new(caminho,rotulo);
    public static IEnumerable<D.RegistroDialogo> CriarDialogos()
    {
        foreach (var d in DialogosComplementares()) yield return d;
        yield return new("ResultadoExame",typeof(ResultadoExameEdicaoViewModel),new("Registrar resultado de exame","O laudo permanece vinculado ao paciente e ao pedido selecionado.",
            [F("Paciente","Paciente","leitura"),F("DataNova","Data","data"),F("PedidoEscolhido","Pedido de exame","selecao","PedidosDoPaciente"),F("AvisoPedidos","Pedidos","leitura"),F("NomeNovo","Exame"),F("ValorNovo","Resultado"),F("UnidadeNova","Unidade"),F("ReferenciaNova","Referência"),F("LaboratorioNovo","Laboratório"),F("ObservacoesNovas","Observações","textarea"),F("LaudoEscolhido","Laudo selecionado","leitura")],
            [A("EscolherLaudo","Escolher laudo"),A("RemoverLaudo","Retirar arquivo selecionado"),A("Registrar","Registrar resultado","primario")],[]),Permissao.EditarProntuario);
        yield return new("Medida",typeof(MedidaEdicaoViewModel),new("Registrar medida",null,
            [F("Paciente","Paciente","leitura"),F("TipoNovo","O que foi medido","selecao","TiposRegistraveis","Nome"),F("AjudaDoTipo","Orientação","leitura"),F("DataNova","Data","data"),F("ValorNovo","Valor"),F("RotuloSegundoValor","Segundo valor","leitura",visivel:"TemSegundoValor"),F("ValorSecundarioNovo","Valor complementar",visivel:"TemSegundoValor"),F("ObservacoesNovas","Observações","textarea")],
            [A("Registrar","Registrar medida","primario")],[]),Permissao.EditarProntuario);
        yield return new("Problema",typeof(ProblemaEdicaoViewModel),new("Problema ou alerta clínico",null,
            [F("Natureza","Natureza","selecao","Naturezas"),F("ExplicacaoNatureza","Orientação","leitura"),F("Descricao","Descrição","textarea"),F("Cid","CID"),F("DescricaoCid","Descrição do CID","leitura"),F("Inicio","Início","data"),F("Observacoes","Observações","textarea")],
            [A("BuscarCid","Buscar CID"),A("Salvar","Salvar","primario")],[]),Permissao.EditarProntuario);
        yield return new("AplicarAvaliacao",typeof(AplicarAvaliacaoViewModel),new("Aplicar avaliação",null,
            [F("Titulo","Instrumento","leitura"),F("Janela","Período avaliado","leitura"),F("Fonte","Fonte","leitura"),F("Ressalva","Orientação","leitura"),F("Data","Data","data"),F("Observacoes","Observações","textarea"),F("Parcial","Resultado parcial","leitura")],
            [A("Salvar","Registrar avaliação","primario")],
            [new("Itens","Questões do instrumento","Itens",[C("Rotulo","Pergunta")],[],[F("Escolhida","Resposta","selecao","Opcoes")])]),Permissao.EditarProntuario);
        yield return new("AnexosSessao",typeof(AnexosSessaoViewModel),new("Anexos da sessão",null,
            [F("Sessao","Sessão","leitura"),F("LimiteTexto","Limites do arquivo","leitura")],
            [A("Anexar","Anexar arquivo","primario","PodeEditarProntuario")],
            [new("Anexos","Arquivos da sessão","Anexos",[C("NomeArquivo","Arquivo"),C("Resumo","Informações")],[A("Baixar","Salvar em disco"),A("Remover","Retirar anexo","perigo")])]),Permissao.VerProntuario);
        yield return new("ResumoProntuario",typeof(ResumoProntuarioViewModel),new("Prontuário do paciente",null,
            [F("Paciente","Paciente","leitura"),F("LinhaIdentificacao","Identificação","leitura"),F("DiagnosticosTexto","Diagnósticos","leitura"),F("PlanoTerapeutico","Plano terapêutico","leitura"),F("AlergiasTexto","Alergias","leitura")],
            [A("AbrirCompleto","Abrir prontuário completo"),A("SegundaVia","Segunda via da anamnese",habilitado:"TemFolhaDeAnamnese")],
            [new("Evolucoes","Evoluções","Evolucoes",[C("DataTexto","Data"),C("Autor","Profissional"),C("Texto","Evolução")],[A("AbrirSessao","Abrir sessão")]),
             new("Anamnese","Anamnese","Anamnese",[C("Rotulo","Campo"),C("Texto","Registro")],[]),
             new("Anexos","Anexos","Anexos",[C("NomeArquivo","Arquivo"),C("Contexto","Contexto")],[])]),Permissao.VerProntuario);
        yield return new("ResultadosDoPedido",typeof(ResultadosDoPedidoViewModel),new("Resultados do pedido",null,
            [F("Paciente","Paciente","leitura"),F("ExameRotulo","Exame","leitura"),F("PedidoTexto","Pedido","leitura")],[A("AbrirNoPaciente","Abrir no paciente")],
            [new("Resultados","Resultados registrados","Resultados",[C("DataTexto","Data"),C("Nome","Exame"),C("Laboratorio","Laboratório"),C("Valor","Resultado"),C("Referencia","Referência"),C("Observacoes","Observações"),C("ArquivoNome","Laudo")],[A("AbrirLaudo","Abrir laudo")])]),Permissao.VerProntuario);
        yield return new("PrescricaoInterna",typeof(PrescricaoInternaEdicaoViewModel),new("Prescrição de infusão","Revise o preparo, os medicamentos e os alertas antes de liberar.",
            [F("Paciente","Paciente","leitura"),F("Numero","Prescrição","leitura"),F("AlertasTexto","Alertas clínicos","leitura"),F("Indicacao","Indicação","texto-rico",formato:"IndicacaoFormatada"),F("BuscaModeloWeb","Buscar modelo pelo nome"),F("ModeloSelecionado","Modelo salvo","selecao","ModelosDisponiveisWeb","Nome"),F("PreviaModelo","Prévia do modelo","leitura"),F("Observacoes","Observações","texto-rico",formato:"ObservacoesFormatadas"),F("DataPrescricao","Data","data"),F("HoraPrescricao","Hora","hora")],
            [A("CopiarUltimaPrescricao","Copiar última prescrição",habilitado:"PodeCopiarUltimaPrescricao"),A("CriarInfusao","Criar infusão"),A("RecarregarMedicamentos","Atualizar medicamentos"),A("SalvarRascunho","Salvar rascunho",habilitado:"PodePrescrever"),A("Assinar","Liberar para enfermagem","primario","PodeAssinar")],
            [new("Infusoes","Preparo das infusões","Infusoes",[C("Titulo","Infusão")],[A("AcrescentarItem","Adicionar medicamento"),A("UsarModeloWeb","Usar modelo selecionado"),A("SalvarModeloWeb","Salvar como modelo"),A("RemoverInfusao","Remover infusão","perigo")],
                [F("Diluente","Diluente"),F("Volume","Volume (mL)"),F("Via","Via","selecao","vm:Vias"),F("Tempo","Tempo"),F("Horario","Horário","hora")]),
             new("Itens","Medicamentos prescritos","Itens",[C("InfusaoRotulo","Infusão")],[A("RemoverItem","Remover medicamento","perigo")],
                [F("Descricao","Medicamento","sugestao",opcoes:"vm:CatalogoMedicamentos",rotuloOpcao:"Rotulo"),F("Dose","Quantidade ou dose com unidade"),F("SeNecessario","Se necessário (SOS)","booleano"),F("Observacoes","Observações","texto-rico",formato:"ObservacoesFormatadas")])]),Permissao.Prescrever);
    }
}
