using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Aplicação e histórico dos instrumentos de avaliação clínica (parcela 36).
///
/// Por que existe
/// --------------
/// A EVA responde "quanto dói" e é o que a acupuntura precisa. As outras quatro
/// especialidades da clínica não têm pergunta com resposta em número: a psiquiatria
/// pergunta como o humor andou, a geriatria o que a pessoa ainda faz sozinha, a
/// neurocirurgia o que a dor impede de fazer, a endocrinologia o risco que o paciente
/// carrega. Até aqui isso tudo virava texto livre na evolução — e texto livre não
/// desenha curva, não compara consulta com consulta e não vai para o relatório.
///
/// As regras
/// ---------
/// - **Tudo o que descreve o instrumento é COPIADO na aplicação.** Nome, enunciado,
///   rótulo da alternativa, faixa e interpretação. Corrigir a redação de uma pergunta
///   amanhã não pode reescrever o que o paciente respondeu hoje.
/// - **Escala incompleta é recusada**, salvo quando o próprio instrumento prevê omissão
///   (só o Oswestry). Numa escala de soma, o item em branco vira zero — e zero significa
///   "não tenho esse sintoma": a pergunta não respondida viraria resposta não dada.
/// - **Peso fora das alternativas é recusado.** Aceitar um valor que o item não oferece
///   produziria um escore que a escala não sabe interpretar, com aparência de escore
///   válido.
/// - **O alerta de item não se dissolve no total** — ver <see cref="IInstrumentoAvaliacao.AlertaDeItem"/>.
/// - O sistema PONTUA e REGISTRA. Ele não diagnostica: a faixa é a leitura publicada da
///   escala, e a conduta é de quem assina o prontuário.
/// </summary>
public sealed class AvaliacaoClinicaService
{
    private readonly IClinicaRepositorio _repo;

    public AvaliacaoClinicaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Instrumentos oferecidos a uma especialidade (null = todos).</summary>
    public static IReadOnlyList<IInstrumentoAvaliacao> Disponiveis(string? especialidadeCodigo)
        => RegistroInstrumentos.DaEspecialidade(especialidadeCodigo);

    /// <summary>O instrumento de um código, ou null quando não está mais no catálogo.</summary>
    public static IInstrumentoAvaliacao? Instrumento(string? codigo)
        => RegistroInstrumentos.Obter(codigo);

    /// <summary>Avaliações do paciente (sem as respostas), da mais recente para a mais antiga.</summary>
    public Task<IReadOnlyList<AvaliacaoClinica>> DoPacienteAsync(
        int pacienteId, string? instrumentoCodigo = null, CancellationToken ct = default)
        => _repo.AvaliacoesDoPacienteAsync(pacienteId, instrumentoCodigo, ct: ct);

    /// <summary>Uma aplicação inteira, com as respostas — a segunda via da avaliação.</summary>
    public Task<AvaliacaoClinica?> ObterAsync(int avaliacaoId, CancellationToken ct = default)
        => _repo.ObterAvaliacaoAsync(avaliacaoId, ct);

    /// <summary>
    /// Aplica um instrumento e grava o resultado.
    /// </summary>
    /// <param name="respostas">Código do item → peso da alternativa escolhida.</param>
    public async Task<AvaliacaoClinica> AplicarAsync(
        int pacienteId,
        string instrumentoCodigo,
        IReadOnlyDictionary<string, int> respostas,
        DateOnly data,
        int? profissionalId = null,
        int? evolucaoId = null,
        string? especialidadeCodigo = null,
        string? observacoes = null,
        string? operador = null,
        CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(pacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        var instrumento = RegistroInstrumentos.Obter(instrumentoCodigo)
            ?? throw new InvalidOperationException(
                $"Instrumento \"{instrumentoCodigo}\" não existe no catálogo de avaliações.");

        var validas = Validar(instrumento, respostas);

        var pontuacao = instrumento.Pontuar(validas);
        var faixa = instrumento.FaixaDe(pontuacao);

        var avaliacao = new AvaliacaoClinica
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            EvolucaoId = evolucaoId,
            Data = data,
            InstrumentoCodigo = instrumento.Codigo,
            // Daqui para baixo é tudo CÓPIA: a avaliação tem de continuar legível mesmo
            // que o instrumento mude de redação, de faixa ou saia do catálogo.
            InstrumentoNome = instrumento.Nome,
            EspecialidadeCodigo = Limpar(especialidadeCodigo),
            Pontuacao = pontuacao,
            PontuacaoMaxima = instrumento.PontuacaoMaxima,
            Unidade = instrumento.Unidade,
            FaixaNome = faixa?.Nome,
            FaixaInterpretacao = faixa?.Interpretacao,
            FaixaGravidade = faixa?.Gravidade ?? GravidadeFaixa.Normal,
            AlertaItem = instrumento.AlertaDeItem(validas),
            Observacoes = Limpar(observacoes),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        var ordem = 0;
        foreach (var item in instrumento.Itens)
        {
            if (!validas.TryGetValue(item.Codigo, out var valor)) continue;

            avaliacao.Respostas.Add(new RespostaAvaliacao
            {
                Ordem = ordem++,
                ItemCodigo = item.Codigo,
                Enunciado = item.Enunciado,
                Valor = valor,
                OpcaoRotulo = item.RotuloDe(valor) ?? string.Empty
            });
        }

        await _repo.AdicionarAvaliacaoAsync(avaliacao, ct);

        // Auditoria no MESMO SaveChanges, como toda escrita de prontuário.
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AvaliacaoAplicada",
            Detalhe = $"{instrumento.Nome} em {data:dd/MM/yyyy} — {avaliacao.PontuacaoFormatada}"
                      + (faixa is null ? string.Empty : $" ({faixa.Nome})"),
            PacienteId = pacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return avaliacao;
    }

    /// <summary>
    /// CANCELA uma aplicação. Não apaga (parcela 52).
    ///
    /// O caso que motivava a exclusão — o questionário aplicado no paciente errado —
    /// continua resolvido: a avaliação sai da série, do gráfico e do relatório. O que
    /// muda é que ela não deixa de existir, e aqui isso pesa mais do que na evolução: é
    /// o escore que decidiu encaminhar (ou não) um paciente que marcou o item 9 do
    /// PHQ-9, e a Lei 13.787/2018 pede justamente que esse registro sobreviva à opinião
    /// posterior de alguém sobre ele.
    /// </summary>
    public async Task CancelarAsync(
        int avaliacaoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que a avaliação está sendo cancelada.");

        var avaliacao = await _repo.ObterAvaliacaoAsync(avaliacaoId, ct)
            ?? throw new InvalidOperationException("Avaliação não encontrada.");

        if (avaliacao.Cancelada)
            throw new InvalidOperationException("Esta avaliação já está cancelada.");

        avaliacao.CanceladaEm = DateTime.Now;
        avaliacao.MotivoCancelamento = motivo.Trim();
        avaliacao.CanceladaPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AvaliacaoCancelada",
            Detalhe = $"{avaliacao.InstrumentoNome} de {avaliacao.Data:dd/MM/yyyy} — {motivo.Trim()}",
            PacienteId = avaliacao.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Como o escore de um instrumento andou para este paciente, em ordem cronológica —
    /// a lista vem do mais novo para o mais velho, e o gráfico precisa do contrário.
    ///
    /// Os rótulos de faixa saem do que foi GRAVADO em cada aplicação, não de recalcular
    /// pela definição de hoje: se as faixas do instrumento mudarem, o histórico continua
    /// dizendo o que foi dito ao paciente na época.
    /// </summary>
    public async Task<EvolucaoDoEscore> EvolucaoDoEscoreAsync(
        int pacienteId, string instrumentoCodigo, CancellationToken ct = default)
    {
        var aplicacoes = await _repo.AvaliacoesDoPacienteAsync(pacienteId, instrumentoCodigo, ct: ct);

        var pontos = aplicacoes
            .OrderBy(a => a.Data).ThenBy(a => a.Id)
            .Select(a => new PontoEscore(a.Data, a.Pontuacao, a.FaixaNome, a.FaixaGravidade))
            .ToList();

        var instrumento = RegistroInstrumentos.Obter(instrumentoCodigo);

        // O nome sai da última aplicação quando o instrumento não está mais no catálogo:
        // ele foi copiado justamente para o histórico não virar um código solto.
        var nome = instrumento?.Nome
                   ?? aplicacoes.FirstOrDefault()?.InstrumentoNome
                   ?? instrumentoCodigo;

        return new EvolucaoDoEscore(
            instrumentoCodigo,
            nome,
            instrumento?.Unidade ?? aplicacoes.FirstOrDefault()?.Unidade ?? "pontos",
            instrumento?.PontuacaoMaxima ?? aplicacoes.FirstOrDefault()?.PontuacaoMaxima ?? 0,
            instrumento?.MelhorQuandoMenor ?? true,
            pontos);
    }

    /// <summary>
    /// Confere as respostas contra a definição do instrumento e devolve o conjunto que
    /// vai ser pontuado.
    ///
    /// Item que o instrumento não conhece é IGNORADO em silêncio — questionário antigo
    /// aberto numa versão nova mandaria códigos que já não existem, e recusar a aplicação
    /// inteira por causa disso deixaria o profissional sem saída. Peso inválido e escala
    /// incompleta, ao contrário, são recusados: os dois produzem escore errado com cara
    /// de escore certo.
    /// </summary>
    private static Dictionary<string, int> Validar(
        IInstrumentoAvaliacao instrumento, IReadOnlyDictionary<string, int> respostas)
    {
        var validas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var faltando = new List<string>();

        foreach (var item in instrumento.Itens)
        {
            if (!respostas.TryGetValue(item.Codigo, out var valor))
            {
                faltando.Add(item.Codigo);
                continue;
            }

            if (item.RotuloDe(valor) is null)
                throw new InvalidOperationException(
                    $"Resposta inválida em \"{item.Enunciado}\": {valor} não é uma das "
                    + "alternativas do item.");

            validas[item.Codigo] = valor;
        }

        if (validas.Count == 0)
            throw new InvalidOperationException(
                $"Responda ao menos um item de {instrumento.Nome} antes de gravar.");

        if (faltando.Count > 0 && !instrumento.AceitaItemOmitido)
            throw new InvalidOperationException(
                $"Faltam {faltando.Count} de {instrumento.Itens.Count} respostas. "
                + $"{instrumento.Nome} só tem leitura válida com a escala completa — "
                + "item em branco entraria como zero, que é uma resposta que o paciente não deu.");

        return validas;
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
