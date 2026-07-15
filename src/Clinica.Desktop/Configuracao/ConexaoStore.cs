using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// Guarda a connection string do PostgreSQL localmente, **criptografada por usuário do Windows**
/// (DPAPI). Assim o banco é configurado no primeiro acesso, sem editar arquivos nem versionar segredos.
/// Aceita tanto o formato Npgsql (Host=...;) quanto a URI da Neon (postgresql://...).
/// </summary>
public static class ConexaoStore
{
    private static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClinicaFaturamento");

    private static string Arquivo => Path.Combine(Pasta, "conexao.dat");

    public static bool ExisteConfiguracao() => File.Exists(Arquivo);

    /// <summary>Lê a connection string salva (ou null se não houver / falhar a descriptografia).</summary>
    public static string? Carregar()
    {
        try
        {
            if (!File.Exists(Arquivo)) return null;
            var protegido = File.ReadAllBytes(Arquivo);
            var bytes = ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Salva a connection string criptografada para o usuário atual do Windows.</summary>
    public static void Salvar(string connectionString)
    {
        Directory.CreateDirectory(Pasta);
        var bytes = Encoding.UTF8.GetBytes(connectionString);
        var protegido = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Arquivo, protegido);
    }

    public static void Limpar()
    {
        if (File.Exists(Arquivo)) File.Delete(Arquivo);
    }

    /// <summary>
    /// Normaliza a entrada para o formato Npgsql. Aceita a URI da Neon
    /// (postgresql://user:pass@host/db?sslmode=require) e converte para
    /// Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;.
    /// </summary>
    public static string Normalizar(string entrada)
    {
        entrada = entrada.Trim();

        if (entrada.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            entrada.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(entrada);
            var userInfo = uri.UserInfo.Split(':', 2);

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.Trim('/'),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
                SslMode = SslMode.Require,
                TrustServerCertificate = true
                // channel_binding é negociado automaticamente pelo Npgsql (SCRAM).
            };
            return builder.ConnectionString;
        }

        return entrada;
    }

    /// <summary>Tenta abrir a conexão para validar antes de salvar.</summary>
    public static async Task<(bool Ok, string Mensagem)> TestarAsync(string connectionString)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            return (true, "Conexão bem-sucedida!");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
