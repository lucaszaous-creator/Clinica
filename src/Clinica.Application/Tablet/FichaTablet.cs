using Clinica.Domain.Entities;
using Clinica.Application.Servicos;

namespace Clinica.Application.Tablet;

public sealed record AnamneseTablet(Guid Idempotencia, string Versao, string? AntecedentesPessoais,
    string? AntecedentesFamiliares, string? HabitosDeVida, string? HistoriaObstetrica,
    string? RevisaoDeSistemas, string? Observacoes, string? Motivo);
public sealed record MedidaTablet(Guid Idempotencia, DateOnly Data, string TipoCodigo,
    decimal Valor, decimal? ValorSecundario, string? Observacoes);
public sealed record ProblemaTablet(Guid Idempotencia, int Id, string? Versao, NaturezaProblema Natureza,
    string Descricao, string? Cid, DateOnly? Inicio, string? Observacoes);
public sealed record SituacaoProblemaTablet(Guid Idempotencia, string Versao, SituacaoProblema Situacao,
    DateOnly? Fim, string? Motivo);
public sealed record CancelarRegistroTablet(Guid Idempotencia, string Versao, string Motivo);
public sealed record AnexoTablet(Guid Idempotencia, DateOnly Data, string Titulo, string NomeArquivo,
    string TipoConteudo, byte[] Conteudo, string? Observacoes);
public sealed record ResultadoFichaTablet(int Id);
public sealed record ResultadoExameTablet(Guid Idempotencia,DateOnly Data,string Nome,string Valor,
    string? Unidade,string? Referencia,string? Laboratorio,string? Observacoes);
public sealed record RegistroEnfermagemTablet(Guid Idempotencia,DateOnly Data,TimeOnly Hora,string Texto,
    bool Intercorrencia,SinaisVitais? Sinais,string? AlergiaObservada,int? RetificaId=null,string? Motivo=null,
    int? AgendamentoId=null);
public sealed record VinculoEnfermagemTablet(Guid Idempotencia, int AgendamentoId, string Motivo);
public sealed record ObservacaoEnfermagemTablet(TimeOnly Hora, string Texto, bool Intercorrencia,
    SinaisVitais? Sinais = null, string? AlergiaObservada = null, bool NegaAlergia = false,
    string? FaseAtendimento = null);
public sealed record ObservacoesEnfermagemTablet(Guid Idempotencia, DateOnly Data, int AgendamentoId,
    ObservacaoEnfermagemTablet[] Observacoes, Guid? EditorId = null);
public sealed record ReservarEdicaoEnfermagemTablet(int AgendamentoId, Guid EditorId, bool Liberar = false);
public sealed record ResultadoObservacoesEnfermagemTablet(int[] Ids);
public sealed record SalvarModeloEnfermagemTablet(Guid Idempotencia, int Id, string? Versao, string Nome, string Texto);
public sealed record ModeloEnfermagemTablet(int Id, string Nome, string Texto, string Versao);
public sealed record SalvarModeloEvolucaoTablet(Guid Idempotencia, int Id, string? Versao, string Nome,
    bool Compartilhado, bool Ativo, string? QueixaPrincipal = null, string? HistoriaDoencaAtual = null,
    string? ExameFisico = null, string? HipoteseDiagnostica = null, string? CidSessao = null, string? TextoEvolucao = null,
    string? Conduta = null, string? Orientacoes = null, string? PlanoTerapeutico = null);
public sealed record ModeloEvolucaoTablet(int Id, string Nome, bool Compartilhado, bool Ativo, string Versao,
    string? QueixaPrincipal, string? HistoriaDoencaAtual, string? ExameFisico,
    string? HipoteseDiagnostica, string? CidSessao, string? TextoEvolucao,
    string? Conduta, string? Orientacoes, string? PlanoTerapeutico);
public sealed record InfusaoExternaTablet(Guid Idempotencia, RegistroInfusaoExterna Dados);
public sealed record NovoModeloDocumentoTablet(Guid Idempotencia,string Nome,TipoDocumentoClinico Tipo,string Texto,
    string? CorpoFormatado=null,int Id=0,string? Versao=null,bool ParaInfusao=false,string? ConfiguracaoInfusao=null);
public sealed record RascunhoTablet(Guid Idempotencia, string Versao, string Motivo, string? Corpo,
    string? Observacoes, int? DiasAfastamento, string? Indicacao, bool AssinaturaEnfermagem,
    ItemInfusaoTablet[]? Itens,string? CorpoFormatado=null,string? ObservacoesFormatadas=null,string? IndicacaoFormatada=null,
    DateOnly? DataPrescricao=null,TimeOnly? HoraPrescricao=null);
public sealed record ItemInfusaoTablet(string Descricao, string? Dose, string? Diluente, string? Volume,
    ViaAdministracao Via, string? TempoInfusao, TimeOnly? HoraPrevista, bool SeNecessario, string? Observacoes,
    string? DescricaoFormatada=null,string? ObservacoesFormatadas=null);
