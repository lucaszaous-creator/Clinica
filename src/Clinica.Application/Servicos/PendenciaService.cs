using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
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

    /// <summary>
    /// Janela (em dias) para alertar consultas a vencer quando não há ParametrosService
    /// (testes); com ele, vale a configuração GLOBAL salva no banco.
    /// </summary>
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
                    Descricao: c.Descricao,
                    PacienteTelefone: paciente?.Telefone,
                    ObservacaoPendencia: c.ObservacaoPendencia,
                    ObservacaoPendenciaEm: c.ObservacaoPendenciaEm);
            })
            .OrderByDescending(p => p.DiasEmAtraso)
            .ThenBy(p => p.PacienteNome)
            .ToList();
    }

    /// <summary>Consultas a renovar (vencidas ou a vencer dentro da janela de alerta).</summary>
    public async Task<IReadOnlyList<PendenciaConsulta>> ConsultasAVencerAsync(DateOnly referencia, CancellationToken ct = default)
    {
        // Fonte de verdade: as consultas emitidas (entidade Consulta), a mesma usada na
        // aba Consultas. Antes este método estimava o vencimento pelo último atendimento
        // de faturamento, o que divergia da consulta real (e sumia quando não havia
        // atendimento), deixando consultas a vencer de fora das pendências.
        var pacientes = await _repo.PacientesComConsultasAsync(ct);
        var snapshot = _parametros is null ? null : await _parametros.ObterAsync(ct);
        var janela = _parametros is null
            ? JanelaAlertaConsultaDias
            : await _parametros.ObterJanelaAlertaConsultaAsync(ct);
        var resultado = new List<PendenciaConsulta>();

        foreach (var p in pacientes)
        {
            var validade = p.Convenio == Convenio.Personalizado
                ? CatalogoConvenios.ValidadeConsultaDias(p.ConvenioCodigo)
                : (snapshot?.ValidadeConsultaDias(p.Convenio) ?? ConvenioInfo.ValidadeConsultaDias(p.Convenio));
            if (validade is null)
                continue; // convênio não usa consulta renovável

            // Consulta vigente = a mais recente que não foi substituída (não Renovada).
            var vigente = p.Consultas
                .Where(c => c.Status != StatusConsulta.Renovada)
                .OrderByDescending(c => c.DataEmissao)
                .FirstOrDefault();
            if (vigente is null)
                continue; // ainda não emitiu nenhuma consulta

            var diasParaVencer = vigente.DiasParaVencer(referencia);
            if (diasParaVencer > janela)
                continue; // ainda longe do vencimento

            var urgencia = vigente.EstaVencida(referencia) ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo;

            resultado.Add(new PendenciaConsulta(p.Id, p.Nome, p.Convenio, vigente.DataVencimento, diasParaVencer, urgencia));
        }

        return resultado.OrderBy(c => c.DiasParaVencer).ToList();
    }

    /// <summary>Janela (em dias) do alerta de carteirinhas a vencer.</summary>
    public const int JanelaAlertaCarteirinhaDias = 30;

    /// <summary>
    /// Glosas em aberto com prazo de recurso correndo, das mais urgentes para as demais.
    /// Amarelo = restam até 7 dias; vermelho = 3 dias ou menos (ou prazo estourado).
    /// </summary>
    public async Task<IReadOnlyList<PendenciaRecursoGlosa>> GlosasARecorrerAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var glosadas = await _repo.CodigosGlosadosAsync(somenteEmAberto: true, ct);

        return glosadas
            .Where(c => c.DataLimiteRecurso is not null)
            .Select(c =>
            {
                var dias = c.DiasParaFimRecurso(referencia) ?? 0;
                var paciente = c.Atendimento?.Paciente;
                var motivo = !string.IsNullOrWhiteSpace(c.MotivoGlosaCodigo)
                    ? Domain.Regras.MotivosGlosa.Descricao(c.MotivoGlosaCodigo)
                    : c.MotivoGlosa ?? string.Empty;
                return new PendenciaRecursoGlosa(
                    CodigoId: c.Id,
                    PacienteNome: paciente?.Nome ?? "(desconhecido)",
                    Convenio: paciente?.Convenio ?? default,
                    Tipo: c.Tipo,
                    NumeroGuia: c.NumeroGuiaReal,
                    DataGlosa: c.DataGlosa,
                    MotivoResumo: motivo,
                    DataLimiteRecurso: c.DataLimiteRecurso!.Value,
                    DiasParaFimPrazo: dias,
                    Urgencia: dias <= 3 ? NivelUrgencia.Vermelho
                            : dias <= 7 ? NivelUrgencia.Amarelo
                            : NivelUrgencia.Verde);
            })
            .OrderBy(p => p.DiasParaFimPrazo)
            .ToList();
    }

    /// <summary>
    /// Carteirinhas vencidas ou vencendo em até 30 dias (carteirinha vencida = guia
    /// recusada na hora pela operadora — o equivalente local da checagem de elegibilidade).
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCarteirinha>> CarteirinhasAVencerAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var pacientes = await _repo.BuscarPacientesAsync(null, ct);

        return pacientes
            .Where(p => p.ValidadeCarteirinha is not null)
            .Select(p =>
            {
                var dias = p.ValidadeCarteirinha!.Value.DayNumber - referencia.DayNumber;
                return (Paciente: p, Dias: dias);
            })
            .Where(x => x.Dias <= JanelaAlertaCarteirinhaDias)
            .Select(x => new PendenciaCarteirinha(
                x.Paciente.Id,
                x.Paciente.Nome,
                x.Paciente.Convenio,
                x.Paciente.Carteirinha,
                x.Paciente.ValidadeCarteirinha!.Value,
                x.Dias,
                x.Dias < 0 ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo))
            .OrderBy(p => p.DiasParaVencer)
            .ToList();
    }

    /// <summary>Total de pendências para o badge/contador do topo (inclui glosas com prazo de recurso).</summary>
    public async Task<int> TotalPendenciasAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var codigos = await CodigosPendentesAsync(referencia, ct);
        var consultas = await ConsultasAVencerAsync(referencia, ct);
        var recursos = await GlosasARecorrerAsync(referencia, ct);
        return codigos.Count + consultas.Count + recursos.Count(r => r.Urgencia != NivelUrgencia.Verde);
    }
}
