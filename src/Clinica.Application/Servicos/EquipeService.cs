using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Cadastro de quem atende e de onde se atende: profissionais e salas. É a fundação
/// da recepção — a agenda multiprofissional, o repasse no financeiro e a produtividade
/// no BI se apoiam neste cadastro.
///
/// Excluir só é permitido enquanto o registro não foi usado; depois disso o caminho é
/// desativar, para não sumir com o passado da agenda.
/// </summary>
public sealed class EquipeService
{
    private readonly IClinicaRepositorio _repo;

    public EquipeService(IClinicaRepositorio repo) => _repo = repo;

    // ---------------- Profissionais ----------------

    public Task<IReadOnlyList<Profissional>> ProfissionaisAsync(CancellationToken ct = default)
        => _repo.ProfissionaisAsync(ct);

    /// <summary>Só os que estão na ativa — é o que a agenda oferece para marcar.</summary>
    public async Task<IReadOnlyList<Profissional>> ProfissionaisAtivosAsync(CancellationToken ct = default)
        => (await _repo.ProfissionaisAsync(ct)).Where(p => p.Ativo).ToList();

    public Task<Profissional?> ObterProfissionalAsync(int id, CancellationToken ct = default)
        => _repo.ObterProfissionalAsync(id, ct);

    /// <summary>Cria ou atualiza um profissional. Devolve a entidade persistida.</summary>
    public async Task<Profissional> SalvarProfissionalAsync(Profissional dados, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Informe o nome do profissional.");

        if (dados.DuracaoPadraoMinutos is { } duracao && duracao <= 0)
            throw new InvalidOperationException("A duração padrão precisa ser maior que zero.");

        Profissional destino;
        if (dados.Id == 0)
        {
            destino = new Profissional();
            await _repo.AdicionarProfissionalAsync(destino, ct);
        }
        else
        {
            destino = await _repo.ObterProfissionalAsync(dados.Id, ct)
                ?? throw new InvalidOperationException("Profissional não encontrado.");
        }

        destino.Nome = dados.Nome.Trim();
        destino.NomeCurto = Limpar(dados.NomeCurto);
        destino.RegistroConselho = Limpar(dados.RegistroConselho);
        destino.EspecialidadeCodigo = Limpar(dados.EspecialidadeCodigo);
        destino.Telefone = Limpar(dados.Telefone);
        destino.Email = Limpar(dados.Email);
        destino.Cor = Limpar(dados.Cor);
        destino.DuracaoPadraoMinutos = dados.DuracaoPadraoMinutos;
        destino.Ativo = dados.Ativo;
        destino.Ordem = dados.Ordem;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Exclui o profissional. Recusa quando já existe agenda ou lista de espera
    /// apontando para ele — nesse caso o certo é desativar.
    /// </summary>
    public async Task ExcluirProfissionalAsync(int id, CancellationToken ct = default)
    {
        if (await _repo.ProfissionalEmUsoAsync(id, ct))
            throw new InvalidOperationException(
                "Este profissional já tem agenda registrada. Desative-o em vez de excluir, "
                + "para não apagar o histórico.");

        await _repo.RemoverProfissionalAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    // ---------------- Salas ----------------

    public Task<IReadOnlyList<Sala>> SalasAsync(CancellationToken ct = default)
        => _repo.SalasAsync(ct);

    public async Task<IReadOnlyList<Sala>> SalasAtivasAsync(CancellationToken ct = default)
        => (await _repo.SalasAsync(ct)).Where(s => s.Ativa).ToList();

    public Task<Sala?> ObterSalaAsync(int id, CancellationToken ct = default)
        => _repo.ObterSalaAsync(id, ct);

    public async Task<Sala> SalvarSalaAsync(Sala dados, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Informe o nome da sala.");

        if (dados.Capacidade < 1)
            throw new InvalidOperationException("A capacidade da sala precisa ser pelo menos 1.");

        var nome = dados.Nome.Trim();
        var existentes = await _repo.SalasAsync(ct);
        if (existentes.Any(s => s.Id != dados.Id
                                && string.Equals(s.Nome, nome, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Já existe uma sala chamada \"{nome}\".");

        Sala destino;
        if (dados.Id == 0)
        {
            destino = new Sala();
            await _repo.AdicionarSalaAsync(destino, ct);
        }
        else
        {
            destino = await _repo.ObterSalaAsync(dados.Id, ct)
                ?? throw new InvalidOperationException("Sala não encontrada.");
        }

        destino.Nome = nome;
        destino.Capacidade = dados.Capacidade;
        destino.Ativa = dados.Ativa;
        destino.Ordem = dados.Ordem;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    public async Task ExcluirSalaAsync(int id, CancellationToken ct = default)
    {
        if (await _repo.SalaEmUsoAsync(id, ct))
            throw new InvalidOperationException(
                "Esta sala já tem agenda registrada. Desative-a em vez de excluir, "
                + "para não apagar o histórico.");

        await _repo.RemoverSalaAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
