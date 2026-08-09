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

    private async Task<int?> ValidadeAsync(Convenio familia, string? codigo, CancellationToken ct)
    {
        if (familia == Convenio.Personalizado)
            return CatalogoConvenios.ValidadeConsultaDias(codigo);
        return _parametros is null
            ? ConvenioInfo.ValidadeConsultaDias(familia)
            : (await _parametros.ObterAsync(ct)).ValidadeConsultaDias(familia);
    }

    /// <summary>Validade efetiva do convênio de um paciente (personalizado usa o catálogo).</summary>
    private static int? ValidadeEfetiva(Convenio familia, string? codigo, ParametrosSnapshot? snapshot)
        => familia == Convenio.Personalizado
            ? CatalogoConvenios.ValidadeConsultaDias(codigo)
            : (snapshot?.ValidadeConsultaDias(familia) ?? ConvenioInfo.ValidadeConsultaDias(familia));

    /// <summary>
    /// Registra/renova a consulta do paciente: encerra a consulta ativa anterior (Renovada) e cria
    /// uma nova, com validade conforme os Parâmetros do convênio. Lança se o convênio não usa consulta.
    /// </summary>
    public async Task<Consulta> RenovarAsync(int pacienteId, DateOnly dataEmissao, string? observacoes = null, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException($"Paciente {pacienteId} não encontrado.");

        var validade = await ValidadeAsync(paciente.Convenio, paciente.ConvenioCodigo, ct);
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
        var janela = await JanelaAsync(ct);

        return pacientes
            .Select(p => Avaliar(p, p.Consultas, snapshot, janela, referencia))
            .OrderByDescending(s => s.PrecisaRenovar)
            .ThenBy(s => s.DiasParaVencer ?? int.MaxValue)
            .ThenBy(s => s.PacienteNome)
            .ToList();
    }

    /// <summary>
    /// Situação de consulta de um conjunto de pacientes — a agenda do dia, a fila do
    /// balcão, o paciente que está sendo marcado.
    ///
    /// Existe porque a consulta a renovar só era lida em dois lugares (a aba Consultas e o
    /// painel de pendências), e nenhum deles é onde a secretária está quando o paciente
    /// está na frente dela. Cobrar a renovação com a pessoa presente custa uma frase;
    /// descobri-la depois custa telefonema — é o mesmo argumento do
    /// <see cref="ElegibilidadeService"/>.
    ///
    /// Não repete a regra do <see cref="ListarAsync"/>: as duas passam pelo mesmo
    /// <c>Avaliar</c>. Duas definições de "precisa renovar" divergiriam na primeira
    /// correção, e a agenda passaria a discordar da aba Consultas sobre o mesmo paciente.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, StatusConsultaPaciente>> SituacaoDeAsync(
        IReadOnlyCollection<Paciente> pacientes, DateOnly referencia, CancellationToken ct = default)
    {
        if (pacientes.Count == 0) return new Dictionary<int, StatusConsultaPaciente>();

        var snapshot = _parametros is null ? null : await _parametros.ObterAsync(ct);
        var janela = await JanelaAsync(ct);

        var ids = pacientes.Select(p => p.Id).Distinct().ToList();
        var consultas = (await _repo.ConsultasDosPacientesAsync(ids, ct))
            .GroupBy(c => c.PacienteId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Consulta>)g.ToList());

        var resultado = new Dictionary<int, StatusConsultaPaciente>();
        foreach (var p in pacientes)
        {
            if (resultado.ContainsKey(p.Id)) continue; // o mesmo paciente pode ter dois horários no dia
            var doPaciente = consultas.TryGetValue(p.Id, out var c) ? c : Array.Empty<Consulta>();
            resultado[p.Id] = Avaliar(p, doPaciente, snapshot, janela, referencia);
        }

        return resultado;
    }

    /// <summary>Situação de consulta de um paciente só (o escolhido no formulário).</summary>
    public async Task<StatusConsultaPaciente?> DoPacienteAsync(
        int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct);
        if (paciente is null) return null;

        var snapshot = _parametros is null ? null : await _parametros.ObterAsync(ct);
        var consultas = await _repo.ConsultasDoPacienteAsync(pacienteId, ct);
        return Avaliar(paciente, consultas, snapshot, await JanelaAsync(ct), referencia);
    }

    private async Task<int> JanelaAsync(CancellationToken ct)
        => _parametros is null ? JanelaAlertaDias : await _parametros.ObterJanelaAlertaConsultaAsync(ct);

    /// <summary>
    /// A regra de "precisa renovar", num lugar só: convênio que usa consulta, consulta
    /// vigente (a mais recente que não foi substituída) e a distância até o vencimento.
    /// </summary>
    private static StatusConsultaPaciente Avaliar(
        Paciente p, IEnumerable<Consulta> consultas, ParametrosSnapshot? snapshot,
        int janela, DateOnly referencia)
    {
        var validade = ValidadeEfetiva(p.Convenio, p.ConvenioCodigo, snapshot);
        var usaConsulta = validade is not null;

        // A consulta vigente é a mais recente que não foi substituída (não Renovada).
        var vigente = consultas
            .Where(c => c.Status != StatusConsulta.Renovada)
            .OrderByDescending(c => c.DataEmissao)
            .FirstOrDefault();

        if (!usaConsulta)
            return new StatusConsultaPaciente(p.Id, p.Nome, p.Convenio, false,
                vigente?.DataEmissao, vigente?.DataVencimento, null, false, false, NivelUrgencia.Verde)
                { ConvenioCodigo = p.ConvenioCodigo };

        if (vigente is null)
            // Convênio usa consulta, mas o paciente ainda não tem nenhuma emitida.
            return new StatusConsultaPaciente(p.Id, p.Nome, p.Convenio, true,
                null, null, null, false, true, NivelUrgencia.Amarelo)
                { ConvenioCodigo = p.ConvenioCodigo };

        var dias = vigente.DiasParaVencer(referencia);
        var vencida = vigente.EstaVencida(referencia);
        var precisaRenovar = dias <= janela; // vencida ou a vencer dentro da janela
        var urgencia = vencida ? NivelUrgencia.Vermelho
            : dias <= janela ? NivelUrgencia.Amarelo
            : NivelUrgencia.Verde;

        return new StatusConsultaPaciente(p.Id, p.Nome, p.Convenio, true,
            vigente.DataEmissao, vigente.DataVencimento, dias, vencida, precisaRenovar, urgencia)
            { ConvenioCodigo = p.ConvenioCodigo };
    }
}
