using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Consultas renováveis: registra/renova a consulta do paciente (validade dos Parâmetros do convênio)
/// e lista a situação de todos, alarmando as vencidas e as a vencer dentro da janela de alerta.
/// </summary>
public sealed class ConsultaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService? _parametros;

    /// <summary>Dias antes do vencimento em que a consulta já entra em alerta (padrão 5).</summary>
    public int JanelaAlertaDias { get; set; } = 5;

    public ConsultaService(IClinicaRepositorio repo, ParametrosService? parametros = null)
    {
        _repo = repo;
        _parametros = parametros;
    }

    private async Task<int?> ValidadeAsync(Convenio convenio, CancellationToken ct)
        => _parametros is null
            ? ConvenioInfo.ValidadeConsultaDias(convenio)
            : (await _parametros.ObterAsync(ct)).ValidadeConsultaDias(convenio);

    /// <summary>
    /// Registra/renova a consulta do paciente: encerra a consulta ativa anterior (Renovada) e cria
    /// uma nova, com validade conforme os Parâmetros do convênio. Lança se o convênio não usa consulta.
    /// </summary>
    public async Task<Consulta> RenovarAsync(int pacienteId, DateOnly dataEmissao, string? observacoes = null, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException($"Paciente {pacienteId} não encontrado.");

        var validade = await ValidadeAsync(paciente.Convenio, ct);
        if (validade is null)
            throw new InvalidOperationException(
                $"O convênio {ConvenioInfo.NomeExibicao(paciente.Convenio)} não usa consulta renovável.");

        var anteriores = await _repo.ConsultasDoPacienteAsync(pacienteId, ct);
        foreach (var c in anteriores.Where(c => c.Status == StatusConsulta.Ativa))
            c.Status = StatusConsulta.Renovada;

        var nova = new Consulta
        {
            PacienteId = pacienteId,
            Convenio = paciente.Convenio,
            DataEmissao = dataEmissao,
            ValidadeDias = validade.Value,
            DataVencimento = dataEmissao.AddDays(validade.Value),
            Status = StatusConsulta.Ativa,
            Observacoes = observacoes
        };
        await _repo.AdicionarConsultaAsync(nova, ct);
        await _repo.SalvarAsync(ct);
        return nova;
    }

    /// <summary>Situação de consulta de todos os pacientes (a consulta vigente), com alarme de expiração.</summary>
    public async Task<IReadOnlyList<StatusConsultaPaciente>> ListarAsync(DateOnly referencia, CancellationToken ct = default)
    {
        var pacientes = await _repo.PacientesComConsultasAsync(ct);
        var snapshot = _parametros is null ? null : await _parametros.ObterAsync(ct);
        var janela = _parametros is null
            ? JanelaAlertaDias
            : await _parametros.ObterJanelaAlertaConsultaAsync(ct);
        var lista = new List<StatusConsultaPaciente>();

        foreach (var p in pacientes)
        {
            var validade = snapshot?.ValidadeConsultaDias(p.Convenio) ?? ConvenioInfo.ValidadeConsultaDias(p.Convenio);
            var usaConsulta = validade is not null;

            // A consulta vigente é a mais recente que não foi substituída (não Renovada).
            var vigente = p.Consultas
                .Where(c => c.Status != StatusConsulta.Renovada)
                .OrderByDescending(c => c.DataEmissao)
                .FirstOrDefault();

            if (!usaConsulta)
            {
                lista.Add(new StatusConsultaPaciente(p.Id, p.Nome, p.Convenio, false,
                    vigente?.DataEmissao, vigente?.DataVencimento, null, false, false, NivelUrgencia.Verde));
                continue;
            }

            if (vigente is null)
            {
                // Convênio usa consulta, mas o paciente ainda não tem nenhuma emitida.
                lista.Add(new StatusConsultaPaciente(p.Id, p.Nome, p.Convenio, true,
                    null, null, null, false, true, NivelUrgencia.Amarelo));
                continue;
            }

            var dias = vigente.DiasParaVencer(referencia);
            var vencida = vigente.EstaVencida(referencia);
            var precisaRenovar = dias <= janela; // vencida ou a vencer dentro da janela
            var urgencia = vencida ? NivelUrgencia.Vermelho
                : dias <= janela ? NivelUrgencia.Amarelo
                : NivelUrgencia.Verde;

            lista.Add(new StatusConsultaPaciente(p.Id, p.Nome, p.Convenio, true,
                vigente.DataEmissao, vigente.DataVencimento, dias, vencida, precisaRenovar, urgencia));
        }

        return lista
            .OrderByDescending(s => s.PrecisaRenovar)
            .ThenBy(s => s.DiasParaVencer ?? int.MaxValue)
            .ThenBy(s => s.PacienteNome)
            .ToList();
    }
}
