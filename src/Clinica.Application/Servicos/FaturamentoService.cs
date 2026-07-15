using Clinica.Application.Abstracoes;

namespace Clinica.Application.Servicos;

/// <summary>Registra a BAIXA de uma guia (faturamento efetivado). Não trata de recebimento/recebíveis.</summary>
public sealed class FaturamentoService
{
    private readonly IClinicaRepositorio _repo;

    public FaturamentoService(IClinicaRepositorio repo) => _repo = repo;

    public async Task DarBaixaAsync(
        int codigoId, DateOnly dataBaixa, string? numeroGuia, string? usuario, string? observacao,
        CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        codigo.DarBaixa(dataBaixa, numeroGuia, usuario, observacao);
        await _repo.SalvarAsync(ct);
    }
}
