using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Responde "este paciente precisa assinar algum termo para a sessão de HOJE, e já
/// assinou?" (parcela 66).
///
/// O caso que trouxe a feature é o BSV: a clínica precisa do consentimento do procedimento
/// e da <b>declaração de jejum</b> assinados antes de puncionar. Até aqui o sistema não
/// tinha onde guardar nem como cobrar — o termo era papel solto na pasta, e descobrir na
/// hora que ele não foi assinado significa parar o procedimento com o paciente na maca.
///
/// A validade é ESCOLHA DA CLÍNICA, por procedimento (ago/2026, 3ª rodada), e nasce "vale a
/// partir da assinatura": o consentimento é assinado quando o paciente estiver por perto —
/// inclusive na consulta em que ele vem tirar dúvidas, semanas antes —, e obrigar a esperar
/// o dia jogaria fora justamente o momento em que ele lê o texto com calma.
///
/// A opção "só no dia" existe porque as DECLARAÇÕES moram dentro do termo e nem toda
/// declaração sobrevive à antecedência: "estou em jejum" assinado na semana passada é uma
/// afirmação sobre o futuro. A clínica que quiser perguntar o jejum no dia cria um termo
/// curto só com essa declaração e liga a caixa — os dois convivem, porque a exigência é por
/// MODELO e não por tipo.
///
/// ⚠️ Seja qual for a validade, RECUSA e papel pendente contam só no DIA: uma recusa de três
/// semanas atrás não pode calar o pedido no dia do procedimento, e um papel emitido e nunca
/// assinado carrega a data velha.
///
/// ⚠️ Ele NUNCA impede o atendimento — informa, como o
/// <see cref="ElegibilidadeService"/>, e pela mesma razão: quem decide adiar um
/// procedimento é quem o faz. Um software que trave o BSV porque uma linha de banco está
/// vazia produz o desfecho pior de todos — a clínica faz o procedimento assim mesmo e
/// deixa de registrar qualquer coisa.
/// </summary>
public sealed class TermoProcedimentoService
{
    private readonly IClinicaRepositorio _repo;

    public TermoProcedimentoService(IClinicaRepositorio repo) => _repo = repo;

    // ==================== Configuração ====================

    /// <summary>Todas as exigências cadastradas, ativas e inativas, para a tela de Configurações.</summary>
    public Task<IReadOnlyList<ExigenciaTermoProcedimento>> ExigenciasAsync(
        CancellationToken ct = default)
        => _repo.ExigenciasTermoAsync(ct);

    /// <summary>
    /// Amarra uma modalidade a um modelo de termo.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Modelo inexistente, de outro tipo, ou modalidade já amarrada.
    /// </exception>
    /// <param name="soValeNoDiaDoProcedimento">
    /// O termo é pedido a cada sessão em vez de valer a partir da assinatura. Nasce FALSO —
    /// ver <see cref="ExigenciaTermoProcedimento.SoValeNoDiaDoProcedimento"/>.
    /// </param>
    public async Task<ExigenciaTermoProcedimento> ExigirAsync(
        ModalidadeAtendimento modalidade, int modeloId, string? modalidadeCodigo = null,
        string? operador = null, bool soValeNoDiaDoProcedimento = false,
        CancellationToken ct = default)
    {
        var modelo = await _repo.ObterModeloDocumentoAsync(modeloId, ct)
            ?? throw new InvalidOperationException("Modelo de termo não encontrado.");

        // Um modelo de receita amarrado como termo produziria um "termo de procedimento"
        // com o corpo de uma prescrição, e ninguém perceberia até o paciente ler.
        if (modelo.Tipo != TipoDocumentoClinico.TermoProcedimento)
            throw new InvalidOperationException(
                $"\"{modelo.Nome}\" é um modelo de "
                + $"{TipoDocumentoInfo.Rotular(modelo.Tipo).ToLowerInvariant()}. "
                + "Escolha um modelo de termo de procedimento.");

        var existentes = await _repo.ExigenciasTermoAsync(ct);
        var codigo = NormalizarCodigo(modalidadeCodigo);

        // ⚠️ Modalidade já exigida TROCA o modelo em vez de recusar (parcela 66, 2ª rodada).
        //
        // A primeira versão lançava "troque o modelo da exigência que existe" — e não havia
        // por onde trocar: a única saída era desligar a linha antiga, e desligada ela deixa
        // de cobrar o termo, o que é o oposto do que a clínica queria ao reescrever o
        // texto. Mensagem de erro que manda fazer o que a tela não faz é botão que não faz
        // nada com uma etapa a mais.
        //
        // Trocar é seguro porque aplicar COPIA: os termos já assinados guardam o texto que
        // o paciente leu e o `ModeloOrigemId` deles continua apontando para o modelo
        // ANTIGO. O que muda é o que será copiado da próxima vez.
        var existente = existentes.FirstOrDefault(x =>
            x.Modalidade == modalidade
            && string.Equals(x.ModalidadeCodigo, codigo, StringComparison.OrdinalIgnoreCase));

        if (existente is not null)
        {
            var rastreada = await _repo.ObterExigenciaTermoAsync(existente.Id, ct)
                ?? throw new InvalidOperationException("Exigência não encontrada.");

            var anterior = rastreada.Modelo?.Nome;
            rastreada.ModeloDocumentoId = modeloId;
            rastreada.SoValeNoDiaDoProcedimento = soValeNoDiaDoProcedimento;
            rastreada.Ativa = true;

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "ExigenciaTermoTrocada",
                Detalhe = $"{modalidade}: \"{anterior}\" passa a ser \"{modelo.Nome}\""
            }, ct);

            await _repo.SalvarAsync(ct);
            return rastreada;
        }

        var exigencia = new ExigenciaTermoProcedimento
        {
            Modalidade = modalidade,
            ModalidadeCodigo = codigo,
            ModeloDocumentoId = modeloId,
            Ativa = true,
            SoValeNoDiaDoProcedimento = soValeNoDiaDoProcedimento,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        await _repo.AdicionarExigenciaTermoAsync(exigencia, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ExigenciaTermoCriada",
            Detalhe = $"{modalidade} passa a exigir o termo \"{modelo.Nome}\""
        }, ct);

        await _repo.SalvarAsync(ct);
        return exigencia;
    }

    /// <summary>
    /// "Vale para a família inteira" é gravado como STRING VAZIA, nunca NULL.
    ///
    /// ⚠️ Não é preciosismo: o índice único é `(Modalidade, ModalidadeCodigo)`, e o
    /// PostgreSQL trata NULL como DISTINTO de qualquer outro NULL — com null, o índice
    /// ficaria inerte justamente no caso NORMAL (código nulo = família inteira), e dois
    /// cliques concorrentes em "Passar a exigir" inseririam duas linhas. Duas exigências
    /// para a mesma sessão fazem o paciente assinar o mesmo papel duas vezes.
    /// </summary>
    private static string NormalizarCodigo(string? codigo)
        => string.IsNullOrWhiteSpace(codigo) ? string.Empty : codigo.Trim();

    /// <summary>
    /// Liga ou desliga uma exigência. Desligar em vez de apagar: a clínica que suspende a
    /// cobrança por um mês não perde a amarração nem o texto.
    /// </summary>
    public async Task AlternarAsync(
        int exigenciaId, bool ativa, string? operador = null, CancellationToken ct = default)
    {
        var exigencia = await _repo.ObterExigenciaTermoAsync(exigenciaId, ct)
            ?? throw new InvalidOperationException("Exigência não encontrada.");

        if (exigencia.Ativa == ativa) return;

        exigencia.Ativa = ativa;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = ativa ? "ExigenciaTermoAtivada" : "ExigenciaTermoDesativada",
            Detalhe = $"{exigencia.Modalidade} — termo \"{exigencia.Modelo?.Nome}\""
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    // ==================== A pergunta do balcão ====================

    /// <summary>
    /// O que falta de termo para este paciente hoje, considerando o que está marcado na
    /// agenda dele no dia.
    ///
    /// Lista vazia = ou não há procedimento que exija termo, ou todos já estão assinados.
    /// A diferença entre os dois casos não importa para quem lê: nos dois não há nada a
    /// fazer.
    /// </summary>
    public async Task<IReadOnlyList<SituacaoTermo>> SituacaoDoDiaAsync(
        int pacienteId, DateOnly data, CancellationToken ct = default)
    {
        var exigencias = (await _repo.ExigenciasTermoAsync(ct)).Where(e => e.Ativa).ToList();
        if (exigencias.Count == 0) return [];

        var agendamentos = await _repo.AgendamentosDoPacienteNoDiaAsync(pacienteId, data, ct);
        var modalidades = ModalidadesQuePedemTermo(agendamentos);
        if (modalidades.Count == 0) return [];

        // Todos os termos do paciente, de QUALQUER data: quem decide o que conta é a
        // validade de cada exigência, e ela é resolvida no `Resolver`. Ler só o dia aqui
        // faria o termo sem prazo — assinado semanas antes, que é o caso normal — parecer
        // pendente para sempre.
        var termos = await _repo.TermosDoPacienteAsync(pacienteId, ct);

        return Resolver(exigencias, modalidades, termos, data);
    }

    /// <summary>
    /// Quem, entre os pacientes do DIA, ainda tem termo por assinar.
    ///
    /// Existe separado do <see cref="SituacaoDoDiaAsync"/> porque a fila do balcão pergunta
    /// por trinta cartões de uma vez: chamar o método de um paciente trinta vezes daria
    /// sessenta idas a um banco remoto para desenhar um quadro. Aqui são TRÊS leituras,
    /// qualquer que seja o tamanho do dia.
    ///
    /// ⚠️ Os dois caminhos resolvem pela MESMA função (<see cref="Resolver"/>): duas
    /// definições de "falta assinar" divergiriam na primeira correção, e a que ninguém
    /// lembraria de ajustar é justamente a do quadro — onde o erro aparece como cartão
    /// limpo, que é indistinguível de termo em dia.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<SituacaoTermo>>> DoDiaAsync(
        DateOnly data, CancellationToken ct = default)
    {
        var vazio = new Dictionary<int, IReadOnlyList<SituacaoTermo>>();

        var exigencias = (await _repo.ExigenciasTermoAsync(ct)).Where(e => e.Ativa).ToList();
        if (exigencias.Count == 0) return vazio;

        var agendamentos = await _repo.AgendamentosNoPeriodoAsync(
            data.ToDateTime(TimeOnly.MinValue), data.ToDateTime(TimeOnly.MaxValue), ct);

        var pacientes = agendamentos.Select(a => a.PacienteId).Distinct().ToList();
        var termos = await _repo.TermosDosPacientesAsync(pacientes, ct);
        var porPaciente = termos.ToLookup(d => d.PacienteId);

        foreach (var grupo in agendamentos.GroupBy(a => a.PacienteId))
        {
            var modalidades = ModalidadesQuePedemTermo(grupo);
            if (modalidades.Count == 0) continue;

            var situacoes = Resolver(
                exigencias, modalidades, porPaciente[grupo.Key].ToList(), data);

            if (situacoes.Count > 0) vazio[grupo.Key] = situacoes;
        }

        return vazio;
    }

    /// <summary>
    /// Os termos que a clínica escreveu, para quem quer colher um AVULSO — fora do dia do
    /// procedimento (parcela 66, 3ª rodada).
    ///
    /// É o que destrava a porta pedida pela cliente: o paciente aparece na consulta em que
    /// vem tirar dúvidas, e a assinatura se colhe ali, sem esperar o dia. Só os modelos
    /// ATIVOS, e só os que alguma exigência usa não — qualquer termo escrito pode ser
    /// colhido, porque a clínica pode ter um papel que não amarra a modalidade nenhuma.
    /// </summary>
    public async Task<IReadOnlyList<ModeloDocumento>> ModelosDisponiveisAsync(
        CancellationToken ct = default)
        => (await _repo.ModelosDocumentoAsync(TipoDocumentoClinico.TermoProcedimento, ct))
            .Where(m => m.Ativo)
            .OrderBy(m => m.Ordem).ThenBy(m => m.Nome)
            .ToList();

    /// <summary>
    /// As modalidades do dia que podem pedir termo.
    ///
    /// Cancelado e falta ficam de fora: não há procedimento para consentir. O agendamento
    /// que ainda não chegou ENTRA — é justamente o caso que o balcão precisa resolver
    /// antes, e não depois.
    /// </summary>
    private static IReadOnlyList<(ModalidadeAtendimento Modalidade, string? Codigo, int? ProfissionalId)>
        ModalidadesQuePedemTermo(IEnumerable<Agendamento> agendamentos)
        => agendamentos
            .Where(a => a.Status is not (StatusAgendamento.Cancelado or StatusAgendamento.Faltou))
            .Select(a => (a.ModalidadePrevista, Codigo: Limpar(a.ModalidadeCodigo), a.ProfissionalId))
            .Distinct()
            .ToList();

    private static IReadOnlyList<SituacaoTermo> Resolver(
        IReadOnlyList<ExigenciaTermoProcedimento> exigencias,
        IReadOnlyList<(ModalidadeAtendimento Modalidade, string? Codigo, int? ProfissionalId)> modalidades,
        IReadOnlyList<DocumentoClinico> termosDoPaciente,
        DateOnly dia)
    {
        var situacoes = new List<SituacaoTermo>();

        foreach (var exigencia in exigencias)
        {
            // Código vazio (ou nulo, nas linhas anteriores à normalização) = vale para a
            // FAMÍLIA inteira, que é o caso normal: quem faz BSV assina o termo do BSV,
            // seja qual for o nome que a clínica deu à variante.
            //
            // ⚠️ O índice é procurado, e não a tupla: `FirstOrDefault` sobre uma tupla de
            // valor devolveria `Modalidade = 0`, que é um valor REAL do enum — "não achei"
            // ficaria indistinguível de "achei a primeira modalidade da lista".
            var indice = -1;
            for (var i = 0; i < modalidades.Count; i++)
            {
                var m = modalidades[i];
                if (m.Modalidade != exigencia.Modalidade) continue;
                if (!string.IsNullOrEmpty(exigencia.ModalidadeCodigo)
                    && !string.Equals(exigencia.ModalidadeCodigo, m.Codigo,
                        StringComparison.OrdinalIgnoreCase)) continue;

                indice = i;
                break;
            }

            if (indice < 0) continue;

            var profissionalId = modalidades[indice].ProfissionalId;

            // O termo cumprido é o que veio DESTE modelo. Casar pelo modelo, e não pelo
            // tipo, é o que permite dois procedimentos no mesmo dia exigirem dois termos
            // diferentes sem um cobrir o outro.
            //
            // A JANELA depende da validade escolhida para esta exigência (parcela 66, 3ª
            // rodada): sem prazo, vale o que foi assinado em qualquer data — é o caso
            // normal, e é o que permite colher na consulta em que o paciente tira dúvidas.
            // Marcada como "só no dia", só conta o do próprio dia, porque a declaração de
            // jejum não se herda.
            var doModelo = termosDoPaciente
                .Where(d => d.ModeloOrigemId == exigencia.ModeloDocumentoId && !d.Cancelado)
                .ToList();

            // ASSINADO herda pela janela da validade: sem prazo, o de qualquer data vale.
            var assinado = doModelo
                .Where(d => !exigencia.SoValeNoDiaDoProcedimento || d.Data == dia)
                .OrderByDescending(d => d.Data)
                .FirstOrDefault(d => d.PacienteAssinou);

            // ⚠️ RECUSA e PAPEL PENDENTE contam só no DIA, qualquer que seja a validade.
            //
            // A recusa é uma decisão de um momento, não um estado permanente: herdá-la
            // faria um "não" de três semanas atrás calar o pedido no dia do procedimento —
            // e o paciente pode ter mudado de ideia, tanto que veio fazer.
            //
            // O papel emitido e nunca assinado também não se reaproveita de outro dia: ele
            // carrega a DATA da emissão, e reusá-lo faria a assinatura de hoje nascer com
            // data velha — que numa exigência "só no dia" não contaria nunca.
            var recusado = doModelo.FirstOrDefault(d => d.PacienteRecusou && d.Data == dia);
            var emitido = doModelo.FirstOrDefault(
                d => d.AguardaAssinaturaDoPaciente && d.Data == dia);

            situacoes.Add(new SituacaoTermo(
                exigencia.Id,
                exigencia.Modalidade,
                exigencia.Modelo?.Nome ?? "Termo",
                exigencia.ModeloDocumentoId,
                assinado?.Id ?? recusado?.Id ?? emitido?.Id,
                assinado is not null,
                recusado is not null,
                recusado?.MotivoRecusaPaciente,
                DeclaracoesNegadas(assinado),
                profissionalId));
        }

        return situacoes;
    }

    /// <summary>
    /// As declarações que o paciente respondeu NÃO — "não estou em jejum", "não informei
    /// meus medicamentos".
    ///
    /// ⚠️ Elas não impedem nada, e é decisão. O termo existe para registrar a verdade, e um
    /// paciente que chegou sem jejum é exatamente o fato que precisa ficar escrito e
    /// assinado. Bloquear a emissão produziria o desfecho pior: ninguém emite o termo, o
    /// procedimento acontece assim mesmo, e não sobra registro nenhum. O que elas fazem é
    /// acender VERMELHO onde quem decide vai ver.
    /// </summary>
    private static IReadOnlyList<string> DeclaracoesNegadas(DocumentoClinico? documento)
        => documento is null
            ? []
            : documento.Itens
                .Where(i => RespostaDeclaracao.EhNegativa(i.Quantidade))
                .Select(i => i.Descricao)
                .ToList();

    private static string? Limpar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// Como o paciente respondeu uma declaração do termo.
///
/// É texto e não booleano porque mora em <see cref="ItemDocumento.Quantidade"/>, que é o
/// campo genérico que as sete impressões já usam — e porque a resposta precisa sair
/// IMPRESSA com essa palavra na via que o paciente leva. Um booleano viraria "True" no
/// papel se alguém esquecesse de traduzir.
/// </summary>
public static class RespostaDeclaracao
{
    public const string Sim = "Sim";
    public const string Nao = "Não";

    /// <summary>
    /// A resposta é um "não". Compara sem acento e sem caixa porque o valor pode ter sido
    /// gravado por versões diferentes da tela — e uma comparação estrita faria uma
    /// declaração negada deixar de acender o alerta, que é a falha que custa caro.
    /// </summary>
    public static bool EhNegativa(string? resposta)
        => resposta is not null
           && (resposta.Trim().Equals(Nao, StringComparison.OrdinalIgnoreCase)
               || resposta.Trim().Equals("Nao", StringComparison.OrdinalIgnoreCase));

    public static bool EhPositiva(string? resposta)
        => resposta is not null && resposta.Trim().Equals(Sim, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Um termo exigido para a sessão de hoje, e em que pé ele está.</summary>
/// <param name="DocumentoId">
/// O documento já emitido, quando existe. Null = nem foi emitido — a coleta começa do zero.
/// </param>
/// <param name="DeclaracoesNegadas">
/// O que o paciente respondeu NÃO. Assinado com declaração negada é o caso mais grave da
/// tela: o termo está cumprido e o procedimento pode não estar seguro.
/// </param>
public sealed record SituacaoTermo(
    int ExigenciaId,
    ModalidadeAtendimento Modalidade,
    string NomeDoTermo,
    int ModeloId,
    int? DocumentoId,
    bool Assinado,
    bool Recusado,
    string? MotivoRecusa,
    IReadOnlyList<string> DeclaracoesNegadas,
    int? ProfissionalId = null)
{
    /// <summary>Falta assinar: nem assinado, nem recusado.</summary>
    public bool Pendente => !Assinado && !Recusado;

    /// <summary>Assinado, mas com alguma declaração respondida "não".</summary>
    public bool TemDeclaracaoNegada => Assinado && DeclaracoesNegadas.Count > 0;
}
