using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Tabela de preço do PARTICULAR por especialidade atendida (set/2026).
///
/// **Cadastrada no Gerente, lida pela Recepção e pelo Financeiro.** Quem decide quanto a
/// clínica cobra é a direção; quem cobra é o balcão (no Finalizar) e quem confere é o
/// Financeiro (na aba Particulares da Conciliação). O mesmo banco, nenhuma cópia.
///
/// Três regras, herdadas da tabela por convênio e pela mesma razão:
///
/// 1. **A mais ESPECÍFICA ganha.** Preço com especialidade vence o genérico da modalidade;
///    código de variante vence o da família. Sem isso a clínica cadastraria a exceção e o
///    balcão continuaria vendo o preço geral.
/// 2. **Vigência.** Reajuste é linha nova; a sessão de março segue valendo o preço de março.
/// 3. **Sem preço cadastrado não se inventa valor** — devolve <see cref="PrecoProposto.Nenhum"/>
///    e o campo fica vazio para ser digitado, com a procedência dizendo por quê.
/// </summary>
public sealed class PrecoParticularService
{
    private readonly IClinicaRepositorio _repo;

    public PrecoParticularService(IClinicaRepositorio repo) => _repo = repo;

    public Task<IReadOnlyList<PrecoParticular>> CatalogoAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => _repo.PrecosParticularAsync(somenteAtivos, ct);

    public async Task<PrecoParticular> SalvarAsync(
        PrecoParticular dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.ModalidadeCodigo))
            throw new InvalidOperationException("Diga de qual modalidade é o preço (acupuntura, consulta, BSV…).");
        if (dados.Valor <= 0m)
            throw new InvalidOperationException(
                "O valor deve ser maior que zero — preço zero é o mesmo que não ter tabela, "
                + "e nesse caso é melhor não cadastrar.");
        if (dados.VigenteDe is { } de && dados.VigenteAte is { } ate && de > ate)
            throw new InvalidOperationException("A vigência começa antes de terminar.");

        var preco = dados.Id == 0
            ? null
            : await _repo.ObterPrecoParticularAsync(dados.Id, ct)
              ?? throw new InvalidOperationException("Preço não encontrado.");

        if (preco is null)
        {
            preco = new PrecoParticular { CriadoPor = operador };
            await _repo.AdicionarPrecoParticularAsync(preco, ct);
        }

        preco.ModalidadeCodigo = dados.ModalidadeCodigo.Trim();
        // "Vale para todas" é NULO aqui (a chave não é única): string vazia viraria uma
        // especialidade chamada "" que nunca casa com nada.
        preco.EspecialidadeCodigo = string.IsNullOrWhiteSpace(dados.EspecialidadeCodigo)
            ? null : dados.EspecialidadeCodigo.Trim();
        preco.Valor = dados.Valor;
        preco.VigenteDe = dados.VigenteDe;
        preco.VigenteAte = dados.VigenteAte;
        preco.Ativo = dados.Ativo;
        preco.Observacoes = string.IsNullOrWhiteSpace(dados.Observacoes) ? null : dados.Observacoes.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = dados.Id == 0 ? "PrecoParticularCriado" : "PrecoParticularAtualizado",
            Detalhe = $"{preco.Descricao} · {preco.Vigencia}",
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
        }, ct);
        await _repo.SalvarAsync(ct);
        return preco;
    }

    public async Task ExcluirAsync(int precoId, CancellationToken ct = default)
    {
        await _repo.RemoverPrecoParticularAsync(precoId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// O preço que vale para esta sessão. Candidatos: vigentes no dia, cuja modalidade casa
    /// pelo CÓDIGO ou pela FAMÍLIA (o código embutido da família é o nome do enum) e cuja
    /// especialidade é a da sessão ou "qualquer". Vence o mais específico; entre iguais, o
    /// reajuste mais novo.
    /// </summary>
    public async Task<PrecoParticular?> ResolverAsync(
        string? modalidadeCodigo,
        ModalidadeAtendimento familia,
        string? especialidadeCodigo,
        DateOnly dia,
        CancellationToken ct = default)
    {
        var codigo = string.IsNullOrWhiteSpace(modalidadeCodigo) ? familia.ToString() : modalidadeCodigo;
        var codigoDaFamilia = familia.ToString();
        var especialidade = string.IsNullOrWhiteSpace(especialidadeCodigo) ? null : especialidadeCodigo;

        var candidatos = (await _repo.PrecosParticularAsync(somenteAtivos: true, ct))
            .Where(p => p.VigenteEm(dia))
            .Select(p => (Preco: p, Pontos: Pontuar(p, codigo, codigoDaFamilia, especialidade)))
            .Where(c => c.Pontos >= 0)
            .ToList();

        return candidatos
            .OrderByDescending(c => c.Pontos)
            .ThenByDescending(c => c.Preco.VigenteDe ?? DateOnly.MinValue)
            .ThenByDescending(c => c.Preco.Id)
            .Select(c => c.Preco)
            .FirstOrDefault();
    }

    /// <summary>
    /// -1 = não serve. Modalidade: código exato vale 2, família vale 1. Especialidade:
    /// declarada e igual vale +1, "qualquer" vale 0, declarada e diferente não serve.
    /// </summary>
    private static int Pontuar(PrecoParticular p, string codigo, string codigoDaFamilia, string? especialidade)
    {
        int modalidade;
        if (string.Equals(p.ModalidadeCodigo, codigo, StringComparison.OrdinalIgnoreCase)) modalidade = 2;
        else if (string.Equals(p.ModalidadeCodigo, codigoDaFamilia, StringComparison.OrdinalIgnoreCase)) modalidade = 1;
        else return -1;

        if (string.IsNullOrWhiteSpace(p.EspecialidadeCodigo)) return modalidade;
        if (especialidade is not null
            && string.Equals(p.EspecialidadeCodigo, especialidade, StringComparison.OrdinalIgnoreCase))
            return modalidade + 1;
        return -1;
    }

    /// <summary>O valor a propor, com a procedência escrita. Não grava nada.</summary>
    public async Task<PrecoProposto> ProporAsync(
        string? modalidadeCodigo, ModalidadeAtendimento familia, string? especialidadeCodigo,
        DateOnly dia, CancellationToken ct = default)
    {
        var preco = await ResolverAsync(modalidadeCodigo, familia, especialidadeCodigo, dia, ct);
        return preco is null
            ? PrecoProposto.Nenhum
            : new PrecoProposto(preco.Valor, $"tabela do particular: {preco.Descricao} ({preco.Vigencia})");
    }
}
