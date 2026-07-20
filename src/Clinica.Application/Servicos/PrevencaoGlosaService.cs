using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Radar de glosas: cruza as guias candidatas a um lote com o HISTÓRICO de glosas da
/// própria clínica e avisa ANTES do envio o que provavelmente voltará glosado.
/// Os sistemas de mercado mostram a taxa de glosa depois do prejuízo; aqui a
/// estatística vira prevenção — o momento em que ainda dá para corrigir a guia.
/// </summary>
public sealed class PrevencaoGlosaService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Mínimo de glosas de um mesmo padrão (convênio+tipo) para o alerta estatístico.</summary>
    public const int MinimoOcorrencias = 3;

    /// <summary>Taxa histórica de glosa (glosadas ÷ baixadas do padrão) que dispara o alerta.</summary>
    public const double TaxaMinimaAlerta = 0.20;

    public PrevencaoGlosaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Analisa as guias candidatas a um lote e devolve os alertas de risco, do mais
    /// específico (guia certa de ser recusada) ao estatístico. Vazio = nada a apontar.
    /// </summary>
    public async Task<IReadOnlyList<string>> AnalisarAsync(
        IReadOnlyList<CodigoFaturamento> candidatas, DateOnly referencia, CancellationToken ct = default)
    {
        var alertas = new List<string>();
        if (candidatas.Count == 0)
            return alertas;

        AlertarCarteirinhasVencidas(alertas, candidatas, referencia);
        AlertarDuplicidades(alertas, candidatas);
        await AlertarPadroesHistoricosAsync(alertas, candidatas, referencia, ct);

        return alertas;
    }

    /// <summary>Carteirinha vencida na data de envio = glosa 1201 na certa (recusa na origem).</summary>
    private static void AlertarCarteirinhasVencidas(
        List<string> alertas, IReadOnlyList<CodigoFaturamento> candidatas, DateOnly referencia)
    {
        var vencidas = candidatas
            .Select(c => c.Atendimento?.Paciente)
            .Where(p => p?.ValidadeCarteirinha is { } v && v < referencia)
            .Select(p => p!.Nome)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        foreach (var nome in vencidas)
            alertas.Add($"Carteirinha de {nome} está VENCIDA — a guia volta glosada " +
                        $"(motivo 1201). Atualize a validade na ficha do paciente antes de enviar.");
    }

    /// <summary>Mesmo paciente + mesmo procedimento + mesma data, duas vezes no lote = cheiro de 1705.</summary>
    private static void AlertarDuplicidades(List<string> alertas, IReadOnlyList<CodigoFaturamento> candidatas)
    {
        var duplicadas = candidatas
            .Where(c => c.Atendimento is not null)
            .GroupBy(c => (c.Atendimento!.PacienteId, c.Tipo, c.Atendimento!.Data))
            .Where(g => g.Count() > 1);

        foreach (var g in duplicadas)
        {
            var nome = g.First().Atendimento?.Paciente?.Nome ?? $"paciente {g.Key.PacienteId}";
            alertas.Add($"Possível duplicidade: {g.Count()} guias de {g.Key.Tipo} para {nome} " +
                        $"na mesma data ({g.Key.Data:dd/MM/yyyy}) — operadoras glosam por " +
                        "cobrança em duplicidade (motivo 1705).");
        }
    }

    /// <summary>
    /// Estatística da própria clínica: padrões (convênio + tipo de procedimento) com taxa
    /// histórica de glosa alta geram aviso com o motivo mais comum — o "por quê" acionável.
    /// </summary>
    private async Task AlertarPadroesHistoricosAsync(
        List<string> alertas, IReadOnlyList<CodigoFaturamento> candidatas, DateOnly referencia, CancellationToken ct)
    {
        var historicoBaixadas = await _repo.CodigosBaixadosNoPeriodoAsync(DateOnly.MinValue, referencia, ct);
        if (historicoBaixadas.Count == 0)
            return;

        var candidatasIds = candidatas.Select(c => c.Id).ToHashSet();

        // Denominador e numerador por padrão (o lote atual fica de fora da estatística).
        var porPadrao = historicoBaixadas
            .Where(c => !candidatasIds.Contains(c.Id) && c.Atendimento?.Paciente is not null)
            .GroupBy(c => (Convenio: c.Atendimento!.Paciente!.Convenio, c.Tipo))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var padrao in candidatas
                     .Where(c => c.Atendimento?.Paciente is not null)
                     .GroupBy(c => (Convenio: c.Atendimento!.Paciente!.Convenio, c.Tipo)))
        {
            if (!porPadrao.TryGetValue(padrao.Key, out var historico))
                continue;

            var glosadas = historico.Where(c => c.Glosa != StatusGlosa.SemGlosa).ToList();
            if (glosadas.Count < MinimoOcorrencias)
                continue;

            var taxa = (double)glosadas.Count / historico.Count;
            if (taxa < TaxaMinimaAlerta)
                continue;

            var motivoComum = glosadas
                .Where(c => !string.IsNullOrWhiteSpace(c.MotivoGlosaCodigo))
                .GroupBy(c => c.MotivoGlosaCodigo!)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            var motivoTxt = motivoComum is null
                ? string.Empty
                : $" Motivo mais comum: {motivoComum.Key} — {MotivosGlosa.Descricao(motivoComum.Key)}.";

            alertas.Add($"Guias de {padrao.Key.Tipo} do convênio {NomeConvenio(padrao.Key.Convenio)} " +
                        $"foram glosadas em {taxa:P0} das vezes ({glosadas.Count} de {historico.Count})." +
                        motivoTxt + $" Este lote tem {padrao.Count()} guia(s) nesse padrão — confira antes de enviar.");
        }
    }

    private static string NomeConvenio(Convenio convenio) => ConvenioInfo.NomeExibicao(convenio);
}
