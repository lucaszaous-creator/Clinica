// Mídia FICTÍCIA da prévia — gerada em código: nenhuma foto, voz ou documento de pessoa real.
using System.IO;
using System.IO.Compression;
using System.Text;
using Clinica.Application.Abstracoes;

namespace PreviaBlazor;

/// <summary>
/// A MÍDIA das conversas do CRM de mentira (24/10/2026, mídia na tela nativa do WhatsApp): uma foto
/// (PNG), uma nota de voz (WAV) e um PDF de verdade — arquivos válidos, que o navegador da prévia
/// mostra, toca e abre, gerados aqui para não haver arquivo binário no repositório. O
/// <see cref="CrmDeMentira"/> os serve pela mesma porta do cliente de verdade
/// (<see cref="IClienteDoCrm.MidiaAsync"/>). Uma vez por processo (são só leitura).
/// </summary>
public static class MidiaDeExemplo
{
    // ⚠️ PRIMEIRO: os campos estáticos nascem na ordem do arquivo, e a foto abaixo já precisa da tabela.
    private static readonly uint[] TabelaCrc = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    /// <summary>A "foto do pedido médico": uma folha com cabeçalho, linhas e assinatura sobre a mesa.</summary>
    public static readonly ArquivoDaMidia Foto = new(Png(480, 360), "image/png", null);

    /// <summary>A nota de voz: seis segundos e meio de "fala" (tom com sílabas e pausas), 8 kHz mono.</summary>
    public static readonly ArquivoDaMidia NotaDeVoz = new(Wav(6.5), "audio/wav", null);

    /// <summary>O PDF de uma página que a clínica manda (as orientações do BSV).</summary>
    public static readonly ArquivoDaMidia Orientacoes = new(Pdf("Orientações para o dia do BSV",
    [
        "Chegue 15 minutos antes do horário marcado.",
        "Venha com roupa confortável e traga um acompanhante.",
        "Faça uma refeição leve até duas horas antes.",
        "Traga o pedido médico e a carteirinha do convênio.",
    ]), "application/pdf", "orientacoes-bsv.pdf");

    /// <summary>O pedido médico que o paciente mandou em PDF.</summary>
    public static readonly ArquivoDaMidia PedidoMedico = new(Pdf("Pedido médico",
    [
        "Paciente: João Batista Ferreira",
        "Solicito 10 sessões de acupuntura para lombalgia crônica.",
        "Dr. Paulo Andrade - CRM 00000 (fictício)",
    ]), "application/pdf", "Pedido medico - Joao.pdf");

    // ---------------------------------------------------------------- PNG

    private static byte[] Png(int largura, int altura)
    {
        var linhas = new byte[altura * (1 + largura * 3)];
        var i = 0;
        for (var y = 0; y < altura; y++)
        {
            linhas[i++] = 0; // filtro "nenhum"
            for (var x = 0; x < largura; x++)
            {
                var (r, g, b) = Pixel(x, y, largura, altura);
                linhas[i++] = r; linhas[i++] = g; linhas[i++] = b;
            }
        }
        using var saida = new MemoryStream();
        saida.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13];
        EscreverBE(ihdr, 0, (uint)largura);
        EscreverBE(ihdr, 4, (uint)altura);
        ihdr[8] = 8; ihdr[9] = 2; // 8 bits, RGB
        Pedaco(saida, "IHDR", ihdr);
        using (var comprimido = new MemoryStream())
        {
            using (var z = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true)) z.Write(linhas);
            Pedaco(saida, "IDAT", comprimido.ToArray());
        }
        Pedaco(saida, "IEND", []);
        return saida.ToArray();
    }

    /// <summary>A mesa (degradê de madeira), a folha levemente sombreada, o cabeçalho azul, as linhas de texto e a assinatura.</summary>
    private static (byte, byte, byte) Pixel(int x, int y, int w, int h)
    {
        const int esq = 70, topo = 28, dir = 410, base_ = 350;
        var dentroDaFolha = x >= esq && x < dir && y >= topo && y < base_;
        if (!dentroDaFolha)
        {
            var sombra = x >= esq + 6 && x < dir + 6 && y >= topo + 6 && y < base_ + 6;
            var veio = (int)(8 * Math.Sin((x + y * 0.3) / 9.0));
            var tom = 120 + y * 30 / h + veio - (sombra ? 35 : 0);
            return ((byte)Math.Clamp(tom + 30, 0, 255), (byte)Math.Clamp(tom - 5, 0, 255), (byte)Math.Clamp(tom - 40, 0, 255));
        }
        var fx = x - esq;
        var fy = y - topo;
        if (fy < 46) return fx is > 16 and < 60 && fy is > 10 and < 36 ? ((byte)255, (byte)255, (byte)255) : ((byte)37, (byte)99, (byte)235);
        // Linhas de "texto": barras cinza de comprimentos diferentes.
        for (var l = 0; l < 8; l++)
        {
            var ly = 70 + l * 24;
            var comprimento = 300 - (l * 37 % 120);
            if (fy >= ly && fy < ly + 7 && fx >= 20 && fx < 20 + comprimento) return (120, 128, 140);
        }
        // A assinatura: uma onda azul-escura.
        var onda = 286 + 10 * Math.Sin(fx / 7.0) * Math.Sin(fx / 23.0);
        if (fx is > 150 and < 310 && Math.Abs(fy - onda) < 1.6) return (30, 41, 90);
        if (fy == 300 && fx is > 140 and < 320) return (160, 166, 175);
        return (252, 250, 245);
    }

    private static void Pedaco(Stream s, string tipo, byte[] dados)
    {
        var tamanho = new byte[4];
        EscreverBE(tamanho, 0, (uint)dados.Length);
        s.Write(tamanho);
        var corpo = new byte[4 + dados.Length];
        Encoding.ASCII.GetBytes(tipo).CopyTo(corpo, 0);
        dados.CopyTo(corpo, 4);
        s.Write(corpo);
        var crc = new byte[4];
        EscreverBE(crc, 0, Crc32(corpo));
        s.Write(crc);
    }

    private static void EscreverBE(byte[] b, int i, uint v)
    {
        b[i] = (byte)(v >> 24); b[i + 1] = (byte)(v >> 16); b[i + 2] = (byte)(v >> 8); b[i + 3] = (byte)v;
    }

    private static uint Crc32(byte[] dados)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in dados) c = TabelaCrc[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    // ---------------------------------------------------------------- WAV

    private static byte[] Wav(double segundos)
    {
        const int taxa = 8000;
        var amostras = (int)(segundos * taxa);
        using var saida = new MemoryStream();
        using var w = new BinaryWriter(saida, Encoding.ASCII, leaveOpen: true);
        w.Write("RIFF"u8); w.Write(36 + amostras * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(taxa); w.Write(taxa * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(amostras * 2);
        var fase = 0.0;
        for (var n = 0; n < amostras; n++)
        {
            var t = n / (double)taxa;
            // A "voz": fundamental perto de 170 Hz que sobe e desce, três harmônicos, sílabas de ~0,3 s e duas pausas.
            var f0 = 170 + 25 * Math.Sin(2 * Math.PI * 0.7 * t);
            fase += 2 * Math.PI * f0 / taxa;
            var som = Math.Sin(fase) + 0.55 * Math.Sin(2 * fase) + 0.3 * Math.Sin(3 * fase) + 0.12 * Math.Sin(5 * fase);
            var silaba = Math.Pow(Math.Max(0, Math.Sin(2 * Math.PI * 3.3 * t)), 0.6);
            var pausa = t is > 2.1 and < 2.5 or > 4.4 and < 4.9 || t > segundos - 0.3 ? 0 : 1;
            w.Write((short)(6500 * som * silaba * pausa / 1.97));
        }
        w.Flush();
        return saida.ToArray();
    }

    // ---------------------------------------------------------------- PDF

    private static byte[] Pdf(string titulo, IReadOnlyList<string> linhas)
    {
        var latin1 = Encoding.Latin1;
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var conteudo = new StringBuilder();
        conteudo.Append("BT /F1 20 Tf 60 770 Td (").Append(Esc(titulo)).Append(") Tj ET\n");
        conteudo.Append("0.15 0.39 0.92 RG 2 w 60 755 m 535 755 l S\n");
        for (var i = 0; i < linhas.Count; i++)
            conteudo.Append("BT /F1 12 Tf 60 ").Append(720 - i * 24).Append(" Td (").Append(Esc(linhas[i])).Append(") Tj ET\n");
        conteudo.Append("BT /F1 9 Tf 60 60 Td (Clinica SemDor - documento ficticio da previa) Tj ET\n");
        var fluxo = latin1.GetBytes(conteudo.ToString());

        var objetos = new List<byte[]>
        {
            latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            latin1.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            latin1.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>"),
            latin1.GetBytes($"<< /Length {fluxo.Length} >>\nstream\n").Concat(fluxo).Concat(latin1.GetBytes("endstream")).ToArray(),
            latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
        };
        using var saida = new MemoryStream();
        void Escrever(string s) => saida.Write(latin1.GetBytes(s));
        Escrever("%PDF-1.4\n");
        var posicoes = new List<long>();
        for (var i = 0; i < objetos.Count; i++)
        {
            posicoes.Add(saida.Position);
            Escrever($"{i + 1} 0 obj\n");
            saida.Write(objetos[i]);
            Escrever("\nendobj\n");
        }
        var xref = saida.Position;
        Escrever($"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
        foreach (var p in posicoes) Escrever($"{p:0000000000} 00000 n \n");
        Escrever($"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return saida.ToArray();
    }
}
