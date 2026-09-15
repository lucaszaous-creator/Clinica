using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Domain.Prontuario;

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

        // ⚠️ A chave é modalidade+variante+MODELO (parcela 67): um procedimento pode exigir
        // VÁRIOS termos, e o BSV é o caso que obrigou a mudança — o consentimento (lido com
        // calma, vale a partir da assinatura) e a declaração do dia (o jejum, que não se
        // herda) são dois papéis com validades opostas.
        //
        // Até aqui a chave era só modalidade+variante, então exigir o segundo termo APAGAVA
        // o primeiro em silêncio: a clínica amarrava o jejum e perdia o consentimento sem
        // uma palavra. O `Resolver` sempre soube devolver vários (ele percorre as
        // exigências e o balcão lê uma LISTA) — quem não deixava era a escrita.
        //
        // O que se mantém da 2ª rodada da parcela 66: repetir a MESMA amarração não recusa,
        // atualiza. Antes ela lançava "troque o modelo da exigência que existe" e não havia
        // por onde trocar — mensagem de erro que manda fazer o que a tela não faz é botão
        // que não faz nada com uma etapa a mais. Trocar o TEXTO continua sendo editar o
        // modelo (mesmo Id); trocar de MODELO é ligar o novo e desligar o antigo, dois
        // cliques visíveis na mesma tela — e visível é o ponto, porque com a chave antiga a
        // troca acontecia sozinha e a lista deixava de mostrar o que sumiu.
        //
        // Aplicar COPIA: os termos já assinados guardam o texto que o paciente leu e o
        // `ModeloOrigemId` deles continua apontando para o modelo com que foram feitos.
        var existente = existentes.FirstOrDefault(x =>
            x.Modalidade == modalidade
            && x.ModeloDocumentoId == modeloId
            && string.Equals(x.ModalidadeCodigo, codigo, StringComparison.OrdinalIgnoreCase));

        if (existente is not null)
        {
            var rastreada = await _repo.ObterExigenciaTermoAsync(existente.Id, ct)
                ?? throw new InvalidOperationException("Exigência não encontrada.");

            // O modelo é o MESMO (é a chave); o que a repetição atualiza é a validade e o
            // religar de uma exigência desligada — que é o gesto de quem clica "Exigir" de
            // novo sobre a mesma amarração.
            rastreada.SoValeNoDiaDoProcedimento = soValeNoDiaDoProcedimento;
            rastreada.Ativa = true;

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "ExigenciaTermoAtualizada",
                Detalhe = $"{modalidade} — \"{modelo.Nome}\": "
                          + (soValeNoDiaDoProcedimento
                              ? "passa a ser exigido a cada sessão"
                              : "passa a valer a partir da assinatura")
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

        return Resolver(exigencias, modalidades, termos, data,
            await AssinaturasRemotasAguardandoAsync(termos, ct));
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

        // A QUARTA leitura, e ela só acontece quando há papel por assinar: quem já assinou
        // não tem coleta em aberto, e o dia sem termo pendente não paga consulta nenhuma.
        var aguardando = await AssinaturasRemotasAguardandoAsync(termos, ct);

        foreach (var grupo in agendamentos.GroupBy(a => a.PacienteId))
        {
            var modalidades = ModalidadesQuePedemTermo(grupo);
            if (modalidades.Count == 0) continue;

            var situacoes = Resolver(
                exigencias, modalidades, porPaciente[grupo.Key].ToList(), data, aguardando);

            if (situacoes.Count > 0) vazio[grupo.Key] = situacoes;
        }

        return vazio;
    }

    /// <summary>
    /// Quais dos papéis por assinar já têm assinatura do celular GUARDADA, esperando a
    /// conferência (set/2026).
    ///
    /// Só os que AGUARDAM assinatura entram na pergunta: o assinado não tem coleta em
    /// aberto, e mandar a lista inteira de termos do paciente ao banco seria pagar por uma
    /// resposta que já se sabe.
    /// </summary>
    private async Task<IReadOnlySet<int>> AssinaturasRemotasAguardandoAsync(
        IReadOnlyList<DocumentoClinico> termos, CancellationToken ct)
    {
        var candidatos = termos
            .Where(d => d.AguardaAssinaturaDoPaciente)
            .Select(d => d.Id)
            .Distinct()
            .ToList();

        if (candidatos.Count == 0) return new HashSet<int>();

        return (await _repo.DocumentosComAssinaturaRemotaAguardandoAsync(candidatos, ct))
            .ToHashSet();
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
    /// As sessões a que um termo colhido AGORA pode se referir (set/2026).
    ///
    /// É a lista da janela de escolha do balcão: "a qual sessão este termo pertence?".
    /// Vai de hoje para a frente, e não só hoje, porque a coleta antecipada é justamente
    /// o caso que a clínica pediu — o paciente aparece para tirar dúvidas e assina ali o
    /// consentimento do procedimento da semana que vem. Perguntar só sobre hoje deixaria
    /// de fora a razão de a porta avulsa existir.
    ///
    /// ⚠️ Cancelado e falta ficam de fora (<c>OcupaAgenda</c>): não há procedimento a
    /// consentir num horário que não vai acontecer, e oferecê-lo faria a procedência
    /// apontar para uma sessão desdita.
    ///
    /// Lista vazia é resposta legítima e comum: o paciente que passou no balcão sem nada
    /// marcado assina um termo AVULSO, sem sessão — e é por isso que quem chama não pode
    /// tratar o vazio como erro.
    /// </summary>
    /// <param name="dias">Quantos dias para a frente olhar. 60 cobre o horizonte em que a
    /// clínica marca procedimento; alargar isso não melhora a escolha, só alonga a lista.</param>
    public async Task<IReadOnlyList<SessaoParaTermo>> SessoesParaTermoAsync(
        int pacienteId, DateOnly de, int dias = 60, CancellationToken ct = default)
    {
        var agendamentos = await _repo.AgendamentosDoPacienteNoPeriodoAsync(
            pacienteId, de, de.AddDays(dias), ct);

        return agendamentos
            .Where(a => a.OcupaAgenda)
            .Select(a => new SessaoParaTermo(
                a.Id,
                a.DataHora,
                CatalogoModalidades.Nome(a.ModalidadeCodigo, a.ModalidadePrevista),
                a.Profissional?.Nome,
                DateOnly.FromDateTime(a.DataHora) == de))
            .ToList();
    }

    /// <summary>
    /// As modalidades do dia que podem pedir termo.
    ///
    /// Cancelado e falta ficam de fora: não há procedimento para consentir. O agendamento
    /// que ainda não chegou ENTRA — é justamente o caso que o balcão precisa resolver
    /// antes, e não depois.
    /// </summary>
    /// <param name="agendamentos">Os horários do paciente no dia.</param>
    /// <remarks>
    /// ⚠️ O agrupamento continua sendo por MODALIDADE, e não por horário, porque é ele que
    /// decide a COBERTURA — "este paciente já assinou o termo do BSV hoje?". Amarrar a
    /// cobertura ao horário faria um termo assinado de manhã deixar de valer à tarde, o que
    /// é decisão da direção e não efeito colateral de uma coluna nova (set/2026).
    ///
    /// O que passou a viajar junto é o <c>AgendamentoId</c> — e só quando a modalidade tem
    /// UM horário no dia. Com dois, ele vem nulo de propósito: escolher o primeiro seria
    /// gravar procedência inventada, e quem sabe desempatar é quem está com o paciente na
    /// frente (a janela de escolha).
    /// </remarks>
    private static IReadOnlyList<(ModalidadeAtendimento Modalidade, string? Codigo, int? ProfissionalId, int? AgendamentoId)>
        ModalidadesQuePedemTermo(IEnumerable<Agendamento> agendamentos)
        => agendamentos
            .Where(a => a.OcupaAgenda)
            // ⚠️ O agrupamento é por MODALIDADE, e o profissional NÃO entra na chave.
            //
            // Ele entrava, e isso abria um buraco na regra do horário: duas sessões de BSV
            // no mesmo dia com médicos diferentes viravam dois grupos de UM, e cada um
            // trazia o próprio `AgendamentoId` — o `Resolver` pegava o primeiro e gravava
            // procedência ESCOLHIDA POR ACIDENTE, que é exatamente o que a coluna nova
            // existe para não fazer. O teste não pegava porque os dois horários dele não
            // tinham profissional, e aí caíam no mesmo grupo.
            //
            // O profissional continua saindo daqui (o primeiro do grupo), como sempre saiu:
            // com dois médicos no mesmo procedimento o `Resolver` já escolhia um, e isso
            // não mudou.
            .GroupBy(a => (a.ModalidadePrevista, Codigo: Limpar(a.ModalidadeCodigo)))
            .Select(g => (g.Key.ModalidadePrevista, g.Key.Codigo,
                          ProfissionalId: g.First().ProfissionalId,
                          AgendamentoId: g.Count() == 1 ? g.First().Id : (int?)null))
            .ToList();

    private static IReadOnlyList<SituacaoTermo> Resolver(
        IReadOnlyList<ExigenciaTermoProcedimento> exigencias,
        IReadOnlyList<(ModalidadeAtendimento Modalidade, string? Codigo, int? ProfissionalId, int? AgendamentoId)> modalidades,
        IReadOnlyList<DocumentoClinico> termosDoPaciente,
        DateOnly dia,
        IReadOnlySet<int> assinaturasRemotasAguardando)
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
                profissionalId,
                modalidades[indice].AgendamentoId,
                // A assinatura do celular está guardada e falta conferir. Só faz sentido
                // sobre o papel EMITIDO deste dia — é ele que a coleta remota aponta.
                emitido is not null && assinaturasRemotasAguardando.Contains(emitido.Id)));
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
                .Where(RespostaDeclaracao.RequerAtencao)
                .Select(i => i.Descricao)
                .ToList();

    private static string? Limpar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}


/// <summary>Um termo exigido para a sessão de hoje, e em que pé ele está.</summary>
/// <param name="DocumentoId">
/// O documento já emitido, quando existe. Null = nem foi emitido — a coleta começa do zero.
/// </param>
/// <param name="DeclaracoesNegadas">
/// O que o paciente respondeu NÃO. Assinado com declaração negada é o caso mais grave da
/// tela: o termo está cumprido e o procedimento pode não estar seguro.
/// </param>
/// <param name="AgendamentoId">
/// A SESSÃO a que o termo se refere, quando a modalidade tem UM horário no dia (set/2026).
/// Nulo com dois horários: escolher o primeiro seria inventar a procedência, e quem
/// desempata é quem está com o paciente na frente.
/// </param>
/// <param name="AssinaturaRemotaAguardaConferencia">
/// O paciente JÁ ASSINOU pelo celular, a assinatura está guardada no banco e falta alguém
/// conferir a identidade e concluir (set/2026).
///
/// ⚠️ Continua sendo <see cref="Pendente"/>: o documento não está selado, e o termo só
/// está cumprido quando estiver. O que muda é a FRASE que o balcão lê — "falta o termo" e
/// "o termo está assinado, falta conferir" mandam fazer coisas diferentes, e tratá-los
/// como o mesmo estado faz a recepcionista mandar outro link para quem já assinou.
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
    int? ProfissionalId = null,
    int? AgendamentoId = null,
    bool AssinaturaRemotaAguardaConferencia = false)
{

    /// <summary>Falta assinar: nem assinado, nem recusado.</summary>
    public bool Pendente => !Assinado && !Recusado;

    /// <summary>
    /// Falta assinar e NINGUÉM assinou ainda — nem no balcão, nem no celular.
    ///
    /// É o que o selo vermelho da lista do dia lê. O termo já assinado pelo celular tem
    /// selo próprio: um clique resolve, e cobrá-lo com a mesma cor de quem não assinou
    /// nada ensina a ignorar a cor.
    /// </summary>
    public bool PendenteSemAssinatura => Pendente && !AssinaturaRemotaAguardaConferencia;

    /// <summary>Assinado, mas com alguma declaração respondida "não".</summary>
    public bool TemDeclaracaoNegada => Assinado && DeclaracoesNegadas.Count > 0;

    /// <summary>
    /// O VERBO do que falta fazer: "Colher" quando ninguém assinou, "Conferir" quando o
    /// paciente já assinou pelo celular (set/2026).
    ///
    /// ⚠️ Mora AQUI porque são QUATRO telas dizendo a mesma frase — a lista do dia, a
    /// ficha, o Consultório e a Enfermagem. Quatro cópias divergem na primeira correção, e
    /// a que ficar para trás manda a pessoa repetir o gesto que não funciona: reenviar o
    /// link para quem já assinou, que é justamente o que o link write-once recusa.
    /// </summary>
    public string VerboDaPendencia => AssinaturaRemotaAguardaConferencia ? "Conferir" : "Colher";

    /// <summary>"Colher: Termo do BSV" — o rótulo do botão que resolve esta pendência.</summary>
    public string RotuloDaPendencia => $"{VerboDaPendencia}: {NomeDoTermo}";
}

/// <summary>
/// Como as telas escolhem QUAL pendência de termo mostrar (set/2026).
/// </summary>
public static class PendenciasDeTermo
{
    /// <summary>
    /// A pendência que a tela deve oferecer: o que JÁ FOI ASSINADO no celular vem primeiro.
    ///
    /// ⚠️ A ordem não é estilo. O assinado está a UM clique de terminar; o não assinado
    /// ainda precisa do paciente na frente. Oferecer o outro primeiro faria a técnica
    /// colher de novo a assinatura de quem já assinou — e o link não aceita a segunda.
    /// </summary>
    public static SituacaoTermo? Primeira(IEnumerable<SituacaoTermo> situacoes)
    {
        var lista = situacoes as IReadOnlyList<SituacaoTermo> ?? situacoes.ToList();

        return lista.FirstOrDefault(s => s.AssinaturaRemotaAguardaConferencia)
               ?? lista.FirstOrDefault(s => s.Pendente);
    }
}


/// <summary>
/// Uma sessão que pode receber a procedência de um termo (set/2026).
///
/// O rótulo da modalidade vem resolvido do <c>CatalogoModalidades</c> — a variante que a
/// clínica cadastrou, e não o nome da família —, porque é o que a pessoa lê na agenda: um
/// paciente com duas sessões no mesmo dia se distingue por "BSV" contra "Acupuntura", não
/// por dois horários iguais.
/// </summary>
/// <param name="Hoje">
/// Serve à janela para separar "a sessão de hoje" das próximas — a primeira é o caso
/// esperado, e as outras existem para a coleta antecipada.
/// </param>
public sealed record SessaoParaTermo(
    int AgendamentoId,
    DateTime Quando,
    string Modalidade,
    string? Profissional,
    bool Hoje)
{
    /// <summary>
    /// "Hoje, 09h00" · "seg, 15/09, 14h30".
    ///
    /// Em cultura pt-BR FIXA, e não na da máquina: é rótulo de escolha clínica, e dois
    /// postos não podem oferecer "Mon" e "seg" para o mesmo horário — a mesma razão da
    /// mensagem de confirmação que sai da clínica.
    /// </summary>
    public string Rotulo => Hoje
        ? $"Hoje, {Quando:HH'h'mm}"
        : string.Create(Cultura, $"{Quando:ddd, dd/MM}, {Quando:HH'h'mm}");

    /// <summary>
    /// "Acupuntura + eletro · Dra. Helena Prado" — e só a modalidade quando o horário
    /// ainda não tem dono. Frase montada por concatenação não sabe PULAR o que não
    /// existe, e sairia com um "·" solto no fim (a lição do crachá do paciente).
    /// </summary>
    public string Detalhe => string.IsNullOrWhiteSpace(Profissional)
        ? Modalidade
        : $"{Modalidade}  ·  {Profissional}";

    private static readonly System.Globalization.CultureInfo Cultura
        = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
}
