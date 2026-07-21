using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Catálogo de especialidades de consulta: lê/grava as entradas no banco e mantém o cache em
/// memória (<see cref="CatalogoEspecialidades"/>). Garante que as embutidas sempre existam.
/// </summary>
public sealed class EspecialidadeCatalogoService
{
    private readonly IClinicaRepositorio _repo;

    public EspecialidadeCatalogoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Todas as entradas (embutidas garantidas + adicionadas), ordenadas por nome.</summary>
    public async Task<IReadOnlyList<EspecialidadeCadastro>> ListarAsync(CancellationToken ct = default)
    {
        var salvas = (await _repo.EspecialidadesAsync(ct)).ToDictionary(e => e.Codigo, StringComparer.OrdinalIgnoreCase);

        // Garante as embutidas mesmo em bancos anteriores ao catálogo.
        foreach (var esp in Enum.GetValues<Especialidade>())
        {
            var codigo = esp.ToString();
            if (!salvas.ContainsKey(codigo))
                salvas[codigo] = new EspecialidadeCadastro
                {
                    Codigo = codigo,
                    Nome = EspecialidadeInfo.NomeExibicao(esp),
                    Ativo = true
                };
        }

        return salvas.Values.OrderBy(e => e.Nome).ToList();
    }

    /// <summary>Recarrega o cache em memória a partir do banco (chamar no start e após salvar).</summary>
    public async Task RecarregarCacheAsync(CancellationToken ct = default)
    {
        var lista = await ListarAsync(ct);
        CatalogoEspecialidades.Atualizar(lista.Select(e => new EntradaEspecialidade(e.Codigo, e.Nome, e.Ativo)));
    }

    public async Task SalvarAsync(IEnumerable<EspecialidadeCadastro> especialidades, CancellationToken ct = default)
    {
        foreach (var e in especialidades)
        {
            e.Codigo = e.Codigo.Trim();
            e.Nome = string.IsNullOrWhiteSpace(e.Nome) ? e.Codigo : e.Nome.Trim();
            await _repo.SalvarEspecialidadeAsync(e, ct);
        }
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
    }

    /// <summary>É uma das especialidades embutidas (o código coincide com o enum)?</summary>
    public static bool EhEmbutida(string codigo) => Enum.TryParse<Especialidade>(codigo, ignoreCase: false, out _);

    /// <summary>
    /// Exclui uma especialidade do catálogo. Recusa embutidas (a rotação da Petrobras depende
    /// delas) e especialidades já usadas em atendimentos/guias — nesses casos, desativar.
    /// </summary>
    public async Task<(bool Ok, string Mensagem)> ExcluirAsync(string codigo, CancellationToken ct = default)
    {
        if (EhEmbutida(codigo))
            return (false, "Especialidades embutidas não podem ser excluídas. Desative-a para ocultá-la dos lançamentos.");

        var salva = (await _repo.EspecialidadesAsync(ct))
            .FirstOrDefault(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (salva is null)
            return (true, "Especialidade removida."); // nunca chegou a ser salva — nada a excluir no banco

        if (await _repo.EspecialidadeEmUsoAsync(salva.Codigo, ct))
            return (false, $"Há registros usando \"{salva.Nome}\". Desative a especialidade em vez de excluir — o histórico é preservado.");

        await _repo.ExcluirEspecialidadeAsync(salva.Codigo, ct);
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
        return (true, $"Especialidade \"{salva.Nome}\" excluída.");
    }
}
