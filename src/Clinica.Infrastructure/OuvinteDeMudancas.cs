using Npgsql;

namespace Clinica.Infrastructure;

/// <summary>
/// Aviso instantâneo de que outra máquina gravou algo que esta tela mostra (out/2026, item 4
/// do plano de desempenho).
/// </summary>
/// <remarks>
/// Até aqui cada posto descobria a mudança do outro RELENDO por relógio — a cada 15 s os
/// pedidos da enfermagem, a cada 30 s/1 min a agenda —, e cada batida era uma ida ao banco
/// remoto mesmo quando nada tinha mudado. O banco agora avisa (gatilho
/// <c>clinica_avisar_mudanca</c>, migration <c>AvisosInstantaneos</c>) e este ouvinte, numa
/// conexão própria e ociosa, repassa o nome da TABELA que mudou.
///
/// ⚠️ É COMPLEMENTO, nunca substituto: os relógios continuam (mais espaçados quando o ouvinte
/// está de pé). Aviso perdido — rede que caiu, VPS reiniciando — não pode deixar uma tela
/// parada para sempre. Por isso <see cref="Ativo"/> existe: quem escolhe o intervalo do
/// relógio pergunta se o aviso está chegando.
///
/// O aviso não carrega dado nenhum: só o nome da tabela. Quem quer saber o que mudou relê
/// pelo caminho de sempre, com as permissões de sempre.
/// </remarks>
public static class OuvinteDeMudancas
{
    /// <summary>O canal do <c>pg_notify</c>; o mesmo nome está no gatilho da migration.</summary>
    public const string Canal = "clinica_mudancas";

    /// <summary>Uma tabela mudou noutra máquina (ou nesta). Disparado fora da thread de tela.</summary>
    public static event Action<string>? Mudou;

    /// <summary>Verdadeiro enquanto a conexão de escuta está aberta e escutando.</summary>
    public static bool Ativo { get; private set; }

    private static int _iniciado;

    /// <summary>Liga a escuta (uma vez por processo). Falha nunca derruba nada: os relógios seguem.</summary>
    public static void Iniciar(string connectionString)
    {
        if (Interlocked.Exchange(ref _iniciado, 1) == 1) return;
        _ = Task.Run(() => EscutarAsync(connectionString));
    }

    /// <summary>Repassa um aviso como se tivesse chegado do banco (testes e o próprio ouvinte).</summary>
    public static void Avisar(string tabela) => Mudou?.Invoke(tabela);

    private static async Task EscutarAsync(string connectionString)
    {
        // Conexão FORA do pool: ela fica presa no LISTEN a vida inteira do app, e dentro do
        // pool ocuparia uma das conexões que as telas usam.
        var b = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        if (b.KeepAlive == 0) b.KeepAlive = ConexaoRemota.KeepAliveSegundos;
        var cadeia = b.ConnectionString;
        var espera = TimeSpan.FromSeconds(5);
        var registrarFalha = true; // uma linha por queda, não uma por tentativa
        var jaConectou = false;

        while (true)
        {
            try
            {
                await using var conexao = new NpgsqlConnection(cadeia);
                conexao.Notification += (_, e) =>
                {
                    try { Avisar(e.Payload); }
                    catch (Exception ex) { Clinica.Application.Diagnostico.Registrar("Aviso de mudança no banco", ex); }
                };
                await conexao.OpenAsync();
                await using (var cmd = new NpgsqlCommand($"LISTEN {Canal}", conexao))
                    await cmd.ExecuteNonQueryAsync();

                Ativo = true;
                registrarFalha = true;
                espera = TimeSpan.FromSeconds(5);
                // Ao RECONECTAR, o que mudou durante a queda não foi avisado: um aviso genérico
                // faz as telas abertas relerem uma vez. Na primeira conexão não — as telas
                // acabaram de ler na abertura.
                if (jaConectou) Avisar("*");
                jaConectou = true;

                while (true) await conexao.WaitAsync();
            }
            catch (Exception ex)
            {
                if (registrarFalha)
                    Clinica.Application.Diagnostico.Registrar("Escuta de avisos do banco fora do ar; os relógios seguem e ela tenta de novo", ex);
                registrarFalha = false;
                Ativo = false;
            }

            await Task.Delay(espera);
            if (espera < TimeSpan.FromMinutes(2)) espera *= 2;
        }
    }
}
