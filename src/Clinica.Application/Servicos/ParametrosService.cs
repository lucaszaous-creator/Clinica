using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Valores efetivos dos parâmetros (defaults do código sobrepostos pela configuração salva).</summary>
public sealed class ParametrosSnapshot
{
    private readonly IReadOnlyDictionary<Convenio, ParametroConvenio> _map;
    public ParametrosSnapshot(IReadOnlyDictionary<Convenio, ParametroConvenio> map) => _map = map;

    public int? ValidadeConsultaDias(Convenio c)
        => _map.TryGetValue(c, out var p) ? p.ValidadeConsultaDias : ConvenioInfo.ValidadeConsultaDias(c);

    public int DiasSegundoCodigo(Convenio c)
        => _map.TryGetValue(c, out var p) ? p.DiasSegundoCodigo : 1;

    public IReadOnlyCollection<ParametroConvenio> Todos => (IReadOnlyCollection<ParametroConvenio>)_map.Values;
}

/// <summary>Lê e grava os parâmetros editáveis dos convênios.</summary>
public sealed class ParametrosService
{
    private readonly IClinicaRepositorio _repo;

    public ParametrosService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Snapshot com os valores efetivos: defaults do código + overrides salvos.</summary>
    public async Task<ParametrosSnapshot> ObterAsync(CancellationToken ct = default)
    {
        var salvos = (await _repo.ParametrosAsync(ct)).ToDictionary(p => p.Convenio);

        var map = new Dictionary<Convenio, ParametroConvenio>();
        foreach (var c in Enum.GetValues<Convenio>())
        {
            map[c] = salvos.TryGetValue(c, out var s)
                ? s
                : new ParametroConvenio
                {
                    Convenio = c,
                    ValidadeConsultaDias = ConvenioInfo.ValidadeConsultaDias(c),
                    DiasSegundoCodigo = 1
                };
        }
        return new ParametrosSnapshot(map);
    }

    public async Task SalvarAsync(IEnumerable<ParametroConvenio> parametros, CancellationToken ct = default)
    {
        foreach (var p in parametros)
            await _repo.SalvarParametroAsync(p, ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Configurações globais (valem para todas as máquinas) ----

    public const string ChaveJanelaAlertaConsulta = "JanelaAlertaConsultaDias";
    public const int JanelaAlertaConsultaPadrao = 5;

    /// <summary>Dias de antecedência do alerta de consultas a vencer (global, salvo no banco).</summary>
    public async Task<int> ObterJanelaAlertaConsultaAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveJanelaAlertaConsulta, ct), out var dias) && dias >= 0
            ? dias
            : JanelaAlertaConsultaPadrao;

    public async Task SalvarJanelaAlertaConsultaAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveJanelaAlertaConsulta, Math.Max(0, dias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }
}
