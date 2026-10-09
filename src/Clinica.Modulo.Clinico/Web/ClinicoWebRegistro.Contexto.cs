using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using D=Clinica.Desktop.Shell.Web.DialogosWebController;
using P=Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Clinico.Web;
public sealed record RegistroClinicoLeitura(string Titulo,string Texto);
public static partial class ClinicoWebRegistro
{
    private static D.Definicao ComoDialogo(string titulo,params P.Secao[] secoes)
        => new(titulo,null,secoes.SelectMany(s=>s.Campos.Select(c=>new D.Campo(c.Propriedade,c.Propriedade,c.Rotulo,c.Tipo,c.Opcoes,c.RotuloOpcao??"Rotulo",c.Visivel,c.Guarda,Formato:c.Formato,ValorOpcao:c.ValorOpcao))
            .Concat(s.Indicadores.Select(i=>{var p=i.Split('|');return F(p[0],p.Length>1?p[1]:p[0],"leitura");}))).DistinctBy(c=>c.Chave).ToArray(),
            secoes.SelectMany(s=>s.Acoes.Select(a=>A(a.Comando,a.Rotulo,a.Estilo,a.Guarda))).DistinctBy(a=>a.Chave).ToArray(),
            secoes.SelectMany(s=>s.Tabelas.Select(t=>new D.Tabela(t.Chave,t.Titulo,t.Origens[0],t.Colunas.Select(c=>C(c.Propriedade,c.Rotulo)).ToArray(),t.Acoes.Select(a=>A(a.Comando,a.Rotulo,a.Estilo,a.Guarda?.Replace("vm:",""))).ToArray(),
                t.Campos.Select(c=>new D.Campo(c.Propriedade,c.Propriedade,c.Rotulo,c.Tipo,c.Opcoes,c.RotuloOpcao??"Rotulo",c.Visivel,c.Guarda,Formato:c.Formato,ValorOpcao:c.ValorOpcao)).ToArray()))).ToArray());
    private static IEnumerable<D.RegistroDialogo> DialogosComplementares()
    {
        yield return new("DocumentosPaciente",typeof(PacienteWorkspaceViewModel),ComoDialogo("Documentos do paciente",ComPrefixo(Prescricoes(),"Prescricoes"),ComPrefixo(Infusoes(),"Infusoes"),new P.Secao("AtualizarDocumentos","Documentos",null,[],[],[],[new("AtualizarDocumentos","Atualizar documentos")])),Permissao.VerProntuario);
        yield return new("RegistroClinicoLeitura",typeof(RegistroClinicoLeitura),new("Registro clínico",null,[F("Titulo","Registro","leitura"),F("Texto","Conteúdo","leitura")],[],[]),Permissao.VerProntuario);
        yield return new("EmissoesAtendimento",typeof(AtendimentoViewModel),ComoDialogo("Documentos do atendimento",Aprimorar(Emissoes())),Permissao.VerProntuario);
        yield return new("ConsultaFichaClinica",typeof(PacienteWorkspaceViewModel),ComoDialogo("Ficha do paciente",ComPrefixo(Capa(),"Capa"),ComPrefixo(Anamnese(),"Anamnese")),Permissao.VerProntuario);
        yield return new("ConsultaExamesPaciente",typeof(AnexosPacienteViewModel),ComoDialogo("Exames e anexos",Anexos()),Permissao.VerProntuario);
        var detalhe = new D.Definicao("Campos complementares da sessão",null,
            [F("QueixaPrincipal","Queixa principal","textarea"),F("HistoriaDoencaAtual","História da doença atual","textarea"),F("ExameFisico","Exame físico","textarea"),F("HipoteseDiagnostica","Hipótese diagnóstica","textarea"),F("CidSessao","CID"),F("DescricaoCid","Descrição do CID","leitura"),F("Conduta","Conduta","textarea"),F("Orientacoes","Orientações","textarea"),F("PlanoTerapeutico","Plano terapêutico","textarea"),F("RetornoSugeridoNota","Retorno sugerido — observações","textarea"),F("Encaminhamento","Encaminhamento","textarea")],[A("BuscarCid","Buscar CID")],
            [new("CamposPersonalizados","Campos complementares da clínica","CamposPersonalizados",[C("Rotulo","Campo"),C("Ajuda","Orientação")],[],
                [F("RespostaTextoWeb","Resposta",visivel:"EhCaixaDeTexto"),F("RespostaListaWeb","Resposta","selecao","Opcoes",visivel:"EhLista"),F("RespostaSimNaoWeb","Resposta","selecao","OpcoesSimNaoWeb",visivel:"EhSimNao")],Habilitado:"PodeEditarProntuario")]);
        yield return new("DetalheSessao",typeof(AtendimentoViewModel),detalhe,Permissao.EditarProntuario);
        yield return new("DetalheSessao",typeof(FolhaDaSessaoViewModel),detalhe,Permissao.EditarProntuario);
        yield return new("ModelosEvolucao",typeof(ModelosEvolucaoViewModel),new("Modelos de evolução",null,
            [F("Selecionado","Modelo","selecao","Modelos","Nome"),F("Selecionado.Previa","Prévia","leitura"),F("AvisoDoModelo","Orientação","leitura"),F("NomeNovo","Nome do novo modelo"),F("ParaAClinica","Compartilhar com a clínica","booleano")],
            [A("Repetir","Repetir última sessão",habilitado:"TemRepetir"),A("Aplicar","Aplicar nesta sessão","primario","PodeMexer"),A("SalvarComoModelo","Salvar como modelo",habilitado:"PodeMexer"),A("Apagar","Apagar modelo","perigo","PodeMexer")],[]),Permissao.EditarProntuario);
        yield return new("MapaCorporal",typeof(MapaCorporalViewModel),new("Mapa corporal","Marque os pontos no corpo. Eles serão gravados junto da sessão.",
            [F("Titulo","Sessão","leitura"),F("TecnicaSelecionada","Técnica","selecao","Tecnicas"),F("NomeProximoPonto","Nome do próximo ponto"),F("MarcacaoWeb","Mapa corporal","mapa-corporal"),F("Resumo","Pontos marcados","leitura"),F("Observacoes","Observações","textarea"),F("SessaoAnteriorSelecionada","Sessão anterior","selecao","SessoesAnteriores"),F("EstadoDoHistorico","Histórico","leitura"),F("ProtocoloSelecionado","Modelo corporal","selecao","Protocolos","Nome"),F("EstadoDosModelos","Modelos","leitura"),F("NomeDoModelo","Nome para salvar como modelo"),F("ProtocoloDaClinica","Compartilhar modelo com a clínica","booleano")],
            [A("Desfazer","Desfazer",habilitado:"PodeDesfazer"),A("Limpar","Limpar pontos",habilitado:"PodeEditar"),A("RepetirAnterior","Repetir anterior",habilitado:"PodeEditar"),A("CopiarSessao","Copiar sessão escolhida",habilitado:"PodeEditar"),A("AplicarProtocolo","Aplicar modelo",habilitado:"PodeEditar"),A("SalvarComoProtocolo","Salvar como modelo",habilitado:"PodeGuardarModelo"),A("ExcluirProtocolo","Apagar modelo","perigo","PodeApagarModelo"),A("UsarMapaWeb","Usar mapa nesta sessão","primario","PodeEditar")],
            [new("Pontos","Pontos marcados","Pontos",[C("FaceRotulo","Face"),C("X","Posição horizontal"),C("Y","Posição vertical")],[A("SelecionarPonto","Selecionar ponto"),A("RemoverPonto","Remover ponto")],[F("Nome","Nome do ponto"),F("Tecnica","Técnica","selecao","vm:Tecnicas"),F("Observacao","Observação do ponto","textarea")])]),Permissao.EditarProntuario);
    }
}
