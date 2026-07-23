using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Orquestra a rodada de pendências ("rodar as pendências") — o fechamento de ciclo periódico que
/// impede a pendência de acumular indefinidamente. A cada N dias (configurável, padrão 10) o sistema
/// alarda; a secretária então baixa o que puder e, no que ficar, registra uma NÃO CONFORMIDADE com
/// justificativa. A não conformidade silencia a pendência (sai do painel) e fica documentada no
/// relatório — só volta a ser pendência se for reaberta manualmente (quando aparece solução).
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

    /// <summary>Situação atual da rodada (para o banner do painel e a decisão de bloqueio).</summary>
    public async Task<RodadaPendenciasStatus> ObterStatusAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var intervalo = await _parametros.ObterIntervaloRodadaPendenciasAsync(ct);
        var ultima = await _parametros.ObterDataUltimaRodadaPendenciasAsync(ct);
        var proxima = ultima?.AddDays(intervalo);
        var vencida = proxima is { } p && hoje >= p;
        var diasEmAtraso = vencida ? hoje.DayNumber - proxima!.Value.DayNumber : 0;

        var guias = await _pendencias.CodigosPendentesAsync(hoje, ct);

        var aplicaConsultas = await _parametros.ObterRodadaAplicaConsultasAsync(ct);
        var aplicaCarteirinhas = await _parametros.ObterRodadaAplicaCarteirinhasAsync(ct);
        var consultas = aplicaConsultas ? (await _pendencias.ConsultasAVencerAsync(hoje, ct)).Count : 0;
        var carteirinhas = aplicaCarteirinhas ? (await _pendencias.CarteirinhasAVencerAsync(hoje, ct)).Count : 0;

        return new RodadaPendenciasStatus(
            IntervaloDias: intervalo,
            UltimaRodada: ultima,
            ProximaRodada: proxima,
            Vencida: vencida,
            DiasEmAtraso: diasEmAtraso,
            GuiasParaDecisao: guias.Count,
            AplicaConsultas: aplicaConsultas,
            ConsultasParaRevisar: consultas,
            AplicaCarteirinhas: aplicaCarteirinhas,
            CarteirinhasParaRevisar: carteirinhas);
    }

    /// <summary>
    /// Ancora o ciclo no primeiro uso: se nunca houve rodada, define a última rodada como hoje —
    /// assim a primeira cobrança acontece um intervalo depois, de forma previsível.
    /// </summary>
    public async Task GarantirAncoraAsync(DateOnly hoje, CancellationToken ct = default)
    {
        if (await _parametros.ObterDataUltimaRodadaPendenciasAsync(ct) is null)
            await _parametros.SalvarDataUltimaRodadaPendenciasAsync(hoje, ct);
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

    /// <summary>Conclui a rodada: carimba hoje como a última rodada (zera o prazo do próximo ciclo).</summary>
    public async Task ConcluirRodadaAsync(DateOnly hoje, string? usuario, CancellationToken ct = default)
    {
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "RodadaPendencias",
            Detalhe = $"Rodada de pendências concluída em {hoje:dd/MM/yyyy}"
        }, ct);
        // Mesmo SaveChanges da auditoria: grava a data direto no KV para manter a operação atômica.
        await _repo.SalvarConfiguracaoAsync(
            ParametrosService.ChaveDataUltimaRodadaPendencias, hoje.ToString("yyyy-MM-dd"), ct);
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
