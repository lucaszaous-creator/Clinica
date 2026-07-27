using System.IO;
using System.Reflection;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// Log de erros em arquivo de texto, para rastrear problema em produção sem depender
/// de o usuário conseguir reproduzir o erro.
///
/// Fica numa pasta <c>logs</c> ÚNICA, na raiz da instalação — do lado do app, fácil de
/// achar e de mandar por e-mail. Com o Velopack o executável mora em
/// <c>…\ClinicaFaturamento\current\</c> e cada atualização substitui essa pasta; por
/// isso o log sobe um nível e grava em <c>…\ClinicaFaturamento\logs\</c>, que sobrevive
/// às atualizações. Se a pasta da instalação não for gravável (instalação em
/// Program Files sem permissão), cai para <c>%APPDATA%\ClinicaFaturamento\logs</c>.
///
/// A pasta se mantém leve sozinha: um .txt por mês, arquivo rotacionado ao passar de
/// 2 MB e nada com mais de 90 dias.
///
/// Nunca lança — um log que falha não pode derrubar o app.
/// </summary>
public static class LogErros
{
    private const long TamanhoMaximoBytes = 2 * 1024 * 1024; // 2 MB por arquivo
    private const int DiasParaExpurgo = 90;

    private static readonly object Trava = new();
    private static string? _pasta;
    private static DateTime _ultimoExpurgo = DateTime.MinValue;

    /// <summary>Pasta onde os .txt são gravados (resolvida uma vez por execução).</summary>
    public static string Pasta => _pasta ??= ResolverPasta();

    /// <summary>
    /// Grava uma ocorrência. <paramref name="contexto"/> é o que estava acontecendo na
    /// tela ("Painel — rodada de pendências"), não o nome do método.
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

    /// <summary>
    /// Raiz da instalação (fora da pasta versionada do Velopack) ou, se não der para
    /// gravar lá, a pasta do usuário.
    /// </summary>
    private static string ResolverPasta()
    {
        try
        {
            var baseApp = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Layout do Velopack: …\ClinicaFaturamento\current\ → grava um nível acima.
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
            "ClinicaFaturamento", "logs");
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
