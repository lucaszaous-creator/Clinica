using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O QUE A FOLHA GUARDA SOZINHA enquanto o médico escreve (out/2026, "como no Excel": o
/// texto não se perde). Os mesmos campos que o <see cref="RascunhoDaSessao.ParaGravacao"/>
/// manda ao prontuário — EVA, texto com negrito/itálico, campos complementares e os
/// personalizados pelo id do campo —, em JSON numa linha só (<see cref="RascunhoEvolucao"/>).
/// O mapa corporal não entra: ele continua sendo gravado pelo Salvar.
///
/// ⚠️ Campo novo do rascunho da sessão entra AQUI também, nos três métodos — senão o texto
/// recuperado (e o que o sistema grava no prazo) sai sem ele, sem erro nenhum.
/// </summary>
public sealed record ConteudoDoRascunho(
    DateOnly Data,
    int? EvaAntes,
    int? EvaDepois,
    string? QueixaPrincipal,
    string? HistoriaDoencaAtual,
    string? ExameFisico,
    string? HipoteseDiagnostica,
    string? CidSessao,
    string? Conduta,
    string? TextoEvolucao,
    string? TextoEvolucaoFormatado,
    string? Orientacoes,
    string? PlanoTerapeutico,
    DateOnly? RetornoSugeridoEm,
    string? RetornoSugeridoNota,
    string? Encaminhamento,
    Dictionary<int, string?> Campos)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ConteudoDoRascunho De(RascunhoDaSessao r) => new(
        r.Data, r.EvaAntes, r.EvaDepois, r.QueixaPrincipal, r.HistoriaDoencaAtual, r.ExameFisico,
        r.HipoteseDiagnostica, r.CidSessao, r.Conduta, r.TextoEvolucao, r.TextoEvolucaoFormatado,
        r.Orientacoes, r.PlanoTerapeutico, r.RetornoSugeridoEm, r.RetornoSugeridoNota, r.Encaminhamento,
        r.Campos.ToDictionary(c => c.Id, c => c.Resposta));

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>Nulo quando o JSON não é um rascunho legível (nunca derruba a folha).</summary>
    public static ConteudoDoRascunho? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ConteudoDoRascunho>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Devolve à folha o que estava escrito. Personalizado que saiu do catálogo fica de fora.</summary>
    public void Aplicar(RascunhoDaSessao r)
    {
        r.Data = Data;
        r.EvaAntes = EvaAntes;
        r.EvaDepois = EvaDepois;
        r.QueixaPrincipal = QueixaPrincipal;
        r.HistoriaDoencaAtual = HistoriaDoencaAtual;
        r.ExameFisico = ExameFisico;
        r.HipoteseDiagnostica = HipoteseDiagnostica;
        r.CidSessao = CidSessao;
        r.Conduta = Conduta;
        r.TextoEvolucao = TextoEvolucao;
        r.TextoEvolucaoFormatado = TextoEvolucaoFormatado;
        r.Orientacoes = Orientacoes;
        r.PlanoTerapeutico = PlanoTerapeutico;
        r.RetornoSugeridoEm = RetornoSugeridoEm;
        r.RetornoSugeridoNota = RetornoSugeridoNota;
        r.Encaminhamento = Encaminhamento;
        foreach (var c in r.Campos)
            if (Campos.TryGetValue(c.Id, out var resposta)) c.Resposta = resposta;
    }

    /// <summary>A evolução que o SISTEMA grava no prazo — o mesmo formato do Salvar da folha.</summary>
    public Evolucao ParaEvolucao(int evolucaoId, int pacienteId, int? profissionalId, int? agendamentoId,
        IReadOnlyList<CampoPersonalizadoProntuario> definicoes) => new()
    {
        Id = evolucaoId,
        PacienteId = pacienteId,
        ProfissionalId = profissionalId,
        AgendamentoId = agendamentoId,
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
        CamposPersonalizados = CampoPersonalizadoService.Montar(definicoes, Campos).ToList()
    };
}

/// <summary>
/// As REGRAS do salvamento automático, num lugar só e com teste: quando guardar (sem pesar
/// para quem digita) e o que o rodapé diz.
/// </summary>
public static class RascunhoAutomatico
{
    /// <summary>De quanto em quanto a folha confere se há o que guardar.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

    /// <summary>Digitando sem parar, guarda mesmo assim a cada tanto — o teto da perda.</summary>
    public static readonly TimeSpan TetoSemGuardar = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Guarda quando o conteúdo mudou desde a última guarda E a pessoa PAROU de digitar (o que
    /// está na tela é igual ao da conferência anterior) — ou quando já passou o teto. Assim
    /// quem digita não paga uma gravação por tecla, e quem para tem tudo guardado em segundos.
    /// </summary>
    public static bool DeveGuardar(string atual, string? guardado, string? daConferenciaAnterior,
        DateTime agora, DateTime? ultimaGuarda)
    {
        if (atual == guardado) return false;
        var parou = atual == daConferenciaAnterior;
        var passouOTeto = ultimaGuarda is null || agora - ultimaGuarda.Value >= TetoSemGuardar;
        return parou || passouOTeto;
    }

    /// <summary>A linha do rodapé. Diz só o que é verdade: guardado como rascunho, ainda não no prontuário.</summary>
    /// <remarks>Sem a conclusão automática ligada (<paramref name="horasDoPrazo"/> nulo), não promete prazo.</remarks>
    public static string Rodape(DateTime guardadoEm, int? horasDoPrazo)
        => horasDoPrazo is { } h
            ? $"Rascunho guardado às {guardadoEm:HH':'mm}. Se não salvar, é salvo e encerrado {h} h após a última alteração."
            : $"Rascunho guardado às {guardadoEm:HH':'mm}. Salve a sessão para gravar no prontuário.";

    /// <summary>O aviso de quando a folha reabre com o que não foi salvo.</summary>
    public static string Recuperado(DateTime guardadoEm)
        => $"Recuperamos o que estava escrito às {guardadoEm:dd/MM HH':'mm} e ainda não foi salvo.";
}
