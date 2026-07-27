using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Clinica.Desktop.Shell.Configuracao;

/// <summary>
/// Guarda a connection string do PostgreSQL localmente, **criptografada por usuário do Windows**
/// (DPAPI). Aceita tanto o formato Npgsql (Host=...;) quanto a URI da Neon (postgresql://...).
///
/// Diferença para a versão do faturamento: os apps da suíte gravam numa pasta comum
/// (%APPDATA%\ClinicaSemDor) e **leem a pasta do faturamento como fallback**. Assim, se o
/// faturamento já está instalado na máquina, Recepção/Financeiro/Gerente sobem sem que a
/// clínica precise configurar a conexão de novo.
/// </summary>
public static class ConexaoStore
{
    /// <summary>Pasta comum da suíte (onde os apps novos gravam).</summary>
    private static string PastaSuite => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClinicaSemDor");

    /// <summary>Pasta do app de faturamento — somente leitura, nunca gravamos aqui.</summary>
    private static string PastaFaturamento => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClinicaFaturamento");

    private static string Arquivo => Path.Combine(PastaSuite, "conexao.dat");

    private static string ArquivoFaturamento => Path.Combine(PastaFaturamento, "conexao.dat");

    public static bool ExisteConfiguracao() => File.Exists(Arquivo) || File.Exists(ArquivoFaturamento);

    /// <summary>
    /// Lê a connection string salva: primeiro a da suíte, depois a do faturamento.
    /// Retorna null se não houver nenhuma (ou se a descriptografia falhar nas duas).
    /// </summary>
    public static string? Carregar() => Ler(Arquivo) ?? Ler(ArquivoFaturamento);

    private static string? Ler(string caminho)
    {
        try
        {
            if (!File.Exists(caminho)) return null;
            var protegido = File.ReadAllBytes(caminho);
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
        Directory.CreateDirectory(PastaSuite);
        var bytes = Encoding.UTF8.GetBytes(connectionString);
        var protegido = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Arquivo, protegido);
    }

    /// <summary>Apaga apenas a configuração da suíte (a do faturamento não é nossa para apagar).</summary>
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
