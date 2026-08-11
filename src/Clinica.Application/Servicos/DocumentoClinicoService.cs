using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os documentos clínicos que saem da Recepção: receita, atestado, declaração de
/// comparecimento, pedido de exame, relatório de evolução, termo de consentimento e
/// anamnese (feature 07 e a página 21 da proposta).
///
/// Duas decisões mandam em tudo o que está aqui:
///
/// 1. <b>Documento emitido é fato.</b> Uma vez impresso e entregue, existe no mundo —
///    não se apaga nem se reescreve. Corrige-se CANCELANDO com motivo e emitindo outro,
///    como o consentimento da parcela 2 faz com a revogação.
/// 2. <b>O conteúdo fica gravado, não é remontado na reimpressão.</b> A segunda via de
///    um relatório tem de sair idêntica à que o paciente levou, mesmo que o prontuário
///    tenha andado desde então. Por isso até os documentos montados a partir do
///    prontuário gravam suas linhas na emissão.
/// </summary>
public sealed class DocumentoClinicoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly ConsentimentoService _consentimentos;

    /// <summary>Alfabeto do código de conferência, sem os caracteres que se confundem à mão (I, O, 0, 1).</summary>
    private const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Roteiro da anamnese. O que o sistema sabe vem preenchido; o resto sai em linhas.</summary>
    public static readonly IReadOnlyList<string> RoteiroAnamnese =
    [
        "Queixa principal",
        "História da doença atual",
        "Antecedentes pessoais e cirúrgicos",
        "Medicações em uso",
        "Alergias",
        "Hábitos (sono, atividade física, alimentação)",
        "Antecedentes familiares",
        "Exame físico",
        "Hipótese diagnóstica",
        "Conduta inicial"
    ];

    public DocumentoClinicoService(
        IClinicaRepositorio repo, ProntuarioService prontuario, ConsentimentoService consentimentos)
    {
        _repo = repo;
        _prontuario = prontuario;
        _consentimentos = consentimentos;
    }

    // ==================== Leitura ====================

    /// <summary>Documentos do paciente, do mais recente para o mais antigo.</summary>
    public Task<IReadOnlyList<DocumentoClinico>> DoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => _repo.DocumentosDoPacienteAsync(pacienteId, ct);

    public Task<DocumentoClinico?> ObterAsync(int documentoId, CancellationToken ct = default)
        => _repo.ObterDocumentoAsync(documentoId, ct);

    /// <summary>Confere uma via em papel pelo código impresso no rodapé.</summary>
    public Task<DocumentoClinico?> PorCodigoAsync(string codigo, CancellationToken ct = default)
        => _repo.ObterDocumentoPorCodigoAsync((codigo ?? string.Empty).Trim().ToUpperInvariant(), ct);

    // ==================== Emissão ====================

    /// <summary>
    /// Emite um documento escrito pelo profissional (receita, atestado, declaração,
    /// pedido de exame). Numera, gera o código de conferência e grava auditoria no
    /// mesmo SaveChanges.
    /// </summary>
    public async Task<DocumentoClinico> EmitirAsync(
        DocumentoClinico dados, string? operador = null, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(dados.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var itens = dados.Itens
            .Where(i => !string.IsNullOrWhiteSpace(i.Descricao))
            .ToList();

        await ValidarAsync(dados, itens, ct);

        var data = dados.Data == default ? DateOnly.FromDateTime(DateTime.Today) : dados.Data;

        var documento = new DocumentoClinico
        {
            Numero = await NumerarAsync(data.Year, ct),
            CodigoVerificacao = GerarCodigo(),
            Tipo = dados.Tipo,
            PacienteId = dados.PacienteId,
            ProfissionalId = dados.ProfissionalId,
            EvolucaoId = dados.EvolucaoId,
            Data = data,
            Titulo = Limpar(dados.Titulo),
            Corpo = Limpar(dados.Corpo),
            Observacoes = Limpar(dados.Observacoes),
            DiasAfastamento = dados.DiasAfastamento,
            Cid = Limpar(dados.Cid),
            CidAutorizado = dados.CidAutorizado,
            PeriodoInicio = dados.PeriodoInicio,
            PeriodoFim = dados.PeriodoFim,
            HoraChegada = dados.HoraChegada,
            HoraSaida = dados.HoraSaida,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        var ordem = 1;
        foreach (var i in itens)
            documento.Itens.Add(new ItemDocumento
            {
                Ordem = ordem++,
                Descricao = i.Descricao.Trim(),
                Detalhe = Limpar(i.Detalhe),
                Quantidade = Limpar(i.Quantidade)
            });

        await _repo.AdicionarDocumentoAsync(documento, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "DocumentoClinicoEmitido",
            Detalhe = $"{TipoDocumentoInfo.Rotular(documento.Tipo)} {documento.Numero} — {paciente.Nome}",
            PacienteId = documento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return documento;
    }

    /// <summary>
    /// Cancela um documento emitido. NÃO apaga: a via que o paciente levou continua no
    /// mundo, e o registro é o que prova que ela não vale mais.
    /// </summary>
    public async Task CancelarAsync(
        int documentoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que o documento está sendo cancelado.");

        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");

        if (documento.Cancelado)
            throw new InvalidOperationException("Este documento já foi cancelado.");

        documento.CanceladoEm = DateTime.Now;
        documento.MotivoCancelamento = motivo.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "DocumentoClinicoCancelado",
            Detalhe = $"{TipoDocumentoInfo.Rotular(documento.Tipo)} {documento.Numero} — {motivo.Trim()}",
            PacienteId = documento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    // ============ Documentos montados a partir do prontuário ============

    /// <summary>
    /// Relatório de evolução: o que o tratamento fez com a dor, sessão a sessão. Só
    /// entram no resumo as sessões com o PAR de EVA — a mesma regra do gráfico da
    /// parcela 2, porque meia medida não diz se melhorou.
    /// </summary>
    public async Task<DocumentoClinico> EmitirRelatorioEvolucaoAsync(
        int pacienteId, int? profissionalId = null, DateOnly? inicio = null, DateOnly? fim = null,
        string? operador = null, CancellationToken ct = default)
    {
        var evolucoes = (await _prontuario.DoPacienteAsync(pacienteId, ct))
            .Where(e => (inicio is null || e.Data >= inicio) && (fim is null || e.Data <= fim))
            .OrderBy(e => e.Data).ThenBy(e => e.Id)
            .ToList();

        if (evolucoes.Count == 0)
            throw new InvalidOperationException(
                "Não há sessão registrada no prontuário para relatar neste período.");

        var comPar = evolucoes.Where(e => e.TemParEva).ToList();

        var corpo = $"Relatório da evolução clínica de {evolucoes[0].Data:dd/MM/yyyy} a "
                    + $"{evolucoes[^1].Data:dd/MM/yyyy}, com {evolucoes.Count} sessão(ões) registrada(s). ";

        corpo += comPar.Count == 0
            ? "Nenhuma sessão do período tem a escala de dor (EVA) medida antes E depois, "
              + "então não é possível afirmar a variação da dor pelo registro."
            : $"{comPar.Count} sessão(ões) têm a EVA medida antes e depois: a dor foi de "
              + $"{comPar[0].EvaAntes}/10 na primeira medida para {comPar[^1].EvaDepois}/10 na última, "
              + $"com alívio médio de {comPar.Average(e => e.VariacaoEva!.Value):0.#} ponto(s) por sessão.";

        var itens = evolucoes.Select(e => new ItemDocumento
        {
            Descricao = e.TemParEva
                ? $"{e.Data:dd/MM/yyyy} · EVA {e.EvaAntes} → {e.EvaDepois}"
                : $"{e.Data:dd/MM/yyyy} · EVA não medida",
            Detalhe = Juntar(e.QueixaPrincipal, e.Conduta, e.TextoEvolucao),
            Quantidade = e.Profissional?.Rotulo
        }).ToList();

        // As escalas aplicadas no período entram no MESMO relatório (parcela 36).
        //
        // Elas são gravadas pelo Consultório e, sem isto, seriam lidas só lá — o quinto
        // caso do defeito recorrente do projeto, e o mais caro deste: o relatório de
        // evolução é o papel que o paciente leva ao convênio e ao outro médico, e é
        // justamente o escore de uma escala validada que sustenta "o tratamento está
        // funcionando" para quem não acompanhou o tratamento.
        //
        // A EVA continua no corpo e as escalas viram ITENS datados, na mesma linha do
        // tempo das sessões: separá-las em dois blocos faria o leitor comparar o escore
        // de agosto com a sessão de junho.
        var avaliacoes = (await _repo.AvaliacoesDoPacienteAsync(pacienteId, null, ct: ct))
            .Where(a => (inicio is null || a.Data >= inicio) && (fim is null || a.Data <= fim))
            .OrderBy(a => a.Data).ThenBy(a => a.Id)
            .ToList();

        foreach (var a in avaliacoes)
            itens.Add(new ItemDocumento
            {
                Descricao = $"{a.Data:dd/MM/yyyy} · {a.InstrumentoNome}: {a.PontuacaoFormatada}",
                // A faixa vai como foi GRAVADA na aplicação, não recalculada pela
                // definição de hoje: o relatório precisa dizer o que foi dito ao paciente
                // na época.
                Detalhe = Juntar(a.FaixaNome, a.FaixaInterpretacao, a.Observacoes),
                Quantidade = a.Profissional?.Rotulo
            });

        if (avaliacoes.Count > 0)
        {
            var porInstrumento = avaliacoes
                .GroupBy(a => a.InstrumentoCodigo)
                .Select(g => g.OrderBy(a => a.Data).ThenBy(a => a.Id).ToList())
                .ToList();

            corpo += " Escalas aplicadas no período: " + string.Join("; ", porInstrumento.Select(g =>
                g.Count == 1
                    // Uma aplicação é linha de base, não evolução — a mesma regra do par EVA.
                    ? $"{g[0].InstrumentoNome}, {g[0].PontuacaoFormatada} em {g[0].Data:dd/MM/yyyy} "
                      + "(aplicação única, sem evolução a relatar)"
                    : $"{g[0].InstrumentoNome}, de {g[0].PontuacaoFormatada} em {g[0].Data:dd/MM/yyyy} "
                      + $"para {g[^1].PontuacaoFormatada} em {g[^1].Data:dd/MM/yyyy}")) + ".";
        }

        return await EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.RelatorioEvolucao,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = DateOnly.FromDateTime(DateTime.Today),
            PeriodoInicio = evolucoes[0].Data,
            PeriodoFim = evolucoes[^1].Data,
            Corpo = corpo,
            Itens = itens
        }, operador, ct);
    }

    /// <summary>
    /// Anamnese. Vem preenchida com o que o sistema já sabe (a primeira sessão do
    /// prontuário) e sai com LINHAS para o que ele não guarda — antecedentes,
    /// medicações, hábitos. Imprimir só o que existe daria uma folha quase vazia; o
    /// papel serve para a entrevista acontecer.
    /// </summary>
    public async Task<DocumentoClinico> EmitirAnamneseAsync(
        int pacienteId, int? profissionalId = null, string? operador = null,
        CancellationToken ct = default)
    {
        var evolucoes = await _prontuario.DoPacienteAsync(pacienteId, ct);
        var primeira = evolucoes.OrderBy(e => e.Data).ThenBy(e => e.Id).FirstOrDefault();

        var preenchido = new Dictionary<string, string?>
        {
            ["Queixa principal"] = primeira?.QueixaPrincipal,
            ["História da doença atual"] = primeira?.TextoEvolucao,
            ["Conduta inicial"] = primeira?.Conduta
        };

        var ordem = 0;
        var itens = RoteiroAnamnese.Select(secao => new ItemDocumento
        {
            Ordem = ++ordem,
            Descricao = secao,
            Detalhe = preenchido.TryGetValue(secao, out var valor) ? valor : null
        }).ToList();

        var corpo = primeira is null
            ? "Anamnese inicial — o prontuário ainda não tem sessão registrada, então o "
              + "roteiro sai em branco para a entrevista."
            : $"Anamnese com o que o prontuário registrou na primeira sessão "
              + $"({primeira.Data:dd/MM/yyyy}). As seções sem resposta ficam em branco para "
              + "serem preenchidas na entrevista.";

        return await EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Anamnese,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            EvolucaoId = primeira?.Id,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Corpo = corpo,
            Itens = itens
        }, operador, ct);
    }

    /// <summary>
    /// Termo de consentimento LGPD em papel, para assinar. Imprime a situação de cada
    /// finalidade no momento da emissão — o registro no sistema continua sendo o que
    /// vale; o papel é a prova assinada que a lei pede quando alguém a exige.
    /// </summary>
    public async Task<DocumentoClinico> EmitirTermoConsentimentoAsync(
        int pacienteId, int? profissionalId = null, string? operador = null,
        CancellationToken ct = default)
    {
        var situacao = await _consentimentos.SituacaoAsync(pacienteId, ct);

        var ordem = 0;
        var itens = ConsentimentoService.Finalidades.Select(finalidade =>
        {
            situacao.TryGetValue(finalidade, out var atual);
            return new ItemDocumento
            {
                Ordem = ++ordem,
                Descricao = ConsentimentoService.Rotular(finalidade),
                Detalhe = Descrever(atual),
                Quantidade = atual?.Vigente == true ? "Autorizado" : "Pendente"
            };
        }).ToList();

        const string termo =
            "Autorizo a clínica a tratar meus dados pessoais e de saúde para as finalidades "
            + "assinaladas abaixo, nos termos da Lei 13.709/2018 (LGPD). Fui informado(a) de que "
            + "posso revogar qualquer uma destas autorizações a qualquer momento, sem prejuízo do "
            + "meu atendimento, e de que a revogação não apaga o registro do consentimento dado "
            + "no período em que meus dados já foram tratados.";

        return await EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Consentimento,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Corpo = termo,
            Itens = itens
        }, operador, ct);
    }

    // ==================== Modelos ====================

    public Task<IReadOnlyList<ModeloDocumento>> ModelosAsync(
        TipoDocumentoClinico? tipo = null, CancellationToken ct = default)
        => _repo.ModelosDocumentoAsync(tipo, ct);

    /// <summary>
    /// Guarda um modelo reutilizável. Nome já usado no mesmo tipo SOBRESCREVE o anterior:
    /// quem clica "salvar como modelo" duas vezes com o mesmo nome está corrigindo o
    /// modelo, não criando um gêmeo.
    /// </summary>
    public async Task<ModeloDocumento> SalvarModeloAsync(
        ModeloDocumento dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Dê um nome ao modelo.");

        var nome = dados.Nome.Trim();
        var itens = dados.Itens
            .Where(i => !string.IsNullOrWhiteSpace(i.Descricao))
            .ToList();

        if (itens.Count == 0 && string.IsNullOrWhiteSpace(dados.Corpo))
            throw new InvalidOperationException(
                "Um modelo vazio não serve para nada: escreva o texto ou ao menos uma linha.");

        var modelo = dados.Id != 0
            ? await _repo.ObterModeloDocumentoAsync(dados.Id, ct)
            : await _repo.ObterModeloDocumentoPorNomeAsync(dados.Tipo, nome, ct);

        var novo = modelo is null;
        if (modelo is null)
        {
            modelo = new ModeloDocumento
            {
                Tipo = dados.Tipo,
                CriadoEm = DateTime.Now,
                CriadoPor = operador
            };
            await _repo.AdicionarModeloDocumentoAsync(modelo, ct);
        }
        else
        {
            await _repo.RemoverItensDoModeloAsync(modelo.Id, ct);
            modelo.Itens.Clear();
            modelo.AtualizadoEm = DateTime.Now;
        }

        modelo.Nome = nome;
        modelo.Titulo = Limpar(dados.Titulo);
        modelo.Corpo = Limpar(dados.Corpo);
        modelo.Ativo = dados.Ativo;
        modelo.Ordem = dados.Ordem;

        var ordem = 1;
        foreach (var i in itens)
            modelo.Itens.Add(new ItemModelo
            {
                Ordem = ordem++,
                Descricao = i.Descricao.Trim(),
                Detalhe = Limpar(i.Detalhe),
                Quantidade = Limpar(i.Quantidade)
            });

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = novo ? "ModeloDocumentoCriado" : "ModeloDocumentoAlterado",
            Detalhe = $"{TipoDocumentoInfo.Rotular(modelo.Tipo)} — {modelo.Nome}"
        }, ct);

        await _repo.SalvarAsync(ct);
        return modelo;
    }

    public async Task ExcluirModeloAsync(int modeloId, CancellationToken ct = default)
    {
        await _repo.RemoverModeloDocumentoAsync(modeloId, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Bastidores ====================

    private async Task ValidarAsync(
        DocumentoClinico dados, IReadOnlyList<ItemDocumento> itens, CancellationToken ct)
    {
        if (TipoDocumentoInfo.ExigeItens(dados.Tipo) && itens.Count == 0)
            throw new InvalidOperationException(
                dados.Tipo == TipoDocumentoClinico.Receita
                    ? "Uma receita sem nenhum item não é receita: acrescente ao menos um."
                    : "Diga ao menos um exame no pedido.");

        // Receita, atestado e pedido de exame só existem porque alguém habilitado
        // assina. Sem assinante o papel não vale nada — e vale menos ainda descobrir
        // isso na frente do paciente, com o documento já impresso.
        if (ExigeAssinante(dados.Tipo))
        {
            if (dados.ProfissionalId is not { } profissionalId)
                throw new InvalidOperationException(
                    $"{TipoDocumentoInfo.Rotular(dados.Tipo)} precisa do profissional que assina.");

            if (await _repo.ObterProfissionalAsync(profissionalId, ct) is null)
                throw new InvalidOperationException("Profissional não encontrado.");
        }

        if (dados.Tipo == TipoDocumentoClinico.Atestado)
        {
            var temPeriodo = dados.PeriodoInicio is not null && dados.PeriodoFim is not null;
            if (dados.DiasAfastamento is null && !temPeriodo)
                throw new InvalidOperationException(
                    "Informe os dias de afastamento (ou o período) do atestado.");

            if (dados.DiasAfastamento is { } dias && dias < 1)
                throw new InvalidOperationException("O afastamento é de pelo menos um dia.");
        }

        if (dados.PeriodoInicio is { } de && dados.PeriodoFim is { } ate && ate < de)
            throw new InvalidOperationException("O fim do período é anterior ao início.");

        if (dados.HoraChegada is { } chegada && dados.HoraSaida is { } saida && saida < chegada)
            throw new InvalidOperationException("A saída é anterior à chegada.");
    }

    /// <summary>Documento que só vale assinado por profissional habilitado.</summary>
    private static bool ExigeAssinante(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.Receita
               or TipoDocumentoClinico.Atestado
               or TipoDocumentoClinico.PedidoExame;

    private async Task<string> NumerarAsync(int ano, CancellationToken ct)
    {
        var sequencial = await _repo.ProximoNumeroDocumentoAsync(ano, ct);
        return $"{ano}/{sequencial:0000}";
    }

    /// <summary>
    /// Código curto de conferência da via em papel. Vem de um Guid (não do conteúdo):
    /// código derivado do texto viraria uma forma de adivinhar documento alheio.
    /// </summary>
    private static string GerarCodigo()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var letras = new char[11];
        var origem = 0;

        for (var i = 0; i < letras.Length; i++)
            letras[i] = i == 5 ? '-' : AlfabetoCodigo[bytes[origem++] % AlfabetoCodigo.Length];

        return new string(letras);
    }

    private static string Descrever(ConsentimentoLgpd? registro) => registro switch
    {
        null => "Nunca perguntado",
        { RevogadoEm: { } revogado } => $"Revogado em {revogado:dd/MM/yyyy}",
        { Concedido: true } r => $"Concedido em {r.RegistradoEm:dd/MM/yyyy}",
        var r => $"Recusado em {r.RegistradoEm:dd/MM/yyyy}"
    };

    private static string? Juntar(params string?[] partes)
    {
        var texto = string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
