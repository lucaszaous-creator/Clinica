using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Orquestra a rodada de pendências ("rodar as pendências") — o fechamento de ciclo que impede a
/// pendência de acumular indefinidamente. O prazo é contado POR ATENDIMENTO: cada guia pendente vence
/// N dias (configurável, padrão 10) depois do atendimento do paciente. Ao vencer sem baixa, o sistema
/// EXIGE uma decisão — baixa ou NÃO CONFORMIDADE com justificativa — e bloqueia o uso até a resolução.
/// A não conformidade silencia a pendência (sai do painel) e fica documentada no relatório — só volta
/// a ser pendência se for reaberta manualmente ou quando o paciente retorna.
/// </summary>
public sealed class RodadaPendenciasService
{
    private readonly IClinicaRepositorio _repo;
    private readonly PendenciaService _pendencias;
    private readonly ParametrosService _parametros;

    public RodadaPendenciasService(IClinicaRepositorio repo, PendenciaService pendencias, ParametrosService parametros)
    {
        _repo = repo;
        _pendencias = pendencias;
        _parametros = parametros;
    }

    /// <summary>
    /// Ancora a carência no primeiro uso: se a rodada por atendimento ainda não tem data de início,
    /// define-a como hoje — assim o backlog acumulado só começa a contar o prazo a partir de agora e
    /// não bloqueia tudo de uma vez na primeira abertura desta versão.
    /// </summary>
    public async Task GarantirInicioAsync(DateOnly hoje, CancellationToken ct = default)
    {
        if (await _parametros.ObterInicioRodadaPorAtendimentoAsync(ct) is null)
            await _parametros.SalvarInicioRodadaPorAtendimentoAsync(hoje, ct);
    }

    /// <summary>Situação atual da rodada (para o banner do painel e a decisão de bloqueio).</summary>
    public async Task<RodadaPendenciasStatus> ObterStatusAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var prazo = await _parametros.ObterIntervaloRodadaPendenciasAsync(ct);
        var ativacao = await _parametros.ObterInicioRodadaPorAtendimentoAsync(ct);
        var vencidas = await _pendencias.CodigosVencidosParaDecisaoAsync(hoje, prazo, ativacao, ct);
        var pendentes = await _pendencias.CodigosPendentesAsync(hoje, ct);

        var aplicaConsultas = await _parametros.ObterRodadaAplicaConsultasAsync(ct);
        var aplicaCarteirinhas = await _parametros.ObterRodadaAplicaCarteirinhasAsync(ct);
        var consultas = aplicaConsultas ? (await _pendencias.ConsultasAVencerAsync(hoje, ct)).Count : 0;
        var carteirinhas = aplicaCarteirinhas ? (await _pendencias.CarteirinhasAVencerAsync(hoje, ct)).Count : 0;

        return new RodadaPendenciasStatus(
            PrazoDias: prazo,
            GuiasParaDecisao: vencidas.Count,
            GuiasPendentes: pendentes.Count,
            AplicaConsultas: aplicaConsultas,
            ConsultasParaRevisar: consultas,
            AplicaCarteirinhas: aplicaCarteirinhas,
            CarteirinhasParaRevisar: carteirinhas);
    }

    /// <summary>
    /// Guias pendentes cujo prazo de decisão (atendimento + N dias) já venceu — as que exigem baixa ou
    /// não conformidade e bloqueiam o uso até a resolução. Usa o prazo configurado (padrão 10 dias).
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> GuiasVencidasParaDecisaoAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var prazo = await _parametros.ObterIntervaloRodadaPendenciasAsync(ct);
        var ativacao = await _parametros.ObterInicioRodadaPorAtendimentoAsync(ct);
        return await _pendencias.CodigosVencidosParaDecisaoAsync(hoje, prazo, ativacao, ct);
    }

    /// <summary>Marca uma guia pendente como não conformidade (com justificativa), silenciando-a.</summary>
    public async Task MarcarNaoConformidadeAsync(int codigoId, string justificativa, string? usuario, CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        codigo.MarcarNaoConformidade(justificativa);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "NaoConformidade",
            Detalhe = justificativa.Trim(),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Reabre uma não conformidade: a guia volta a ser pendência e pode ser baixada.</summary>
    public async Task ReabrirNaoConformidadeAsync(int codigoId, string? usuario, CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        codigo.ReabrirNaoConformidade();
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "NaoConformidadeReaberta",
            Detalhe = "Não conformidade reaberta — voltou a ser pendência",
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Guias atualmente em não conformidade (backlog documentado), da mais recente para a mais antiga.</summary>
    public async Task<IReadOnlyList<NaoConformidadeItem>> NaoConformidadesAsync(CancellationToken ct = default)
    {
        var codigos = await _repo.CodigosEmNaoConformidadeAsync(ct);
        return codigos
            .Select(c =>
            {
                var paciente = c.Atendimento?.Paciente;
                return new NaoConformidadeItem(
                    CodigoId: c.Id,
                    PacienteNome: paciente?.Nome ?? "(desconhecido)",
                    Convenio: paciente?.Convenio ?? default,
                    Tipo: c.Tipo,
                    Ordem: c.Ordem,
                    DataPrevista: c.DataPrevistaFaturamento,
                    Justificativa: c.NaoConformidadeJustificativa ?? string.Empty,
                    Em: c.NaoConformidadeEm);
            })
            .OrderByDescending(n => n.Em)
            .ThenBy(n => n.PacienteNome)
            .ToList();
    }
}
