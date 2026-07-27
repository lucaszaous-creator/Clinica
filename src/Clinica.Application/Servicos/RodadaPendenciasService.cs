using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Orquestra a rodada de pendências ("rodar as pendências") — o mecanismo que impede a pendência de
/// acumular indefinidamente. O prazo é POR GUIA: cada pendência exige decisão quando fica pendente há
/// mais do que o intervalo configurado (contado da DATA PREVISTA de faturamento — de quando ela virou
/// pendência). Ao vencer, o sistema alarda; a secretária baixa o que puder e, no que ficar, registra
/// uma NÃO CONFORMIDADE com justificativa. A não conformidade sai do painel (vai para a aba NC) e fica
/// no relatório — só volta a ser pendência se for reaberta (manualmente ou quando o paciente volta).
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
        var vencidas = await GuiasParaDecisaoAsync(hoje, intervalo, ct);
        // Atraso além do prazo da guia mais antiga (DiasEmAtraso é a idade da pendência; - intervalo = quanto passou).
        var maiorAtraso = vencidas.Count > 0 ? vencidas.Max(g => g.DiasEmAtraso) - intervalo : 0;

        var aplicaConsultas = await _parametros.ObterRodadaAplicaConsultasAsync(ct);
        var aplicaCarteirinhas = await _parametros.ObterRodadaAplicaCarteirinhasAsync(ct);
        var consultas = aplicaConsultas ? (await _pendencias.ConsultasAVencerAsync(hoje, ct)).Count : 0;
        var carteirinhas = aplicaCarteirinhas ? (await _pendencias.CarteirinhasAVencerAsync(hoje, ct)).Count : 0;

        return new RodadaPendenciasStatus(
            IntervaloDias: intervalo,
            Vencida: vencidas.Count > 0,
            GuiasParaDecisao: vencidas.Count,
            MaiorAtrasoDias: maiorAtraso,
            AplicaConsultas: aplicaConsultas,
            ConsultasParaRevisar: consultas,
            AplicaCarteirinhas: aplicaCarteirinhas,
            CarteirinhasParaRevisar: carteirinhas);
    }

    /// <summary>
    /// Guias que já passaram do prazo de decisão: pendentes há pelo menos o intervalo configurado
    /// (contado da data prevista de faturamento). São as que a rodada cobra — baixa ou não conformidade.
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> GuiasParaDecisaoAsync(DateOnly hoje, CancellationToken ct = default)
        => await GuiasParaDecisaoAsync(hoje, await _parametros.ObterIntervaloRodadaPendenciasAsync(ct), ct);

    private async Task<IReadOnlyList<PendenciaCodigo>> GuiasParaDecisaoAsync(DateOnly hoje, int intervalo, CancellationToken ct)
    {
        var guias = await _pendencias.CodigosPendentesAsync(hoje, ct);
        // DiasEmAtraso = hoje - DataPrevistaFaturamento (idade da pendência). Vence quando >= intervalo.
        return guias.Where(g => g.DiasEmAtraso >= intervalo).ToList();
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
