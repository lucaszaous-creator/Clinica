using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Alimenta o dashboard: lista todos os códigos (em especial os 2º códigos) pendentes de baixa
/// e as consultas a renovar, com o semáforo de urgência.
/// </summary>
public sealed class PendenciaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService? _parametros;

    /// <summary>Janela (em dias) para começar a alertar sobre consultas a vencer.</summary>
    public int JanelaAlertaConsultaDias { get; set; } = 5;

    public PendenciaService(IClinicaRepositorio repo, ParametrosService? parametros = null)
    {
        _repo = repo;
        _parametros = parametros;
    }

    /// <summary>Códigos pendentes de baixa cuja data prevista já chegou, ordenados do mais atrasado ao menos.</summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> CodigosPendentesAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var abertos = await _repo.CodigosEmAbertoAsync(ct);

        return abertos
            .Where(c => c.EstaPendente(referencia))
            .Select(c =>
            {
                var atraso = referencia.DayNumber - c.DataPrevistaFaturamento.DayNumber;
                var paciente = c.Atendimento?.Paciente;
                return new PendenciaCodigo(
                    CodigoId: c.Id,
                    PacienteId: paciente?.Id ?? 0,
                    PacienteNome: paciente?.Nome ?? "(desconhecido)",
                    Convenio: paciente?.Convenio ?? default,
                    Tipo: c.Tipo,
                    Ordem: c.Ordem,
                    DataPrevista: c.DataPrevistaFaturamento,
                    FormaObtencao: c.FormaObtencao,
                    DiasEmAtraso: atraso,
                    Urgencia: atraso <= 0 ? NivelUrgencia.Amarelo : NivelUrgencia.Vermelho,
                    Descricao: c.Descricao);
            })
            .OrderByDescending(p => p.DiasEmAtraso)
            .ThenBy(p => p.PacienteNome)
            .ToList();
    }

    /// <summary>Consultas a renovar (vencidas ou a vencer dentro da janela de alerta).</summary>
    public async Task<IReadOnlyList<PendenciaConsulta>> ConsultasAVencerAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var pacientes = await _repo.PacientesComAtendimentosAsync(ct);
        var snapshot = _parametros is null ? null : await _parametros.ObterAsync(ct);
        var resultado = new List<PendenciaConsulta>();

        foreach (var p in pacientes)
        {
            var validade = snapshot?.ValidadeConsultaDias(p.Convenio) ?? ConvenioInfo.ValidadeConsultaDias(p.Convenio);
            if (validade is null || p.Atendimentos.Count == 0)
                continue;

            var ultimo = p.Atendimentos.Max(a => a.Data);
            var vencimento = ultimo.AddDays(validade.Value);
            var diasParaVencer = vencimento.DayNumber - referencia.DayNumber;

            if (diasParaVencer > JanelaAlertaConsultaDias)
                continue; // ainda longe do vencimento

            var urgencia = diasParaVencer < 0 ? NivelUrgencia.Vermelho
                : diasParaVencer == 0 ? NivelUrgencia.Amarelo
                : NivelUrgencia.Amarelo;

            resultado.Add(new PendenciaConsulta(p.Id, p.Nome, p.Convenio, vencimento, diasParaVencer, urgencia));
        }

        return resultado.OrderBy(c => c.DiasParaVencer).ToList();
    }

    /// <summary>Total de pendências para o badge/contador do topo.</summary>
    public async Task<int> TotalPendenciasAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var codigos = await CodigosPendentesAsync(referencia, ct);
        var consultas = await ConsultasAVencerAsync(referencia, ct);
        return codigos.Count + consultas.Count;
    }
}
