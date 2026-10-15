using Npgsql;

namespace Clinica.Infrastructure;

/// <summary>
/// Ajustes da conexão para o banco REMOTO da clínica (out/2026, desempenho dos cinco apps).
/// </summary>
/// <remarks>
/// Medido na VPS: abrir uma conexão física custou 2,7–4,7 s (TCP + TLS + mTLS + autenticação),
/// contra ~250 ms de uma consulta numa conexão já aberta. O pool do Npgsql reaproveita
/// conexões, mas com os padrões dele (mínimo 0, ociosa descartada em 5 min, sem keepalive)
/// quem volta do café, troca de tela depois de alguns minutos parado ou tem uma conexão
/// derrubada em silêncio pelo roteador paga de novo esses segundos — é a "tela que demora
/// para abrir" de todos os módulos ao mesmo tempo.
///
/// Só se preenche o que veio no PADRÃO: uma connection string que já traz o valor (o
/// ambiente de homologação, um ajuste do suporte) é respeitada como está.
/// </remarks>
public static class ConexaoRemota
{
    /// <summary>Conexões que ficam abertas mesmo ociosas.</summary>
    public const int MinimoNoPool = 2;

    /// <summary>Segundos entre os pacotes de keepalive — curto o bastante para NAT e firewall não derrubarem a conexão ociosa.</summary>
    public const int KeepAliveSegundos = 30;

    /// <summary>Tempo que uma conexão ACIMA do mínimo fica ociosa antes de ser fechada.</summary>
    public const int OciosaSegundos = 900;

    public static string Ajustar(string connectionString)
    {
        NpgsqlConnectionStringBuilder b;
        try { b = new NpgsqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException) { return connectionString; } // quem decide o erro é a abertura, com a frase dela
        if (!b.Pooling) return connectionString;
        if (b.MinPoolSize == 0) b.MinPoolSize = Math.Min(MinimoNoPool, b.MaxPoolSize);
        if (b.KeepAlive == 0) b.KeepAlive = KeepAliveSegundos;
        if (b.ConnectionIdleLifetime == 300) b.ConnectionIdleLifetime = OciosaSegundos;
        return b.ConnectionString;
    }

    /// <summary>
    /// Abre as conexões mínimas do pool em paralelo, para a PRIMEIRA troca de tela não pagar
    /// a abertura. Falha aqui nunca derruba nada: a tela abriria a conexão de qualquer jeito.
    /// </summary>
    public static async Task AquecerAsync(string connectionString, CancellationToken ct = default)
    {
        var conexoes = new List<NpgsqlConnection>();
        try
        {
            for (var i = 0; i < MinimoNoPool; i++) conexoes.Add(new NpgsqlConnection(connectionString));
            await Task.WhenAll(conexoes.Select(c => c.OpenAsync(ct)));
        }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("Aquecimento do pool de conexões", ex); }
        finally { foreach (var c in conexoes) await c.DisposeAsync(); }
    }
}
