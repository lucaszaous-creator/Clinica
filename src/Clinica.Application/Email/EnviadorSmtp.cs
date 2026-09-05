using System.Net;
using System.Net.Mail;

namespace Clinica.Application.Email;

/// <summary>
/// Entrega pelo <see cref="SmtpClient"/> do próprio .NET — sem pacote novo. Os cinco apps se
/// auto-atualizam por Velopack, e uma dependência a mais é risco desproporcional para
/// mandar texto simples (a regra dos gráficos sem biblioteca).
///
/// <b>Um cliente por envio, descartado no fim.</b> O <c>SmtpClient</c> não é seguro para
/// uso concorrente e guarda a conexão; reaproveitá-lo entre a abertura da Recepção e um
/// clique em "Enviar e-mails" horas depois produziria a falha mais difícil de reproduzir —
/// a conexão morta que só aparece no segundo uso.
///
/// <b>Texto simples, nunca HTML.</b> O lembrete não leva dado clínico nem imagem, e HTML é
/// o que dispara o filtro de spam de quem nunca recebeu e-mail da clínica.
/// </summary>
public sealed class EnviadorSmtp : IEnviadorDeEmail
{
    public async Task EnviarAsync(
        OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default)
    {
        using var cliente = new SmtpClient(opcoes.Host, opcoes.Porta)
        {
            EnableSsl = opcoes.UsarTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = (int)OpcoesEmail.TempoLimite.TotalMilliseconds,
            // Sem usuário o .NET tenta sem credencial — é o caso do relay interno.
            Credentials = opcoes.Usuario is null
                ? null
                : new NetworkCredential(opcoes.Usuario, opcoes.Senha ?? string.Empty)
        };

        var de = opcoes.NomeRemetente is null
            ? new MailAddress(opcoes.Remetente)
            : new MailAddress(opcoes.Remetente, opcoes.NomeRemetente);

        using var mensagem = new MailMessage(de, new MailAddress(destinatario))
        {
            Subject = assunto,
            Body = corpo,
            IsBodyHtml = false,
            // A resposta do paciente ("não vou poder") volta para a clínica, não para o
            // servidor: sem isto, "responder" num remetente técnico cai no vazio.
            ReplyToList = { de }
        };

        await cliente.SendMailAsync(mensagem, ct);
    }
}
