using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Controle de glosas: registra quando o convênio recusa uma guia faturada, acompanha a
/// reapresentação e a recuperação — para não perder o faturamento.
/// </summary>
public sealed class GlosaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService? _parametros;

    public GlosaService(IClinicaRepositorio repo, ParametrosService? parametros = null)
    {
        _repo = repo;
        _parametros = parametros;
    }

    public Task<IReadOnlyList<CodigoFaturamento>> ListarAsync(bool somenteEmAberto, CancellationToken ct = default)
        => _repo.CodigosGlosadosAsync(somenteEmAberto, ct);

    public async Task RegistrarAsync(int codigoId, DateOnly dataGlosa, string? motivo,
        string? motivoCodigo = null, CancellationToken ct = default)
    {
        var codigo = await Obter(codigoId, ct);
        var prazo = _parametros is null
            ? ParametrosService.PrazoRecursoGlosaPadrao
            : await _parametros.ObterPrazoRecursoGlosaAsync(ct);
        codigo.RegistrarGlosa(dataGlosa, motivo, motivoCodigo, prazo);
        await _repo.SalvarAsync(ct);
    }

    public async Task ReapresentarAsync(int codigoId, DateOnly data, CancellationToken ct = default)
    {
        var codigo = await Obter(codigoId, ct);
        codigo.Reapresentar(data);
        await _repo.SalvarAsync(ct);
    }

    public async Task MarcarRecuperadaAsync(int codigoId, CancellationToken ct = default)
    {
        var codigo = await Obter(codigoId, ct);
        codigo.MarcarGlosaRecuperada();
        await _repo.SalvarAsync(ct);
    }

    private async Task<CodigoFaturamento> Obter(int codigoId, CancellationToken ct)
        => await _repo.ObterCodigoAsync(codigoId, ct)
           ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");
}
