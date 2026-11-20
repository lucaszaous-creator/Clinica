using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

// ============================================================================================
// O que o REPOSITÓRIO devolve ao FechamentoRepasseService (nov/2026). Projeções planas,
// filtradas no BANCO: a prévia de uma competência não pode trazer a clínica inteira para
// descartar em memória. Quem decide (regra, bloqueio, valor) é o serviço; aqui só há fatos.
// ============================================================================================

/// <summary>
/// Uma receita de entrada REALIZADA e viva que ainda não virou crédito de repasse. Na guia
/// (<see cref="CodigoFaturamentoId"/> preenchido) é a prova de que o convênio pagou; no
/// particular, de que o paciente pagou a sessão.
/// </summary>
public sealed record ReceitaParaRepasse(
    int LancamentoId,
    int AtendimentoId,
    int? CodigoFaturamentoId,
    int PacienteId,
    string Paciente,
    DateOnly DataAtendimento,
    DateOnly DataRecebimento,
    decimal Valor,
    string? DocumentoReferencia,
    string? ConvenioCodigoLancamento,
    string? ConvenioCodigoFicha,
    Convenio ConvenioFicha,
    TipoCodigo? TipoGuia,
    string? NumeroGuia,
    int? NumeroLote,
    string? ModalidadeCodigo,
    ModalidadeAtendimento Modalidade);

/// <summary>
/// Um horário que aponta para o atendimento — é por ele que se sabe quem atendeu, porque o
/// atendimento (entidade do faturamento) não guarda profissional.
/// <see cref="Substituido"/>: o horário apontava pela coluna <c>AtendimentoSubstitutoId</c>
/// (a sessão foi lançada por fora dele).
/// </summary>
public sealed record HorarioDoAtendimento(
    int AgendamentoId,
    int AtendimentoId,
    int? ProfissionalId,
    string? ModalidadeCodigo,
    bool Substituido);

/// <summary>Guia baixada (faturada) sem receita realizada viva — aguardando o convênio.</summary>
public sealed record GuiaBaixadaSemRecebimento(
    int CodigoFaturamentoId,
    int AtendimentoId,
    string Paciente,
    DateOnly DataAtendimento,
    DateOnly? DataBaixa,
    string? ConvenioCodigoFicha,
    Convenio ConvenioFicha,
    TipoCodigo Tipo,
    string? NumeroGuia,
    bool ReceitaPrevista,
    StatusGlosa Glosa);
