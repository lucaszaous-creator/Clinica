using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O atendimento pelo CRM, como a Mesa da direção o mostra: os últimos sete dias FECHADOS
/// (ontem para trás) e o dia de hoje em andamento.
/// </summary>
public sealed record CrmNaMesa(
    ResumoCrmDiario? Hoje,
    int DiasNaSemana,
    int Conversas,
    int Encerradas,
    int? RespostaTipicaSegundos,
    int RespostasMedidas,
    int SmsEnviados,
    int SmsSegmentos,
    int SmsFalhas,
    int ConfirmacoesPedidas,
    int ConfirmacoesRespondidas,
    DateTime? UltimoCalculo,
    string? Atraso)
{
    /// <summary>Nenhum resumo chegou: o envio ainda não foi ligado no CRM, ou a ponte não está no ar.</summary>
    public bool SemDados => Hoje is null && DiasNaSemana == 0;

    /// <summary>Fração das confirmações respondidas. Nula sem confirmação pedida — 0% diria que ninguém respondeu.</summary>
    public double? FracaoConfirmada => ConfirmacoesPedidas > 0 ? (double)ConfirmacoesRespondidas / ConfirmacoesPedidas : null;
}

/// <summary>Lê os resumos que o CRM manda pela ponte (caminho A). Só leitura.</summary>
public sealed class CrmNaDirecaoService(IClinicaRepositorio repo)
{
    public async Task<CrmNaMesa> MesaAsync(DateOnly hoje, DateTime agora, CancellationToken ct = default)
        => CrmNaTela.Montar(await repo.ResumosCrmAsync(hoje.AddDays(-CrmNaTela.DiasDaSemana), hoje, ct), hoje, agora);
}

/// <summary>A conta e as frases do bloco do CRM na Mesa, puras.</summary>
public static class CrmNaTela
{
    public const int DiasDaSemana = 7;

    /// <summary>O CRM recalcula a cada 30 min; passado disto, a mesa diz que os números podem estar velhos.</summary>
    public static readonly TimeSpan AtrasoTolerado = TimeSpan.FromHours(3);

    private static readonly CultureInfo PtBr = new("pt-BR");

    public static CrmNaMesa Montar(IReadOnlyList<ResumoCrmDiario> resumos, DateOnly hoje, DateTime agora)
    {
        var semana = resumos.Where(r => r.Dia < hoje && r.Dia >= hoje.AddDays(-DiasDaSemana)).ToList();
        var deHoje = resumos.FirstOrDefault(r => r.Dia == hoje);

        // A "mediana da semana" não é a média das medianas; o que dá para dizer honestamente é o
        // tempo TÍPICO: a média das medianas de cada dia, pesada pelo número de respostas.
        var base_ = semana.Where(r => r.RespostaMedianaSegundos is not null && r.RespostasMedidas > 0).ToList();
        var medidas = base_.Sum(r => r.RespostasMedidas);
        int? tipica = medidas > 0
            ? (int)Math.Round(base_.Sum(r => (double)r.RespostaMedianaSegundos!.Value * r.RespostasMedidas) / medidas)
            : null;

        var ultimo = resumos.Count == 0 ? (DateTime?)null : resumos.Max(r => r.GeradoEm);
        string? atraso = ultimo is { } u && agora - u > AtrasoTolerado
            ? $"O último resumo do CRM foi calculado em {u.ToString("dd/MM 'às' HH:mm", PtBr)}. Os números podem estar atrasados."
            : null;

        return new CrmNaMesa(deHoje, semana.Count,
            semana.Sum(r => r.ConversasIniciadas), semana.Sum(r => r.ConversasEncerradas),
            tipica, medidas,
            semana.Sum(r => r.SmsEnviados), semana.Sum(r => r.SmsSegmentos), semana.Sum(r => r.SmsFalhas),
            semana.Sum(r => r.ConfirmacoesPedidas), semana.Sum(r => r.ConfirmacoesRespondidas),
            ultimo, atraso);
    }

    /// <summary>"45 s", "12 min", "1 h 20 min" — ou "—" sem medida.</summary>
    public static string Duracao(int? segundos) => segundos switch
    {
        null => "—",
        < 60 => $"{segundos} s",
        < 3600 => $"{(int)Math.Round(segundos.Value / 60.0)} min",
        _ => segundos.Value % 3600 / 60 is var m and > 0 ? $"{segundos.Value / 3600} h {m} min" : $"{segundos.Value / 3600} h"
    };

    /// <summary>A frase de quando não há nada: diz o que liga o envio, não "zero conversas".</summary>
    public const string SemResumo =
        "O CRM ainda não mandou nenhum resumo. Ele começa a mandar quando o envio do resumo diário é ligado no CRM e a ponte está atualizada.";

    /// <summary>A linha das confirmações: "18 de 24 (75%)" ou "nenhuma pedida".</summary>
    public static string Confirmacoes(CrmNaMesa m) => m.FracaoConfirmada is { } f
        ? $"{m.ConfirmacoesRespondidas} de {m.ConfirmacoesPedidas} ({f.ToString("P0", PtBr)})"
        : "nenhuma pedida";

    /// <summary>A linha do SMS: "40 (52 segmentos)", com as falhas quando houver.</summary>
    public static string Sms(CrmNaMesa m)
    {
        if (m.SmsEnviados == 0 && m.SmsFalhas == 0) return "nenhum";
        var texto = m.SmsSegmentos == m.SmsEnviados
            ? $"{m.SmsEnviados}"
            : $"{m.SmsEnviados} ({m.SmsSegmentos} segmentos)";
        return m.SmsFalhas > 0 ? $"{texto} · {m.SmsFalhas} falharam" : texto;
    }
}
