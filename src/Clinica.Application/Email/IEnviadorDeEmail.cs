namespace Clinica.Application.Email;

/// <summary>
/// Quem entrega um e-mail. É interface por UMA razão: o teste precisa provar que o lembrete
/// escolhe os pacientes certos, marca o canal e não reenvia — sem um servidor SMTP de
/// verdade. A implementação de produção é <see cref="EnviadorSmtp"/>.
/// </summary>
public interface IEnviadorDeEmail
{
    /// <summary>Envia UM e-mail de texto simples. Lança quando o servidor recusa ou não responde.</summary>
    Task EnviarAsync(OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default);

    /// <summary>
    /// Envia UM e-mail de texto simples com UM anexo (out/2026 — o documento ao paciente).
    /// Lança quando o servidor recusa ou não responde.
    ///
    /// Tem corpo padrão que RECUSA de propósito: os enviadores falsos dos testes do lembrete
    /// não precisam conhecer anexo, e um que não o implemente não pode fingir que mandou o
    /// documento mandando só o texto.
    /// </summary>
    Task EnviarComAnexoAsync(OpcoesEmail opcoes, string destinatario, string assunto, string corpo,
        AnexoDeEmail anexo, CancellationToken ct = default)
        => throw new NotSupportedException("Este enviador de e-mail não envia anexos.");
}

/// <summary>Um arquivo anexado ao e-mail. <paramref name="TipoMime"/> é o do conteúdo ("application/pdf").</summary>
public sealed record AnexoDeEmail(string NomeArquivo, byte[] Conteudo, string TipoMime);
