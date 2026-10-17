using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// Toda operação da ponte que abre transação passa por aqui (out/2026 — achado ao rodar a
/// ponte contra um Postgres de verdade, com a configuração da clínica).
///
/// <c>AddClinica</c> liga o <c>EnableRetryOnFailure</c> do Npgsql, e o EF RECUSA transação
/// aberta à mão sob uma estratégia que repete ("does not support user-initiated
/// transactions"). O comentário de <c>ConfigurarNpgsql</c> já avisava: quem introduzir
/// <c>BeginTransaction</c> precisa usar a estratégia de execução. Sem isto, confirmar,
/// cancelar, remarcar e marcar pelo CRM devolviam 409 em TODA chamada, com os testes
/// verdes — eles montam o <c>DbContext</c> sem a estratégia.
///
/// A repetição é segura porque cada operação da ponte é idempotente pelo comando
/// (a trilha guarda o <c>CommandId</c>): a segunda tentativa acha a primeira e devolve o
/// mesmo recibo. O que a tentativa que falhou deixou rastreado é descartado antes de repetir.
/// </summary>
public static class TransacaoDaPonte
{
    public static Task<T> Executar<T>(DbContext db, Func<Task<T>> operacao, CancellationToken ct)
    {
        var tentativa = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async _ =>
        {
            // Só na REPETIÇÃO: na primeira vez o que está rastreado é de quem chamou.
            if (tentativa++ > 0) db.ChangeTracker.Clear();
            return await operacao();
        }, ct);
    }
}
