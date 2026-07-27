using System.IO;
using System.Reflection;
using Clinica.Application;

namespace Clinica.Desktop.Shell.Configuracao;

/// <summary>
/// Log de erros em arquivo dos apps da suíte (Recepção, Financeiro, Gerente Geral).
/// Mesma política do faturamento (<c>Clinica.Desktop.Configuracao.LogErros</c>): um
/// .txt por mês numa pasta <c>logs</c> na raiz da instalação — fora da pasta
/// versionada do Velopack, para sobreviver às atualizações —, rotacionado em 2 MB e
/// expurgado em 90 dias. Sem permissão de escrita lá, cai para
/// <c>%APPDATA%\ClinicaSemDor\logs</c>.
///
/// Nunca lança: um log que falha não pode derrubar o app.
///
/// São duas implementações no repositório enquanto o faturamento não migrar para o
/// shell (Fase 4) — o mesmo débito assumido para o design system.
/// </summary>
public static class LogSuite
{
    private const long TamanhoMaximoBytes = 2 * 1024 * 1024; // 2 MB por arquivo
    private const int DiasParaExpurgo = 90;

    private static readonly object Trava = new();
    private static string? _pasta;
    private static DateTime _ultimoExpurgo = DateTime.MinValue;

    /// <summary>Pasta onde os .txt são gravados (resolvida uma vez por execução).</summary>
    public static string Pasta => _pasta ??= ResolverPasta();

    /// <summary>
    /// Liga o log ao <see cref="Diagnostico"/>, para que as degradações deliberadas de
    /// Application/Infrastructure também deixem rastro. Chamado uma vez, na abertura.
    /// </summary>
    public static void Instalar() => Diagnostico.Sink = Registrar;

    /// <summary>
    /// Grava uma ocorrência. <paramref name="contexto"/> é o que estava acontecendo
    /// ("Recepção — fila do dia"), não o nome do método.
    /// </summary>
    public static void Registrar(string contexto, Exception ex)
    {
        try
        {
            var pasta = Pasta;
            Directory.CreateDirectory(pasta);

            var arquivo = Path.Combine(pasta, $"erros-{DateTime.Now:yyyy-MM}.txt");
            var linha =
                $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] v{Versao} | {contexto}{Environment.NewLine}" +
                $"{ex}{Environment.NewLine}{Environment.NewLine}";

            lock (Trava)
            {
                RotacionarSeGrande(arquivo);
                File.AppendAllText(arquivo, linha);
                ExpurgarAntigos(pasta);
            }
        }
        catch
        {
            // Sem disco/permissão: segue sem log.
        }
    }

    /// <summary>Versão do app, para saber em qual build o erro aconteceu.</summary>
    private static string Versao =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";

    private static string ResolverPasta()
    {
        try
        {
            var baseApp = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Layout do Velopack: …\Clinica.Recepcao\current\ → grava um nível acima.
            var raiz = baseApp.Name.Equals("current", StringComparison.OrdinalIgnoreCase) && baseApp.Parent is not null
                ? baseApp.Parent.FullName
                : baseApp.FullName;

            var candidata = Path.Combine(raiz, "logs");
            if (PodeGravar(candidata)) return candidata;
        }
        catch
        {
            // Caminho estranho (rede, permissão): usa a pasta do usuário.
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClinicaSemDor", "logs");
    }

    /// <summary>Testa a escrita de verdade: existir a pasta não garante permissão.</summary>
    private static bool PodeGravar(string pasta)
    {
        try
        {
            Directory.CreateDirectory(pasta);
            var teste = Path.Combine(pasta, ".escrita");
            File.WriteAllText(teste, string.Empty);
            File.Delete(teste);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Passou de 2 MB: arquiva o atual com a hora no nome e recomeça.</summary>
    private static void RotacionarSeGrande(string arquivo)
    {
        try
        {
            var info = new FileInfo(arquivo);
            if (!info.Exists || info.Length < TamanhoMaximoBytes) return;

            var arquivado = Path.Combine(
                info.DirectoryName!,
                $"{Path.GetFileNameWithoutExtension(arquivo)}-{DateTime.Now:ddHHmmss}.txt");
            File.Move(arquivo, arquivado, overwrite: true);
        }
        catch
        {
            // Não conseguiu rotacionar: segue anexando no mesmo arquivo.
        }
    }

    /// <summary>Mantém a pasta leve: nada com mais de 90 dias. Roda no máximo 1x por dia.</summary>
    private static void ExpurgarAntigos(string pasta)
    {
        if (_ultimoExpurgo.Date == DateTime.Today) return;
        _ultimoExpurgo = DateTime.Today;

        try
        {
            var limite = DateTime.Now.AddDays(-DiasParaExpurgo);
            foreach (var f in Directory.EnumerateFiles(pasta, "erros-*.txt"))
            {
                try
                {
                    if (File.GetLastWriteTime(f) < limite) File.Delete(f);
                }
                catch
                {
                    // Arquivo em uso: fica para a próxima.
                }
            }
        }
        catch
        {
            // Pasta sumiu no meio: nada a expurgar.
        }
    }
}
