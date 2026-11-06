using System.Security.Cryptography;
using System.Text.Json;
using Clinica.Infrastructure.Tablet;
using Microsoft.AspNetCore.DataProtection;
using QRCoder;

namespace Clinica.Assinaturas.Api;

/// <summary>
/// As peças da entrada do portal (nov/2026): o aparelho, a entrada pendente entre a senha e o
/// código, e o QR do primeiro acesso.
/// </summary>
public static class EntradaDoPortal
{
    // O cookie do aparelho mora em CookiesDoPortal (Infrastructure): a gestão web também o lê.
    public const string PropositoEntrada = "EntradaPendentePortal.v1";
    public static readonly TimeSpan VidaDaEntrada = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Quem passou da senha e ainda deve o próximo passo. <paramref name="Etapa"/>: "codigo" (tem
    /// autenticador), "liberacao" (primeiro acesso: falta o código que a direção entregou) ou
    /// "cadastro" (liberação conferida; <paramref name="SegredoNovo"/> é o segredo do QR).
    /// </summary>
    public sealed record Pendente(int UsuarioId, string Credencial, string Dispositivo, string? SegredoNovo, string Etapa = "codigo");

    public static string Proteger(ITimeLimitedDataProtector p, Pendente pendente)
        => p.Protect(JsonSerializer.Serialize(pendente), VidaDaEntrada);

    public static Pendente? Abrir(ITimeLimitedDataProtector p, string? cookie)
    {
        if (string.IsNullOrEmpty(cookie) || cookie.Length > 4000) return null;
        try { return JsonSerializer.Deserialize<Pendente>(p.Unprotect(cookie)); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return null; }
    }

    /// <summary>O QR em SVG, gerado aqui: o portal não carrega biblioteca de fora (CSP sem origem externa).</summary>
    public static string QrSvg(string endereco)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(endereco, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(dados).GetGraphic(new System.Drawing.Size(220, 220), "#111827", "#FFFFFF",
            drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}

/// <summary>O segredo TOTP no banco é cifrado pela proteção de dados da API (as chaves ficam no diretório protegido da VPS).</summary>
public sealed class ProtecaoDoSegredoDoPortal(IDataProtectionProvider provedor) : IProtecaoDoSegredo
{
    private readonly IDataProtector _p = provedor.CreateProtector("SegundoFatorPortal.v1");
    public string Proteger(byte[] segredo) => _p.Protect(Convert.ToBase64String(segredo));
    public byte[] Abrir(string protegido) => Convert.FromBase64String(_p.Unprotect(protegido));
}
