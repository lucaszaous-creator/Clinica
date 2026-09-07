using System.Security.Cryptography;
using Clinica.Domain.Entities;

namespace Clinica.Domain;

/// <summary>
/// A MÍDIA DO PRONTUÁRIO (set/2026): o vídeo da marcha, o áudio da ausculta — o registro
/// que o texto da evolução não substitui.
///
/// O problema
/// ----------
/// O anexo de prontuário guarda os bytes NO BANCO, com teto de 10 MB, e o teto não é
/// capricho: o banco é remoto, e um anexo gigante trava a sincronização de todo mundo com
/// o erro aparecendo longe da causa. Um vídeo de quarenta segundos passa disso sem
/// esforço — então o profissional gravava no celular, mandava por WhatsApp e o registro
/// clínico ficava fora do prontuário: sem guarda, sem trilha de acesso e num aplicativo
/// que a clínica não controla.
///
/// A decisão
/// ---------
/// O arquivo grande vai para o armazenamento que a clínica JÁ tem (o mesmo S3-compatível
/// da publicação de receitas), e a linha continua no banco. O que muda de tudo o que já
/// existe ali é o VERBO: <c>GuardarPrivadoAsync</c>, sem ACL pública.
///
/// ⚠️ <b>Mídia clínica NÃO vira URL pública.</b> A receita publicada abre para um
/// farmacêutico ANÔNIMO porque é o desenho inteiro dela — a barreira ali é um token de
/// 128 bits e a decisão está escrita. Um vídeo do paciente é dado de saúde (art. 5º, II
/// da LGPD): endereço "inadivinhável" vaza por print, por histórico e por encaminhamento
/// de mensagem, e o dia em que vazar não há como saber quem baixou. Quem lê a mídia é o
/// APP, com credencial, e o caminho inadivinhável é a segunda tranca — nunca a primeira.
///
/// ⚠️ <b>Ponto 10 do compromisso de conformidade.</b> Dado clínico em serviço externo é
/// transferência internacional (art. 33) até que se prove o contrário — e a prova aqui é
/// a mesma da publicação: o provedor é escolhido pela clínica em Configurações, e a
/// orientação escrita na tela é usar região no BRASIL. O sistema não pode garantir onde o
/// balde fica; o que ele faz é não esconder a pergunta.
/// </summary>
public static class MidiaClinica
{
    /// <summary>
    /// Teto da mídia guardada no armazenamento remoto: 200 MB.
    ///
    /// Não é o teto do banco (10 MB), e a diferença é a razão da feature. O número sai do
    /// que a clínica de fato produz — um vídeo de 1080p de um minuto no celular fica entre
    /// 60 e 130 MB — com folga para um exame mais longo. Acima disso a pergunta é outra:
    /// não é registro de sessão, é acervo, e acervo não sobe pelo balcão.
    ///
    /// Cabe em <c>int</c> de propósito: <see cref="AnexoProntuario.Tamanho"/> é int, e
    /// alargar a coluna seria migration não aditiva em tabela clínica.
    /// </summary>
    public const int TamanhoMaximo = 200 * 1024 * 1024;

    /// <summary>
    /// Acima de quantos bytes o arquivo DEIXA de caber no banco e precisa do remoto. É o
    /// teto do anexo comum: abaixo dele nada muda — o anexo continua nascendo no banco,
    /// que não depende de a clínica ter contratado armazenamento nenhum.
    /// </summary>
    public const int LimiteDoBanco = 10 * 1024 * 1024;

    /// <summary>Comprimento do token do caminho — o mesmo da publicação (128 bits).</summary>
    public const int TamanhoToken = PublicacaoDocumento.TamanhoToken;

    /// <summary>
    /// O caminho do objeto. Prefixo <c>m/</c> — separado do <c>r/</c> das receitas de
    /// propósito: são coisas com regras opostas (uma pública com prazo, outra privada e
    /// guardada 20 anos), e uma varredura de expiração que confundisse as duas apagaria
    /// registro clínico. Um nível de pasta pelo prefixo do token para o balde não virar um
    /// diretório de milhares de irmãos.
    /// </summary>
    public static string CaminhoDoObjeto(string token, string extensao)
        => $"m/{token[..2]}/{token}{Extensao(extensao)}";

    /// <summary>
    /// Sorteia o token do caminho. <see cref="RandomNumberGenerator"/>, nunca
    /// <see cref="Random"/>: o caminho é a segunda tranca, e tranca previsível não é
    /// tranca.
    /// </summary>
    public static string GerarToken() => PublicacaoDocumento.GerarToken();

    /// <summary>
    /// A extensão saneada — só letras e dígitos, minúscula, no máximo 8. O nome do arquivo
    /// vem de fora: deixá-lo compor o caminho do objeto cru é como se escreve um caminho
    /// com "../" dentro. O nome ORIGINAL fica na linha do banco, que é onde ele serve.
    /// </summary>
    private static string Extensao(string? nomeOuExtensao)
    {
        if (string.IsNullOrWhiteSpace(nomeOuExtensao)) return string.Empty;

        var bruta = nomeOuExtensao.Contains('.')
            ? nomeOuExtensao[(nomeOuExtensao.LastIndexOf('.') + 1)..]
            : nomeOuExtensao;

        var limpa = new string(bruta.Where(char.IsLetterOrDigit).Take(8).ToArray()).ToLowerInvariant();
        return limpa.Length == 0 ? string.Empty : "." + limpa;
    }

    /// <summary>
    /// O tipo de anexo que o arquivo é, pelo MIME — para a lista do prontuário mostrar
    /// "vídeo" sem depender de alguém escolher no combo. Desconhecido continua
    /// <see cref="TipoAnexo.Documento"/>: adivinhar mais do que se sabe seria rotular de
    /// vídeo um PDF que a clínica anexou.
    /// </summary>
    public static TipoAnexo TipoDe(string? tipoConteudo, string? nomeArquivo = null)
    {
        var mime = tipoConteudo?.Trim().ToLowerInvariant() ?? string.Empty;
        if (mime.StartsWith("video/")) return TipoAnexo.Video;
        if (mime.StartsWith("audio/")) return TipoAnexo.Audio;
        if (mime.StartsWith("image/")) return TipoAnexo.Imagem;

        var nome = nomeArquivo?.Trim().ToLowerInvariant() ?? string.Empty;
        if (VideosConhecidos.Any(nome.EndsWith)) return TipoAnexo.Video;
        if (AudiosConhecidos.Any(nome.EndsWith)) return TipoAnexo.Audio;
        if (ImagensConhecidas.Any(nome.EndsWith)) return TipoAnexo.Imagem;

        return TipoAnexo.Documento;
    }

    /// <summary>
    /// O MIME pelo nome do arquivo, para o anexo nascer com o tipo certo sem depender de
    /// alguém escolher num combo. Desconhecido devolve NULO, nunca um palpite: MIME errado
    /// gravado é o que faz o navegador do celular baixar o vídeo em vez de tocá-lo, e
    /// corrigir depois exige mexer no registro.
    /// </summary>
    public static string? MimeDe(string? nomeArquivo)
    {
        var nome = nomeArquivo?.Trim().ToLowerInvariant() ?? string.Empty;
        var ponto = nome.LastIndexOf('.');
        if (ponto < 0) return null;

        return nome[(ponto + 1)..] switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            "gif" => "image/gif",
            "heic" => "image/heic",
            "pdf" => "application/pdf",
            "mp4" or "m4v" => "video/mp4",
            "mov" => "video/quicktime",
            "webm" => "video/webm",
            "avi" => "video/x-msvideo",
            "mkv" => "video/x-matroska",
            "mp3" => "audio/mpeg",
            "m4a" => "audio/mp4",
            "wav" => "audio/wav",
            "ogg" => "audio/ogg",
            "aac" => "audio/aac",
            _ => null
        };
    }

    private static readonly string[] VideosConhecidos = [".mp4", ".mov", ".m4v", ".webm", ".avi", ".mkv"];
    private static readonly string[] AudiosConhecidos = [".mp3", ".m4a", ".wav", ".ogg", ".aac"];
    private static readonly string[] ImagensConhecidas = [".jpg", ".jpeg", ".png", ".webp", ".heic", ".gif"];

    /// <summary>
    /// O tamanho como a tela o escreve — "1,4 MB", "820 KB". Mora aqui porque a lista da
    /// sessão, a da ficha e a exportação escrevem a mesma coisa, e três formatações
    /// divergiriam na primeira correção.
    /// </summary>
    public static string TamanhoLegivel(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };
}
