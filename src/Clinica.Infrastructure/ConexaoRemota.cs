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
    public const int MinimoNoPool = 4;

    /// <summary>
    /// Conexões abertas na abertura do app, enquanto a pessoa digita o login: as leituras que a
    /// primeira tela dispara ao mesmo tempo (<see cref="LeituraParalela.Vagas"/> +
    /// <see cref="LeituraParalela.VagasDeDentro"/>). As acima do mínimo ficam ociosas por
    /// <see cref="OciosaSegundos"/> e depois fecham — o servidor tem max_connections = 60
    /// (docs/banco-na-vps.md), e só o mínimo fica preso o dia inteiro.
    /// </summary>
    public const int AquecidasNaAbertura = LeituraParalela.Vagas + LeituraParalela.VagasDeDentro;

    /// <summary>Segundos entre os pacotes de keepalive — curto o bastante para NAT e firewall não derrubarem a conexão ociosa.</summary>
    public const int KeepAliveSegundos = 30;

    /// <summary>Tempo que uma conexão ACIMA do mínimo fica ociosa antes de ser fechada.</summary>
    public const int OciosaSegundos = 900;

    /// <summary>
    /// Prazo para ABRIR uma conexão (o padrão do Npgsql é 15 s). Abrir custa 2,7–4,7 s no
    /// caso bom, e out/2026 mostrou aberturas estourando os 15 s de vez em quando: o servidor
    /// registra a chegada e nada mais vem do cliente — pacote perdido no caminho, não banco
    /// lento (o mesmo aperto de mão leva 60 ms dentro da VPS). Dobrar a folga absorve o pico
    /// sem esconder rede fora do ar, que continua falhando — só mais tarde.
    /// </summary>
    public const int PrazoAberturaSegundos = 30;

    /// <summary>
    /// Esperas entre as tentativas AUTOMÁTICAS de abrir o banco na abertura do app. Falha
    /// passageira não pode virar diálogo na cara de quem só quer começar o dia: a 2ª
    /// tentativa quase sempre passa.
    /// </summary>
    public static readonly TimeSpan[] EsperasEntreTentativas = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    public static string Ajustar(string connectionString)
    {
        NpgsqlConnectionStringBuilder b;
        try { b = new NpgsqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException) { return connectionString; } // quem decide o erro é a abertura, com a frase dela
        if (!b.Pooling) return connectionString;
        if (b.MinPoolSize == 0) b.MinPoolSize = Math.Min(MinimoNoPool, b.MaxPoolSize);
        if (b.KeepAlive == 0) b.KeepAlive = KeepAliveSegundos;
        if (b.ConnectionIdleLifetime == 300) b.ConnectionIdleLifetime = OciosaSegundos;
        if (b.Timeout == 15) b.Timeout = PrazoAberturaSegundos;
        return b.ConnectionString;
    }

    /// <summary>
    /// A falha é de REDE (timeout, conexão derrubada, servidor momentaneamente indisponível)
    /// e não de CONFIGURAÇÃO (senha, certificado, endereço errado)? Só a primeira se resolve
    /// tentando de novo — e só a segunda justifica oferecer outra conexão. Confundir as duas
    /// é o que fazia uma instabilidade de segundos terminar com "Deseja informar outra
    /// conexão?", cujo "Sim" APAGA a configuração da máquina.
    /// </summary>
    public static bool EhFalhaPassageira(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case PostgresException pg:
                    // Recusa do servidor: só é passageira se ele próprio diz que é (subindo,
                    // sem vagas). Senha errada (28P01) e banco inexistente não são.
                    return pg.IsTransient || pg.SqlState is "57P03" or "53300";
                case TimeoutException:
                case System.Net.Sockets.SocketException:
                case System.IO.IOException:
                    return true;
                case NpgsqlException npg when npg.IsTransient:
                    return true;
            }
        }
        return false;
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
            for (var i = 0; i < AquecidasNaAbertura; i++) conexoes.Add(new NpgsqlConnection(connectionString));
            await Task.WhenAll(conexoes.Select(c => c.OpenAsync(ct)));
        }
        catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("Aquecimento do pool de conexões", ex); }
        finally { foreach (var c in conexoes) await c.DisposeAsync(); }
    }
}
