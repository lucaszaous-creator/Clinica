using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

// ============================================================================ o que a tela desenha

/// <summary>
/// A leitura da tela (26/10): o painel por secretária e a fila do período, com os nomes da equipe do CRM
/// (id → nome) para a conversa. A fila já vem ORDENADA (<see cref="AuditoriaNaTela.OrdenarFila"/>).
/// </summary>
public sealed record PainelDaAuditoria(AppQualityPanel Painel, IReadOnlyList<AppQualityItem> Fila,
    IReadOnlyDictionary<string, string> Nomes, int Dias)
{
    /// <summary>"de 19/10 a 26/10" — o intervalo que o CRM mediu (no fuso de quem lê).</summary>
    public string Periodo => AuditoriaNaTela.Periodo(Painel.From, Painel.To);
}

/// <summary>Um cartão "por secretária", com os números já escritos para a tela.</summary>
public sealed record CartaoDaSecretaria(AppQualityAgent Secretaria, string PrimeiraResposta, string NoPrazo, string TomDoPrazo,
    string Nota, string TomDaNota, string Avaliacoes);

/// <summary>
/// Uma linha da conversa na auditoria. <see cref="Tipo"/>: "recebida" (do paciente), "enviada" (da equipe),
/// "automatica" (lembrete, menu — sem pessoa) ou "nota" (nota interna: o paciente NUNCA viu, e não serve de evidência).
/// <see cref="DaAvaliada"/>: enviada pela secretária que está sendo avaliada.
/// </summary>
public sealed record MensagemNaAuditoria(string Id, string Tipo, string Autor, string Texto, string Hora, string Dia,
    bool DaAvaliada, string? Situacao)
{
    public bool Nota => Tipo == "nota";
}

/// <summary>Uma versão guardada da avaliação (a mais nova primeiro): quem, quando, rascunho ou final, a nota.</summary>
public sealed record VersaoDaAvaliacao(int Versao, string Quem, string Quando, string Estado, string Nota, string Tom, bool DoSemDor);

/// <summary>A conversa aberta para auditar, composta: o que a coluna da esquerda e o rodapé desenham.</summary>
public sealed record DetalheDaAuditoria(AppQualityDetail Detalhe, AppQualityItem Item, string Paciente,
    IReadOnlyList<MensagemNaAuditoria> Linhas, IReadOnlyList<VersaoDaAvaliacao> Historico, IReadOnlyList<string> OutrasPessoas)
{
    private readonly Dictionary<string, MensagemNaAuditoria> _porId =
        Linhas.Where(l => !l.Nota).GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.First());

    /// <summary>A mensagem pelo id (evidência e achados citam ids); nulo se não está na conversa.</summary>
    public MensagemNaAuditoria? Mensagem(string id) => _porId.GetValueOrDefault(id);
}

/// <summary>A nota de um critério no formulário (mutável: a tela liga os campos aqui).</summary>
public sealed class RespostaDoCriterio
{
    public required AppQualityCriterion Criterio { get; init; }
    /// <summary>-1 não avaliável, 0 não atendeu, 1 em parte, 2 atendeu; nulo = ainda sem nota.</summary>
    public int? Nota { get; set; }
    public string Justificativa { get; set; } = "";
    /// <summary>Os ids das mensagens que provam a nota (na ordem em que foram citadas).</summary>
    public List<string> Evidencias { get; init; } = [];
    /// <summary>O que o @SemDor sugeriu para este critério no último rascunho dele (nulo sem rascunho).</summary>
    public AppQualityAnswer? SugestaoDoSemDor { get; init; }

    /// <summary>A nota e a justificativa ainda são as que o @SemDor sugeriu (a tela marca "Sugestão do @SemDor").</summary>
    public bool ComoOSemDorSugeriu => SugestaoDoSemDor is { } s && s.Score == Nota
        && string.Equals((s.Reason ?? "").Trim(), Justificativa.Trim(), StringComparison.Ordinal)
        && (s.MessageIds ?? []).SequenceEqual(Evidencias);
}

/// <summary>
/// O formulário da gerente sobre UMA secretária numa conversa. <see cref="Versao"/> e <see cref="Impressao"/> são o
/// que o CRM confere ao gravar (a última versão desta secretária nesta conversa e o Fingerprint do detalhe).
/// </summary>
public sealed class FormularioDaAuditoria
{
    public required string ConversaId { get; init; }
    public required string SecretariaId { get; init; }
    public int Versao { get; set; }
    public string Impressao { get; set; } = "";
    public required IReadOnlyList<RespostaDoCriterio> Criterios { get; init; }
    /// <summary>"resolved" | "followup" | "inconclusive"; vazio = ainda não escolhido.</summary>
    public string Resultado { get; set; } = "";
    public string Devolutiva { get; set; } = "";
    public string PlanoDeAcao { get; set; } = "";
    public DateOnly? Prazo { get; set; }
    /// <summary>O plano de ação já foi cumprido (só vale com plano).</summary>
    public bool AcaoCumprida { get; set; }
    /// <summary>O formulário nasceu do rascunho do @SemDor (a última versão era dele).</summary>
    public bool PartiuDoSemDor { get; init; }
    /// <summary>A última versão é uma avaliação FINAL da gerente: gravar de novo é corrigir.</summary>
    public bool JaConcluida { get; init; }
}

// ============================================================================ o serviço

/// <summary>
/// A AUDITORIA DO ATENDIMENTO (26/10, pedido da direção): a gerente confere o atendimento das secretárias no
/// WhatsApp. O @SemDor pré-avalia uma amostra semanal por secretária, citando as mensagens; a gerente lê, confirma
/// ou corrige — <b>a nota que vale é sempre a dela</b>. Tudo mora no CRM (<c>/api/quality</c>); este serviço só
/// lê e grava por <see cref="IClienteDoCrm"/>, com a sessão de QUEM ENTROU (<see cref="SessaoUsuario.Atual"/>), como o
/// <see cref="WhatsAppDaRecepcaoService"/>. As falhas são as do cliente: <see cref="CrmNaoConfigurado"/>,
/// <see cref="CrmIndisponivel"/> e <see cref="RecusaDoCrm"/> (403 = não é da gestão no CRM; 409 = a conversa mudou).
/// O que a tela AFIRMA vem de <see cref="AuditoriaNaTela"/>, puro e com teste.
/// </summary>
public sealed class AuditoriaDoAtendimentoService(IClienteDoCrm crm)
{
    private static int UsuarioId => SessaoUsuario.Atual.UsuarioId;

    /// <summary>O relógio da clínica (o Windows da direção está no fuso dela).</summary>
    public static DateTimeOffset Agora => DateTimeOffset.Now;

    /// <summary>A Gerência informou o endereço do CRM?</summary>
    public Task<bool> ConfiguradoAsync(CancellationToken ct = default) => crm.ConfiguradoAsync(ct);

    /// <summary>
    /// O painel do período e a fila, em fila (a mesma sessão do CRM). A sessão vem antes: dá os nomes de toda a
    /// equipe, para a conversa nomear também quem não é secretária (a gestão, quem transferiu).
    /// </summary>
    public async Task<PainelDaAuditoria> LerAsync(int dias, CancellationToken ct = default)
    {
        var u = UsuarioId;
        var d = AuditoriaNaTela.DiasValidos(dias);
        var sessao = await crm.SessaoAsync(u, ct);
        var painel = await crm.PainelDaAuditoriaAsync(u, d, ct);
        var fila = await crm.FilaDaAuditoriaAsync(u, ct);
        return new PainelDaAuditoria(painel, AuditoriaNaTela.OrdenarFila(fila), AuditoriaNaTela.Nomes(sessao.Agents, painel, fila), d);
    }

    /// <summary>A conversa de um item da fila, composta para a tela.</summary>
    public async Task<DetalheDaAuditoria> AbrirAsync(AppQualityItem item, IReadOnlyDictionary<string, string> nomes, CancellationToken ct = default)
        => AuditoriaNaTela.Detalhe(await crm.DetalheDaAuditoriaAsync(UsuarioId, item.ConversationId, ct), item, nomes, Agora);

    /// <summary>
    /// Grava a avaliação: <paramref name="final"/> = "Confirmar avaliação" (a nota que vale); falso = "Guardar
    /// rascunho". O que falta no formulário volta como <see cref="InvalidOperationException"/> com a frase (a tela
    /// mostra inline), antes de ir ao CRM; a recusa do CRM chega como <see cref="RecusaDoCrm"/>.
    /// </summary>
    public Task<AppQualityReview> SalvarAsync(FormularioDaAuditoria formulario, bool final, CancellationToken ct = default)
    {
        if (AuditoriaNaTela.Recusa(formulario, final, DateOnly.FromDateTime(Agora.DateTime)) is { } recusa)
            return Task.FromException<AppQualityReview>(new InvalidOperationException(recusa));
        return crm.SalvarAuditoriaAsync(UsuarioId, AuditoriaNaTela.Montar(formulario, final), ct);
    }
}

// ============================================================================ as regras da tela, puras

/// <summary>O que a tela da auditoria afirma: rótulos, tons, frases, a ordem da fila e o formulário. Puro.</summary>
public static class AuditoriaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Os períodos que o CRM aceita (dias); o primeiro é o padrão.</summary>
    public static readonly IReadOnlyList<int> Periodos = [7, 30];

    /// <summary>O revisor do rascunho automático no CRM.</summary>
    public const string RevisorSemDor = "semdor";

    public const string ParaConferir = "para-conferir";
    public const string Avaliada = "avaliada";

    /// <summary>Em até quantos minutos úteis a primeira resposta conta como no prazo (a mesma régua do CRM).</summary>
    public const int PrazoDaRespostaEmMinutos = 15;

    /// <summary>A partir de quanto a nota final é boa (verde) e regular (amarela); abaixo, vermelha.</summary>
    public const decimal NotaBoa = 80m, NotaRegular = 60m;

    /// <summary>O mínimo de letras de uma justificativa para concluir (a mesma régua do CRM).</summary>
    public const int MinimoDaJustificativa = 10;

    /// <summary>Os tetos de texto do CRM (o campo da tela não deixa passar).</summary>
    public const int TetoDaJustificativa = 2000, TetoDaDevolutiva = 4000, TetoDoPlano = 2000, TetoDeEvidencias = 30;

    public const string FaixaDaNota = "A nota que vale é a sua. O @SemDor só sugere, citando as mensagens.";
    public const string FaixaDaTransparencia = "As secretárias devem saber que o atendimento é auditado.";

    /// <summary>7 ou 30 (o que o CRM aceita); outro valor vira 7.</summary>
    public static int DiasValidos(int dias) => dias == 30 ? 30 : 7;

    public static string Periodo(DateTimeOffset de, DateTimeOffset ate)
        => $"de {de.ToLocalTime().ToString("dd/MM", PtBr)} a {ate.ToLocalTime().ToString("dd/MM", PtBr)}";

    // ---------------------------------------------------------------- os nomes

    /// <summary>Id → nome: a equipe da sessão, as secretárias do painel e as da fila (a primeira com nome vence).</summary>
    public static IReadOnlyDictionary<string, string> Nomes(IEnumerable<AppAgent>? equipe, AppQualityPanel? painel, IEnumerable<AppQualityItem>? fila)
    {
        var nomes = new Dictionary<string, string>(StringComparer.Ordinal);
        void Por(string? id, string? nome)
        {
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(nome) && !nomes.ContainsKey(id)) nomes[id] = nome.Trim();
        }
        foreach (var a in equipe ?? []) Por(a.Id, a.Name);
        foreach (var a in painel?.Agents ?? []) Por(a.AgentId, a.Name);
        foreach (var i in fila ?? []) Por(i.SubjectId, i.SubjectName);
        return nomes;
    }

    // ---------------------------------------------------------------- as notas

    /// <summary>As quatro opções de cada critério, na ordem da tela.</summary>
    public static readonly IReadOnlyList<(int Valor, string Rotulo, string Tom)> Opcoes =
    [
        (-1, "Não avaliável", "neutro"),
        (0, "Não atendeu", "perigo"),
        (1, "Em parte", "aviso"),
        (2, "Atendeu", "sucesso")
    ];

    public static string RotuloDaNota(int? nota) => nota switch
    {
        -1 => "Não avaliável",
        0 => "Não atendeu",
        1 => "Em parte",
        2 => "Atendeu",
        _ => "Sem nota"
    };

    public static string TomDaNota(int? nota) => nota switch
    {
        2 => "sucesso",
        1 => "aviso",
        0 => "perigo",
        _ => "neutro"
    };

    /// <summary>A nota 0–100: "82" — ou "—" sem nota.</summary>
    public static string NotaFinal(decimal? nota) => nota is { } n ? n.ToString("0.#", PtBr) : "—";

    /// <summary>A cor da nota 0–100: boa (≥ 80), regular (≥ 60), baixa; sem nota, neutra.</summary>
    public static string TomDaNotaFinal(decimal? nota) => nota switch
    {
        null => "neutro",
        >= NotaBoa => "sucesso",
        >= NotaRegular => "aviso",
        _ => "perigo"
    };

    /// <summary>
    /// A nota que o CRM vai calcular ao concluir: a média das notas AVALIÁVEIS (0, 1, 2) × 50, arredondada. É só a
    /// prévia na tela — a que fica é a que o CRM devolve. Nulo sem nenhuma avaliável.
    /// </summary>
    public static decimal? NotaPrevista(IEnumerable<int?> notas)
    {
        var validas = notas.Where(n => n is >= 0 and <= 2).Select(n => n!.Value).ToArray();
        return validas.Length == 0 ? null : Math.Round((decimal)validas.Average() * 50m, 0, MidpointRounding.ToEven);
    }

    // ---------------------------------------------------------------- os números da secretária

    /// <summary>"8 min", "1 h 20 min", "menos de 1 min" (minutos úteis) — ou "—" sem resposta medida.</summary>
    public static string PrimeiraResposta(double? minutos)
    {
        if (minutos is not { } m || double.IsNaN(m) || m < 0) return "—";
        if (m < 1) return "menos de 1 min";
        var total = (int)Math.Round(m, MidpointRounding.AwayFromZero);
        if (total < 60) return $"{total} min";
        var h = total / 60; var resto = total % 60;
        return resto == 0 ? $"{h} h" : $"{h} h {resto} min";
    }

    /// <summary>"12 de 15 no prazo" — ou "nenhuma resposta medida".</summary>
    public static string NoPrazo(int noPrazo, int medidas)
        => medidas <= 0 ? "nenhuma resposta medida" : $"{Math.Min(noPrazo, medidas)} de {medidas} no prazo";

    /// <summary>A cor do prazo: 80% ou mais no prazo é bom; de 50% a 80%, atenção; abaixo, ruim. Sem medida, neutro.</summary>
    public static string TomDoPrazo(int noPrazo, int medidas)
    {
        if (medidas <= 0) return "neutro";
        var f = (double)noPrazo / medidas;
        return f >= 0.8 ? "sucesso" : f >= 0.5 ? "aviso" : "perigo";
    }

    /// <summary>"3 avaliações", "1 avaliação", "nenhuma avaliação".</summary>
    public static string Avaliacoes(int quantas) => quantas switch
    {
        <= 0 => "nenhuma avaliação",
        1 => "1 avaliação",
        _ => $"{quantas} avaliações"
    };

    public static CartaoDaSecretaria Cartao(AppQualityAgent a)
        => new(a, PrimeiraResposta(a.FirstReplyMedianMinutes), NoPrazo(a.RepliedOnTime, a.RepliesMeasured),
            TomDoPrazo(a.RepliedOnTime, a.RepliesMeasured), NotaFinal(a.AverageScore), TomDaNotaFinal(a.AverageScore), Avaliacoes(a.Reviewed));

    // ---------------------------------------------------------------- a fila

    /// <summary>As que esperam a gerente ("para-conferir") primeiro; dentro de cada grupo, a mais recente antes.</summary>
    public static IReadOnlyList<AppQualityItem> OrdenarFila(IEnumerable<AppQualityItem> fila)
        => fila.OrderBy(i => i.State == ParaConferir ? 0 : 1).ThenByDescending(i => i.At).ToList();

    /// <summary>"hoje às 17:02", "ontem às 11:05", "qua 22/10 às 11:05" — a mesma frase do WhatsApp da clínica.</summary>
    public static string Quando(DateTimeOffset momento, DateTimeOffset agora) => ConversasNaTela.Quando(momento, agora);

    /// <summary>A fila de uma secretária (nulo ou vazio = todas), na ordem em que veio.</summary>
    public static IReadOnlyList<AppQualityItem> FiltrarFila(IEnumerable<AppQualityItem> fila, string? secretariaId)
        => string.IsNullOrWhiteSpace(secretariaId) ? fila.ToList() : fila.Where(i => i.SubjectId == secretariaId).ToList();

    public static string RotuloDoEstado(string? estado) => estado switch
    {
        ParaConferir => "Para conferir",
        Avaliada => "Avaliada",
        _ => string.IsNullOrWhiteSpace(estado) ? "—" : estado
    };

    public static string TomDoEstado(string? estado) => estado switch
    {
        ParaConferir => "aviso",
        Avaliada => "sucesso",
        _ => "neutro"
    };

    /// <summary>A cor do motivo da escolha: reclamação em vermelho, sem resposta e espera em amarelo, o sorteio neutro.</summary>
    public static string TomDoMotivo(string? motivo)
    {
        var m = motivo ?? "";
        if (m.Contains("Reclama", StringComparison.OrdinalIgnoreCase)) return "perigo";
        if (m.Contains("sem resposta", StringComparison.OrdinalIgnoreCase) || m.Contains("Espera", StringComparison.OrdinalIgnoreCase)) return "aviso";
        return "neutro";
    }

    /// <summary>Achado "attention" é aviso; o resto, informação.</summary>
    public static string TomDoAchado(string? nivel) => nivel == "attention" ? "aviso" : "info";

    // ---------------------------------------------------------------- a conversa

    /// <summary>
    /// A conversa em linhas, na ordem do tempo: as mensagens e as notas internas (marcadas — o paciente nunca as viu).
    /// A trilha (assumiu, transferiu) não entra: a auditoria lê o que foi DITO.
    /// </summary>
    public static DetalheDaAuditoria Detalhe(AppQualityDetail d, AppQualityItem item, IReadOnlyDictionary<string, string> nomes, DateTimeOffset agora)
    {
        var c = d.Conversation;
        var paciente = ConversasNaTela.NomeDaConversa(c);
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        var linhas = new List<(DateTimeOffset At, MensagemNaAuditoria L)>();
        foreach (var m in c.Messages ?? [])
        {
            var local = m.At.ToOffset(agora.Offset);
            var recebida = m.Direction == "in";
            var automatica = !recebida && (string.IsNullOrWhiteSpace(m.AgentId) || m.AgentId == "automation");
            var tipo = recebida ? "recebida" : automatica ? "automatica" : "enviada";
            var autor = recebida ? paciente : automatica ? "Mensagem automática" : ConversasNaTela.NomeDaPessoa(m.AgentId, nomes);
            linhas.Add((m.At, new MensagemNaAuditoria(m.Id, tipo, autor, TextoDa(m), local.ToString("HH:mm", PtBr),
                ConversasNaTela.Dia(DateOnly.FromDateTime(local.DateTime), hoje), !recebida && m.AgentId == item.SubjectId, SituacaoDa(m))));
        }
        foreach (var n in c.Notes ?? [])
        {
            var local = n.At.ToOffset(agora.Offset);
            linhas.Add((n.At, new MensagemNaAuditoria(n.Id, "nota", ConversasNaTela.NomeDaPessoa(n.AgentId, nomes), n.Text ?? "",
                local.ToString("HH:mm", PtBr), ConversasNaTela.Dia(DateOnly.FromDateTime(local.DateTime), hoje), false, null)));
        }
        var historico = (d.History ?? [])
            .Where(r => r.SubjectId == item.SubjectId)
            .OrderByDescending(r => r.Version)
            .Select(r => new VersaoDaAvaliacao(r.Version,
                r.ReviewerId == RevisorSemDor ? "@SemDor" : ConversasNaTela.NomeDaPessoa(r.ReviewerId, nomes),
                ConversasNaTela.Quando(r.ReviewedAt, agora),
                r.State == "final" ? "Concluída" : "Rascunho",
                NotaFinal(r.State == "final" ? r.Score : NotaPrevista((r.Answers ?? []).Select(a => a.Score))),
                r.State == "final" ? TomDaNotaFinal(r.Score) : "neutro",
                r.ReviewerId == RevisorSemDor))
            .ToList();
        var outras = (d.Subjects ?? []).Where(s => s != item.SubjectId).Select(s => ConversasNaTela.NomeDaPessoa(s, nomes)).Distinct().ToList();
        return new DetalheDaAuditoria(d, item, paciente, linhas.OrderBy(l => l.At).Select(l => l.L).ToList(), historico, outras);
    }

    /// <summary>O texto do balão: a legenda da mídia vem depois do tipo entre colchetes ("[Foto] olha o exame").</summary>
    public static string TextoDa(AppMessage m)
    {
        static string Com(string rotulo, string? texto) => string.IsNullOrWhiteSpace(texto) ? rotulo : $"{rotulo} {texto.Trim()}";
        return (m.Type ?? "").ToUpperInvariant() switch
        {
            "IMAGE" => m.Sticker ? "[Figurinha]" : Com("[Foto]", m.Text),
            "AUDIO" => Com("[Áudio]", m.Text),
            "VIDEO" => Com("[Vídeo]", m.Text),
            "DOCUMENT" => Com(string.IsNullOrWhiteSpace(m.FileName) ? "[Documento]" : $"[Documento: {m.FileName.Trim()}]", m.Text),
            "TEMPLATE" => Com(m.Template is { Name: { Length: > 0 } nome } ? $"[Modelo {nome}]" : "[Modelo]", m.Text),
            _ => m.Text ?? ""
        };
    }

    /// <summary>O envio que não se confirmou (a auditoria não conta como resposta): "Não enviada: motivo", "Envio não confirmado".</summary>
    public static string? SituacaoDa(AppMessage m) => m.Direction == "in" ? null : m.Status switch
    {
        "failed" => string.IsNullOrWhiteSpace(m.Error) ? "Não enviada" : $"Não enviada: {m.Error.Trim()}",
        "uncertain" or "sending" => "Envio não confirmado",
        _ => null
    };

    /// <summary>O rótulo da evidência: "14:02 · Ana: Bom dia, posso…".</summary>
    public static string Evidencia(MensagemNaAuditoria m) => $"{m.Hora} · {m.Autor}: {Trecho(m.Texto, 48)}";

    /// <summary>O começo do texto numa linha, com reticências quando corta.</summary>
    public static string Trecho(string? texto, int maximo)
    {
        var t = string.Join(' ', (texto ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length <= maximo ? t : t[..Math.Max(1, maximo - 1)].TrimEnd() + "…";
    }

    // ---------------------------------------------------------------- o formulário

    /// <summary>Os resultados da conversa, na ordem da tela (chave do CRM, rótulo).</summary>
    public static readonly IReadOnlyList<(string Chave, string Rotulo)> Resultados =
    [
        ("resolved", "Resolvido"),
        ("followup", "Precisa de retorno"),
        ("inconclusive", "Inconclusivo")
    ];

    public static string RotuloDoResultado(string? chave)
        => Resultados.FirstOrDefault(r => r.Chave == chave).Rotulo ?? "—";

    /// <summary>A última versão desta secretária nesta conversa (a de maior número), ou nula.</summary>
    public static AppQualityReview? Ultima(AppQualityDetail d, string secretariaId)
        => (d.History ?? []).Where(r => r.SubjectId == secretariaId).MaxBy(r => r.Version);

    /// <summary>
    /// O formulário da gerente: parte da ÚLTIMA versão desta secretária. Se ela é o rascunho do @SemDor, as notas, as
    /// justificativas e as evidências vêm dele (e cada critério guarda a sugestão, para a tela marcar "Sugestão do
    /// @SemDor"); o resultado NÃO vem — o do rascunho é só o valor padrão do CRM, e escolher é da gerente. Se a última é
    /// da gerente (rascunho ou concluída), tudo vem dela. Sem versão, em branco. A <see cref="FormularioDaAuditoria.Versao"/>
    /// é a da última versão (0 sem nenhuma), como o CRM confere.
    /// </summary>
    public static FormularioDaAuditoria Formulario(AppQualityDetail d, string secretariaId)
    {
        var ultima = Ultima(d, secretariaId);
        var doSemDor = ultima?.ReviewerId == RevisorSemDor;
        var rascunhoDoSemDor = (d.History ?? []).Where(r => r.SubjectId == secretariaId && r.ReviewerId == RevisorSemDor).MaxBy(r => r.Version);
        var respostas = (ultima?.Answers ?? []).GroupBy(a => a.Criterion).ToDictionary(g => g.Key, g => g.First());
        var sugestoes = (rascunhoDoSemDor?.Answers ?? []).GroupBy(a => a.Criterion).ToDictionary(g => g.Key, g => g.First());
        var criterios = (d.Criteria ?? []).Select(k =>
        {
            var r = respostas.GetValueOrDefault(k.Id);
            return new RespostaDoCriterio
            {
                Criterio = k,
                Nota = r?.Score is >= -1 and <= 2 ? r.Score : null,
                Justificativa = r?.Reason ?? "",
                Evidencias = (r?.MessageIds ?? []).Distinct().ToList(),
                SugestaoDoSemDor = sugestoes.GetValueOrDefault(k.Id)
            };
        }).ToList();
        return new FormularioDaAuditoria
        {
            ConversaId = d.Conversation.Id,
            SecretariaId = secretariaId,
            Versao = ultima?.Version ?? 0,
            Impressao = d.Fingerprint,
            Criterios = criterios,
            Resultado = !doSemDor && ultima is not null && Resultados.Any(x => x.Chave == ultima.Outcome) ? ultima.Outcome : "",
            Devolutiva = doSemDor ? "" : ultima?.Feedback ?? "",
            PlanoDeAcao = doSemDor ? "" : ultima?.Action ?? "",
            Prazo = !doSemDor && ultima?.DueAt is { } prazo ? DateOnly.FromDateTime(prazo.LocalDateTime) : null,
            AcaoCumprida = !doSemDor && ultima?.ActionDone == true,
            PartiuDoSemDor = doSemDor,
            JaConcluida = !doSemDor && ultima?.State == "final"
        };
    }

    /// <summary>
    /// Depois do 409 (a conversa mudou, ou outra versão foi gravada): o formulário fica como a gerente deixou, só
    /// troca o que o CRM confere — a impressão nova e a última versão. Ela relê a conversa e confirma de novo.
    /// </summary>
    public static void Rebasear(FormularioDaAuditoria f, AppQualityDetail novo)
    {
        f.Impressao = novo.Fingerprint;
        f.Versao = Ultima(novo, f.SecretariaId)?.Version ?? 0;
    }

    /// <summary>Cita ou tira a mensagem como evidência do critério (nota interna não serve: o paciente não a viu).</summary>
    public static bool AlternarEvidencia(RespostaDoCriterio r, MensagemNaAuditoria m)
    {
        if (m.Nota) return false;
        if (!r.Evidencias.Remove(m.Id))
        {
            if (r.Evidencias.Count >= TetoDeEvidencias) return false;
            r.Evidencias.Add(m.Id);
        }
        return true;
    }

    /// <summary>
    /// O que falta para gravar — a frase para a tela, ou nulo. Concluir pede o que o CRM pede (as cinco notas,
    /// justificativa de <see cref="MinimoDaJustificativa"/> letras, ao menos uma mensagem nos critérios avaliáveis, o
    /// resultado, o próximo passo quando precisa de retorno); o plano de ação pede prazo, e o prazo não pode ter passado,
    /// também no rascunho.
    /// </summary>
    public static string? Recusa(FormularioDaAuditoria f, bool final, DateOnly hoje)
    {
        var plano = f.PlanoDeAcao.Trim();
        if (final)
        {
            foreach (var r in f.Criterios)
            {
                var titulo = r.Criterio.Title;
                if (r.Nota is null) return $"Dê a nota de “{titulo}” (ou marque Não avaliável).";
                if (r.Justificativa.Trim().Length < MinimoDaJustificativa) return $"Justifique a nota de “{titulo}” (pelo menos {MinimoDaJustificativa} letras).";
                if (r.Nota >= 0 && r.Evidencias.Count == 0) return $"Cite ao menos uma mensagem como evidência de “{titulo}”: escolha o critério e clique na mensagem da conversa.";
            }
            if (string.IsNullOrWhiteSpace(f.Resultado)) return "Escolha o resultado da conversa: resolvido, precisa de retorno ou inconclusivo.";
            if (f.Resultado == "followup" && plano.Length == 0) return "A conversa precisa de retorno: escreva o próximo passo no plano de ação, com o prazo.";
        }
        if (plano.Length > 0 && f.Prazo is null) return "Dê o prazo do plano de ação.";
        if (plano.Length == 0 && f.Prazo is not null) return "Escreva o plano de ação ou tire o prazo.";
        if (f.Prazo is { } p && p < hoje) return "O prazo do plano de ação já passou. Escolha hoje ou uma data à frente.";
        if (f.Devolutiva.Length > TetoDaDevolutiva) return $"A devolutiva passou de {TetoDaDevolutiva} letras.";
        if (f.PlanoDeAcao.Length > TetoDoPlano) return $"O plano de ação passou de {TetoDoPlano} letras.";
        if (f.Criterios.FirstOrDefault(r => r.Justificativa.Length > TetoDaJustificativa) is { } longa)
            return $"A justificativa de “{longa.Criterio.Title}” passou de {TetoDaJustificativa} letras.";
        return null;
    }

    /// <summary>
    /// O corpo do POST: os cinco critérios SEMPRE (o CRM recusa menos), o resultado — no rascunho sem escolha vai
    /// "inconclusive", que é o valor padrão do CRM — e o prazo ao MEIO-DIA do dia escolhido, no fuso de quem grava:
    /// meio-dia não vira o dia anterior nem o seguinte em fuso nenhum do Brasil.
    /// </summary>
    public static AppQualitySave Montar(FormularioDaAuditoria f, bool final)
    {
        var respostas = f.Criterios.Select(r => new AppQualityAnswer(r.Criterio.Id, r.Nota, r.Justificativa.Trim(), r.Evidencias.Distinct().ToArray())).ToArray();
        var plano = f.PlanoDeAcao.Trim();
        DateTimeOffset? prazo = plano.Length > 0 && f.Prazo is { } p ? MeioDia(p) : null;
        var resultado = Resultados.Any(x => x.Chave == f.Resultado) ? f.Resultado : "inconclusive";
        return new AppQualitySave(f.ConversaId, f.SecretariaId, f.Versao, f.Impressao, final ? "final" : "draft",
            respostas, resultado, f.Devolutiva.Trim(), plano, prazo, plano.Length > 0 && f.AcaoCumprida);
    }

    private static DateTimeOffset MeioDia(DateOnly dia)
    {
        var local = dia.ToDateTime(new TimeOnly(12, 0));
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    /// <summary>A confirmação passageira depois de gravar.</summary>
    public static string Gravou(AppQualityReview r) => r.State == "final"
        ? $"Avaliação confirmada. Nota {NotaFinal(r.Score)}."
        : "Rascunho guardado. A nota só vale quando você confirmar.";

    // ---------------------------------------------------------------- as falhas

    /// <summary>403 do CRM: quem entrou não é da gestão lá (admin ou supervisor).</summary>
    public static bool SemPermissao(Exception e) => e is RecusaDoCrm { Status: 403 };

    public const string TituloSemPermissao = "Seu acesso no CRM não inclui a auditoria";

    /// <summary>A frase do 403: a do CRM, e quem libera.</summary>
    public static string DescricaoSemPermissao(Exception e)
        => $"{(string.IsNullOrWhiteSpace(e.Message) ? "O CRM recusou o acesso." : e.Message.Trim())} A auditoria é da gestão: perfil administrador ou supervisor no CRM.";

    public const string TituloDaFalha = "Não foi possível abrir a auditoria do atendimento";

    /// <summary>A tela não abriu: sem ligação com o CRM, ou falha do próprio app.</summary>
    public static string FalhaDeAbertura(Exception e)
        => e is CrmIndisponivel
            ? $"{ConversasNaTela.SemLigacao(e)} Os números não estão zerados — eles não puderam ser lidos. Tente de novo em instantes."
            : e is RecusaDoCrm r ? r.Message : "A auditoria não pôde ser lida agora. Tente de novo em instantes; se continuar, avise o suporte.";

    /// <summary>A conversa não abriu.</summary>
    public static string FalhaDaConversa(Exception e) => e is RecusaDoCrm r ? r.Message : ConversasNaTela.FalhaDaConversa(e);

    /// <summary>O 409 ao gravar: a frase do CRM e o que a tela fez (releu, e o que a gerente escreveu continua ali).</summary>
    public static string Conflito(RecusaDoCrm r)
        => $"{r.Message.Trim()} Relemos a conversa; o que você escreveu continua aqui — confira as mensagens novas e grave de novo.";
}
