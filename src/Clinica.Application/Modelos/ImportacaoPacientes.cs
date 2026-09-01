using System.Globalization;
using System.Text;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// A carteira reduzida ao que a importação precisa para reconhecer quem já está na base
/// (ver <c>IClinicaRepositorio.FichasResumidasAsync</c>). <see cref="Documento"/> vem
/// como foi GRAVADO — as linhas anteriores à normalização têm máscara.
/// </summary>
public sealed record FichaResumida(
    int Id, string Nome, string? Documento, DateOnly? DataNascimento, string? ChaveImportacao);

/// <summary>O arquivo exportado do sistema anterior, já lido: cabeçalho + linhas.</summary>
public sealed class TabelaImportada
{
    public required IReadOnlyList<string> Colunas { get; init; }
    public required IReadOnlyList<string[]> Linhas { get; init; }
    public required char Separador { get; init; }
    public required string Codificacao { get; init; }

    public string SeparadorRotulo => Separador switch
    {
        ';' => "ponto e vírgula",
        ',' => "vírgula",
        '\t' => "tabulação",
        _ => Separador.ToString()
    };
}

/// <summary>Os campos da FICHA que a importação sabe preencher.</summary>
public enum CampoImportacao
{
    /// <summary>O id do paciente NO SISTEMA ANTERIOR — vira a chave de idempotência.</summary>
    IdOrigem,
    Nome,
    Cpf,
    Telefone,
    DataNascimento,
    Sexo,
    Endereco,
    Convenio,
    Carteirinha,
    ValidadeCarteirinha,
    Origem,
    Observacoes
}

public static class CamposImportacao
{
    public static IReadOnlyList<CampoImportacao> Todos { get; } =
        Enum.GetValues<CampoImportacao>();

    public static string Rotulo(CampoImportacao campo) => campo switch
    {
        CampoImportacao.IdOrigem => "ID no sistema anterior",
        CampoImportacao.Nome => "Nome",
        CampoImportacao.Cpf => "CPF",
        CampoImportacao.Telefone => "Telefone / WhatsApp",
        CampoImportacao.DataNascimento => "Data de nascimento",
        CampoImportacao.Sexo => "Sexo",
        CampoImportacao.Endereco => "Endereço",
        CampoImportacao.Convenio => "Convênio",
        CampoImportacao.Carteirinha => "Nº da carteirinha",
        CampoImportacao.ValidadeCarteirinha => "Validade da carteirinha",
        CampoImportacao.Origem => "Como conheceu a clínica",
        CampoImportacao.Observacoes => "Observações",
        _ => campo.ToString()
    };

    /// <summary>O que acontece se a coluna NÃO for informada — a consequência ao lado do campo.</summary>
    public static string Dica(CampoImportacao campo) => campo switch
    {
        CampoImportacao.IdOrigem =>
            "Sem ele, importar o mesmo arquivo de novo só reconhece quem tem CPF.",
        CampoImportacao.Nome => "Obrigatório — linha sem nome não entra.",
        CampoImportacao.Cpf =>
            "É por ele que a ficha já existente é reconhecida e COMPLETADA em vez de duplicada.",
        CampoImportacao.Convenio =>
            "Cada nome de convênio do arquivo precisa apontar para um convênio cadastrado aqui.",
        CampoImportacao.Sexo => "Em branco, a ficha fica para conferir.",
        CampoImportacao.Origem => "Indicação, convênio, internet… — alimenta o relatório de origem.",
        _ => "Opcional."
    };
}

/// <summary>Qual coluna do arquivo alimenta cada campo (nulo = não importar).</summary>
public sealed class MapeamentoImportacao
{
    private readonly Dictionary<CampoImportacao, int> _colunas = new();

    public int? ColunaDe(CampoImportacao campo)
        => _colunas.TryGetValue(campo, out var i) ? i : null;

    public void Definir(CampoImportacao campo, int? coluna)
    {
        if (coluna is null) _colunas.Remove(campo);
        else _colunas[campo] = coluna.Value;
    }

    public bool Tem(CampoImportacao campo) => _colunas.ContainsKey(campo);
}

/// <summary>
/// Leitor de CSV sem biblioteca: o Smart Clinic exporta planilha, e a clínica salva como
/// CSV. Aceita aspas (com <c>""</c> escapado e quebra de linha dentro do campo), detecta
/// o separador (ponto e vírgula, vírgula ou tabulação) e a codificação (UTF-8 com ou sem
/// BOM; senão Latin-1, que é o que o Excel em português grava por padrão — sem isso
/// "Conceição" vira "Concei��o" na ficha).
/// </summary>
public static class LeitorCsv
{
    public static TabelaImportada Ler(byte[] bytes)
    {
        if (bytes.Length == 0) throw new ArgumentException("O arquivo está vazio.");

        string texto;
        string codificacao;
        var semBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? bytes.AsSpan(3).ToArray()
            : bytes;
        try
        {
            texto = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(semBom);
            codificacao = "UTF-8";
        }
        catch (DecoderFallbackException)
        {
            texto = Encoding.Latin1.GetString(semBom);
            codificacao = "ANSI (Latin-1)";
        }

        return Ler(texto, codificacao);
    }

    public static TabelaImportada Ler(string texto, string codificacao = "UTF-8", char? separador = null)
    {
        var registros = Dividir(texto, separador ?? DetectarSeparador(texto));
        var sep = separador ?? DetectarSeparador(texto);

        var naoVazios = registros
            .Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
            .ToList();
        if (naoVazios.Count == 0) throw new ArgumentException("O arquivo não tem linhas.");

        var colunas = naoVazios[0].Select(c => c.Trim()).ToArray();
        if (colunas.All(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A primeira linha do arquivo (o cabeçalho) está em branco.");

        var linhas = new List<string[]>();
        foreach (var r in naoVazios.Skip(1))
        {
            // Linha mais curta ou mais longa que o cabeçalho: ajusta em vez de recusar —
            // o Excel apara colunas vazias no fim, e recusar o arquivo inteiro por isso
            // faria a clínica editá-lo à mão.
            var linha = new string[colunas.Length];
            for (var i = 0; i < colunas.Length; i++)
                linha[i] = i < r.Count ? r[i].Trim() : string.Empty;
            linhas.Add(linha);
        }

        return new TabelaImportada
        {
            Colunas = colunas, Linhas = linhas, Separador = sep, Codificacao = codificacao
        };
    }

    /// <summary>O separador é o que mais aparece FORA de aspas na primeira linha.</summary>
    public static char DetectarSeparador(string texto)
    {
        var primeira = texto.Split('\n').FirstOrDefault() ?? string.Empty;
        var contagem = new Dictionary<char, int> { [';'] = 0, [','] = 0, ['\t'] = 0 };
        var dentro = false;
        foreach (var ch in primeira)
        {
            if (ch == '"') dentro = !dentro;
            else if (!dentro && contagem.ContainsKey(ch)) contagem[ch]++;
        }
        var melhor = contagem.OrderByDescending(kv => kv.Value).First();
        return melhor.Value == 0 ? ';' : melhor.Key;
    }

    private static List<List<string>> Dividir(string texto, char sep)
    {
        var registros = new List<List<string>>();
        var atual = new List<string>();
        var campo = new StringBuilder();
        var dentro = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var ch = texto[i];
            if (dentro)
            {
                if (ch == '"')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; }
                    else dentro = false;
                }
                else campo.Append(ch);
                continue;
            }

            if (ch == '"') dentro = true;
            else if (ch == sep) { atual.Add(campo.ToString()); campo.Clear(); }
            else if (ch == '\r') { /* o \n que vem a seguir fecha a linha */ }
            else if (ch == '\n')
            {
                atual.Add(campo.ToString()); campo.Clear();
                registros.Add(atual); atual = new List<string>();
            }
            else campo.Append(ch);
        }

        if (campo.Length > 0 || atual.Count > 0)
        {
            atual.Add(campo.ToString());
            registros.Add(atual);
        }
        return registros;
    }
}

/// <summary>
/// Sugere o mapeamento pelo NOME das colunas. É sugestão: a tela mostra e a direção
/// confirma — o arquivo de cada sistema chama as coisas de um jeito, e a lista abaixo
/// cobre o que o Smart Clinic e o Excel costumam escrever.
/// </summary>
public static class SugestorDeMapeamento
{
    private static readonly (CampoImportacao Campo, string[] Exatos, string[] Contem)[] Regras =
    [
        (CampoImportacao.Cpf, ["cpf", "documento", "cpfpaciente", "cpfdopaciente"], ["cpf"]),
        (CampoImportacao.DataNascimento,
            ["datadenascimento", "datanascimento", "nascimento", "dtnascimento", "dtnasc", "datanasc"],
            ["nascimento", "dtnasc"]),
        (CampoImportacao.ValidadeCarteirinha,
            ["validade", "validadedacarteirinha", "validadecarteirinha", "validadeconvenio", "validadedoconvenio"],
            ["validade"]),
        (CampoImportacao.Carteirinha,
            ["carteirinha", "carteira", "numerodacarteirinha", "numerocarteirinha", "ncarteirinha",
             "matriculaconvenio", "numerocarteira", "numerodoconvenio", "carteiradoconvenio"],
            ["carteir"]),
        (CampoImportacao.Convenio,
            ["convenio", "plano", "planodesaude", "operadora", "conveniomedico", "nomedoconvenio"],
            ["convenio", "plano"]),
        (CampoImportacao.Telefone,
            ["telefone", "celular", "whatsapp", "fone", "telefone1", "telefonecelular", "contato", "telcelular"],
            ["telefone", "celular", "whatsapp"]),
        (CampoImportacao.Endereco,
            ["endereco", "logradouro", "enderecocompleto", "rua"], ["endereco", "logradouro"]),
        (CampoImportacao.Sexo, ["sexo", "genero"], ["sexo", "genero"]),
        (CampoImportacao.Origem,
            ["origem", "comoconheceu", "indicacao", "comoconheceuaclinica", "canal", "comonosconheceu"],
            ["comoconheceu", "origem"]),
        (CampoImportacao.Observacoes,
            ["observacoes", "observacao", "obs", "anotacoes", "observacoesgerais"], ["observa", "anotac"]),
        (CampoImportacao.IdOrigem,
            ["id", "codigo", "cod", "idpaciente", "codigopaciente", "codigodopaciente", "matricula",
             "prontuario", "nprontuario", "registro", "idsmartclinic"],
            ["idpaciente", "codigopaciente"]),
        (CampoImportacao.Nome,
            ["nome", "nomecompleto", "paciente", "nomedopaciente", "nomepaciente"], ["nome"])
    ];

    public static MapeamentoImportacao Sugerir(IReadOnlyList<string> colunas)
    {
        var mapa = new MapeamentoImportacao();
        var usadas = new HashSet<int>();
        var normalizadas = colunas.Select(Normalizar).ToArray();

        // Exatos primeiro, para "Validade" não cair em "carteir" nem "Nome do convênio" em "nome".
        foreach (var (campo, exatos, _) in Regras)
        {
            var i = Array.FindIndex(normalizadas, n => exatos.Contains(n));
            if (i >= 0 && usadas.Add(i)) mapa.Definir(campo, i);
        }
        foreach (var (campo, _, contem) in Regras)
        {
            if (mapa.Tem(campo)) continue;
            var i = Array.FindIndex(normalizadas, n => contem.Any(n.Contains));
            if (i >= 0 && usadas.Add(i)) mapa.Definir(campo, i);
        }
        return mapa;
    }

    /// <summary>Sem acento, sem espaço, sem pontuação, minúsculas.</summary>
    public static string Normalizar(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposto)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>O que a importação vai fazer com a linha.</summary>
public enum DestinoLinha
{
    /// <summary>Ficha nova.</summary>
    Criar,
    /// <summary>Já existe (pelo CPF ou por nome + nascimento): só os campos VAZIOS são preenchidos.</summary>
    Completar,
    /// <summary>Já entrou por esta chave numa importação anterior — pulada.</summary>
    JaImportada,
    /// <summary>Não entra: a linha diz por quê.</summary>
    Problema
}

public sealed record LinhaPrevia(
    int Numero,
    string Nome,
    string? CpfFormatado,
    string? ConvenioTexto,
    DestinoLinha Destino,
    string Detalhe,
    IReadOnlyList<string> Avisos)
{
    /// <summary>A ficha montada (Criar) ou o que se vai completar (Completar). Nula nos outros.</summary>
    public Paciente? Ficha { get; init; }
    public int? PacienteExistenteId { get; init; }
    public string? Chave { get; init; }

    public bool EhCriar => Destino == DestinoLinha.Criar;
    public bool EhCompletar => Destino == DestinoLinha.Completar;
    public bool EhJaImportada => Destino == DestinoLinha.JaImportada;
    public bool EhProblema => Destino == DestinoLinha.Problema;
    public bool TemAvisos => Avisos.Count > 0;
    public string AvisosTexto => string.Join(" · ", Avisos);

    public string DestinoRotulo => Destino switch
    {
        DestinoLinha.Criar => "Nova ficha",
        DestinoLinha.Completar => "Completar ficha",
        DestinoLinha.JaImportada => "Já importada",
        DestinoLinha.Problema => "Não entra",
        _ => Destino.ToString()
    };
}

public sealed record PreviaImportacao(
    string Sistema,
    IReadOnlyList<LinhaPrevia> Linhas,
    IReadOnlyList<string> AvisosGerais)
{
    public int Criar => Linhas.Count(l => l.EhCriar);
    public int Completar => Linhas.Count(l => l.EhCompletar);
    public int JaImportadas => Linhas.Count(l => l.EhJaImportada);
    public int Problemas => Linhas.Count(l => l.EhProblema);
    public int ComAviso => Linhas.Count(l => l.TemAvisos && !l.EhProblema);

    /// <summary>Há o que gravar?</summary>
    public bool TemTrabalho => Criar + Completar > 0;
}

public sealed record ResultadoImportacao(
    int Criados, int Completados, int Pulados, IReadOnlyList<string> Erros)
{
    public bool TeveErro => Erros.Count > 0;
}
