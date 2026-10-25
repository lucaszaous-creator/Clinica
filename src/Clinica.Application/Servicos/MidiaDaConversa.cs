using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>O que uma mensagem do WhatsApp carrega além do texto.</summary>
public enum TipoDeMidia { Imagem, Audio, Video, Documento }

/// <summary>
/// A mídia de UMA mensagem da conversa, como a tela a mostra: o tipo, o rótulo ("Foto", "Áudio"),
/// o nome que o CRM deu (documento costuma ter; foto e nota de voz, não) e a mensagem, que decide
/// a rota (<see cref="MidiaDaConversa.Rota"/>). <see cref="Chave"/> identifica o arquivo na memória da tela.
/// </summary>
public sealed record MidiaDaMensagem(TipoDeMidia Tipo, string Rotulo, string? Nome, string ConversaId, AppMessage Mensagem)
{
    public string Chave => ConversaId + "/" + Mensagem.Id;

    public bool Recebida => string.Equals(Mensagem.Direction, "in", StringComparison.OrdinalIgnoreCase);

    /// <summary>Foto e áudio chegam sozinhos ao abrir a conversa (são pequenos e é o que se olha primeiro); vídeo e documento, só quando a pessoa pede.</summary>
    public bool CarregaSozinha => Tipo is TipoDeMidia.Imagem or TipoDeMidia.Audio;

    /// <summary>
    /// Figurinha do WhatsApp (<c>AppMessage.Sticker</c>): é uma imagem — carrega, baixa e falha como
    /// foto —, mas a tela a desenha pequena, sem balão e sem "Ampliar", como o WhatsApp.
    /// </summary>
    public bool Figurinha => Tipo == TipoDeMidia.Imagem && Mensagem.Sticker;
}

/// <summary>
/// O arquivo já trazido do CRM, pronto para a tela: o <see cref="Endereco"/> é o <c>data:</c> que a
/// foto, o áudio e o vídeo usam (nulo quando o formato não se mostra — aí só se baixa), e a duração,
/// quando o arquivo a diz (OGG da nota de voz, WAV).
/// </summary>
public sealed record MidiaCarregada(ArquivoDaMidia Arquivo, string? Endereco, double? Duracao)
{
    /// <summary>Quanto ela ocupa na memória: os bytes e o texto do endereço (UTF-16, dois bytes por letra).</summary>
    public long Ocupa => Arquivo.Conteudo.LongLength + (Endereco?.Length ?? 0) * 2L;
}

/// <summary>
/// A MÍDIA DO WHATSAPP NA TELA (24/10/2026, pedido da direção: "ver imagens, ouvir áudios e baixar
/// itens enviados dentro da tela"). Tudo o que a tela decide sobre o arquivo de uma mensagem mora
/// aqui, com teste — a rota no CRM, o tipo, o teto, os rótulos, o nome para salvar — e o Razor só
/// desenha. As rotas e o teto são os do CRM (semdor-crm, <c>Program.cs</c> e <c>MediaStore</c>).
/// </summary>
public static class MidiaDaConversa
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O teto de um arquivo (o <c>MediaStore.Limit</c> do CRM, 16 MB): o CRM não guarda nada maior.</summary>
    public const int Teto = 16 * 1024 * 1024;

    /// <summary>Quanto a tela guarda na memória entre as releituras (a lista volta a cada 7 s e a mídia não pode ser baixada de novo a cada uma).</summary>
    public const long TetoDaMemoria = 64L * 1024 * 1024;

    /// <summary>A gravação mais longa, em segundos — o mesmo limite do CRM web (<c>reception-media.js</c>).</summary>
    public const int DuracaoMaximaDaGravacao = 120;

    /// <summary>O tipo pelo <c>AppMessage.Type</c> do CRM; nulo para texto, modelo e botão.</summary>
    public static TipoDeMidia? Tipo(string? tipo) => tipo?.Trim().ToUpperInvariant() switch
    {
        "IMAGE" => TipoDeMidia.Imagem,
        "AUDIO" => TipoDeMidia.Audio,
        "VIDEO" => TipoDeMidia.Video,
        "DOCUMENT" => TipoDeMidia.Documento,
        _ => null
    };

    /// <summary>"Foto", "Áudio", "Vídeo", "Documento" — as palavras do WhatsApp.</summary>
    public static string Rotulo(TipoDeMidia tipo) => tipo switch
    {
        TipoDeMidia.Imagem => "Foto",
        TipoDeMidia.Audio => "Áudio",
        TipoDeMidia.Video => "Vídeo",
        _ => "Documento"
    };

    /// <summary>
    /// A mídia da mensagem, ou nulo quando ela é só texto. Uma saída com <c>AttachmentId</c> que não
    /// diz o tipo (o documento que segue um modelo) é documento: o arquivo existe e a rota também.
    /// </summary>
    public static MidiaDaMensagem? Da(string conversaId, AppMessage m)
    {
        var tipo = Tipo(m.Type) ?? (string.IsNullOrWhiteSpace(m.AttachmentId) ? null : TipoDeMidia.Documento);
        if (tipo is not { } t) return null;
        var nome = string.IsNullOrWhiteSpace(m.FileName) ? null : m.FileName.Trim();
        return new MidiaDaMensagem(t, t == TipoDeMidia.Imagem && m.Sticker ? "Figurinha" : Rotulo(t), nome, conversaId, m);
    }

    /// <summary>
    /// A rota do arquivo no CRM, sempre com <c>download=true</c> (a resposta traz o nome no
    /// Content-Disposition — o mesmo download serve para mostrar e para salvar). A RECEBIDA é
    /// <c>/api/conversations/{id}/media/{messageId}</c> (o CRM só a acha com Direction "in"); a ENVIADA,
    /// <c>/api/attachments/{attachmentId}</c>. Nulo quando a mensagem não tem arquivo que o CRM sirva
    /// (a saída automática sem anexo, o texto).
    /// </summary>
    public static string? Rota(string conversaId, AppMessage m)
    {
        if (Da(conversaId, m) is null) return null;
        if (!string.IsNullOrWhiteSpace(m.AttachmentId))
            return "/api/attachments/" + Uri.EscapeDataString(m.AttachmentId) + "?download=true";
        if (!string.Equals(m.Direction, "in", StringComparison.OrdinalIgnoreCase)) return null;
        return "/api/conversations/" + Uri.EscapeDataString(conversaId) + "/media/" + Uri.EscapeDataString(m.Id) + "?download=true";
    }

    /// <summary>A frase de quando a mensagem não tem arquivo que o CRM sirva (ver <see cref="Rota"/>).</summary>
    public const string SemArquivo = "Esta mensagem não tem um arquivo que o CRM possa mostrar.";

    /// <summary>A frase de quando o arquivo passa do teto.</summary>
    public const string GrandeDemais = "O arquivo passa de 16 MB, o limite do WhatsApp. Abra a conversa no CRM pelo navegador.";

    /// <summary>O tipo sem os parâmetros e em minúsculas ("audio/ogg; codecs=opus" → "audio/ogg").</summary>
    public static string MimeLimpo(string? mime)
    {
        var m = (mime ?? "").Split(';', 2)[0].Trim().ToLowerInvariant();
        return m.Length == 0 ? "application/octet-stream" : m;
    }

    /// <summary>
    /// O <c>data:</c> que o navegador da tela mostra, ou nulo quando o arquivo não é do tipo que a
    /// mensagem diz (um PDF numa mensagem de foto não vira <c>&lt;img&gt;</c>; a pessoa baixa).
    /// </summary>
    public static string? Endereco(TipoDeMidia tipo, ArquivoDaMidia a)
    {
        var mime = MimeLimpo(a.Mime);
        var mostra = tipo switch
        {
            TipoDeMidia.Imagem => mime is "image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/bmp",
            TipoDeMidia.Audio => mime.StartsWith("audio/", StringComparison.Ordinal) || mime == "video/webm",
            TipoDeMidia.Video => mime.StartsWith("video/", StringComparison.Ordinal),
            _ => false
        };
        return mostra && a.Conteudo.Length > 0 ? "data:" + mime + ";base64," + Convert.ToBase64String(a.Conteudo) : null;
    }

    /// <summary>O arquivo pronto para a tela: o endereço e a duração (do áudio).</summary>
    public static MidiaCarregada Preparar(MidiaDaMensagem midia, ArquivoDaMidia a)
        => new(a, Endereco(midia.Tipo, a), midia.Tipo == TipoDeMidia.Audio ? Duracao(a.Conteudo, a.Mime) : null);

    // ---------------------------------------------------------------- frases

    /// <summary>"0:42", "12:05", "1:02:03". Vazio quando não se sabe.</summary>
    public static string Tempo(double? segundos)
    {
        if (segundos is not { } s || double.IsNaN(s) || double.IsInfinity(s) || s < 0) return "";
        var t = TimeSpan.FromSeconds(Math.Round(s));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    /// <summary>"Áudio 0:42", ou só "Áudio" quando a duração não se sabe.</summary>
    public static string RotuloDoAudio(double? segundos)
        => Tempo(segundos) is { Length: > 0 } t ? "Áudio " + t : "Áudio";

    /// <summary>"820 bytes", "12 KB", "1,4 MB".</summary>
    public static string Tamanho(long bytes)
    {
        if (bytes < 1024) return bytes == 1 ? "1 byte" : $"{Math.Max(0, bytes)} bytes";
        if (bytes < 1024 * 1024) return $"{Math.Round(bytes / 1024d, MidpointRounding.AwayFromZero).ToString("0", PtBr)} KB";
        return $"{(bytes / (1024d * 1024d)).ToString("0.#", PtBr)} MB";
    }

    /// <summary>A frase da falha de carregar ("Não foi possível carregar a foto: …").</summary>
    public static string Falha(TipoDeMidia tipo, string motivo) => Falha(tipo, motivo, null);

    /// <summary>A frase da falha de carregar a mídia desta mensagem (a figurinha diz "a figurinha").</summary>
    public static string Falha(MidiaDaMensagem midia, string motivo) => Falha(midia.Tipo, motivo, midia.Figurinha ? "a figurinha" : null);

    private static string Falha(TipoDeMidia tipo, string motivo, string? objeto)
    {
        var o = objeto ?? tipo switch { TipoDeMidia.Imagem => "a foto", TipoDeMidia.Audio => "o áudio", TipoDeMidia.Video => "o vídeo", _ => "o documento" };
        var frase = (motivo ?? "").Trim();
        return frase.Length == 0 ? $"Não foi possível carregar {o}." : $"Não foi possível carregar {o}: {frase}";
    }

    /// <summary>A sigla no ícone do documento ("PDF", "DOCX"), pelo nome e, sem extensão, pelo tipo; "DOC" quando não se sabe.</summary>
    public static string Sigla(string? nome, string? mime)
    {
        var ext = Path.GetExtension(nome ?? "");
        if (string.IsNullOrEmpty(ext) || ext.Length < 2) ext = Extensao(mime);
        var sigla = ext.TrimStart('.').ToUpperInvariant();
        return sigla.Length is > 0 and <= 4 && sigla.All(char.IsAsciiLetterOrDigit) ? sigla : "DOC";
    }

    /// <summary>
    /// A linha de baixo do cartão e o subtítulo da foto ampliada: "Foto recebida · 214 KB",
    /// "PDF enviado · 12 KB", "Áudio recebido · 0:06 · 102 KB". Sem o arquivo ainda, só o que se
    /// sabe ("Vídeo recebido").
    /// </summary>
    public static string Descricao(MidiaDaMensagem midia, MidiaCarregada? carregada)
    {
        var (nome, feminino) = midia.Tipo switch
        {
            TipoDeMidia.Imagem => (midia.Figurinha ? "Figurinha" : "Foto", true),
            TipoDeMidia.Audio => ("Áudio", false),
            TipoDeMidia.Video => ("Vídeo", false),
            _ => (Sigla(midia.Nome ?? carregada?.Arquivo.Nome, carregada?.Arquivo.Mime) is var s && s != "DOC" ? s : "Documento", false)
        };
        var partes = new List<string> { $"{nome} {(midia.Recebida ? (feminino ? "recebida" : "recebido") : (feminino ? "enviada" : "enviado"))}" };
        if (Tempo(carregada?.Duracao) is { Length: > 0 } tempo) partes.Add(tempo);
        if (carregada is not null) partes.Add(Tamanho(carregada.Arquivo.Conteudo.LongLength));
        return string.Join(" · ", partes);
    }

    /// <summary>A prévia da lista quando a última mensagem é só mídia: o CRM manda o tipo cru ("AUDIO").</summary>
    public static string PreviaDaLista(string? previa)
    {
        var p = (previa ?? "").Trim();
        return Tipo(p) is { } t && p.Equals(p.ToUpperInvariant(), StringComparison.Ordinal) ? Rotulo(t) : p;
    }

    // ---------------------------------------------------------------- salvar

    /// <summary>A extensão pelo tipo; vazio quando não se conhece.</summary>
    public static string Extensao(string? mime) => MimeLimpo(mime) switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "audio/ogg" or "audio/opus" => ".ogg",
        "audio/mpeg" or "audio/mp3" => ".mp3",
        "audio/mp4" or "audio/x-m4a" => ".m4a",
        "audio/aac" => ".aac",
        "audio/amr" => ".amr",
        "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
        "audio/webm" => ".webm",
        "video/mp4" => ".mp4",
        "video/3gpp" => ".3gp",
        "video/webm" => ".webm",
        "application/pdf" => ".pdf",
        "application/msword" => ".doc",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "application/vnd.ms-excel" => ".xls",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "text/plain" => ".txt",
        _ => ""
    };

    // Os mesmos nos dois sistemas: Path.GetInvalidFileNameChars muda entre o Windows e o Linux dos testes.
    private const string ProibidosNoNome = "\\/:*?\"<>|";

    /// <summary>
    /// O nome sugerido na janela de salvar: o do CRM, limpo; sem ele, o rótulo e a hora da mensagem
    /// ("foto-2026-10-23-1012.jpg"). A extensão vem do tipo quando o nome não tem.
    /// </summary>
    public static string NomeParaSalvar(MidiaDaMensagem midia, ArquivoDaMidia arquivo)
    {
        var limpo = Limpar(arquivo.Nome) ?? Limpar(midia.Nome);
        var extensao = Extensao(arquivo.Mime);
        if (limpo is null)
        {
            var quando = midia.Mensagem.At.ToLocalTime();
            var rotulo = midia.Tipo switch { TipoDeMidia.Imagem => midia.Figurinha ? "figurinha" : "foto", TipoDeMidia.Audio => "audio", TipoDeMidia.Video => "video", _ => "documento" };
            return $"{rotulo}-{quando:yyyy-MM-dd-HHmm}{(extensao.Length > 0 ? extensao : ".bin")}";
        }
        return Path.HasExtension(limpo) || extensao.Length == 0 ? limpo : limpo + extensao;
    }

    private static string? Limpar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;
        var sb = new StringBuilder(nome.Length);
        foreach (var c in nome.Trim().Trim('"'))
            sb.Append(char.IsControl(c) || ProibidosNoNome.Contains(c) ? '-' : c);
        var s = sb.ToString().Trim().TrimStart('.');
        if (s.Length > 120) s = s[..120];
        return s.Length == 0 ? null : s;
    }

    /// <summary>O filtro da janela de salvar ("Arquivo PDF (*.pdf)|*.pdf") e a extensão que ela completa.</summary>
    public static (string Filtro, string Extensao) FiltroParaSalvar(string nome)
    {
        var ext = Path.GetExtension(nome);
        if (string.IsNullOrEmpty(ext) || ext.Length < 2) return ("Todos os arquivos (*.*)|*.*", "");
        return ($"Arquivo {ext[1..].ToUpperInvariant()} (*{ext})|*{ext}", ext);
    }

    // ---------------------------------------------------------------- gravação

    /// <summary>O nome do arquivo da gravação que sobe ao CRM (ele o converte para OGG/Opus e dá o nome final).</summary>
    public static string NomeDaGravacao(string? mime)
        => "gravacao" + (Extensao(mime) is { Length: > 0 } e ? e : ".webm");

    // ---------------------------------------------------------------- duração

    /// <summary>
    /// Quanto dura o áudio, lido do próprio arquivo: OGG (a nota de voz do WhatsApp, Opus a 48 kHz,
    /// ou Vorbis) pela posição da última página, e WAV pelo tamanho dos dados. Nulo para os outros
    /// formatos — aí quem diz é o tocador do navegador.
    /// </summary>
    public static double? Duracao(byte[] conteudo, string? mime)
    {
        try
        {
            var b = conteudo.AsSpan();
            if (b.Length > 12 && b.StartsWith("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WAVE"u8)) return DuracaoWav(b);
            if (b.StartsWith("OggS"u8)) return DuracaoOgg(b);
            return null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static double? DuracaoWav(ReadOnlySpan<byte> b)
    {
        uint taxa = 0;
        for (var i = 12; i + 8 <= b.Length;)
        {
            var id = b.Slice(i, 4);
            var tamanho = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(i + 4, 4));
            if (id.SequenceEqual("fmt "u8) && i + 20 <= b.Length) taxa = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(i + 16, 4));
            if (id.SequenceEqual("data"u8)) return taxa == 0 ? null : Math.Min(tamanho, (uint)(b.Length - i - 8)) / (double)taxa;
            i += 8 + (int)Math.Min(tamanho + (tamanho & 1), int.MaxValue - 16);
        }
        return null;
    }

    private static double? DuracaoOgg(ReadOnlySpan<byte> b)
    {
        // A taxa e o pre-skip vêm do cabeçalho do codec, no primeiro pacote.
        double taxa;
        long preSkip = 0;
        var opus = b.IndexOf("OpusHead"u8);
        var vorbis = b.IndexOf("\u0001vorbis"u8);
        if (opus >= 0 && (vorbis < 0 || opus < vorbis))
        {
            taxa = 48000; // Opus conta sempre a 48 kHz, seja qual for a taxa de entrada.
            preSkip = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(opus + 10, 2));
        }
        else if (vorbis >= 0)
        {
            taxa = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(vorbis + 12, 4));
            if (taxa <= 0) return null;
        }
        else return null;

        // A última página com posição válida (granule ≠ -1): é o número de amostras do fim.
        for (var i = b.LastIndexOf("OggS"u8); i >= 0; i = b[..i].LastIndexOf("OggS"u8))
        {
            if (i + 14 > b.Length) continue;
            var granulo = BinaryPrimitives.ReadInt64LittleEndian(b.Slice(i + 6, 8));
            if (granulo > 0) return Math.Max(0, granulo - preSkip) / taxa;
        }
        return null;
    }
}

/// <summary>
/// A MEMÓRIA da tela para a mídia já trazida do CRM: a lista e a conversa são relidas a cada 7 s, e
/// a foto não pode ser baixada de novo a cada releitura. Guarda até <see cref="Teto"/> bytes
/// (contando o <c>data:</c>) e, cheia, descarta a usada há mais tempo. Um arquivo maior que o teto
/// inteiro não entra (é mostrado e esquecido). Segura para chamadas de mais de uma thread.
/// </summary>
public sealed class MemoriaDeMidia(long teto = MidiaDaConversa.TetoDaMemoria)
{
    private readonly object trava = new();
    private readonly LinkedList<(string Chave, MidiaCarregada Midia)> ordem = new();
    private readonly Dictionary<string, LinkedListNode<(string Chave, MidiaCarregada Midia)>> porChave = new(StringComparer.Ordinal);
    private long total;

    public long Teto { get; } = teto;

    /// <summary>Quanto está guardado agora.</summary>
    public long Total { get { lock (trava) return total; } }

    public int Quantidade { get { lock (trava) return porChave.Count; } }

    /// <summary>A mídia guardada (e ela passa a ser a usada mais recentemente), ou nulo.</summary>
    public MidiaCarregada? Ler(string chave)
    {
        lock (trava)
        {
            if (!porChave.TryGetValue(chave, out var no)) return null;
            ordem.Remove(no);
            ordem.AddLast(no);
            return no.Value.Midia;
        }
    }

    /// <summary>Guarda (ou troca) e descarta as mais antigas até caber.</summary>
    public void Guardar(string chave, MidiaCarregada midia)
    {
        lock (trava)
        {
            if (porChave.Remove(chave, out var antigo))
            {
                ordem.Remove(antigo);
                total -= antigo.Value.Midia.Ocupa;
            }
            if (midia.Ocupa > Teto) return;
            while (total + midia.Ocupa > Teto && ordem.First is { } primeiro)
            {
                ordem.RemoveFirst();
                porChave.Remove(primeiro.Value.Chave);
                total -= primeiro.Value.Midia.Ocupa;
            }
            porChave[chave] = ordem.AddLast((chave, midia));
            total += midia.Ocupa;
        }
    }

    /// <summary>Esquece tudo (a tela foi descartada).</summary>
    public void Limpar()
    {
        lock (trava)
        {
            ordem.Clear();
            porChave.Clear();
            total = 0;
        }
    }
}
