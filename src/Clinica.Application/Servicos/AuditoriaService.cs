using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quantas vezes cada ação apareceu no período — o retrato de quem fez o quê.
/// </summary>
public sealed record ResumoAuditoria(
    int Eventos,
    IReadOnlyList<(string Acao, int Vezes)> PorAcao,
    IReadOnlyList<(string Operador, int Vezes)> PorOperador)
{
    public bool Vazio => Eventos == 0;
}

/// <summary>
/// A trilha de auditoria (parcela 21).
///
/// `EventoAuditoria` é gravado por praticamente tudo o que mexe em dinheiro ou permissão —
/// baixa de guia, estorno, glosa, lote, lançamento, conta, tributo, preço de convênio,
/// criação de usuário, troca de senha — e **nenhuma tela a lia**. É a QUARTA ocorrência do
/// mesmo defeito no projeto (o pacote que debitava, o insumo que baixava, a previsão de
/// recebimento), e a mais grave: as outras três eram dinheiro que ninguém via; esta é a
/// resposta para "quem fez isso?", que é o que se pergunta justamente quando algo deu
/// errado.
///
/// A trilha é **somente leitura, e isso não é limitação de tempo**: um registro de
/// auditoria que pode ser editado ou apagado não é auditoria, é rascunho. Nem esta classe
/// nem a tela oferecem alteração ou exclusão.
///
/// O filtro vai todo para o SQL. Esta é a tabela que mais cresce no sistema — toda ação de
/// dinheiro escreve nela —, e materializar tudo para filtrar em memória é o que a convenção
/// do projeto proíbe desde a busca de pacientes.
/// </summary>
public sealed class AuditoriaService
{
    private readonly IClinicaRepositorio _repo;

    public AuditoriaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Eventos que casam com o filtro, do mais recente para o mais antigo.</summary>
    public Task<IReadOnlyList<EventoAuditoria>> ConsultarAsync(
        FiltroAuditoria filtro, CancellationToken ct = default)
        => _repo.ConsultarAuditoriaAsync(filtro, ct);

    /// <summary>
    /// As ações e os operadores que mais apareceram no resultado.
    ///
    /// Conta sobre o RESULTADO DO FILTRO, não sobre a tabela inteira: a pergunta que a
    /// direção faz é "no que eu estou olhando, quem fez o quê" — contar a base toda daria
    /// um número que não corresponde ao que está na tela, e a tela é a referência de quem
    /// lê.
    /// </summary>
    public async Task<ResumoAuditoria> ResumoAsync(
        FiltroAuditoria filtro, CancellationToken ct = default)
    {
        var eventos = await _repo.ConsultarAuditoriaAsync(filtro, ct);
        if (eventos.Count == 0) return new ResumoAuditoria(0, [], []);

        return new ResumoAuditoria(
            eventos.Count,
            eventos.GroupBy(e => e.Acao)
                .Select(g => (Acao: g.Key, Vezes: g.Count()))
                .OrderByDescending(x => x.Vezes)
                .ThenBy(x => x.Acao)
                .ToList(),
            eventos.GroupBy(e => string.IsNullOrWhiteSpace(e.Operador) ? "(não informado)" : e.Operador)
                .Select(g => (Operador: g.Key, Vezes: g.Count()))
                .OrderByDescending(x => x.Vezes)
                .ThenBy(x => x.Operador)
                .ToList());
    }

    /// <summary>
    /// As ações distintas já registradas, para o filtro oferecer a lista em vez de exigir
    /// que a pessoa saiba escrever "ContasRecorrentesGeradas" de cabeça.
    /// </summary>
    public Task<IReadOnlyList<string>> AcoesConhecidasAsync(CancellationToken ct = default)
        => _repo.AcoesDeAuditoriaAsync(ct);

    /// <summary>A trilha de uma guia — o histórico de baixa, estorno e glosa dela.</summary>
    public Task<IReadOnlyList<EventoAuditoria>> DaGuiaAsync(
        int codigoId, CancellationToken ct = default)
        => _repo.ConsultarAuditoriaAsync(new FiltroAuditoria { CodigoId = codigoId }, ct);

    /// <summary>A trilha de um paciente.</summary>
    public Task<IReadOnlyList<EventoAuditoria>> DoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => _repo.ConsultarAuditoriaAsync(new FiltroAuditoria { PacienteId = pacienteId }, ct);
}
