using System.Buffers.Binary;
using System.IO.Compression;

namespace Clinica.Application.Tablet;

/// <summary>Decodificador restrito ao PNG RGB/RGBA 8 bits exportado pelo canvas.
/// Limita alocação e descompressão, confere CRC e rejeita transparência/folha vazia.
/// A verificação de traço não pretende reconhecer identidade ou autenticar grafia.</summary>
public static class TracoTablet
{
    public static (int Largura, int Altura) Validar(byte[] png)
    {
        try { return Ler(png); }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException or OverflowException)
        { throw new InvalidOperationException("Faça uma rubrica legível na área indicada e tente novamente."); }
    }

    private static (int, int) Ler(byte[] png)
    {
        if (png.Length is < 256 or > 524288 || !png.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            throw new InvalidDataException();
        int w = 0, h = 0, canais = 0, offset = 8;
        bool acabou = false, temIdat = false;
        using var comprimido = new MemoryStream();
        while (offset + 12 <= png.Length)
        {
            var tamanho = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4)));
            if (tamanho > png.Length - offset - 12) throw new InvalidDataException();
            var tipo = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = png.AsSpan(offset + 8, tamanho);
            if (Crc(png.AsSpan(offset + 4, tamanho + 4)) != BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + tamanho, 4)))
                throw new InvalidDataException();
            if (offset == 8 && tipo != "IHDR") throw new InvalidDataException();
            switch (tipo)
            {
                case "IHDR":
                    if (w != 0 || tamanho != 13) throw new InvalidDataException();
                    w = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data));
                    h = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]));
                    if (w is < 100 or > 2048 || h is < 60 or > 1024 || data[8] != 8
                        || data[9] is not (2 or 6) || data[10] != 0 || data[11] != 0 || data[12] != 0)
                        throw new InvalidDataException();
                    canais = data[9] == 6 ? 4 : 3;
                    break;
                case "IDAT": comprimido.Write(data); temIdat = true; break;
                case "IEND":
                    if (tamanho != 0 || offset + 12 != png.Length || !temIdat) throw new InvalidDataException();
                    acabou = true; break;
                case "sRGB": case "gAMA": case "pHYs": break;
                default: throw new InvalidDataException();
            }
            offset += tamanho + 12;
        }
        if (!acabou || w == 0) throw new InvalidDataException();
        comprimido.Position = 0;
        using var z = new ZLibStream(comprimido, CompressionMode.Decompress);
        var anterior = new byte[w * canais];
        var linha = new byte[w * canais];
        int tinta = 0, minX = w, maxX = -1, minY = h, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int filtro = z.ReadByte();
            if (filtro is < 0 or > 4) throw new InvalidDataException();
            z.ReadExactly(linha);
            for (int i = 0; i < linha.Length; i++)
            {
                int a = i >= canais ? linha[i - canais] : 0, b = anterior[i], c = i >= canais ? anterior[i - canais] : 0;
                int valor = filtro switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, 4 => Paeth(a,b,c), _ => 0 };
                linha[i] = unchecked((byte)(linha[i] + valor));
            }
            for (int x = 0; x < w; x++)
            {
                int i = x * canais;
                if ((canais == 3 || linha[i + 3] > 80) && linha[i] + linha[i+1] + linha[i+2] < 540)
                { tinta++; minX = Math.Min(minX,x); maxX = Math.Max(maxX,x); minY = Math.Min(minY,y); maxY = Math.Max(maxY,y); }
            }
            (anterior, linha) = (linha, anterior);
        }
        if (z.ReadByte() != -1 || tinta < 40 || tinta > w * h * 0.85 || maxX-minX < 25 || maxY-minY < 5)
            throw new InvalidDataException();
        return (w,h);
    }
    private static int Paeth(int a, int b, int c)
    { int p=a+b-c, pa=Math.Abs(p-a), pb=Math.Abs(p-b), pc=Math.Abs(p-c); return pa<=pb && pa<=pc ? a : pb<=pc ? b : c; }
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (byte b in bytes)
        { crc ^= b; for (int i=0;i<8;i++) crc = (crc>>1) ^ (0xedb88320u & (uint)-(int)(crc & 1)); }
        return ~crc;
    }
}
