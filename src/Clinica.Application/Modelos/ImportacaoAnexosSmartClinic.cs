using System.IO.Compression;

namespace Clinica.Application.Modelos;

/// <summary>
/// O ZIP DE ARQUIVOS do Smart Clinic (set/2026): a segunda exportação que a clínica
/// recebeu — uma pasta com centenas de PDFs (receitas, laudos, exames) nomeados pelo id
/// do arquivo, e um índice <c>relacao_arquivos.csv</c> que diz de QUAL paciente é cada um
/// (<c>id_arquivo, nome_paciente, id_paciente, titulo, data</c>). Medido no ZIP real: 756
/// PDFs de 113 pacientes, todos "Receita #número", de 2024 a 2026.
///
/// O pacote lê o índice e guarda os bytes por id — sem o índice não há como saber de quem é
/// o arquivo, então ele é obrigatório; PDF sem linha no índice é ignorado e DITO.
/// </summary>
public sealed class PacoteAnexosSmartClinic
{
    public const string Indice = "relacao_arquivos.csv";

    private readonly IReadOnlyDictionary<string, (string Nome, byte[] Bytes)> _porId;

    private PacoteAnexosSmartClinic(
        TabelaImportada relacao, IReadOnlyDictionary<string, (string Nome, byte[] Bytes)> porId,
        IReadOnlyList<string> semLinhaNoIndice)
    {
        Relacao = relacao;
        _porId = porId;
        SemLinhaNoIndice = semLinhaNoIndice;
    }

    /// <summary>O índice, como veio.</summary>
    public TabelaImportada Relacao { get; }

    /// <summary>Quantos arquivos há no ZIP (fora o índice).</summary>
    public int Arquivos => _porId.Count;

    public long BytesTotais => _porId.Values.Sum(a => (long)a.Bytes.Length);

    /// <summary>Arquivos do ZIP que o índice não menciona — não entram, e a prévia diz.</summary>
    public IReadOnlyList<string> SemLinhaNoIndice { get; }

    /// <summary>O arquivo pelo id (o nome sem extensão), ou nulo quando não está no ZIP.</summary>
    public (string Nome, byte[] Bytes)? Arquivo(string idArquivo)
        => _porId.TryGetValue(idArquivo.Trim(), out var a) ? a : null;

    public static PacoteAnexosSmartClinic Abrir(byte[] zip)
    {
        using var memoria = new MemoryStream(zip);
        using var arquivo = new ZipArchive(memoria, ZipArchiveMode.Read);

        TabelaImportada? relacao = null;
        var porId = new Dictionary<string, (string Nome, byte[] Bytes)>(StringComparer.OrdinalIgnoreCase);
        var orfaos = new List<string>();

        foreach (var entrada in arquivo.Entries)
        {
            if (entrada.Name.Length == 0) continue; // pasta
            using var leitura = entrada.Open();
            using var buffer = new MemoryStream();
            leitura.CopyTo(buffer);
            var bytes = buffer.ToArray();

            if (string.Equals(entrada.Name, Indice, StringComparison.OrdinalIgnoreCase))
            {
                relacao = LeitorCsv.Ler(bytes);
                continue;
            }
            var id = Path.GetFileNameWithoutExtension(entrada.Name);
            if (id.Length == 0) continue;
            porId[id] = (entrada.Name, bytes);
        }

        if (relacao is null)
            throw new ArgumentException(
                $"O ZIP não tem o índice {Indice} — sem ele não há como saber de qual paciente é cada arquivo.");
        foreach (var faltando in new[] { "id_arquivo", "id_paciente" })
            if (!relacao.Colunas.Any(c => string.Equals(c.Trim(), faltando, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"O índice {Indice} não tem a coluna {faltando} que o Smart Clinic exporta.");

        var mencionados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var colId = relacao.Colunas.ToList().FindIndex(c => string.Equals(c.Trim(), "id_arquivo", StringComparison.OrdinalIgnoreCase));
        foreach (var linha in relacao.Linhas)
            if (colId < linha.Length && !string.IsNullOrWhiteSpace(linha[colId])) mencionados.Add(linha[colId].Trim());
        foreach (var id in porId.Keys)
            if (!mencionados.Contains(id)) orfaos.Add(porId[id].Nome);

        return new PacoteAnexosSmartClinic(relacao, porId, orfaos);
    }
}

/// <summary>O que vai acontecer com cada linha do índice.</summary>
public enum DestinoAnexo
{
    /// <summary>Entra na ficha do paciente.</summary>
    Novo,
    /// <summary>Já está no sistema (pela chave de importação) — pulado.</summary>
    JaImportado,
    /// <summary>Não há ficha para o id/nome do paciente — importe o pacote de pacientes antes.</summary>
    SemPaciente,
    /// <summary>O índice cita um arquivo que não está no ZIP.</summary>
    SemArquivo,
    /// <summary>Linha sem id, ou arquivo maior que o teto.</summary>
    Invalido
}

public sealed record LinhaAnexoPrevia(
    int Numero, string IdArquivo, string Paciente, string Titulo, DateOnly Data,
    DestinoAnexo Destino, string? Detalhe, int? PacienteId, string? NomeNoZip, string? DataOriginal)
{
    public bool EhProblema => Destino is DestinoAnexo.SemPaciente or DestinoAnexo.SemArquivo or DestinoAnexo.Invalido;
}

/// <summary>A prévia do ZIP de arquivos — nada gravado.</summary>
public sealed record PreviaAnexosSmartClinic(
    IReadOnlyList<LinhaAnexoPrevia> Linhas,
    int Novos, int JaImportados, int SemPaciente, int SemArquivo, int Invalidos,
    int PacientesQueRecebem, long BytesNovos,
    IReadOnlyList<string> Avisos)
{
    public bool TemTrabalho => Novos > 0;
    public int Problemas => SemPaciente + SemArquivo + Invalidos;
}

public sealed record ResultadoAnexosSmartClinic(int Criados, int Pulados, int SemPaciente, IReadOnlyList<string> Erros)
{
    public bool TeveErro => Erros.Count > 0;
}

/// <summary>
/// A conferência do ZIP de arquivos: a releitura do mesmo ZIP contra o banco responde
/// "funcionou?" pela chave de cada arquivo — a mesma prova da importação do pacote.
/// </summary>
public static class ConferenciaAnexosSmartClinic
{
    public static IReadOnlyList<ItemConferencia> Montar(PreviaAnexosSmartClinic releitura)
    {
        var motivos = new List<string>();
        if (releitura.Novos > 0)
            motivos.Add($"{releitura.Novos} arquivo(s) ainda não gravado(s) — importe de novo");
        foreach (var l in releitura.Linhas.Where(l => l.EhProblema).Take(30))
            motivos.Add($"linha {l.Numero} ({l.Paciente}, {l.Titulo}): {l.Detalhe}");
        if (releitura.Problemas > 30)
            motivos.Add($"… e mais {releitura.Problemas - 30} linha(s) de fora (veja a prévia)");

        return
        [
            new ItemConferencia("Arquivos da ficha (relacao_arquivos.csv)",
                releitura.Linhas.Count, releitura.JaImportados, motivos, 0)
        ];
    }

    public static bool Fechou(IReadOnlyList<ItemConferencia> itens) => ConferenciaSmartClinic.Fechou(itens);
}
