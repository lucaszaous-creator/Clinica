using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

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

    /// <summary>
    /// A publicação, para o cancelamento tirar o link do ar (parcela 63).
    ///
    /// OPCIONAL, como o <c>_consultas</c> do <c>AtendimentoService</c>: os testes que só
    /// exercitam emissão constroem este serviço sem ele, e exigi-lo obrigaria a montar um
    /// armazenamento falso em toda suíte que emite um atestado.
    /// </summary>
    private readonly PublicacaoDocumentoService? _publicacao;

    public DocumentoClinicoService(
        IClinicaRepositorio repo, ProntuarioService prontuario, ConsentimentoService consentimentos,
        PublicacaoDocumentoService? publicacao = null)
    {
        _repo = repo;
        _prontuario = prontuario;
        _consentimentos = consentimentos;
        _publicacao = publicacao;
    }

    // ==================== Leitura ====================

    /// <summary>Documentos do paciente, do mais recente para o mais antigo.</summary>
    public Task<IReadOnlyList<DocumentoClinico>> DoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => _repo.DocumentosDoPacienteAsync(pacienteId, ct);

    /// <summary>
    /// Os termos LGPD do paciente que carregam FINALIDADE — os que a coleta pode
    /// reaproveitar e cuja assinatura registra consentimento (parcela 89, 2ª rodada).
    /// </summary>
    public Task<IReadOnlyList<int>> TermosLgpdComFinalidadeAsync(
        int pacienteId, CancellationToken ct = default)
        => _repo.TermosLgpdComFinalidadeAsync(pacienteId, ct);

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
            ModeloOrigemId = dados.ModeloOrigemId,
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
                Quantidade = Limpar(i.Quantidade),
                // ⚠️ A CÓPIA É CAMPO A CAMPO, e o que não estiver nesta lista é DESCARTADO
                // em silêncio — o lugar 3 da auditoria de linha, aqui na emissão. Ela já
                // mordeu DUAS vezes: o `Desenho` era montado certo pelo relatório e sumia
                // neste `new` (a ficha saía sem mapa nenhum), e o `Codigo` do termo LGPD
                // sumiu igual na parcela 89 — sem ele, a resposta assinada do paciente não
                // teria como ser lida de volta como consentimento.
                Desenho = i.Desenho,
                Codigo = Limpar(i.Codigo)
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
    ///
    /// ⚠️ <b>E TIRA O LINK DO AR</b> (parcela 63). A documentação do
    /// <c>PublicacaoDocumentoService</c> afirmava que o cancelamento despublicava desde a
    /// parcela 53, e ele <b>nunca fez isso</b> — a única chamada era a da expiração. Uma
    /// receita cancelada continuava baixável pelo QR até o prazo vencer, que a clínica
    /// configura em 30 ou 180 dias: o papel dizia "cancelada" e o endereço público
    /// entregava o PDF assinado, que é a pior espécie de documento no ar.
    ///
    /// Mora AQUI e não na tela porque o cancelamento tem <b>quatro portas</b> (a ficha do
    /// paciente, as Prescrições, e dois caminhos da central de documentos) — a mesma razão
    /// pela qual a crítica do número da guia mora no <c>FaturamentoService</c>: corrigir
    /// numa tela cobre uma e deixa três passando.
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

        // Depois do SalvarAsync, e é decisão: o cancelamento é o fato que não pode falhar.
        // Se o armazenamento estiver fora do ar, o documento continua CANCELADO — e a
        // falha vai para o log com o caminho do arquivo, pelo próprio DespublicarAsync.
        // Desfazer o cancelamento porque o S3 não respondeu deixaria válido um documento
        // que a clínica acabou de invalidar, o que é o pior dos dois desfechos.
        if (_publicacao is not null)
            await _publicacao.DespublicarAsync(documento, operador, ct);
    }

    // ============ Documentos montados a partir do prontuário ============

    /// <summary>
    /// Relatório de evolução: o que o tratamento fez com a dor, sessão a sessão. Só
    /// entram no resumo as sessões com o PAR de EVA — a mesma regra do gráfico da
    /// parcela 2, porque meia medida não diz se melhorou.
    /// </summary>
    private static DateOnly Menor(DateOnly? a, DateOnly? b)
        => a is null ? b!.Value : b is null ? a.Value : a.Value < b.Value ? a.Value : b.Value;

    private static DateOnly Maior(DateOnly? a, DateOnly? b)
        => a is null ? b!.Value : b is null ? a.Value : a.Value > b.Value ? a.Value : b.Value;

    public async Task<DocumentoClinico> EmitirRelatorioEvolucaoAsync(
        int pacienteId, int? profissionalId = null, DateOnly? inicio = null, DateOnly? fim = null,
        string? operador = null, CancellationToken ct = default)
    {
        var evolucoes = (await _prontuario.DoPacienteAsync(pacienteId, ct))
            .Where(e => (inicio is null || e.Data >= inicio) && (fim is null || e.Data <= fim))
            .OrderBy(e => e.Data).ThenBy(e => e.Id)
            .ToList();

        // ⚠️ A ENFERMAGEM entra no MESMO papel (parcela 78). A clínica disse que todo
        // paciente passa por ela, e até aqui o único jeito de a passagem sair impressa era
        // haver uma folha de infusão: a técnica que colhe sinais vitais, faz o curativo e
        // registra a consulta de enfermagem completa não tinha o que entregar a ninguém.
        //
        // No MESMO documento, e não num tipo novo, porque é o mesmo fato para quem lê: o
        // paciente veio, e isto foi o que aconteteu com ele. Um segundo papel obrigaria a
        // clínica a entregar dois e o convênio a casar duas numerações.
        //
        // Só as VIGENTES: registro cancelado ou já retificado é registro desdito, e ele
        // continua no prontuário — não no papel que sai da clínica.
        var enfermagem = EvolucaoEnfermagem.Vigentes(
                await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, int.MaxValue, ct))
            .Where(e => (inicio is null || e.Data >= inicio) && (fim is null || e.Data <= fim))
            .OrderBy(e => e.Data).ThenBy(e => e.Hora).ThenBy(e => e.Id)
            .ToList();

        if (evolucoes.Count == 0 && enfermagem.Count == 0)
            throw new InvalidOperationException(
                "Não há registro no prontuário para relatar neste período.");

        var comPar = evolucoes.Where(e => e.TemParEva).ToList();

        // O MAPA CORPORAL de cada sessão, numa consulta só (parcela 79). Ele é COPIADO
        // para dentro do documento aqui, e não lido na impressão: a segunda via tem de
        // sair idêntica à que o paciente levou, e a sessão pode ser corrigida depois.
        var mapas = (await _repo.MapasDasEvolucoesAsync(
                evolucoes.Select(e => e.Id).ToList(), ct))
            .ToDictionary(m => m.EvolucaoId);

        var primeira = Menor(evolucoes.FirstOrDefault()?.Data, enfermagem.FirstOrDefault()?.Data);
        var ultima = Maior(evolucoes.LastOrDefault()?.Data, enfermagem.LastOrDefault()?.Data);

        // ⚠️ A abertura conta SÓ o que existe. "0 sessão(ões) registrada(s) e 1 registro(s)
        // de enfermagem" é verdade e se lê como falta — e este é o papel que o paciente
        // leva embora, onde a primeira linha é a que todo mundo lê.
        var contagens = new List<string>();
        if (evolucoes.Count > 0) contagens.Add($"{evolucoes.Count} sessão(ões) registrada(s)");
        if (enfermagem.Count > 0) contagens.Add($"{enfermagem.Count} registro(s) de enfermagem");

        var corpo = $"Relatório da evolução clínica de {primeira:dd/MM/yyyy} a "
                    + $"{ultima:dd/MM/yyyy}, com {string.Join(" e ", contagens)}. ";

        // ⚠️ A frase da EVA só existe quando há sessão do prontuário médico: numa ficha só
        // de enfermagem, "nenhuma sessão tem a EVA medida" seria uma afirmação sobre um
        // registro que não se propôs a medi-la.
        if (evolucoes.Count > 0)
            corpo += comPar.Count == 0
                ? "Nenhuma sessão do período tem a escala de dor (EVA) medida antes E depois, "
                  + "então não é possível afirmar a variação da dor pelo registro."
                : $"{comPar.Count} sessão(ões) têm a EVA medida antes e depois: a dor foi de "
                  + $"{comPar[0].EvaAntes}/10 na primeira medida para {comPar[^1].EvaDepois}/10 na última, "
                  + $"com alívio médio de {comPar.Average(e => e.VariacaoEva!.Value):0.#} ponto(s) por sessão.";

        // ⚠️ A lista carrega a DATA de cada item para ser ordenada no fim. O comentário
        // das escalas promete, desde a parcela 36, que elas entram "na MESMA linha do
        // tempo das sessões" — e elas eram ANEXADAS depois de todas, isto é, o papel
        // saía com as sessões, depois os escores, depois a enfermagem. Comentário que
        // descreve um comportamento e não o realiza é o defeito da parcela 67; aqui ele
        // fazia o leitor comparar o escore de agosto com a sessão de junho.
        var datados = evolucoes.Select(e => (e.Data, Item: new ItemDocumento
        {
            Descricao = e.TemParEva
                ? $"{e.Data:dd/MM/yyyy} · EVA {e.EvaAntes} → {e.EvaDepois}"
                : $"{e.Data:dd/MM/yyyy} · EVA não medida",
            // A anamnese e o exame físico da sessão entram no relatório porque é
            // JUSTAMENTE isso que o outro profissional lê para não recomeçar o raciocínio
            // do zero — sem eles, o papel diz o que foi feito e não diz por quê.
            //
            // ⚠️ A HIPÓTESE entra pelo TEXTO; o CÓDIGO CID, não. É a economia do CID da
            // parcela 3, e ela vale aqui com mais razão: o relatório circula fora da
            // clínica, o código é o que se lê num campo de formulário sem ninguém ler a
            // frase ao lado, e este documento não passa pela autorização expressa que a
            // receita e o atestado pedem. Quem precisa do código pede o atestado.
            Detalhe = Juntar(
                e.QueixaPrincipal,
                e.HistoriaDoencaAtual,
                e.ExameFisico,
                e.HipoteseDiagnostica is { } h ? $"hipótese: {h}" : null,
                e.Conduta,
                e.TextoEvolucao,
                // ⚠️ O PLANO entra no relatório (parcela 75), e é o que o CONVÊNIO mais
                // procura nele: "10 sessões, 2x/semana, reavaliar em 4 semanas" é a frase
                // que sustenta a continuidade do tratamento para quem não o acompanhou.
                // Sem isto, o campo nasceria gravado e o único papel que sai da clínica não
                // o levaria — o defeito recorrente na variante mais cara.
                e.PlanoTerapeutico is { } pl ? $"plano: {pl}" : null,
                // O RETORNO e o ENCAMINHAMENTO (parcela 77) pela mesma razão do plano: o
                // relatório é o papel que o paciente leva ao convênio e ao outro
                // profissional, e "reavaliar em 7 dias" e "encaminhado à psiquiatria" são
                // exatamente o que quem não acompanhou o tratamento precisa ler.
                e.RetornoSugeridoEm is { } rs
                    ? $"retorno sugerido: {rs:dd/MM/yyyy}"
                      + (string.IsNullOrWhiteSpace(e.RetornoSugeridoNota)
                          ? string.Empty
                          : $" ({e.RetornoSugeridoNota})")
                    : null,
                e.Encaminhamento is { } enc ? $"encaminhamento: {enc}" : null),
            Quantidade = e.Profissional?.Rotulo,
            // A EVA e as marcações do mapa, na forma que o PDF desenha.
            Desenho = DesenhoDaSessao.De(e, mapas.GetValueOrDefault(e.Id)).Serializar()
        })).ToList();

        // Os registros de ENFERMAGEM entram como itens datados, na MESMA linha do tempo
        // das sessões — separá-los em dois blocos faria o leitor comparar a passagem de
        // agosto com a consulta de junho, que é o defeito que as escalas já evitam abaixo.
        foreach (var en in enfermagem)
            datados.Add((en.Data, new ItemDocumento
            {
                Descricao = $"{en.Data:dd/MM/yyyy} \u00E0s {en.Hora:HH\\:mm} \u00B7 enfermagem"
                            + (en.EhConsulta ? " (consulta de enfermagem)" : string.Empty),
                Detalhe = Juntar(
                    en.Texto,
                    en.SinaisVitaisResumidos,
                    en.AcessoResumo is { } ac ? $"acesso venoso: {ac}" : null,
                    // As CINCO etapas da COFEN 358/2009, e elas entram quando FORAM
                    // escritas: numa passagem de curativo não existem, e imprimir rótulo
                    // sem conteúdo faria o papel parecer um formulário pela metade.
                    //
                    // ⚠️ O DIAGNÓSTICO e os CUIDADOS (etapas 2, 3 e 4) entram porque são o
                    // que faz a consulta de enfermagem ser uma consulta: sem eles o papel
                    // diz "consulta de enfermagem" e mostra um texto livre — que é
                    // exatamente o que a clínica tinha antes de a etapa existir. As duas
                    // listas já vêm carregadas da consulta do repositório; deixá-las de
                    // fora seria dado gravado sem leitor no único papel que sai da clínica.
                    en.Historico,
                    en.ExameFisico,
                    Lista("diagnóstico(s) de enfermagem", en.Diagnosticos
                        .OrderBy(d => d.Ordem).ThenBy(d => d.Id).Select(d => d.Redacao)),
                    Lista("cuidados prescritos", en.Cuidados
                        .OrderBy(c => c.Ordem).ThenBy(c => c.Id).Select(c => c.Redacao)),
                    en.Avaliacao,
                    en.Intercorrencia ? "INTERCORRÊNCIA registrada" : null),
                // Quem assina a passagem é quem a fez, com o COREN — é o que dá valor ao
                // registro de enfermagem, e o papel circula fora da clínica.
                Quantidade = string.IsNullOrWhiteSpace(en.AutorConselho)
                    ? en.AutorNome
                    : $"{en.AutorNome} ({en.AutorConselho})"
            }));

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
            datados.Add((a.Data, new ItemDocumento
            {
                Descricao = $"{a.Data:dd/MM/yyyy} · {a.InstrumentoNome}: {a.PontuacaoFormatada}",
                // A faixa vai como foi GRAVADA na aplicação, não recalculada pela
                // definição de hoje: o relatório precisa dizer o que foi dito ao paciente
                // na época.
                Detalhe = Juntar(a.FaixaNome, a.FaixaInterpretacao, a.Observacoes),
                Quantidade = a.Profissional?.Rotulo
            }));

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
            // ⚠️ `evolucoes[0]` ESTOURA numa ficha só de enfermagem — e ela passou a
            // existir nesta parcela. O período sai do que de fato entrou no papel.
            PeriodoInicio = primeira,
            PeriodoFim = ultima,
            Corpo = corpo,
            Itens = datados.OrderBy(d => d.Data).Select(d => d.Item).ToList()
        }, operador, ct);
    }

    /// <summary>
    /// Anamnese. Vem preenchida com o que o sistema já sabe e sai com LINHAS para o resto —
    /// o papel serve para a entrevista acontecer.
    ///
    /// ⚠️ O QUE O SISTEMA SABE MUDOU DUAS VEZES, e o comentário desta função ficou para trás
    /// nas duas (corrigido na parcela 75, 2ª rodada):
    /// <list type="bullet">
    ///   <item>a parcela 73 deu à sessão <c>HistoriaDoencaAtual</c>, <c>ExameFisico</c> e
    ///   <c>HipoteseDiagnostica</c> próprios, e este montador continuava lendo a HDA do
    ///   <c>TextoEvolucao</c> — o campo ao lado;</item>
    ///   <item>a parcela 75 criou a <see cref="AnamnesePaciente"/> com antecedentes
    ///   pessoais, familiares e hábitos, e a folha continuava imprimindo LINHA EM BRANCO
    ///   para os três, dizendo por escrito que "o sistema não guarda".</item>
    /// </list>
    ///
    /// Folha em branco sobre dado que existe é pior do que folha em branco: ela faz a
    /// entrevista repetir a pergunta que já foi feita, e o paciente concluir que ninguém
    /// leu o que ele contou da última vez.
    ///
    /// ⚠️ ALERGIAS e MEDICAÇÕES continuam em LINHA de propósito. Elas moram na lista de
    /// problemas, que é uma lista — imprimi-la aqui daria uma segunda cópia do que já sai
    /// no relatório, e a que ninguém lembraria de conferir contra a lista viva é justamente
    /// a que o alerta de prescrição lê.
    /// </summary>
    public async Task<DocumentoClinico> EmitirAnamneseAsync(
        int pacienteId, int? profissionalId = null, string? operador = null,
        CancellationToken ct = default)
    {
        var evolucoes = await _prontuario.DoPacienteAsync(pacienteId, ct);
        var primeira = evolucoes.OrderBy(e => e.Data).ThenBy(e => e.Id).FirstOrDefault();
        var anamnese = await _repo.AnamneseDoPacienteAsync(pacienteId, ct);

        var preenchido = new Dictionary<string, string?>
        {
            ["Queixa principal"] = primeira?.QueixaPrincipal,
            // A HDA tem campo PRÓPRIO desde a parcela 73; ler o TextoEvolucao punha a
            // evolução da sessão no lugar da história da doença — dois campos diferentes.
            ["História da doença atual"] = primeira?.HistoriaDoencaAtual ?? primeira?.TextoEvolucao,
            ["Antecedentes pessoais e cirúrgicos"] = anamnese?.AntecedentesPessoais,
            ["Hábitos (sono, atividade física, alimentação)"] = anamnese?.HabitosDeVida,
            ["Antecedentes familiares"] = anamnese?.AntecedentesFamiliares,
            ["Exame físico"] = primeira?.ExameFisico,
            ["Hipótese diagnóstica"] = primeira?.HipoteseDiagnostica,
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
    /// Termo de consentimento LGPD que o PACIENTE ASSINA (parcela 89).
    ///
    /// ⚠️ Ele mudou de natureza. Até aqui era um RECIBO: imprimia a situação de cada
    /// finalidade ("Autorizado"/"Pendente") a partir do que o balcão já tinha marcado, e o
    /// registro no sistema é que valia. Agora as quatro finalidades saem como
    /// **declarações SEM RESPOSTA**, o paciente responde e assina, e é essa resposta que
    /// vira o consentimento.
    ///
    /// A inversão é o que impede as duas verdades: com o termo sendo recibo, o paciente
    /// podia responder "Não" ao marketing no celular e a clínica continuar mandando
    /// campanha, porque a caixinha do balcão seguia marcada.
    ///
    /// ⚠️ **Nascem sem resposta, e isso é decisão.** Pré-marcar com a situação atual
    /// fabricaria a resposta mais conveniente para a clínica — o oposto do que o termo
    /// existe para provar —, e é a mesma regra do termo de procedimento da parcela 66.
    ///
    /// O <see cref="ItemDocumento.Codigo"/> é o que permite ler a resposta de volta; ver
    /// <see cref="TermoConsentimento"/>.
    /// </summary>
    public async Task<DocumentoClinico> EmitirTermoConsentimentoAsync(
        int pacienteId, int? profissionalId = null, string? operador = null,
        CancellationToken ct = default)
    {
        var ordem = 0;
        var itens = ConsentimentoService.Finalidades.Select(finalidade => new ItemDocumento
        {
            Ordem = ++ordem,
            Codigo = finalidade.ToString(),
            Descricao = TermoConsentimento.Declarar(finalidade),
            Detalhe = TermoConsentimento.Detalhar(finalidade)
            // Quantidade (a resposta) fica NULA de propósito — quem responde é o paciente.
        }).ToList();

        const string termo =
            "Declaro que fui informado(a), de forma clara, sobre como a clínica trata meus "
            + "dados pessoais e de saúde, nos termos da Lei 13.709/2018 (LGPD), e respondo "
            + "abaixo a cada finalidade. Sei que posso revogar qualquer uma destas "
            + "autorizações a qualquer momento, e que a revogação não apaga o registro do "
            + "consentimento dado no período em que meus dados já foram tratados.";

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

    /// <summary>
    /// Emite o TERMO DE PROCEDIMENTO que o paciente vai assinar (parcela 66), COPIANDO o
    /// texto e as declarações de um <see cref="ModeloDocumento"/>.
    ///
    /// Copiar, e não apontar, é a regra do protocolo do mapa corporal e do modelo de
    /// evolução — e aqui ela não é desenho, é a Lei 13.787/2018: referência viva faria
    /// corrigir uma palavra do termo hoje reescrever o que um paciente assinou no mês
    /// passado, e o que ele assinou é justamente o que se contesta.
    ///
    /// As declarações nascem SEM RESPOSTA. Quem as responde é o paciente, na coleta, e
    /// pré-marcar "Sim" seria fabricar a resposta mais conveniente para a clínica — o
    /// oposto do que o termo existe para provar.
    /// </summary>
    public async Task<DocumentoClinico> EmitirTermoProcedimentoAsync(
        int pacienteId, int modeloId, int? profissionalId = null,
        string? operador = null, CancellationToken ct = default)
    {
        var modelo = await _repo.ObterModeloDocumentoAsync(modeloId, ct)
            ?? throw new InvalidOperationException("Modelo de termo não encontrado.");

        if (modelo.Tipo != TipoDocumentoClinico.TermoProcedimento)
            throw new InvalidOperationException(
                $"\"{modelo.Nome}\" não é um modelo de termo de procedimento.");

        var documento = new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.TermoProcedimento,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            ModeloOrigemId = modelo.Id,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Titulo = modelo.Titulo,
            Corpo = modelo.Corpo,
            Itens = modelo.Itens
                .OrderBy(i => i.Ordem)
                .Select(i => new ItemDocumento
                {
                    Ordem = i.Ordem,
                    Descricao = i.Descricao,
                    Detalhe = i.Detalhe,
                    Quantidade = null // a resposta é do paciente, e ele ainda não respondeu
                })
                .ToList()
        };

        return await EmitirAsync(documento, operador, ct);
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
    /// Uma lista rotulada dentro do detalhe do item — "cuidados prescritos: X; Y".
    ///
    /// Lista VAZIA devolve nulo, e não o rótulo sozinho: é a mesma regra do
    /// <see cref="Juntar"/>, e é ela que faz a passagem de curativo sair sem os rótulos
    /// das etapas que ninguém escreveu.
    /// </summary>
    private static string? Lista(string rotulo, IEnumerable<string> itens)
    {
        var texto = string.Join("; ", itens.Where(i => !string.IsNullOrWhiteSpace(i)));
        return string.IsNullOrWhiteSpace(texto) ? null : $"{rotulo}: {texto}";
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

    private static string? Juntar(params string?[] partes)
    {
        var texto = string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
