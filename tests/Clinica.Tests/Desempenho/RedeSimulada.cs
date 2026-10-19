using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Clinica.Tests.Desempenho;

/// <summary>
/// A rede da clínica, simulada (out/2026, medição da abertura dos cinco apps). Duas coisas
/// custam caro contra o Postgres da VPS e nenhuma delas aparece no SQLite dos testes:
///
/// 1. <b>cada ida ao banco</b> — uma consulta, um comando: <see cref="Ida"/> de espera,
///    como o ping de 186–288 ms medido do posto (docs/desempenho-leituras-2026-10-15.md);
/// 2. <b>cada conexão FÍSICA nova</b> — TCP + TLS + mTLS + autenticação, medido em
///    2,7–4,7 s (<c>ConexaoRemota</c>). O pool do Npgsql só abre conexão física quando não
///    há uma ociosa; aqui o mesmo: <see cref="Fisicas"/> conexões existem, e a que pede uma
///    a mais paga <see cref="AberturaFisica"/>.
///
/// Comando SÍNCRONO (ToList/SaveChanges sem Async) também espera — com Thread.Sleep, que é o
/// que ele faria com a thread de tela — e é contado à parte, porque na tela é congelamento.
/// </summary>
public sealed class RedeSimulada
{
    private readonly object _trava = new();
    private int _fisicas, _emUso, _emVoo, _maxEmVoo, _maxEmUso;
    private long _comandos, _sincronos, _aberturasFisicas, _ultimaAtividade, _inicio;
    private readonly List<(long Inicio, long Fim, string Sql)> _linhas = [];

    public TimeSpan Ida { get; set; } = TimeSpan.FromMilliseconds(50);
    public TimeSpan AberturaFisica { get; set; } = TimeSpan.Zero;
    public bool GuardarLinhas { get; set; }

    public int Fisicas { get { lock (_trava) return _fisicas; } }
    public int EmVoo => Volatile.Read(ref _emVoo);
    public long Comandos => Interlocked.Read(ref _comandos);
    public long Sincronos => Interlocked.Read(ref _sincronos);
    public long AberturasFisicas => Interlocked.Read(ref _aberturasFisicas);
    public int MaximoSimultaneo => Volatile.Read(ref _maxEmVoo);
    public int MaximoConexoesEmUso => Volatile.Read(ref _maxEmUso);
    public long UltimaAtividade => Interlocked.Read(ref _ultimaAtividade);

    public IReadOnlyList<(TimeSpan Inicio, TimeSpan Fim, string Sql)> Linhas
    {
        get
        {
            lock (_trava)
                return _linhas.Select(l => (Stopwatch.GetElapsedTime(_inicio, l.Inicio),
                    Stopwatch.GetElapsedTime(_inicio, l.Fim), l.Sql)).ToList();
        }
    }

    /// <summary>
    /// Quantas idas aconteceram EM SÉRIE: a maior cadeia de comandos em que cada um só começou
    /// depois de o anterior voltar. É o número que multiplica a latência da rede — duas leituras
    /// simultâneas custam uma ida; duas em fila custam duas —, e não depende do tempo de CPU da
    /// máquina que mede (o tempo de parede depende).
    /// </summary>
    public int IdasEmSerie
    {
        get
        {
            List<(long Inicio, long Fim)> l;
            lock (_trava) l = _linhas.Select(x => (x.Inicio, x.Fim)).OrderBy(x => x.Fim).ToList();
            var profundidade = new int[l.Count];
            var maior = 0;
            for (var i = 0; i < l.Count; i++)
            {
                var antes = 0;
                for (var j = 0; j < i; j++)
                    if (l[j].Fim <= l[i].Inicio && profundidade[j] > antes) antes = profundidade[j];
                profundidade[i] = antes + 1;
                if (profundidade[i] > maior) maior = profundidade[i];
            }
            return maior;
        }
    }

    /// <summary>Zera as contagens; <paramref name="fisicasProntas"/> conexões já abertas (o pool aquecido).</summary>
    public void Reiniciar(int fisicasProntas)
    {
        lock (_trava)
        {
            _fisicas = fisicasProntas; _emUso = 0; _emVoo = 0; _maxEmVoo = 0; _maxEmUso = 0;
            _comandos = 0; _sincronos = 0; _aberturasFisicas = 0;
            _inicio = _ultimaAtividade = Stopwatch.GetTimestamp();
            _linhas.Clear();
        }
    }

    public DbCommandInterceptor ParaComandos => _paraComandos ??= new InterceptadorDeComandos(this);
    public DbConnectionInterceptor ParaConexoes => _paraConexoes ??= new InterceptadorDeConexoes(this);
    private DbCommandInterceptor? _paraComandos;
    private DbConnectionInterceptor? _paraConexoes;

    private TimeSpan PegarConexao()
    {
        lock (_trava)
        {
            _emUso++;
            if (_emUso > _maxEmUso) _maxEmUso = _emUso;
            if (_emUso <= _fisicas) return TimeSpan.Zero;
            _fisicas = _emUso;
            _aberturasFisicas++;
            return AberturaFisica;
        }
    }

    private void SoltarConexao()
    {
        lock (_trava) _emUso = Math.Max(0, _emUso - 1);
    }

    private long Comecou()
    {
        var agora = Stopwatch.GetTimestamp();
        var emVoo = Interlocked.Increment(ref _emVoo);
        int atual;
        while (emVoo > (atual = Volatile.Read(ref _maxEmVoo)) && Interlocked.CompareExchange(ref _maxEmVoo, emVoo, atual) != atual) { }
        Interlocked.Exchange(ref _ultimaAtividade, agora);
        return agora;
    }

    private void Terminou(long inicio, DbCommand comando)
    {
        var fim = Stopwatch.GetTimestamp();
        Interlocked.Decrement(ref _emVoo);
        Interlocked.Increment(ref _comandos);
        Interlocked.Exchange(ref _ultimaAtividade, fim);
        var sql = GuardarLinhas ? comando.CommandText.Replace('\n', ' ').Replace('\r', ' ') : "";
        lock (_trava) _linhas.Add((inicio, fim, sql.Length > 160 ? sql[..160] : sql));
    }

    private sealed class InterceptadorDeComandos(RedeSimulada rede) : DbCommandInterceptor
    {
        // O início fica no próprio comando (o mesmo objeto chega ao Executing e ao Executed).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbCommand, StrongBox> Inicios = new();
        private sealed class StrongBox { public long Valor; }

        private async ValueTask Ir(DbCommand c, CancellationToken ct)
        {
            Inicios.AddOrUpdate(c, new StrongBox { Valor = rede.Comecou() });
            if (rede.Ida > TimeSpan.Zero) await Task.Delay(rede.Ida, ct);
        }

        private void IrSincrono(DbCommand c)
        {
            Interlocked.Increment(ref rede._sincronos);
            Inicios.AddOrUpdate(c, new StrongBox { Valor = rede.Comecou() });
            if (rede.Ida > TimeSpan.Zero) Thread.Sleep(rede.Ida);
        }

        private void Voltou(DbCommand c)
            => rede.Terminou(Inicios.TryGetValue(c, out var b) ? b.Valor : Stopwatch.GetTimestamp(), c);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
        { await Ir(c, ct); return r; }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r)
        { IrSincrono(c); return r; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken ct = default)
        { Voltou(c); return ValueTask.FromResult(r); }
        public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r)
        { Voltou(c); return r; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default)
        { await Ir(c, ct); return r; }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r)
        { IrSincrono(c); return r; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken ct = default)
        { Voltou(c); return ValueTask.FromResult(r); }
        public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r)
        { Voltou(c); return r; }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
        { await Ir(c, ct); return r; }
        public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r)
        { IrSincrono(c); return r; }
        public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken ct = default)
        { Voltou(c); return ValueTask.FromResult(r); }
        public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r)
        { Voltou(c); return r; }

        public override Task CommandFailedAsync(DbCommand c, CommandErrorEventData e, CancellationToken ct = default)
        { Voltou(c); return Task.CompletedTask; }
        public override void CommandFailed(DbCommand c, CommandErrorEventData e) => Voltou(c);
    }

    private sealed class InterceptadorDeConexoes(RedeSimulada rede) : DbConnectionInterceptor
    {
        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken ct = default)
        {
            var espera = rede.PegarConexao();
            if (espera > TimeSpan.Zero) await Task.Delay(espera, ct);
            return r;
        }

        public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
        {
            var espera = rede.PegarConexao();
            if (espera > TimeSpan.Zero) Thread.Sleep(espera);
            return r;
        }

        public override Task ConnectionClosedAsync(DbConnection c, ConnectionEndEventData e)
        { rede.SoltarConexao(); return Task.CompletedTask; }
        public override void ConnectionClosed(DbConnection c, ConnectionEndEventData e) => rede.SoltarConexao();
    }
}
