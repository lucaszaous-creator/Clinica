using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Controle de lotes TISS: cria o lote a partir das guias baixadas ainda sem lote,
/// acompanha o envio (protocolo da operadora) e registra o retorno (demonstrativo
/// de análise), aplicando as glosas guia a guia. Fecha o ciclo que antes terminava
/// na exportação do XML.
/// </summary>
public sealed class LoteTissService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;

    public LoteTissService(IClinicaRepositorio repo, ParametrosService parametros)
    {
        _repo = repo;
        _parametros = parametros;
    }

    /// <summary>Guias baixadas do período que ainda não entraram em nenhum lote (candidatas ao próximo).</summary>
    public Task<IReadOnlyList<CodigoFaturamento>> CandidatasAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.CodigosBaixadosSemLoteAsync(inicio, fim, ct);

    /// <summary>
    /// Cria o lote com as guias baixadas do período que ainda não foram exportadas,
    /// consumindo o próximo número da sequência TISS. Nulo se não houver guia nova.
    /// </summary>
    public async Task<LoteTiss?> CriarAsync(DateOnly inicio, DateOnly fim, string? registroAnsOperadora, CancellationToken ct = default)
    {
        var codigos = await _repo.CodigosBaixadosSemLoteAsync(inicio, fim, ct);
        if (codigos.Count == 0)
            return null;

        var numero = await _parametros.ObterProximoNumeroLoteTissAsync(ct);
        var lote = new LoteTiss
        {
            Numero = numero,
            DataGeracao = DateOnly.FromDateTime(DateTime.Today),
            RegistroAnsOperadora = registroAnsOperadora
        };
        foreach (var c in codigos)
            lote.Codigos.Add(c);

        await _repo.AdicionarLoteAsync(lote, ct);
        await _repo.SalvarAsync(ct);
        await _parametros.ConfirmarNumeroLoteTissAsync(numero, ct);
        return lote;
    }

    /// <summary>Todos os lotes, do mais recente ao mais antigo, com as guias carregadas.</summary>
    public Task<IReadOnlyList<LoteTiss>> ListarAsync(CancellationToken ct = default)
        => _repo.LotesTissAsync(ct);

    /// <summary>Lote com guias, atendimentos e pacientes carregados (para retorno e regeração do XML).</summary>
    public async Task<LoteTiss> ObterAsync(int loteId, CancellationToken ct = default)
        => await _repo.ObterLoteTissAsync(loteId, ct)
           ?? throw new InvalidOperationException($"Lote {loteId} não encontrado.");

    /// <summary>Marca o lote como entregue à operadora, guardando o protocolo devolvido.</summary>
    public async Task MarcarEnviadoAsync(int loteId, DateOnly data, string? protocolo, CancellationToken ct = default)
    {
        var lote = await ObterAsync(loteId, ct);
        lote.MarcarEnviado(data, protocolo);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Registra o demonstrativo de análise do lote: guias marcadas como glosadas recebem a
    /// glosa (com motivo da tabela ANS e prazo de recurso); as demais são consideradas aceitas.
    /// </summary>
    public async Task RegistrarRetornoAsync(int loteId, DateOnly dataRetorno,
        IReadOnlyList<RetornoGuiaDecisao> decisoes, string? observacao, CancellationToken ct = default)
    {
        var lote = await ObterAsync(loteId, ct);
        var prazo = await _parametros.ObterPrazoRecursoGlosaAsync(ct);

        foreach (var d in decisoes.Where(d => d.Glosada))
        {
            var codigo = lote.Codigos.FirstOrDefault(c => c.Id == d.CodigoId)
                ?? throw new InvalidOperationException($"A guia {d.CodigoId} não pertence ao lote {lote.Numero}.");
            codigo.RegistrarGlosa(dataRetorno, d.MotivoTexto, d.MotivoCodigo, prazo);
        }

        lote.RegistrarRetorno(dataRetorno, observacao);
        await _repo.SalvarAsync(ct);
    }
}
