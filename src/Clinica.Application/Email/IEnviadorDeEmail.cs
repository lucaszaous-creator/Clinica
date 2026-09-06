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
}
