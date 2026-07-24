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
            .Select(c => MapearPendencia(c, referencia))
            .OrderByDescending(p => p.DiasEmAtraso)
            .ThenBy(p => p.PacienteNome)
            .ToList();
    }

    /// <summary>
    /// Guias pendentes cujo PRAZO DE DECISÃO venceu: já se passaram <paramref name="prazoDias"/> dias
    /// (padrão 10) desde o atendimento e a guia continua sem baixa. São as que o sistema EXIGE decidir
    /// (baixa ou não conformidade) e que bloqueiam o uso até a resolução.
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> CodigosVencidosParaDecisaoAsync(
        DateOnly referencia, int prazoDias, CancellationToken ct = default)
    {
        var abertos = await _repo.CodigosEmAbertoAsync(ct);

        return abertos
            .Where(c => c.PrazoDecisaoVencido(referencia, prazoDias))
            .Select(c => MapearPendencia(c, referencia))
            .OrderByDescending(p => p.DiasEmAtraso)
            .ThenBy(p => p.PacienteNome)
            .ToList();
    }

    /// <summary>Converte um código em aberto na linha de pendência exibida (semáforo pela data prevista).</summary>
    private static PendenciaCodigo MapearPendencia(CodigoFaturamento c, DateOnly referencia)
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
    }

    /// <summary>
    /// Guias pendentes de baixa de UM paciente específico — usado para avisar a secretária ao
    /// iniciar um novo atendimento, para que ela cobre a guia em aberto na hora.
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> PendenciasDoPacienteAsync(
        int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var todas = await CodigosPendentesAsync(referencia, ct);
        return todas.Where(p => p.PacienteId == pacienteId).ToList();
    }

    /// <summary>
    /// Não conformidades como linhas de pendência CINZA para o painel: ficam paradas (documentadas)
    /// até o paciente voltar (reabertas) ou serem resolvidas. Não contam como urgência.
    /// </summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> NaoConformidadesComoPendenciaAsync(
        DateOnly referencia, CancellationToken ct = default)
    {
        var ncs = await _repo.CodigosEmNaoConformidadeAsync(ct);
        return ncs
            .Select(c =>
            {
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
                    DiasEmAtraso: referencia.DayNumber - c.DataPrevistaFaturamento.DayNumber,
                    Urgencia: NivelUrgencia.Cinza,
                    Descricao: c.Descricao,
                    PacienteTelefone: paciente?.Telefone,
                    ObservacaoPendencia: c.NaoConformidadeJustificativa,
                    ObservacaoPendenciaEm: c.NaoConformidadeEm,
                    EhNaoConformidade: true);
            })
            .OrderBy(p => p.PacienteNome)
            .ToList();
    }

    /// <summary>Não conformidades de UM paciente (para avisar quando ele volta, no novo atendimento).</summary>
    public async Task<IReadOnlyList<PendenciaCodigo>> NaoConformidadesDoPacienteAsync(
        int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var todas = await NaoConformidadesComoPendenciaAsync(referencia, ct);
        return todas.Where(p => p.PacienteId == pacienteId).ToList();
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

            resultado.Add(new PendenciaConsulta(p.Id, p.Nome, p.Convenio, vigente.DataVencimento, diasParaVencer, urgencia, p.Telefone));
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
                x.Dias < 0 ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo,
                x.Paciente.Telefone))
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
