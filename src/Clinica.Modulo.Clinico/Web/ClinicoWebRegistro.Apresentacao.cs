using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using P=Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Clinico.Web;
public static partial class ClinicoWebRegistro
{
    private static P.Secao Aprimorar(P.Secao s)
    {
        if(s.Chave=="MinhaSemana")s=Semana();
        if(s.Chave=="Anexos")s=s with {Tabelas=s.Tabelas.Select(t=>t with {Visivel=t.Chave switch {
            "PedidosAguardando"=>"MostrandoAguardando", "Resultados"=>"MostrandoResultados",
            "ArquivosDaFicha"=>"MostrandoArquivos", "Anexos"=>"MostrandoSessoes", _=>t.Visivel}}).ToArray()};
        if(s.Chave=="Prontuarios")s=s with {Campos=[..s.Campos,new("SoAssinaturasPendentes","Somente anamneses aguardando assinatura","booleano")],Indicadores=[..s.Indicadores,"RotuloPendentes|Assinaturas pendentes"]};
        if(s.Chave=="Emissoes")s=s with {Acoes=[..s.Acoes,new("EmitirReceitaWeb","Receita"),new("EmitirAtestadoWeb","Atestado médico"),new("EmitirComparecimentoWeb","Declaração de comparecimento"),new("EmitirPedidoWeb","Pedido de exame")]};
        if(s.Chave=="Prontuario")s=s with {Tabelas=s.Tabelas.Select(t=>t.Chave=="Sessoes"?t with {Acoes=[..t.Acoes,new("CopiarRegistroWeb","Copiar registro",Permissao.VerProntuario)],Colunas=t.Colunas.Where(c=>c.Propriedade!="Evolucao").ToArray(),Campos=[..t.Campos,new("Evolucao","Evolução","texto-rico-leitura",Formato:"EvolucaoFormatada")]}:t).ToArray()};
        if(s.Chave=="Prescricoes")s=s with {Tabelas=s.Tabelas.Select(t=>t.Chave=="Documentos"?t with {Acoes=[..t.Acoes,new("Enviar","Entregar documento"),new("RenovarLink","Renovar link"),new("TirarDoAr","Tirar do ar",Estilo:"perigo"),new("Cancelar","Cancelar documento",Estilo:"perigo")]}:t).ToArray()};
        if(s.Chave=="Enfermagem")s=s with {Acoes=[..s.Acoes,new("Passagem.AbrirConsultaWeb","Consulta de enfermagem",Permissao.RegistrarEvolucaoEnfermagem)]};
        if(s.Chave=="Atendimento")s=s with {Campos=s.Campos.Select(c=>c.Propriedade=="RegistroSelecionadoParaVincular"?c with {Visivel="TemRegistrosParaVincular"}:c).ToArray(),Acoes=[..s.Acoes.Select(a=>a.Comando=="VincularRegistro"?a with {Visivel="TemRegistrosParaVincular"}:a),new("AbrirMapa","Mapa corporal",Permissao.EditarProntuario)],Tabelas=s.Tabelas.Select(t=>t.Chave=="CamposPersonalizados"?t with {Campos=[
            new("RespostaTextoWeb","Resposta","texto",Visivel:"EhCaixaDeTexto"),new("RespostaListaWeb","Resposta","selecao",Opcoes:"Opcoes",Visivel:"EhLista"),new("RespostaSimNaoWeb","Resposta","selecao",Fixas:["Sim","Não"],Visivel:"EhSimNao")]}:t).ToArray()};
        if(s.Chave=="HistoricoConsulta")s=s with {Acoes=s.Acoes.Select((a,i)=>a with {Chave="historico-"+i}).ToArray(),Visivel="Aberto",Tabelas=s.Tabelas.Select(t=>t.Chave=="Itens"?t with {Acoes=[..t.Acoes,new("CopiarRegistro","Copiar registro",Permissao.VerProntuario)]}:t).ToArray()};
        s=s with {Tabelas=s.Tabelas.Select(t=>t.Chave.EndsWith("Chips",StringComparison.Ordinal)||s.Chave=="Anamnese"&&t.Chave=="Secoes" ? t with {Colunas=t.Colunas.Any(c=>c.Propriedade=="Marcado")?t.Colunas:[..t.Colunas,new("Marcado","Selecionado","booleano")]}:t).ToArray()};
        // Somente valores quantitativos usam indicadores; contexto e orientação são leitura compacta.
        var quantitativos=new HashSet<string>{"Atendidos","Pacientes","Faltas","NoShow","Ocupacao","Completude","Evolucoes","MelhoraDor","DorInicial","DorAtual","GanhoAcumulado","AlivioMedio","Primeiro","Atual","Variacao","EscalaPrimeira","EscalaAtual","EscalaGanho"};
        var descritivos=s.Indicadores.Where(i=>!quantitativos.Contains(i.Split('|')[0].Split('.').Last())).Select(i=>{var p=i.Split('|');return new P.Campo(p[0],Rotulo(p.Length>1?p[1]:p[0]),"leitura");});
        return s with {Titulo=Rotulo(s.Titulo),Campos=s.Campos.Concat(descritivos).DistinctBy(c=>c.Propriedade).Select(c=>c with {Rotulo=Rotulo(c.Rotulo)}).ToArray(),
            Indicadores=s.Indicadores.Where(i=>quantitativos.Contains(i.Split('|')[0].Split('.').Last())).Select(i=>{var p=i.Split('|');return p[0]+"|"+Rotulo(p.Length>1?p[1]:p[0]);}).ToArray(),
            Acoes=s.Acoes.Select(a=>a with {Rotulo=Rotulo(a.Rotulo)}).ToArray(),
            Tabelas=s.Tabelas.Select(t=>t with {Titulo=Rotulo(t.Titulo),Colunas=t.Colunas.Select(c=>c with {Rotulo=Rotulo(c.Rotulo)}).ToArray(),Campos=t.Campos.Select(c=>c with{Rotulo=Rotulo(c.Rotulo)}).ToArray(),Acoes=t.Acoes.Select(a=>a with{Rotulo=Rotulo(a.Rotulo)}).ToArray()}).ToArray()};
    }
    private static string Rotulo(string r)=>r switch {
        "Meu Dia"=>"Sessões do dia","Termo"=>"Buscar paciente","Selecionado"=>"Paciente encontrado","Escolhido"=>"Paciente selecionado","Termo Sessao"=>"Buscar no histórico","Motivo Da Lista"=>"Orientação","Agenda Fechada"=>"Agenda","Abrir Pendentes"=>"Sessões sem evolução","Status Detalhe"=>"Detalhes da situação","Prontuario"=>"Prontuário","Convenio"=>"Convênio","Endereco"=>"Endereço","Validade Carteirinha"=>"Validade da carteirinha","Observacoes Cadastro"=>"Observações do cadastro","Resumo Problemas"=>"Alertas clínicos","Data Texto"=>"Data","Hora Texto"=>"Hora","Procedencia"=>"Procedência","Dica Da Secao"=>"Orientação","Rotulo Da Secao"=>"Seção","Dica Rodape"=>"Orientação","Leitura Serie"=>"Leitura das medidas","Leitura Curva"=>"Leitura da evolução","Tendencia"=>"Tendência","Historico So Do Consultorio"=>"Histórico disponível","Campos Personalizados"=>"Campos complementares da clínica","Alertas Clinicos"=>"Alertas clínicos","Sala Infusao"=>"Sala de infusão","Codigo Busca"=>"Código de conferência","Validacoes"=>"Aguardando avaliação médica","Numero"=>"Número","Resumo Validacoes"=>"Avaliações pendentes","Resumo Da Lista"=>"Pacientes encontrados","Etapas Em Falta"=>"Etapas pendentes","Aviso De Leitura"=>"Orientação","Aviso Modalidade Enfermagem"=>"Modalidade da sessão","Leitura Completude"=>"Registros do período","Divida Prontuario"=>"Sessões sem evolução","Periodo Escolhido"=>"Período","Periodo"=>"Período","Aplicacoes"=>"Avaliações registradas","Pontuacao"=>"Pontuação","Sessoes"=>"Sessões","Sessoes Enfermagem"=>"Sessões de enfermagem","Registros Pendentes"=>"Registros pendentes","Prontuarios"=>"Prontuários","Prescricoes Clinicas"=>"Receitas e documentos","Prescricao Infusao"=>"Prescrição de infusão","Infusoes"=>"Infusões","Historico"=>"Histórico","Historico Consulta"=>"Histórico clínico","Paciente Capa"=>"Dados e alertas do paciente","Prontuario Clinico"=>"Histórico clínico","Atendimento Enfermagem"=>"Atendimento de enfermagem","Evolucao Dor"=>"Evolução da dor","Anexos Paciente"=>"Exames e anexos","Avaliacoes"=>"Avaliações","Referencia"=>"Referência","Sistolica"=>"Pressão sistólica","Diastolica"=>"Pressão diastólica","Cardiaca"=>"Frequência cardíaca","Respiratoria"=>"Frequência respiratória","Saturacao"=>"Saturação de oxigênio","Consulta Completa"=>"Consulta completa","Intercorrencia"=>"Intercorrência","Alergia Observada"=>"Alergia observada","Texto"=>"Registro","Data Do Atendimento"=>"Data do atendimento","Nome"=>"Nome","Rotulo"=>"Descrição","Situacao"=>"Situação","Observacoes"=>"Observações","Evolucao"=>"Evolução","Prescricoes"=>"Prescrições","Secoes"=>"Seções","Versoes"=>"Versões","Chips"=>"Escolha a seção","Itens"=>"Registros","Folhas Para Emitir"=>"Documentos disponíveis","Texto Da Secao"=>"Texto da seção","Meus Numeros"=>"Meus números",_=>r};
    private static P.Secao Semana()=>new("MinhaSemana","Agenda da semana",null,[new("Referencia","Semana de referência","data")],["Periodo|Período","Profissional|Profissional","Resumo|Resumo"],
        [new("ClinicoSemanaGrade","Disponibilidade por horário",["Faixas"],vm=>((MinhaSemanaViewModel)vm).Faixas.SelectMany(f=>f.Celulas).Cast<object>(),
            [new("DiaISO","Dia"),new("Hora","Hora"),new("Bloqueio","Bloqueio"),new("Continuacao","Continuação de sessão")],[],[],
            Celula:(_,row,col)=>{var c=(CelulaSemana)row;return col switch {"DiaISO"=>c.Dia.ToString("yyyy-MM-dd"),"Hora"=>c.Quando.ToString("HH:mm"),"Bloqueio"=>c.Bloqueio??"","Continuacao"=>c.Continuacao?"Sim":"Não",_=>""};},TipoLinha:typeof(CelulaSemana)),
         new("ClinicoSemanaSessoes","Sessões da semana",["Faixas"],vm=>((MinhaSemanaViewModel)vm).Faixas.SelectMany(f=>f.Celulas).SelectMany(c=>c.Sessoes).Cast<object>(),
            [new("InicioISO","Início"),new("FimISO","Fim"),new("PacienteNome","Paciente"),new("Modalidade","Modalidade"),new("Sala","Sala"),new("GrupoSituacao","Situação"),new("RegistroPendente","Registro pendente"),new("ForaDoDia","Não aconteceu")],[],[new("Abrir","Abrir paciente",Permissao.VerProntuario)],
            Celula:(_,row,col)=>{var c=(SessaoDoDia)row;return col switch {"InicioISO"=>c.DataHora.ToString("yyyy-MM-ddTHH:mm:ss"),"FimISO"=>c.FimPrevisto.ToString("yyyy-MM-ddTHH:mm:ss"),"PacienteNome"=>c.PacienteNome,"Modalidade"=>c.Modalidade,"Sala"=>c.Sala??"","GrupoSituacao"=>StatusDaFila.GrupoAgenda(c.Status,c.Etapa,c.FimAtendimentoEm is not null || (c.InicioAtendimentoEm is not null && c.DataHora.Date < DateTime.Today)),"RegistroPendente"=>c.RegistroPendente?"Sim":"Não","ForaDoDia"=>c.ForaDoDia?"Sim":"Não",_=>""};},TipoLinha:typeof(SessaoDoDia))],
        [new("SemanaAnterior","Semana anterior"),new("SemanaAtual","Esta semana"),new("ProximaSemana","Próxima semana"),new("Carregar","Atualizar")]);
    private static IReadOnlyList<GraficoWebDto> GraficosPaciente(object objeto,string secao)
    {
        var vm=(PacienteWorkspaceViewModel)objeto;
        GraficoWebDto Curva(string chave,string titulo,string unidade,IEnumerable<PontoGrafico> pontos)=>new(chave,titulo,"linha",unidade,pontos.Select(p=>new PontoWebDto(p.Rotulo,p.Valor,p.Valor?.ToString("0.##",CultureInfo.GetCultureInfo("pt-BR"))??"Sem medida")).ToArray());
        if(secao=="Dor.Dor")return [Curva("eva-antes","Dor antes da sessão","EVA",vm.Dor.CurvaAntes),Curva("eva-depois","Dor depois da sessão","EVA",vm.Dor.CurvaDepois)];
        if(secao=="Medidas.Medidas")return [Curva("medidas","Evolução das medidas",vm.Medidas.TipoAcompanhado?.Unidade??"",vm.Medidas.Curva)];
        if(secao=="Avaliacoes.Avaliacoes")return [Curva("avaliacoes",vm.Avaliacoes.Instrumento?.Nome??"Avaliações","pontos",vm.Avaliacoes.Curva)];
        return [];
    }
}
