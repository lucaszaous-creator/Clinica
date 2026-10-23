using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// O @SemDor com IA nos apps (out/2026 — Clinica#307). Duas rotas, um lado cada:
/// <list type="bullet">
/// <item>a fila de perguntas do chat dos apps: o CRM lê (é o pulso) e devolve a releitura da IA;
///   quem grava a resposta é <see cref="PerguntasAoSemDorService"/>, com as regras do chat;</item>
/// <item>os avisos "Atenção" do CRM: a lista inteira dos que valem agora substitui a anterior —
///   o que saiu dela sai do sino da Recepção.</item>
/// </list>
/// Sem chave de ligar aqui: quem decide é o CRM (a fila só é lida com a IA ligada).
/// </summary>
public sealed class ClinicSemDorBridge(ClinicaDbContext db, PerguntasAoSemDorService perguntas)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Teto da lista: o sino mostra poucos, e um envio com centenas não é um dia normal.</summary>
    public const int MaximoDeAvisos = 50;

    public async Task<StaffQuestion[]> Questions(CancellationToken ct)
        => (await perguntas.PendentesAsync(10, ct)).Select(p => new StaffQuestion(p.Id, p.Texto)).ToArray();

    public async Task<ClinicDecisionResult> Answer(StaffRewrite input, CancellationToken ct)
        => await perguntas.ResponderAsync(input.Id, input.Rewritten, ct)
            ? new("answered", "Resposta gravada no chat da equipe")
            : new("stale", "A pergunta já não aguardava releitura");

    public Task<ClinicDecisionResult> Alerts(SemDorAlerts input, CancellationToken ct)
        => TransacaoDaPonte.Executar(db, () => AlertsCore(input, DateTimeOffset.UtcNow, ct), ct);

    private async Task<ClinicDecisionResult> AlertsCore(SemDorAlerts input, DateTimeOffset agora, CancellationToken ct)
    {
        var avisos = Validar(input, agora);
        var gerado = Local(input.GeneratedAt);

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var estado = await db.SemDorNoCrm.SingleOrDefaultAsync(x => x.Id == SemDorNoCrm.Unico, ct);
        // Fora de ordem: a lista mais velha não pode trazer de volta um aviso já resolvido.
        if (estado?.AvisosRecebidosEm is { } ultimo && ultimo >= gerado)
            return new("stale", "Já há uma lista de avisos mais recente");
        if (estado is null) db.SemDorNoCrm.Add(estado = new SemDorNoCrm());
        estado.AvisosRecebidosEm = gerado;

        var chaves = avisos.Select(a => a.Chave).ToList();
        await db.AvisosDoSemDor.Where(a => !chaves.Contains(a.Chave)).ExecuteDeleteAsync(ct);
        var atuais = await db.AvisosDoSemDor.Where(a => chaves.Contains(a.Chave)).ToDictionaryAsync(a => a.Chave, ct);
        foreach (var a in avisos)
        {
            if (atuais.TryGetValue(a.Chave, out var linha))
            {
                linha.PrimeiroNome = a.PrimeiroNome; linha.Assunto = a.Assunto; linha.Em = a.Em; linha.Link = a.Link;
            }
            else db.AvisosDoSemDor.Add(a);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new("recorded", $"{avisos.Count} aviso(s) do @SemDor no sino da Recepção");
    }

    /// <summary>
    /// Recusa a lista que não pode ser do CRM: chave fora do formato, link que não é https, data
    /// no futuro. Corta o nome no primeiro (o sino fica na tela do balcão) e os textos no teto.
    /// </summary>
    static List<AvisoDoSemDor> Validar(SemDorAlerts x, DateTimeOffset agora)
    {
        if (x.Alerts is null || x.Alerts.Length > MaximoDeAvisos || x.GeneratedAt > agora.AddMinutes(5))
            throw new InvalidOperationException("Lista de avisos do @SemDor inválida.");
        var lista = new List<AvisoDoSemDor>();
        foreach (var a in x.Alerts.DistinctBy(a => a.Key))
        {
            if (a.Key is not { Length: > 7 and <= 80 } || !a.Key.StartsWith("semdor:", StringComparison.Ordinal)
                || !Uri.TryCreate(a.Link, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps || link.UserInfo != ""
                || a.Link.Length > 400 || a.At > agora.AddMinutes(5))
                throw new InvalidOperationException("Aviso do @SemDor inválido.");
            lista.Add(new AvisoDoSemDor
            {
                Chave = a.Key,
                PrimeiroNome = Cortar((a.FirstName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Paciente", 60),
                Assunto = Cortar((a.Topic ?? "").Trim(), 60),
                Em = Local(a.At),
                Link = a.Link,
            });
        }
        return lista;
    }

    static string Cortar(string s, int teto) => s.Length > teto ? s[..teto] : s;

    static DateTime Local(DateTimeOffset quando) => TimeZoneInfo.ConvertTime(quando, Zone).DateTime;
}
