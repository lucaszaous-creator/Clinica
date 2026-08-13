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
/// A validade é POR SESSÃO, e é decisão da clínica (ago/2026). A razão está no documento
/// que motivou o pedido: a declaração de jejum afirma "ESTOU em jejum", e afirmação sobre
/// o estado de hoje não se herda da semana passada. É por isso que a chave de "já
/// assinou?" é a DATA, e não um prazo em dias.
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
    public async Task<ExigenciaTermoProcedimento> ExigirAsync(
        ModalidadeAtendimento modalidade, int modeloId, string? modalidadeCodigo = null,
        string? operador = null, CancellationToken ct = default)
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
        var codigo = Limpar(modalidadeCodigo);

        if (existentes.Any(x => x.Modalidade == modalidade
                                && string.Equals(x.ModalidadeCodigo, codigo,
                                    StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Esta modalidade já exige um termo. Troque o modelo da exigência que existe "
                + "em vez de criar outra — duas exigências para a mesma sessão fariam o "
                + "paciente assinar o mesmo papel duas vezes.");

        var exigencia = new ExigenciaTermoProcedimento
        {
            Modalidade = modalidade,
            ModalidadeCodigo = codigo,
            ModeloDocumentoId = modeloId,
            Ativa = true,
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

        var termosDeHoje = await _repo.TermosDoPacienteNaDataAsync(pacienteId, data, ct);

        return Resolver(exigencias, modalidades, termosDeHoje);
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

        var termosDoDia = await _repo.TermosDaDataAsync(data, ct);
        var porPaciente = termosDoDia.ToLookup(d => d.PacienteId);

        foreach (var grupo in agendamentos.GroupBy(a => a.PacienteId))
        {
            var modalidades = ModalidadesQuePedemTermo(grupo);
            if (modalidades.Count == 0) continue;

            var situacoes = Resolver(exigencias, modalidades, porPaciente[grupo.Key].ToList());
            if (situacoes.Count > 0) vazio[grupo.Key] = situacoes;
        }

        return vazio;
    }

    /// <summary>
    /// As modalidades do dia que podem pedir termo.
    ///
    /// Cancelado e falta ficam de fora: não há procedimento para consentir. O agendamento
    /// que ainda não chegou ENTRA — é justamente o caso que o balcão precisa resolver
    /// antes, e não depois.
    /// </summary>
    private static IReadOnlyList<(ModalidadeAtendimento Modalidade, string? Codigo)>
        ModalidadesQuePedemTermo(IEnumerable<Agendamento> agendamentos)
        => agendamentos
            .Where(a => a.Status is not (StatusAgendamento.Cancelado or StatusAgendamento.Faltou))
            .Select(a => (a.ModalidadePrevista, Codigo: Limpar(a.ModalidadeCodigo)))
            .Distinct()
            .ToList();

    private static IReadOnlyList<SituacaoTermo> Resolver(
        IReadOnlyList<ExigenciaTermoProcedimento> exigencias,
        IReadOnlyList<(ModalidadeAtendimento Modalidade, string? Codigo)> modalidades,
        IReadOnlyList<DocumentoClinico> termosDeHoje)
    {
        var situacoes = new List<SituacaoTermo>();

        foreach (var exigencia in exigencias)
        {
            var casa = modalidades.Any(m =>
                m.Modalidade == exigencia.Modalidade
                && (exigencia.ModalidadeCodigo is null
                    || string.Equals(exigencia.ModalidadeCodigo, m.Codigo,
                        StringComparison.OrdinalIgnoreCase)));

            if (!casa) continue;

            // O termo cumprido é o que veio DESTE modelo e foi assinado hoje. Casar pelo
            // modelo, e não pelo tipo, é o que permite dois procedimentos no mesmo dia
            // exigirem dois termos diferentes sem um cobrir o outro.
            var doModelo = termosDeHoje
                .Where(d => d.ModeloOrigemId == exigencia.ModeloDocumentoId && !d.Cancelado)
                .ToList();

            var assinado = doModelo.FirstOrDefault(d => d.PacienteAssinou);
            var recusado = doModelo.FirstOrDefault(d => d.PacienteRecusou);
            var emitido = doModelo.FirstOrDefault(d => d.AguardaAssinaturaDoPaciente);

            situacoes.Add(new SituacaoTermo(
                exigencia.Id,
                exigencia.Modalidade,
                exigencia.Modelo?.Nome ?? "Termo",
                exigencia.ModeloDocumentoId,
                assinado?.Id ?? recusado?.Id ?? emitido?.Id,
                assinado is not null,
                recusado is not null,
                recusado?.MotivoRecusaPaciente,
                DeclaracoesNegadas(assinado)));
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
    IReadOnlyList<string> DeclaracoesNegadas)
{
    /// <summary>Falta assinar: nem assinado, nem recusado.</summary>
    public bool Pendente => !Assinado && !Recusado;

    /// <summary>Assinado, mas com alguma declaração respondida "não".</summary>
    public bool TemDeclaracaoNegada => Assinado && DeclaracoesNegadas.Count > 0;
}
