using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Catálogo de convênios: lê/grava as entradas no banco e mantém o cache em memória
/// (<see cref="CatalogoConvenios"/>) que serve nome/família de forma síncrona às telas.
/// Garante que os quatro convênios embutidos sempre existam.
/// </summary>
public sealed class ConvenioCatalogoService
{
    private readonly IClinicaRepositorio _repo;

    public ConvenioCatalogoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Todas as entradas (embutidos garantidos + variantes), ordenadas por nome.</summary>
    public async Task<IReadOnlyList<ConvenioCadastro>> ListarAsync(CancellationToken ct = default)
    {
        var salvos = (await _repo.ConveniosAsync(ct)).ToDictionary(c => c.Codigo, StringComparer.OrdinalIgnoreCase);

        // Garante os 4 embutidos mesmo em bancos anteriores ao catálogo.
        foreach (var familia in Enum.GetValues<Convenio>())
        {
            var codigo = familia.ToString();
            if (!salvos.ContainsKey(codigo))
                salvos[codigo] = new ConvenioCadastro
                {
                    Codigo = codigo,
                    Nome = ConvenioInfo.NomeExibicaoPadrao(familia),
                    Familia = familia,
                    Ativo = true
                };
        }

        return salvos.Values.OrderBy(c => c.Nome).ToList();
    }

    /// <summary>Recarrega o cache em memória a partir do banco (chamar no start e após salvar).</summary>
    public async Task RecarregarCacheAsync(CancellationToken ct = default)
    {
        var lista = await ListarAsync(ct);
        CatalogoConvenios.Atualizar(lista.Select(c => new EntradaConvenio(
            c.Codigo, c.Nome, c.Familia, c.Ativo,
            c.Familia == Convenio.Personalizado ? c.ParaConfig() : null)));
    }

    public async Task SalvarAsync(IEnumerable<ConvenioCadastro> convenios, CancellationToken ct = default)
    {
        foreach (var c in convenios)
        {
            c.Codigo = c.Codigo.Trim();
            c.Nome = string.IsNullOrWhiteSpace(c.Nome) ? ConvenioInfo.NomeExibicaoPadrao(c.Familia) : c.Nome.Trim();
            await _repo.SalvarConvenioAsync(c, ct);
        }
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
    }
}
