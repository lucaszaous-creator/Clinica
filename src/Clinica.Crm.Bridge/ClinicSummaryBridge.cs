using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// O dia do CRM para a direção (out/2026, caminho A). O CRM manda as contagens do dia e esta
/// classe grava UMA linha por dia em <c>ResumosCrmDiarios</c>; quem lê é o Gerente.
///
/// O mesmo dia chega várias vezes (hoje é parcial até virar ontem), então gravar é SUBSTITUIR —
/// e só por um cálculo mais NOVO: o envio que chega fora de ordem não pode trocar o dia fechado
/// pelo parcial da manhã. A ordem é decidida pelo <c>GeneratedAt</c> do CRM, não pela hora em
/// que a ponte recebeu.
/// </summary>
public sealed class ClinicSummaryBridge(ClinicaDbContext db)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public Task<ClinicDecisionResult> Daily(CrmDailySummary input, CancellationToken ct)
        => TransacaoDaPonte.Executar(db, () => DailyCore(input, DateTimeOffset.UtcNow, ct), ct);

    private async Task<ClinicDecisionResult> DailyCore(CrmDailySummary input, DateTimeOffset agora, CancellationToken ct)
    {
        Validar(input, agora);
        var gerado = TimeZoneInfo.ConvertTime(input.GeneratedAt, Zone).DateTime;
        var recebido = TimeZoneInfo.ConvertTime(agora, Zone).DateTime;

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var linha = await db.ResumosCrmDiarios.SingleOrDefaultAsync(r => r.Dia == input.Day, ct);
        if (linha is not null && linha.GeradoEm >= gerado)
            return new("stale", "Já há um resumo mais recente deste dia");
        if (linha is null)
        {
            linha = new ResumoCrmDiario { Dia = input.Day };
            db.ResumosCrmDiarios.Add(linha);
        }

        linha.GeradoEm = gerado;
        linha.RecebidoEm = recebido;
        linha.ConversasIniciadas = input.ConversationsStarted;
        linha.ConversasEncerradas = input.ConversationsClosed;
        linha.ConversasAbertas = input.ConversationsOpen;
        linha.MensagensRecebidas = input.MessagesIn;
        linha.MensagensEnviadas = input.MessagesOut;
        linha.RespostaMedianaSegundos = input.MedianResponseSeconds;
        linha.RespostasMedidas = input.RepliesMeasured;
        linha.SmsEnviados = input.SmsSent;
        linha.SmsSegmentos = input.SmsSegments;
        linha.SmsFalhas = input.SmsFailed;
        linha.ConfirmacoesPedidas = input.ConfirmationsAsked;
        linha.ConfirmacoesRespondidas = input.ConfirmationsAnswered;
        linha.Resolvidas = input.Resolved;
        linha.Agendadas = input.Scheduled;
        linha.SemResposta = input.NoResponse;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new("recorded", "Resumo do dia gravado na clínica");
    }

    /// <summary>
    /// Recusa o que não pode ser um dia do CRM: contagem negativa, mediana sem base, dia no
    /// futuro ou cálculo feito antes do próprio dia. Número impossível gravado vira número na
    /// tela da direção, com cara de exato.
    /// </summary>
    static void Validar(CrmDailySummary x, DateTimeOffset agora)
    {
        int[] contagens = [x.ConversationsStarted, x.ConversationsClosed, x.ConversationsOpen, x.MessagesIn, x.MessagesOut,
            x.RepliesMeasured, x.SmsSent, x.SmsSegments, x.SmsFailed, x.ConfirmationsAsked, x.ConfirmationsAnswered,
            x.Resolved, x.Scheduled, x.NoResponse];
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agora, Zone).DateTime);
        var diaDoCalculo = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.GeneratedAt, Zone).DateTime);
        if (contagens.Any(c => c < 0)
            || x.MedianResponseSeconds is < 0
            || (x.MedianResponseSeconds is null) != (x.RepliesMeasured == 0)
            || x.SmsSegments < x.SmsSent
            || x.ConfirmationsAnswered > x.ConfirmationsAsked
            || x.Day > hoje
            || x.Day < hoje.AddDays(-400)
            || x.GeneratedAt > agora.AddMinutes(5)
            || diaDoCalculo < x.Day)
            throw new InvalidOperationException("Resumo diário do CRM inválido.");
    }
}
