using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// O contato da equipe que volta ao Acompanhamento (out/2026, fase 2 — docs/crm-na-recepcao.md).
/// A mensagem que alguém mandou pelo CRM a um paciente identificado entra no HISTÓRICO de cada caso
/// aberto dele (recall ou novo no BSV), com o nome de quem mandou — como o recall automático já
/// faz (<see cref="ClinicRecallBridge"/>). Não muda etapa, data nem responsável: o desfecho é da
/// pessoa, no "Como foi?". Paciente sem caso aberto: nada a registrar, e isso não é erro.
///
/// Idempotente pelo CommandId (vira a <c>Idempotencia</c> do contato, única por caso): o CRM repete
/// o mesmo registro quando a resposta se perde, e a segunda vez não grava de novo.
/// </summary>
public sealed class ClinicContactBridge(ClinicaDbContext db)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public Task<ClinicDecisionResult> Contact(ClinicContactNotice input, CancellationToken ct) => TransacaoDaPonte.Executar(db, () => ContactCore(input, ct), ct);

    async Task<ClinicDecisionResult> ContactCore(ClinicContactNotice input, CancellationToken ct)
    {
        if (!Guid.TryParseExact(input.CommandId, "N", out var comando) || input.PatientId <= 0
            || input.Channel is not ("whatsapp" or "sms") || string.IsNullOrWhiteSpace(input.Agent) || input.Agent.Length > 120
            || input.SentAt > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Registro de contato inválido.");
        var casos = await db.Acompanhamentos
            .Where(a => a.PacienteId == input.PatientId && a.EncerradoEm == null)
            .Select(a => a.Id).ToListAsync(ct);
        if (casos.Count == 0) return new("skipped", "O paciente não tem acompanhamento aberto");
        var jaGravados = await db.ContatosAcompanhamento
            .Where(c => casos.Contains(c.AcompanhamentoPacienteId) && c.Idempotencia == comando)
            .Select(c => c.AcompanhamentoPacienteId).ToListAsync(ct);
        var em = TimeZoneInfo.ConvertTime(input.SentAt, Zone).DateTime;
        var sms = input.Channel == "sms";
        foreach (var caso in casos.Except(jaGravados))
            db.ContatosAcompanhamento.Add(new ContatoAcompanhamento
            {
                AcompanhamentoPacienteId = caso, Idempotencia = comando, Em = em,
                Operador = input.Agent.Trim() + (sms ? " · CRM · SMS" : " · CRM · WhatsApp"),
                Canal = sms ? CanalContato.Sms : CanalContato.WhatsApp,
                Resultado = sms ? "Mensagem enviada pelo SMS da clínica" : "Mensagem enviada pelo WhatsApp da clínica",
                Observacao = "Registro automático do CRM: a mensagem saiu; a resposta do paciente se registra em \"Como foi?\"."
            });
        await db.SaveChangesAsync(ct);
        return new("recorded", "Contato registrado no acompanhamento");
    }
}
