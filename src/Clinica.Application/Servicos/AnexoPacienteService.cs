using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os ARQUIVOS DA FICHA do paciente (set/2026): a receita, o laudo, o exame em PDF que
/// pertence à pessoa e não a uma sessão — ver <see cref="AnexoPaciente"/> para a razão de
/// não ser o anexo da evolução.
///
/// As regras são as do laudo em arquivo (<see cref="ResultadoExameService"/>), e uma só
/// definição de cada: <b>o MESMO teto</b> do anexo de prontuário (dois limites divergiriam
/// na primeira correção); data do DOCUMENTO informada e nunca futura; título obrigatório
/// (é o que a lista mostra — arquivo sem título é uma linha que não diz o que é);
/// <b>registro clínico não se apaga</b> — cancela-se com motivo escrito, e a linha fica.
///
/// A MONTAGEM é pública e estática (<see cref="Montar"/>) porque tem DOIS chamadores — o
/// anexo pela tela e a importação do acervo do sistema anterior, que grava em lotes com uma
/// linha de trilha por lote — e duas validações divergiriam na primeira correção.
/// </summary>
public sealed class AnexoPacienteService
{
    private readonly IClinicaRepositorio _repo;

    public AnexoPacienteService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Valida e monta o anexo + os bytes, SEM gravar. Quem grava é o chamador: a tela
    /// (<see cref="AnexarAsync"/>) num SaveChanges com a trilha; a importação, em lotes.
    /// </summary>
    public static (AnexoPaciente Anexo, ArquivoAnexoPaciente Arquivo) Montar(
        int pacienteId, DateOnly data, string titulo, string nomeArquivo, byte[] conteudo,
        string? tipoConteudo, string? observacoes, string? operador, string? chaveImportacao = null)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new InvalidOperationException("Diga o que é o arquivo — o título é obrigatório.");
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            throw new InvalidOperationException(
                "O arquivo precisa de um nome — é por ele que a folha é achada depois.");
        if (conteudo.Length == 0)
            throw new InvalidOperationException("O arquivo está vazio — confira o que foi escolhido.");
        // O MESMO teto do anexo de prontuário: dois limites divergiriam na primeira
        // correção, e o de baixo é o que ninguém lembraria de ajustar.
        if (conteudo.Length > ProntuarioService.TamanhoMaximoAnexo)
            throw new InvalidOperationException(
                $"O arquivo tem {conteudo.Length / (1024 * 1024)} MB e o limite é "
                + $"{ProntuarioService.TamanhoMaximoAnexo / (1024 * 1024)} MB.");

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (data > hoje)
            throw new InvalidOperationException(
                "A data do documento não pode ser futura — o arquivo é registro do que já existe.");
        if (data.Year < 1900)
            throw new InvalidOperationException("Confira a data do documento — o ano não é plausível.");

        var anexo = new AnexoPaciente
        {
            PacienteId = pacienteId,
            Data = data,
            Titulo = Cortar(titulo.Trim(), 160)!,
            NomeArquivo = Cortar(nomeArquivo.Trim(), 260)!,
            TipoConteudo = Cortar(tipoConteudo, 120),
            Tamanho = conteudo.Length,
            Observacoes = Cortar(string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim(), 1000),
            ChaveImportacao = Cortar(chaveImportacao, 160),
            CriadoEm = DateTime.Now,
            CriadoPor = string.IsNullOrWhiteSpace(operador) ? null : Cortar(operador.Trim(), 80)
        };
        var arquivo = new ArquivoAnexoPaciente { Anexo = anexo, Conteudo = conteudo };
        return (anexo, arquivo);
    }

    /// <summary>
    /// Anexa um arquivo à ficha: linha, bytes e trilha no MESMO SaveChanges — arquivo sem a
    /// linha que o descreve é órfão, e linha sem os bytes é um "abrir" que não abre.
    /// </summary>
    public async Task<AnexoPaciente> AnexarAsync(
        int pacienteId, DateOnly data, string titulo, string nomeArquivo, byte[] conteudo,
        string? tipoConteudo = null, string? observacoes = null, string? operador = null,
        CancellationToken ct = default)
    {
        _ = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var (anexo, arquivo) = Montar(
            pacienteId, data, titulo, nomeArquivo, conteudo, tipoConteudo, observacoes, operador);

        await _repo.AdicionarAnexoPacienteAsync(anexo, ct);
        await _repo.AdicionarArquivoAnexoPacienteAsync(arquivo, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AnexoFichaRegistrado",
            Detalhe = $"{anexo.Titulo} ({anexo.NomeArquivo}, {anexo.Tamanho} bytes) — documento de {data:dd/MM/yyyy}",
            PacienteId = pacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
        return anexo;
    }

    /// <summary>
    /// CANCELA um arquivo da ficha (o anexado por engano, o do paciente errado). O motivo
    /// é obrigatório e a linha fica, marcada — registro clínico não se apaga.
    /// </summary>
    public async Task CancelarAsync(
        int anexoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Escreva o motivo do cancelamento.");

        var anexo = await _repo.ObterAnexoPacienteAsync(anexoId, ct)
            ?? throw new InvalidOperationException("Arquivo não encontrado.");
        if (anexo.Cancelado)
            throw new InvalidOperationException("Este arquivo já está cancelado.");

        anexo.CanceladoEm = DateTime.Now;
        anexo.CanceladoPor = string.IsNullOrWhiteSpace(operador) ? null : operador.Trim();
        anexo.MotivoCancelamento = Cortar(motivo.Trim(), 500);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AnexoFichaCancelado",
            Detalhe = $"{anexo.Titulo} ({anexo.NomeArquivo}) — {anexo.MotivoCancelamento}",
            PacienteId = anexo.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Os arquivos da ficha, sem os bytes.</summary>
    public Task<IReadOnlyList<AnexoPaciente>> DaFichaAsync(
        int pacienteId, bool incluirCancelados = false, CancellationToken ct = default)
        => _repo.AnexosDaFichaAsync(pacienteId, incluirCancelados, ct);

    /// <summary>Os bytes, sob demanda.</summary>
    public Task<byte[]?> ConteudoAsync(int anexoId, CancellationToken ct = default)
        => _repo.ConteudoDoAnexoPacienteAsync(anexoId, ct);

    private static string? Cortar(string? texto, int max)
        => texto is null ? null : texto.Length <= max ? texto : texto[..max];
}
