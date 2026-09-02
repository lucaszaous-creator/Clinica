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
    /// <summary>Segundo telefone: vale quando o principal está vazio; senão vai para as observações.</summary>
    TelefoneAlternativo,
    DataNascimento,
    Sexo,
    /// <summary>Logradouro — ou o endereço inteiro, quando o arquivo traz numa célula só.</summary>
    Endereco,
    EnderecoNumero,
    EnderecoComplemento,
    Bairro,
    Cidade,
    Estado,
    Cep,
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
        CampoImportacao.Telefone => "Celular / WhatsApp",
        CampoImportacao.TelefoneAlternativo => "Outro telefone",
        CampoImportacao.DataNascimento => "Data de nascimento",
        CampoImportacao.Sexo => "Sexo",
        CampoImportacao.Endereco => "Endereço (rua)",
        CampoImportacao.EnderecoNumero => "Número",
        CampoImportacao.EnderecoComplemento => "Complemento",
        CampoImportacao.Bairro => "Bairro",
        CampoImportacao.Cidade => "Cidade",
        CampoImportacao.Estado => "UF",
        CampoImportacao.Cep => "CEP",
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
        CampoImportacao.Telefone => "O número que vai para o WhatsApp da ficha.",
        CampoImportacao.TelefoneAlternativo =>
            "Usado quando o celular está vazio; se os dois existem, vai para as observações.",
        CampoImportacao.Sexo => "Em branco, a ficha fica para conferir.",
        CampoImportacao.Endereco =>
            "As partes (número, bairro, cidade…) são juntadas numa linha só — é o endereço da receita.",
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
    /// <summary>
    /// A ordem DENTRO de cada lista é prioridade: "celular" vence "telefone" para o campo de
    /// WhatsApp, e "convenio" vence "plano". Medido contra a exportação real do Smart
    /// Clinic (set/2026): sem prioridade, o campo de convênio caía na coluna "operadora" —
    /// que ali é a operadora de TELEFONIA —, e a prévia oferecia 80 números de celular
    /// como nomes de convênio a mapear.
    /// </summary>
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
             "numeroconvenio", "numconvenio", "nconvenio", "numerodoconvenio", "matriculaconvenio",
             "numerocarteira", "carteiradoconvenio", "carteirinhaconvenio"],
            ["carteir"]),
        // "operadora" NÃO entra: no Smart Clinic é a operadora do celular.
        (CampoImportacao.Convenio,
            ["convenio", "planodesaude", "plano", "conveniomedico", "nomedoconvenio"],
            ["convenio", "plano"]),
        (CampoImportacao.Telefone,
            ["celular", "whatsapp", "telefonecelular", "telcelular", "celular1", "telefone", "fone",
             "telefone1", "contato"],
            ["celular", "whatsapp", "telefone"]),
        (CampoImportacao.TelefoneAlternativo,
            ["telefone", "telefonefixo", "telefoneresidencial", "telefone2", "celular2", "outrotelefone",
             "telefonecomercial", "fone2", "telefonealternativo"],
            ["telefone", "fone"]),
        (CampoImportacao.Endereco,
            ["endereco", "logradouro", "enderecocompleto", "rua"], ["endereco", "logradouro"]),
        (CampoImportacao.EnderecoNumero,
            ["numero", "num", "numeroendereco", "endereconumero", "nro"], []),
        (CampoImportacao.EnderecoComplemento,
            ["complemento", "enderecocomplemento", "compl"], ["complemento"]),
        (CampoImportacao.Bairro, ["bairro"], ["bairro"]),
        (CampoImportacao.Cidade, ["cidade", "municipio"], ["cidade", "municipio"]),
        // "estado" só exato: "estadocivil" contém a palavra e não é UF.
        (CampoImportacao.Estado, ["estado", "uf"], []),
        (CampoImportacao.Cep, ["cep"], ["cep"]),
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

        // Exatos primeiro, para "Validade" não cair em "carteir" nem "Nome do convênio" em
        // "nome" — e, dentro de cada campo, o PRIMEIRO sinônimo que existir no arquivo
        // vence, não a primeira coluna que casar com qualquer sinônimo.
        foreach (var (campo, exatos, _) in Regras)
        {
            foreach (var sinonimo in exatos)
            {
                var i = PrimeiroLivre(normalizadas, usadas, n => n == sinonimo);
                if (i < 0) continue;
                usadas.Add(i);
                mapa.Definir(campo, i);
                break;
            }
        }
        foreach (var (campo, _, contem) in Regras)
        {
            if (mapa.Tem(campo)) continue;
            foreach (var trecho in contem)
            {
                var i = PrimeiroLivre(normalizadas, usadas, n => n.Contains(trecho));
                if (i < 0) continue;
                usadas.Add(i);
                mapa.Definir(campo, i);
                break;
            }
        }
        return mapa;
    }

    private static int PrimeiroLivre(string[] colunas, HashSet<int> usadas, Func<string, bool> casa)
    {
        for (var i = 0; i < colunas.Length; i++)
            if (!usadas.Contains(i) && casa(colunas[i])) return i;
        return -1;
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

    /// <summary>
    /// A duplicata do PRÓPRIO arquivo (mesmo CPF de uma linha anterior — o sistema antigo
    /// tinha a pessoa duas vezes): entra FUNDIDA na ficha que a linha indicada produz, na
    /// mesma rodada. É o "completar" resolvido na execução, porque a ficha-alvo ainda não
    /// existe na prévia.
    /// </summary>
    public int? FundeNaLinha { get; init; }

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

    /// <summary>Número da linha do arquivo → id da ficha daqui, para TODA linha que
    /// terminou com uma ficha (criada, completada, já importada, fundida). É por aqui que o
    /// prontuário e a agenda do sistema anterior encontram o paciente.</summary>
    public IReadOnlyDictionary<int, int> IdPorLinha { get; init; } = new Dictionary<int, int>();
}
