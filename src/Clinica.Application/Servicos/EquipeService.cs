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

        // A jornada (set/2026): horário vem em PAR, e o fim depois do início. A recusa mora
        // aqui e não na tela porque o cadastro tem mais de uma porta (a tela da Recepção e o
        // que a importação vier a gravar), e metade de um horário não é horário.
        if ((dados.AtendeDas is null) != (dados.AtendeAte is null))
            throw new InvalidOperationException(
                "Informe o início E o fim do horário de atendimento — ou deixe os dois em branco.");
        if (dados.AtendeDas is { } das && dados.AtendeAte is { } ate && ate <= das)
            throw new InvalidOperationException("O fim do horário de atendimento precisa ser depois do início.");

        var cpf = await CriticarCpfAsync(dados, ct);

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

        // O CPF: a coluna existe desde a parcela 42 e este é o PRIMEIRO gravador dela.
        // Até aqui ela tinha três leitores — as duas assinaturas e o login por certificado —
        // e nenhuma escrita em todo o código, o que fazia a assinatura ICP-Brasil recusar
        // SEMPRE, com a frase "cadastre o CPF em Equipe" apontando para uma tela que não
        // tinha o campo. É o defeito recorrente do projeto na forma inversa: leitor sem
        // gravador, que o CI não vê porque nada quebra — só não funciona.
        destino.Cpf = cpf;
        destino.EspecialidadeCodigo = Limpar(dados.EspecialidadeCodigo);
        destino.Telefone = Limpar(dados.Telefone);
        destino.Email = Limpar(dados.Email);
        destino.Cor = Limpar(dados.Cor);
        destino.DuracaoPadraoMinutos = dados.DuracaoPadraoMinutos;
        // Zero dias não é jornada: vira "não declarado". E só os sete bits valem — um valor
        // fora deles seria um dia que não existe.
        destino.DiasDeAtendimento = dados.DiasDeAtendimento is { } dias && (dias & Profissional.TodosOsDias) != 0
            ? dias & Profissional.TodosOsDias
            : null;
        destino.AtendeDas = dados.AtendeDas;
        destino.AtendeAte = dados.AtendeAte;
        destino.Ativo = dados.Ativo;
        destino.Ordem = dados.Ordem;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Critica o CPF do profissional e devolve só os dígitos (ou null).
    ///
    /// Duas recusas, e as duas existem por causa da assinatura digital:
    ///
    /// <b>CPF inválido é recusado</b> porque ele não é um dado de cadastro qualquer — é a
    /// chave que amarra o certificado ICP-Brasil à pessoa. Aceitar "111" faria a médica
    /// descobrir o erro no dia em que fosse assinar, com o paciente na sala e a mensagem
    /// dizendo que o certificado é de outra pessoa.
    ///
    /// <b>CPF repetido é recusado</b> porque a junção com o certificado é por VALOR, não por
    /// chave estrangeira: dois profissionais com o mesmo CPF tornam ambígua a resposta a
    /// "quem assinou?" e a "quem está entrando?". A recusa mora aqui, na escrita, e não
    /// num índice único do banco, de propósito: migration com índice único falharia no
    /// <c>MigrateAsync</c> da abertura se a base da clínica já tivesse duplicata — e quem
    /// não abriria seria o faturamento, que roda em produção.
    /// </summary>
    private async Task<string?> CriticarCpfAsync(Profissional dados, CancellationToken ct)
    {
        var cpf = Domain.Cpf.Normalizar(dados.Cpf);

        // Vazio é o caso NORMAL: quem não assina digitalmente não precisa de CPF, e exigi-lo
        // travaria o cadastro da equipe inteira por causa de uma feature que nem toda
        // clínica usa.
        if (cpf.Length == 0) return null;

        if (!Domain.Cpf.Valido(cpf))
            throw new InvalidOperationException(
                "O CPF informado não é válido. Ele é o que liga o certificado digital a esta "
                + "pessoa na hora de assinar, então precisa estar correto — ou em branco.");

        var repetido = (await _repo.ProfissionaisAsync(ct))
            .FirstOrDefault(p => p.Id != dados.Id && Domain.Cpf.Normalizar(p.Cpf) == cpf);

        if (repetido is not null)
            throw new InvalidOperationException(
                $"O CPF {Domain.Cpf.Formatar(cpf)} já está cadastrado para {repetido.Nome}. "
                + "Dois profissionais com o mesmo CPF tornariam impossível saber quem assinou "
                + "um documento.");

        return cpf;
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
