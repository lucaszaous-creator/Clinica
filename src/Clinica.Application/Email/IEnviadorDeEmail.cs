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

    /// <summary>
    /// Envia o DOCUMENTO ao paciente: texto + HTML com a marca da clínica + o PDF anexado
    /// (out/2026). O corpo padrão cai no <see cref="EnviarComAnexoAsync"/> com o TEXTO — um
    /// enviador que não conhece HTML continua mandando o documento, só sem a identidade visual.
    /// </summary>
    /// <summary>
    /// Envia UMA mensagem com as duas versões (texto e HTML), sem anexo — o teste do servidor
    /// na tela de Configurações, no modelo do documento. O corpo padrão manda só o TEXTO.
    /// </summary>
    Task EnviarMensagemAsync(OpcoesEmail opcoes, string destinatario, MensagemDeEmail mensagem,
        CancellationToken ct = default)
        => EnviarAsync(opcoes, destinatario, mensagem.Assunto, mensagem.Texto, ct);

    Task EnviarDocumentoAsync(OpcoesEmail opcoes, string destinatario, MensagemDeEmail mensagem,
        AnexoDeEmail anexo, CancellationToken ct = default)
        => EnviarComAnexoAsync(opcoes, destinatario, mensagem.Assunto, mensagem.Texto, anexo, ct);
}

/// <summary>Um arquivo anexado ao e-mail. <paramref name="TipoMime"/> é o do conteúdo ("application/pdf").</summary>
public sealed record AnexoDeEmail(string NomeArquivo, byte[] Conteudo, string TipoMime);
