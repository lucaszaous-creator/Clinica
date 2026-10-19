using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um campo personalizado na folha da sessão (out/2026, migração da folha do médico para
/// Blazor): a definição (rótulo, tipo, ajuda) com a resposta que está sendo escrita. É o
/// <c>CampoDaSessao</c> do shell WPF, subido para cá para a regra (que caixa, que dica) ser
/// alcançada pelo <c>dotnet test</c>.
///
/// A resposta é TEXTO para todos os tipos — quem a normaliza é o
/// <see cref="CampoPersonalizadoService"/>, na gravação, com o tipo ao lado.
/// </summary>
public sealed class CampoDaSessaoNaTela
{
    public required int Id { get; init; }
    public required string Rotulo { get; init; }
    public required TipoCampoPersonalizado Tipo { get; init; }
    public string? Ajuda { get; init; }
    public required IReadOnlyList<string> Opcoes { get; init; }

    public string? Resposta { get; set; }

    public bool EhLista => Tipo == TipoCampoPersonalizado.Lista;
    public bool EhSimNao => Tipo == TipoCampoPersonalizado.SimNao;
    public bool EhTextoLongo => Tipo == TipoCampoPersonalizado.TextoLongo;
    public bool EhCaixaDeTexto => !EhLista && !EhSimNao;

    /// <summary>A dica do que se espera, sem obrigar a pessoa a errar para descobrir.</summary>
    public string Dica => Tipo switch
    {
        TipoCampoPersonalizado.Numero => "número",
        TipoCampoPersonalizado.Data => "dd/mm/aaaa",
        _ => string.Empty
    };

    public bool TemAjuda => !string.IsNullOrWhiteSpace(Ajuda);

    public static CampoDaSessaoNaTela De(CampoPersonalizadoProntuario d) => new()
    {
        Id = d.Id,
        Rotulo = d.Rotulo,
        Tipo = d.Tipo,
        Ajuda = d.Ajuda,
        Opcoes = d.OpcoesDaLista
    };
}

/// <summary>
/// O RASCUNHO da sessão do médico — o que a folha de atendimento edita antes de gravar
/// (out/2026: a <c>FolhaDaSessaoViewModel</c> do shell, sem WPF). Tudo o que a folha
/// DECIDIA mora aqui, puro e testado: o que conta como "em branco", o que conta como "há
/// algo a gravar", o selo do detalhe, a descrição do CID, a aplicação do modelo, a
/// reutilização da sessão anterior e a cópia da última evolução.
///
/// ⚠️ <see cref="SessaoEmBranco"/> e <see cref="TemAlgoParaGravar"/> respondem perguntas
/// DIFERENTES (a lição da parcela 74): a primeira decide se a tela PERGUNTA "encerrar sem
/// escrever?" e deixa a EVA e o mapa de fora com razão; a segunda decide se GRAVA, e a
/// sessão de acupuntura mais comum (EVA 8→3, seis pontos, nenhuma linha) tem MUITO a gravar.
/// </summary>
public sealed class RascunhoDaSessao
{
    /// <summary>Evolução em edição. 0 = sessão nova.</summary>
    public int EvolucaoId { get; set; }

    public DateOnly Data { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int? EvaAntes { get; set; }
    public int? EvaDepois { get; set; }
    public string? QueixaPrincipal { get; set; }
    public string? HistoriaDoencaAtual { get; set; }
    public string? ExameFisico { get; set; }
    public string? HipoteseDiagnostica { get; set; }
    public string? CidSessao { get; set; }
    public string? Conduta { get; set; }
    public string? TextoEvolucao { get; set; }

    /// <summary>
    /// Só negrito/itálico, e só aceito quando recompõe o <see cref="TextoEvolucao"/>. Vazio
    /// (<c>""</c>) é "remova a formatação" — o editor manda isso; nulo é o cliente antigo.
    /// </summary>
    public string? TextoEvolucaoFormatado { get; set; }

    public string? Orientacoes { get; set; }
    public string? PlanoTerapeutico { get; set; }

    /// <summary>QUANDO reavaliar. Não vira agendamento nenhum — quem marca é a recepção.</summary>
    public DateOnly? RetornoSugeridoEm { get; set; }

    public string? RetornoSugeridoNota { get; set; }
    public string? Encaminhamento { get; set; }

    /// <summary>Os campos personalizados que valem para esta sessão (vazia na clínica que não cadastrou nenhum).</summary>
    public List<CampoDaSessaoNaTela> Campos { get; } = [];

    /// <summary>Valores da escala, para os dois seletores de dor.</summary>
    public static IReadOnlyList<int> EscalaEva { get; } =
        Enumerable.Range(Evolucao.EvaMinima, Evolucao.EvaMaxima - Evolucao.EvaMinima + 1).ToList();

    /// <summary>
    /// A descrição do CID digitado — a metade que PEGA o erro ("M54.4" no lugar de "M54.5").
    /// Código fora do catálogo não é recusado: a frase diz que ele não foi reconhecido.
    /// </summary>
    public string? DescricaoCid => string.IsNullOrWhiteSpace(CidSessao)
        ? null
        : CatalogoCid.Descrever(CidSessao) ?? "Código fora do catálogo desta clínica — confira antes de gravar.";

    /// <summary>
    /// Os campos do detalhe que ESTA sessão já tem escritos. Os personalizados NÃO entram:
    /// eles estão à vista na folha, e o selo conta o que está escondido atrás do clique.
    /// </summary>
    public int CamposDetalhados =>
        new[]
        {
            QueixaPrincipal, HistoriaDoencaAtual, ExameFisico, HipoteseDiagnostica, CidSessao,
            Conduta, Orientacoes, PlanoTerapeutico, RetornoSugeridoNota, Encaminhamento
        }.Count(v => !string.IsNullOrWhiteSpace(v));

    public bool TemCamposDetalhados => CamposDetalhados > 0;

    /// <summary>Diz QUANTOS, não "tem conteúdo": o número é o que faz alguém clicar.</summary>
    public string SeloDetalhe => CamposDetalhados == 1 ? "1 campo preenchido" : $"{CamposDetalhados} campos preenchidos";

    /// <summary>
    /// Nada do que o profissional ESCREVERIA foi escrito. Serve ao "Finalizar": encerrar sem
    /// registro é legítimo, e a tela PERGUNTA. A EVA, o CID e o mapa não entram: são medida.
    /// O plano, o retorno e o encaminhamento ENTRAM (a lista do <c>temTexto</c> do serviço).
    /// </summary>
    public bool SessaoEmBranco
        => string.IsNullOrWhiteSpace(QueixaPrincipal)
           && string.IsNullOrWhiteSpace(Conduta)
           && string.IsNullOrWhiteSpace(TextoEvolucao)
           && string.IsNullOrWhiteSpace(HistoriaDoencaAtual)
           && string.IsNullOrWhiteSpace(ExameFisico)
           && string.IsNullOrWhiteSpace(HipoteseDiagnostica)
           && string.IsNullOrWhiteSpace(Orientacoes)
           && string.IsNullOrWhiteSpace(PlanoTerapeutico)
           && RetornoSugeridoEm is null
           && string.IsNullOrWhiteSpace(RetornoSugeridoNota)
           && string.IsNullOrWhiteSpace(Encaminhamento);

    /// <summary>Alguma resposta de campo personalizado foi escrita.</summary>
    public bool TemRespostaPersonalizada => Campos.Any(c => !string.IsNullOrWhiteSpace(c.Resposta));

    /// <summary>
    /// Há ALGUMA COISA a gravar — texto, EVA, CID ou o mapa (pontos ou observações). Pergunta
    /// DIFERENTE de <see cref="SessaoEmBranco"/>; o mapa entra por parâmetro porque ele é
    /// estado próprio (<see cref="MapaDaSessao"/>).
    /// </summary>
    public bool TemAlgoParaGravar(bool mapaTemConteudo)
        => !SessaoEmBranco
           || EvaAntes is not null
           || EvaDepois is not null
           || !string.IsNullOrWhiteSpace(CidSessao)
           || mapaTemConteudo;

    /// <summary>A frase do rodapé: a hora da última gravação, ou a dica da EVA antes dela.</summary>
    public static string DicaRodape(string? ultimaGravacao)
        => string.IsNullOrEmpty(ultimaGravacao)
            ? "A EVA vale em par: registre o depois ao terminar a sessão."
            : ultimaGravacao;

    /// <summary>Abre uma sessão já escrita. Os personalizados casam pelo CampoId, nunca pelo rótulo.</summary>
    public void Preencher(Evolucao e)
    {
        EvolucaoId = e.Id;
        Data = e.Data;
        EvaAntes = e.EvaAntes;
        EvaDepois = e.EvaDepois;
        QueixaPrincipal = e.QueixaPrincipal;
        HistoriaDoencaAtual = e.HistoriaDoencaAtual;
        ExameFisico = e.ExameFisico;
        HipoteseDiagnostica = e.HipoteseDiagnostica;
        CidSessao = e.CidSessao;
        Conduta = e.Conduta;
        TextoEvolucao = e.TextoEvolucao;
        TextoEvolucaoFormatado = e.TextoEvolucaoFormatado;
        Orientacoes = e.Orientacoes;
        PlanoTerapeutico = e.PlanoTerapeutico;
        RetornoSugeridoEm = e.RetornoSugeridoEm;
        RetornoSugeridoNota = e.RetornoSugeridoNota;
        Encaminhamento = e.Encaminhamento;

        foreach (var campo in Campos)
            campo.Resposta = e.CamposPersonalizados.FirstOrDefault(v => v.CampoId == campo.Id) is { } valor
                ? CampoPersonalizadoService.Exibir(valor)
                : null;
    }

    /// <summary>
    /// Zera para uma sessão sem evolução. ⚠️ A DATA é a do HORÁRIO, não a de hoje: escrever a
    /// sessão de terça com a data de hoje mentiria no prontuário e deixaria a pendência de pé.
    /// </summary>
    public void Limpar(DateOnly? dataDoHorario, DateOnly hoje)
    {
        EvolucaoId = 0;
        Data = dataDoHorario ?? hoje;
        EvaAntes = null;
        EvaDepois = null;
        QueixaPrincipal = null;
        HistoriaDoencaAtual = null;
        ExameFisico = null;
        HipoteseDiagnostica = null;
        CidSessao = null;
        Conduta = null;
        TextoEvolucao = null;
        TextoEvolucaoFormatado = null;
        Orientacoes = null;
        PlanoTerapeutico = null;
        RetornoSugeridoEm = null;
        RetornoSugeridoNota = null;
        Encaminhamento = null;
        foreach (var campo in Campos) campo.Resposta = null;
    }

    /// <summary>
    /// A evolução como o <c>ProntuarioService.SalvarAsync</c> a recebe — os doze campos, a
    /// formatação ("" quando o editor a removeu, nunca nulo daqui) e os personalizados
    /// COPIADOS com rótulo e tipo. Lista vazia é "a tela os mostra e estão em branco".
    /// </summary>
    public Evolucao ParaGravacao(int pacienteId, int? profissionalId, int? agendamentoId, int? atendimentoId,
        IReadOnlyList<CampoPersonalizadoProntuario> definicoes) => new()
    {
        Id = EvolucaoId,
        PacienteId = pacienteId,
        ProfissionalId = profissionalId,
        AgendamentoId = agendamentoId,
        AtendimentoId = atendimentoId,
        Data = Data,
        EvaAntes = EvaAntes,
        EvaDepois = EvaDepois,
        QueixaPrincipal = QueixaPrincipal,
        HistoriaDoencaAtual = HistoriaDoencaAtual,
        ExameFisico = ExameFisico,
        HipoteseDiagnostica = HipoteseDiagnostica,
        CidSessao = CidSessao,
        Conduta = Conduta,
        TextoEvolucao = TextoEvolucao,
        TextoEvolucaoFormatado = TextoEvolucaoFormatado ?? string.Empty,
        Orientacoes = Orientacoes,
        PlanoTerapeutico = PlanoTerapeutico,
        RetornoSugeridoEm = RetornoSugeridoEm,
        RetornoSugeridoNota = RetornoSugeridoNota,
        Encaminhamento = Encaminhamento,
        CamposPersonalizados = CampoPersonalizadoService.Montar(
            definicoes, Campos.ToDictionary(c => c.Id, c => c.Resposta)).ToList()
    };

    /// <summary>
    /// O roteiro escolhido: cada campo que o modelo TRAZ passa a valer (o mesmo gesto da
    /// janela WPF); o que ele não traz fica como estava. O texto do modelo é simples, então
    /// substituí-lo remove também o estilo anterior (<c>""</c>).
    /// </summary>
    public void AplicarModelo(ModeloEvolucao m)
    {
        if (!string.IsNullOrWhiteSpace(m.QueixaPrincipal)) QueixaPrincipal = m.QueixaPrincipal;
        if (!string.IsNullOrWhiteSpace(m.Conduta)) Conduta = m.Conduta;
        if (!string.IsNullOrWhiteSpace(m.TextoEvolucao))
        {
            TextoEvolucao = m.TextoEvolucao;
            TextoEvolucaoFormatado = string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(m.Orientacoes)) Orientacoes = m.Orientacoes;
        if (!string.IsNullOrWhiteSpace(m.HistoriaDoencaAtual)) HistoriaDoencaAtual = m.HistoriaDoencaAtual;
        if (!string.IsNullOrWhiteSpace(m.ExameFisico)) ExameFisico = m.ExameFisico;
        if (!string.IsNullOrWhiteSpace(m.HipoteseDiagnostica)) HipoteseDiagnostica = m.HipoteseDiagnostica;
        if (!string.IsNullOrWhiteSpace(m.CidSessao)) CidSessao = m.CidSessao;
        if (!string.IsNullOrWhiteSpace(m.PlanoTerapeutico)) PlanoTerapeutico = m.PlanoTerapeutico;
    }

    /// <summary>O que o "Salvar como modelo" guarda: os nove campos do roteiro (nunca retorno nem encaminhamento).</summary>
    public ModeloEvolucao ComoModelo(string nome, int? dono) => new()
    {
        Nome = nome.Trim(),
        ProfissionalId = dono,
        QueixaPrincipal = QueixaPrincipal,
        Conduta = Conduta,
        TextoEvolucao = TextoEvolucao,
        Orientacoes = Orientacoes,
        HistoriaDoencaAtual = HistoriaDoencaAtual,
        ExameFisico = ExameFisico,
        HipoteseDiagnostica = HipoteseDiagnostica,
        CidSessao = CidSessao,
        PlanoTerapeutico = PlanoTerapeutico
    };

    /// <summary>Há texto que valha guardar como modelo (a regra da janela WPF).</summary>
    public bool TemTextoParaModelo =>
        !string.IsNullOrWhiteSpace(QueixaPrincipal) || !string.IsNullOrWhiteSpace(Conduta)
        || !string.IsNullOrWhiteSpace(TextoEvolucao) || !string.IsNullOrWhiteSpace(Orientacoes);

    /// <summary>
    /// Traz a sessão escolhida para a folha — sem gravar nada, e NUNCA por cima do que já está
    /// escrito: a conduta, o texto da folha (com o estilo dele) e a queixa, cada um só quando o
    /// campo daqui está vazio. Falar em "trazido" sem ter trazido nada é a garantia aparente.
    /// </summary>
    public (string Frase, bool Erro) Reutilizar(ResumoSessaoAnterior? sessao)
    {
        if (sessao is null) return ("Não há sessão anterior para repetir.", true);

        var trazidos = new List<string>();

        if (sessao.Valor(ResumoSessaoAnterior.RotuloConduta) is { } conduta && string.IsNullOrWhiteSpace(Conduta))
        {
            Conduta = conduta;
            trazidos.Add("a conduta");
        }

        if (sessao.Valor(ResumoSessaoAnterior.RotuloEvolucao) is { } folha && string.IsNullOrWhiteSpace(TextoEvolucao))
        {
            TextoEvolucao = folha;
            TextoEvolucaoFormatado = sessao.Campos
                .FirstOrDefault(c => c.Rotulo == ResumoSessaoAnterior.RotuloEvolucao)?.Formato ?? string.Empty;
            trazidos.Add("o texto da sessão");
        }

        if (sessao.Valor(ResumoSessaoAnterior.RotuloQueixa) is { } queixa && string.IsNullOrWhiteSpace(QueixaPrincipal))
        {
            QueixaPrincipal = queixa;
            trazidos.Add("a queixa");
        }

        return trazidos.Count == 0
            ? ($"Nada a trazer da sessão de {sessao.Data}: o que ela tem escrito já está preenchido aqui, "
               + "ou ela não tem conduta nem texto.", true)
            : ($"Da sessão de {sessao.Data}: {string.Join(" e ", trazidos)}. Nada foi gravado — confira e salve.", false);
    }

    /// <summary>
    /// A cópia da ÚLTIMA evolução salva: SUBSTITUI os textos clínicos e as respostas dos
    /// personalizados (a tela confirma antes, quando há algo escrito), e PRESERVA a EVA, os
    /// sinais vitais e as datas desta sessão.
    /// </summary>
    public string Copiar(CopiaEvolucaoPaciente m)
    {
        QueixaPrincipal = m.QueixaPrincipal;
        HistoriaDoencaAtual = m.HistoriaDoencaAtual;
        ExameFisico = m.ExameFisico;
        HipoteseDiagnostica = m.HipoteseDiagnostica;
        CidSessao = m.CidSessao;
        Conduta = m.Conduta;
        TextoEvolucao = m.TextoEvolucao;
        TextoEvolucaoFormatado = m.TextoEvolucaoFormatado ?? string.Empty;
        Orientacoes = m.Orientacoes;
        PlanoTerapeutico = m.PlanoTerapeutico;
        Encaminhamento = m.Encaminhamento;
        foreach (var campo in Campos)
            campo.Resposta = m.CamposPersonalizados.GetValueOrDefault(campo.Id);
        return $"Evolução de {m.DataOrigem:dd/MM/yyyy} copiada. Revise para esta sessão e salve. Nada foi gravado.";
    }

    /// <summary>A pergunta antes de copiar: só quando há algo que a cópia substituiria.</summary>
    public bool CopiaSubstituiAlgo(bool mapaTemConteudo) => TemAlgoParaGravar(mapaTemConteudo) || TemRespostaPersonalizada;

    /// <summary>
    /// A frase depois de gravar. O par incompleto da EVA AVISA e não impede (o "depois" é
    /// medido ao fim do atendimento).
    /// </summary>
    public string FraseDepoisDeGravar()
        => EvaAntes is not null && EvaDepois is null
            ? "Gravado. A EVA está só com a medida ANTES — sem o par não dá para dizer se a sessão aliviou. "
              + "Volte aqui ao terminar para registrar o depois."
            : "Sessão registrada no prontuário.";
}

/// <summary>
/// As frases do FINALIZAR (o "Salvar e finalizar" do cabeçalho da tela do paciente, portado
/// da <c>PacienteWorkspaceViewModel</c>). A ordem é amarra: grava a sessão → encerra →
/// conclui; cada desfecho tem a frase do que a pessoa faz em seguida.
/// </summary>
public static class FinalizacaoDaSessao
{
    public const string SemAcesso =
        "O médico responsável ou o Gerente Geral pode finalizar. A enfermagem salva sua evolução sem encerrar o atendimento.";

    public const string SemAtendimento = "Não há atendimento em curso para finalizar.";

    public const string PerguntaGerenteTitulo = "Concluir a sessão registrada?";

    public const string PerguntaGerente =
        "A conclusão usa a evolução já salva e gera as guias aplicáveis. Se você editou o texto, volte e use "
        + "Salvar sessão antes de concluir. A enfermagem poderá registrar depois na mesma sessão.";

    public const string PassagemTitulo = "Há uma passagem de enfermagem não registrada";

    public const string PassagemNaoRegistrada =
        "Você escreveu uma passagem de enfermagem e ainda não clicou em “Registrar”.\n\nEncerrando agora, ela NÃO é "
        + "gravada — a evolução de enfermagem é registrada uma a uma, com a hora do que foi observado.\n\nEncerrar mesmo assim?";

    public const string EmBrancoTitulo = "Encerrar sem escrever a evolução?";

    public const string EmBranco =
        "Você não escreveu nada desta sessão.\n\nEncerrando assim, ela vai aparecer em “Sessões sem evolução” até "
        + "alguém escrever — e o registro feito de memória, dias depois, é o que este sistema existe para evitar.\n\n"
        + "Encerrar mesmo assim?";

    public const string NaoGravou =
        "A sessão não pôde ser salva, então o atendimento NÃO foi encerrado. A mensagem do erro está na seção Atendimento.";

    public const string SemPaciente =
        "Sessão gravada, mas a conclusão ficou pendente: a tela perdeu o paciente em foco. A recepção conclui pela fila do dia.";

    public const string ConvenioPendente =
        "Sessão gravada. A conclusão está pendente porque o convênio do paciente ainda não está definido — a recepção "
        + "conclui pela agenda do dia. Você também pode tentar finalizar novamente.";

    /// <summary>O desfecho da conclusão, com os recados do lançamento (a NC reaberta) junto.</summary>
    public static string Concluida(int guiasGeradas, IReadOnlyList<string> recados)
    {
        var junto = recados.Count > 0 ? " · " + string.Join(" · ", recados) : string.Empty;
        return guiasGeradas == 0
            ? "Sessão concluída — particular, sem guia a faturar." + junto
            : $"Sessão concluída — {guiasGeradas} guia(s) no faturamento." + junto;
    }

    /// <summary>A falha da conclusão: se a evolução já estava salva, a frase diz como retomar.</summary>
    public static string Falhou(bool gravou, string erro)
        => gravou
            ? "A evolução está salva. Não foi possível confirmar a conclusão: " + erro
              + " Tente finalizar novamente neste mesmo atendimento; não crie outro lançamento."
            : erro;

    /// <summary>A orientação do Gerente quando o atendimento ainda não foi encerrado.</summary>
    public const string OrientacaoGerente =
        "Use Concluir sessão para encerrar o atendimento já registrado e gerar as guias. Se editou a evolução, salve antes.";
}

/// <summary>
/// Um aviso sobre o paciente na sala (a <c>LinhaAlertaClinico</c> do Consultório WPF). A
/// urgência viaja COM cada aviso — nunca recalculada pela tela.
/// </summary>
public sealed record AlertaDaFolha(string Texto, bool Grave, string? Detalhes = null)
{
    public bool TemDetalhes => !string.IsNullOrWhiteSpace(Detalhes);
}

/// <summary>
/// As frases dos avisos da folha do médico. Clínico (alergia, uso contínuo, intercorrência
/// da enfermagem) e administrativo (carteirinha, cota, dívida…) em listas SEPARADAS: alergia
/// se resolve ANTES de prescrever; carteirinha, no balcão depois. E cada leitura que falha
/// DIZ que falhou — lista vazia por erro não pode ter a cara de "nada a avisar".
/// </summary>
public static class AlertasDaFolha
{
    public const string FalhaProblemas =
        "Não foi possível ler a lista de problemas deste paciente — ela está vazia por falha de leitura, "
        + "não porque não haja alergia registrada.";

    public const string FalhaIntercorrencias =
        "Não foi possível ler as intercorrências de enfermagem deste paciente — a ausência delas aqui é falha de "
        + "leitura, não garantia de que não houve nenhuma.";

    public const string FalhaAdministrativos =
        "Não foi possível conferir carteirinha, cota e pendências deste paciente — a lista está vazia por falha de "
        + "leitura, não porque não haja nada.";

    /// <summary>Alergia vermelha (mesmo dada por resolvida — só o descarte a cala); uso contínuo amarelo.</summary>
    public static IReadOnlyList<AlertaDaFolha> DosProblemas(IReadOnlyList<ProblemaPaciente> alertas)
    {
        var linhas = new List<AlertaDaFolha>();
        if (ProblemaPacienteService.ResumirAlergias(alertas) is { } resumo)
            linhas.Add(new AlertaDaFolha(resumo, true, ProblemaPacienteService.DetalharAlergias(alertas)));
        foreach (var p in alertas.Where(p => p.Natureza != NaturezaProblema.Alergia))
            linhas.Add(new AlertaDaFolha(
                $"Uso contínuo — {p.Rotulo}" + (string.IsNullOrWhiteSpace(p.Observacoes) ? string.Empty : $": {p.Observacoes}"),
                false));
        return linhas;
    }

    /// <summary>
    /// As intercorrências da enfermagem nas últimas 48 h (a janela é do domínio,
    /// <c>AlertaAgora</c>), mais recente primeiro. Retificada não alerta: quem corrigiu disse
    /// que o registro anterior estava errado.
    /// </summary>
    public static IReadOnlyList<AlertaDaFolha> DasIntercorrencias(IReadOnlyList<EvolucaoEnfermagem> recentes, DateTime agora)
    {
        var substituidas = recentes.Where(e => e.RetificaEvolucaoId is not null)
            .Select(e => e.RetificaEvolucaoId!.Value).ToHashSet();
        return recentes
            .Where(e => e.AlertaAgora(agora) && !substituidas.Contains(e.Id))
            .OrderByDescending(e => e.Momento)
            .Select(e => new AlertaDaFolha(
                $"INTERCORRÊNCIA na enfermagem em {e.Data:dd/MM} às {e.Hora:HH':'mm} — {e.Texto}"
                + (string.IsNullOrWhiteSpace(e.AutorNome) ? string.Empty : $" ({e.AutorNome})"),
                true))
            .ToList();
    }

    /// <summary>O rótulo e a dica do botão do termo pendente (assinado no celular é âmbar e "conferir").</summary>
    public static (string Rotulo, string Dica) DoTermo(SituacaoTermo termo)
        => termo.AssinaturaRemotaAguardaConferencia
            ? ("Termo assinado · conferir",
               "O paciente já assinou este termo pelo celular. Abra, confira o documento dele e conclua — não é preciso colher de novo.")
            : ("Termo não assinado · colher",
               "O procedimento de hoje exige termo assinado pelo paciente, e ele ainda não assinou. Colha agora — ele está aqui.");
}

/// <summary>
/// Um papel que já saiu para este paciente NO DIA da sessão — a lista "Já saiu hoje", que
/// responde "eu já dei a receita para ela?". Cancelado aparece MARCADO, nunca sumindo.
/// </summary>
public sealed record PapelDeHoje(int DocumentoId, string Rotulo, string Numero, string Detalhe, bool Cancelado)
{
    public string NomeArquivo => $"{Rotulo}-{Numero.Replace('/', '-')}.pdf";

    public static PapelDeHoje De(DocumentoClinico d) => new(
        d.Id,
        CentralDocumentosService.RotularClinico(d.Tipo),
        d.Numero,
        d.Cancelado
            ? $"cancelada — {d.MotivoCancelamento}"
            : $"{d.CriadoEm:HH':'mm}" + (d.Profissional is null ? string.Empty : $" · {d.Profissional.Rotulo}"),
        d.Cancelado);
}
