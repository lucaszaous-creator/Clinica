using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Clinica.Infrastructure;

/// <summary>
/// Contadores do processo: quantas idas ao banco, quanto tempo nelas e quantas estão no ar agora
/// (out/2026). É o que deixa o shell medir cada troca de tela NO POSTO — onde a latência é a da
/// rede da clínica, que nenhum ensaio local reproduz. Só conta; não guarda SQL, parâmetro nem dado.
/// </summary>
public static class MedicaoBanco
{
    private static long _comandos, _ticksNoBanco, _ultimaAtividade;
    private static int _emVoo;

    public static long Comandos => Interlocked.Read(ref _comandos);
    public static TimeSpan TempoNoBanco => TimeSpan.FromTicks(Interlocked.Read(ref _ticksNoBanco));
    public static int EmVoo => Volatile.Read(ref _emVoo);
    /// <summary>Marca (Stopwatch) do último comando que começou ou terminou.</summary>
    public static long UltimaAtividade => Interlocked.Read(ref _ultimaAtividade);

    internal static void Comecou()
    {
        Interlocked.Increment(ref _emVoo);
        Interlocked.Exchange(ref _ultimaAtividade, Stopwatch.GetTimestamp());
    }

    internal static void Terminou(TimeSpan duracao)
    {
        Interlocked.Decrement(ref _emVoo);
        Interlocked.Increment(ref _comandos);
        Interlocked.Add(ref _ticksNoBanco, duracao.Ticks);
        Interlocked.Exchange(ref _ultimaAtividade, Stopwatch.GetTimestamp());
    }
}

/// <summary>Alimenta <see cref="MedicaoBanco"/>. Uma instância para o processo inteiro.</summary>
public sealed class InterceptadorMedicao : DbCommandInterceptor
{
    public static readonly InterceptadorMedicao Instancia = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r)
    { MedicaoBanco.Comecou(); return r; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
    { MedicaoBanco.Comecou(); return ValueTask.FromResult(r); }
    public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r)
    { MedicaoBanco.Terminou(e.Duration); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken ct = default)
    { MedicaoBanco.Terminou(e.Duration); return ValueTask.FromResult(r); }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r)
    { MedicaoBanco.Comecou(); return r; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { MedicaoBanco.Comecou(); return ValueTask.FromResult(r); }
    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r)
    { MedicaoBanco.Terminou(e.Duration); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken ct = default)
    { MedicaoBanco.Terminou(e.Duration); return ValueTask.FromResult(r); }

    public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r)
    { MedicaoBanco.Comecou(); return r; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
    { MedicaoBanco.Comecou(); return ValueTask.FromResult(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r)
    { MedicaoBanco.Terminou(e.Duration); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken ct = default)
    { MedicaoBanco.Terminou(e.Duration); return ValueTask.FromResult(r); }

    public override void CommandFailed(DbCommand c, CommandErrorEventData e) => MedicaoBanco.Terminou(e.Duration);
    public override Task CommandFailedAsync(DbCommand c, CommandErrorEventData e, CancellationToken ct = default)
    { MedicaoBanco.Terminou(e.Duration); return Task.CompletedTask; }

    public override void CommandCanceled(DbCommand c, CommandEndEventData e) => MedicaoBanco.Terminou(e.Duration);
    public override Task CommandCanceledAsync(DbCommand c, CommandEndEventData e, CancellationToken ct = default)
    { MedicaoBanco.Terminou(e.Duration); return Task.CompletedTask; }
}
