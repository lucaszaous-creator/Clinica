using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// Toda unidade que abre transação PRÓPRIA passa por aqui (nov/2026). O <c>AddClinica</c> liga o
/// <c>EnableRetryOnFailure</c> do Npgsql, e o EF RECUSA transação aberta à mão sob uma estratégia que repete
/// ("The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated
/// transactions"). Foi assim que o "Lançar" da Conciliação → Particulares quebrou em produção, com os testes verdes:
/// eles rodam em SQLite, sem a estratégia. A ponte do CRM já tinha o mesmo remédio (<c>TransacaoDaPonte</c>).
///
/// Regras:
/// <list type="bullet">
/// <item>A unidade INTEIRA (begin → trabalho → commit) roda dentro de <c>CreateExecutionStrategy().ExecuteAsync</c>:
/// uma queda de rede no meio faz a estratégia repetir tudo, não só o último comando.</item>
/// <item>Na REPETIÇÃO o ChangeTracker é limpo: o que a tentativa que falhou deixou rastreado não pode ser gravado
/// de novo (na primeira vez, o rastreado é de quem chamou e fica).</item>
/// <item>Já há transação aberta (de quem chamou) → executa direto: a estratégia recusaria e quem retenta é o de fora.</item>
/// </list>
/// ⚠️ Não use em operação que COBRA ou chama serviço externo dentro da transação (assinatura SafeID/A1): repetir
/// seria cobrar de novo. Essas continuam fora daqui, por decisão (área congelada).
/// </summary>
public static class TransacaoResiliente
{
    public static Task<T> ExecutarAsync<T>(DbContext db, Func<CancellationToken, Task<T>> unidade, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) return unidade(ct);
        var tentativa = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async c =>
        {
            if (tentativa++ > 0) db.ChangeTracker.Clear();
            return await unidade(c);
        }, ct);
    }

    public static Task ExecutarAsync(DbContext db, Func<CancellationToken, Task> unidade, CancellationToken ct)
        => ExecutarAsync<bool>(db, async c => { await unidade(c); return true; }, ct);
}
