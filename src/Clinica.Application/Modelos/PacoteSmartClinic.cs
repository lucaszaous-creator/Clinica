using System.IO.Compression;

namespace Clinica.Application.Modelos;

/// <summary>
/// O ZIP que o Smart Clinic entrega à clínica (medido em set/2026): 14 CSVs com ponto e
/// vírgula, UTF-8. Aqui ele vira um dicionário arquivo → tabela; quem sabe o que cada
/// arquivo significa é o <c>ImportacaoSmartClinicService</c>.
/// </summary>
public sealed class PacoteSmartClinic
{
    public const string Pacientes = "pacientes.csv";
    public const string PosOperatorio = "pos_operatorio.csv";
    public const string FichaSoap = "ficha_soap.csv";
    public const string Prescricao = "prescricao.csv";
    public const string FichaClinica = "ficha_clinica.csv";
    public const string ConsultaMulti = "consulta_multi.csv";
    public const string ProntuarioPersonalizado = "prontuario_personalizado.csv";
    public const string FichaPreConsulta = "ficha_pre_consulta.csv";
    public const string FichaPedi = "ficha_pedi.csv";
    public const string Agenda = "agenda.csv";
    public const string Usuarios = "contratante_usuario.csv";
    public const string Exames = "exame.csv";

    /// <summary>Os arquivos de PRONTUÁRIO — cada linha vira uma evolução importada.</summary>
    public static IReadOnlyList<string> ArquivosDeProntuario { get; } =
    [
        PosOperatorio, FichaSoap, Prescricao, FichaClinica, ConsultaMulti,
        ProntuarioPersonalizado, FichaPreConsulta, FichaPedi
    ];

    private readonly Dictionary<string, TabelaImportada> _tabelas;

    /// <summary>Arquivos do ZIP que não são CSV ou não foram lidos (nome → motivo).</summary>
    public IReadOnlyDictionary<string, string> Ignorados { get; }

    private PacoteSmartClinic(Dictionary<string, TabelaImportada> tabelas, Dictionary<string, string> ignorados)
    {
        _tabelas = tabelas;
        Ignorados = ignorados;
    }

    public IReadOnlyCollection<string> Arquivos => _tabelas.Keys;

    public TabelaImportada? Tabela(string arquivo)
        => _tabelas.TryGetValue(arquivo, out var t) ? t : null;

    public bool Tem(string arquivo) => _tabelas.ContainsKey(arquivo);

    /// <summary>Abre o ZIP. Um CSV que não se lê vira entrada em <see cref="Ignorados"/>,
    /// nunca falha do pacote inteiro — a carteira não pode esperar por um arquivo vazio.</summary>
    public static PacoteSmartClinic Abrir(byte[] zip)
    {
        if (zip.Length == 0) throw new ArgumentException("O arquivo está vazio.");

        var tabelas = new Dictionary<string, TabelaImportada>(StringComparer.OrdinalIgnoreCase);
        var ignorados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var memoria = new MemoryStream(zip);
        using var arquivo = new ZipArchive(memoria, ZipArchiveMode.Read);
        foreach (var entrada in arquivo.Entries)
        {
            var nome = Path.GetFileName(entrada.FullName);
            if (string.IsNullOrEmpty(nome)) continue; // pasta
            if (!nome.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ignorados[nome] = "não é CSV";
                continue;
            }
            try
            {
                using var fluxo = entrada.Open();
                using var buffer = new MemoryStream();
                fluxo.CopyTo(buffer);
                tabelas[nome] = LeitorCsv.Ler(buffer.ToArray());
            }
            catch (Exception ex)
            {
                ignorados[nome] = ex.Message;
            }
        }

        if (!tabelas.ContainsKey(Pacientes))
            throw new ArgumentException(
                $"O ZIP não tem o arquivo {Pacientes} — sem ele não há a quem ligar o prontuário e a agenda.");

        return new PacoteSmartClinic(tabelas, ignorados);
    }

    /// <summary>Um pacote montado de tabelas já lidas — para teste e para a leitura de uma pasta.</summary>
    public static PacoteSmartClinic De(IReadOnlyDictionary<string, TabelaImportada> tabelas)
    {
        if (!tabelas.ContainsKey(Pacientes))
            throw new ArgumentException($"Falta o arquivo {Pacientes}.");
        return new PacoteSmartClinic(
            new Dictionary<string, TabelaImportada>(tabelas, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>());
    }
}
