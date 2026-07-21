using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Catálogo de modalidades de atendimento: lê/grava as entradas no banco e mantém o cache em
/// memória (<see cref="CatalogoModalidades"/>) que serve nome/base de forma síncrona às telas.
/// Garante que as modalidades embutidas sempre existam.
/// </summary>
public sealed class ModalidadeCatalogoService
{
    private readonly IClinicaRepositorio _repo;

    public ModalidadeCatalogoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Todas as entradas (embutidas garantidas + variantes), na ordem da base e por nome.</summary>
    public async Task<IReadOnlyList<ModalidadeCadastro>> ListarAsync(CancellationToken ct = default)
    {
        var salvas = (await _repo.ModalidadesAsync(ct)).ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);

        // Garante as embutidas mesmo em bancos anteriores ao catálogo.
        foreach (var baseEnum in Enum.GetValues<ModalidadeAtendimento>())
        {
            var codigo = baseEnum.ToString();
            if (!salvas.ContainsKey(codigo))
                salvas[codigo] = new ModalidadeCadastro
                {
                    Codigo = codigo,
                    Nome = ModalidadeInfo.NomeExibicao(baseEnum),
                    Base = baseEnum,
                    Ativo = true
                };
        }

        return salvas.Values.OrderBy(m => m.Base).ThenBy(m => m.Nome).ToList();
    }

    /// <summary>Recarrega o cache em memória a partir do banco (chamar no start e após salvar).</summary>
    public async Task RecarregarCacheAsync(CancellationToken ct = default)
    {
        var lista = await ListarAsync(ct);
        CatalogoModalidades.Atualizar(lista.Select(m => new EntradaModalidade(m.Codigo, m.Nome, m.Base, m.Ativo)));
    }

    public async Task SalvarAsync(IEnumerable<ModalidadeCadastro> modalidades, CancellationToken ct = default)
    {
        foreach (var m in modalidades)
        {
            m.Codigo = m.Codigo.Trim();
            m.Nome = string.IsNullOrWhiteSpace(m.Nome) ? ModalidadeInfo.NomeExibicao(m.Base) : m.Nome.Trim();
            await _repo.SalvarModalidadeAsync(m, ct);
        }
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
    }

    /// <summary>É uma das modalidades embutidas (o código coincide com o enum)?</summary>
    public static bool EhEmbutida(string codigo) => Enum.TryParse<ModalidadeAtendimento>(codigo, ignoreCase: false, out _);

    /// <summary>
    /// Exclui uma variante do catálogo. Recusa embutidas e modalidades já usadas em paciente,
    /// atendimento ou agendamento — nesses casos o caminho é desativar.
    /// </summary>
    public async Task<(bool Ok, string Mensagem)> ExcluirAsync(string codigo, CancellationToken ct = default)
    {
        if (EhEmbutida(codigo))
            return (false, "Modalidades embutidas não podem ser excluídas. Desative-a para ocultá-la dos lançamentos.");

        var salva = (await _repo.ModalidadesAsync(ct))
            .FirstOrDefault(m => string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (salva is null)
            return (true, "Modalidade removida."); // nunca chegou a ser salva — nada a excluir no banco

        if (await _repo.ModalidadeEmUsoAsync(salva.Codigo, ct))
            return (false, $"Há registros usando \"{salva.Nome}\". Desative a modalidade em vez de excluir — o histórico é preservado.");

        await _repo.ExcluirModalidadeAsync(salva.Codigo, ct);
        await _repo.SalvarAsync(ct);
        await RecarregarCacheAsync(ct);
        return (true, $"Modalidade \"{salva.Nome}\" excluída.");
    }
}
