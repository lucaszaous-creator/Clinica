using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Prontuário: a evolução clínica de cada sessão, com a escala de dor EVA medida antes
/// e depois.
///
/// A regra que dá sentido ao resto: a EVA vale em PAR. Uma medida solta não diz se o
/// tratamento funcionou, e por isso só as sessões com o par completo entram na
/// <see cref="EvolucaoDaDorAsync"/> — deixar uma medida pela metade puxar o gráfico
/// faria a linha oscilar por falta de dado, não por dor.
/// </summary>
public sealed class ProntuarioService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Teto de um anexo, em bytes (10 MB). Foto de celular passa longe disso.</summary>
    public const int TamanhoMaximoAnexo = 10 * 1024 * 1024;

    public ProntuarioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Prontuário do paciente, da sessão mais recente para a mais antiga.</summary>
    public Task<IReadOnlyList<Evolucao>> DoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => _repo.EvolucoesDoPacienteAsync(pacienteId, ct);

    public Task<Evolucao?> ObterAsync(int evolucaoId, CancellationToken ct = default)
        => _repo.ObterEvolucaoAsync(evolucaoId, ct);

    /// <summary>
    /// Registra (ou atualiza) a evolução de uma sessão. Grava auditoria no mesmo
    /// SaveChanges: prontuário é documento clínico, e alteração sem rastro não presta.
    /// </summary>
    public async Task<Evolucao> SalvarAsync(Evolucao dados, string? operador = null, CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(dados.PacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        if (!Evolucao.EvaValida(dados.EvaAntes) || !Evolucao.EvaValida(dados.EvaDepois))
            throw new InvalidOperationException(
                $"A escala de dor vai de {Evolucao.EvaMinima} a {Evolucao.EvaMaxima}.");

        // Evolução vazia é ruído no prontuário: alguma coisa tem de ter sido registrada.
        var temTexto = !string.IsNullOrWhiteSpace(dados.QueixaPrincipal)
                       || !string.IsNullOrWhiteSpace(dados.Conduta)
                       || !string.IsNullOrWhiteSpace(dados.TextoEvolucao)
                       || !string.IsNullOrWhiteSpace(dados.Orientacoes);
        if (!temTexto && dados.EvaAntes is null && dados.EvaDepois is null)
            throw new InvalidOperationException(
                "Registre ao menos a dor (EVA) ou um dos campos da evolução.");

        Evolucao destino;
        var novo = dados.Id == 0;
        if (novo)
        {
            destino = new Evolucao
            {
                PacienteId = dados.PacienteId,
                CriadoEm = DateTime.Now,
                CriadoPor = operador
            };
            await _repo.AdicionarEvolucaoAsync(destino, ct);
        }
        else
        {
            destino = await _repo.ObterEvolucaoAsync(dados.Id, ct)
                ?? throw new InvalidOperationException("Evolução não encontrada.");
            destino.AtualizadoEm = DateTime.Now;
        }

        destino.ProfissionalId = dados.ProfissionalId;
        destino.AtendimentoId = dados.AtendimentoId;
        destino.AgendamentoId = dados.AgendamentoId;
        destino.Data = dados.Data;
        destino.EvaAntes = dados.EvaAntes;
        destino.EvaDepois = dados.EvaDepois;
        destino.QueixaPrincipal = Limpar(dados.QueixaPrincipal);
        destino.Conduta = Limpar(dados.Conduta);
        destino.TextoEvolucao = Limpar(dados.TextoEvolucao);
        destino.Orientacoes = Limpar(dados.Orientacoes);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = novo ? "EvolucaoRegistrada" : "EvolucaoAlterada",
            Detalhe = $"Sessão de {destino.Data:dd/MM/yyyy}"
                      + (destino.TemParEva ? $" — EVA {destino.EvaAntes}→{destino.EvaDepois}" : string.Empty),
            PacienteId = destino.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>Apaga uma evolução (e seus anexos). Deixa rastro na auditoria.</summary>
    public async Task ExcluirAsync(int evolucaoId, string? operador = null, CancellationToken ct = default)
    {
        var evolucao = await _repo.ObterEvolucaoAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Evolução não encontrada.");

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "EvolucaoExcluida",
            Detalhe = $"Sessão de {evolucao.Data:dd/MM/yyyy}",
            PacienteId = evolucao.PacienteId
        }, ct);

        await _repo.RemoverEvolucaoAsync(evolucaoId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Como a dor andou ao longo do tratamento. Só entram as sessões com o par EVA
    /// completo, em ordem cronológica (o prontuário vem do mais novo para o mais velho;
    /// o gráfico precisa do contrário).
    /// </summary>
    public async Task<EvolucaoDaDor> EvolucaoDaDorAsync(int pacienteId, CancellationToken ct = default)
    {
        var evolucoes = await _repo.EvolucoesDoPacienteAsync(pacienteId, ct);

        var pontos = evolucoes
            .Where(e => e.TemParEva)
            .OrderBy(e => e.Data).ThenBy(e => e.Id)
            .Select(e => new PontoEva(e.Data, e.EvaAntes!.Value, e.EvaDepois!.Value))
            .ToList();

        return new EvolucaoDaDor(pontos, evolucoes.Count);
    }

    // ---------------- Anexos ----------------

    public Task<IReadOnlyList<AnexoResumo>> AnexosAsync(int evolucaoId, CancellationToken ct = default)
        => _repo.AnexosDaEvolucaoAsync(evolucaoId, ct);

    /// <summary>Bytes de um anexo — a única leitura que traz o arquivo do banco.</summary>
    public Task<byte[]?> ConteudoAnexoAsync(int anexoId, CancellationToken ct = default)
        => _repo.ConteudoDoAnexoAsync(anexoId, ct);

    public async Task<AnexoProntuario> AnexarAsync(
        int evolucaoId, string nomeArquivo, byte[] conteudo,
        TipoAnexo tipo = TipoAnexo.Documento, string? tipoConteudo = null,
        string? descricao = null, string? operador = null, CancellationToken ct = default)
    {
        var evolucao = await _repo.ObterEvolucaoAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Evolução não encontrada.");

        if (conteudo.Length == 0)
            throw new InvalidOperationException("O arquivo está vazio.");

        // O limite protege o banco remoto: um anexo gigante trava a sincronização de
        // todo mundo, e o erro apareceria longe da causa.
        if (conteudo.Length > TamanhoMaximoAnexo)
            throw new InvalidOperationException(
                $"O arquivo tem {conteudo.Length / (1024 * 1024)} MB e o limite é "
                + $"{TamanhoMaximoAnexo / (1024 * 1024)} MB.");

        var anexo = new AnexoProntuario
        {
            EvolucaoId = evolucaoId,
            NomeArquivo = nomeArquivo.Trim(),
            Tipo = tipo,
            TipoConteudo = tipoConteudo,
            Conteudo = conteudo,
            Tamanho = conteudo.Length,
            Descricao = Limpar(descricao),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        await _repo.AdicionarAnexoAsync(anexo, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AnexoProntuario",
            Detalhe = $"{anexo.NomeArquivo} na sessão de {evolucao.Data:dd/MM/yyyy}",
            PacienteId = evolucao.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return anexo;
    }

    public async Task RemoverAnexoAsync(int anexoId, CancellationToken ct = default)
    {
        await _repo.RemoverAnexoAsync(anexoId, ct);
        await _repo.SalvarAsync(ct);
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
