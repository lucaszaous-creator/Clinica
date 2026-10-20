using System.IO;
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
/// <b>Texto simples no lembrete; texto + HTML no documento.</b> O documento ao paciente leva a
/// marca da clínica (<see cref="EmailDoDocumento"/>), mas SEMPRE com a versão em texto ao lado
/// (multipart/alternative) — e-mail só HTML é o que o filtro de spam marca primeiro.
/// </summary>
public sealed class EnviadorSmtp : IEnviadorDeEmail
{
    public Task EnviarAsync(
        OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default)
        => EnviarAsync(opcoes, destinatario, assunto, corpo, null, ct);

    /// <summary>
    /// O mesmo envio, com o PDF anexado (out/2026 — o documento ao paciente). O anexo vai
    /// num <see cref="MemoryStream"/> próprio, descartado com a mensagem.
    /// </summary>
    public Task EnviarComAnexoAsync(
        OpcoesEmail opcoes, string destinatario, string assunto, string corpo, AnexoDeEmail anexo,
        CancellationToken ct = default)
        => EnviarAsync(opcoes, destinatario, assunto, corpo, anexo, ct);

    /// <summary>
    /// O documento com a identidade da clínica: o texto e o HTML como VERSÕES da mesma mensagem
    /// (o leitor escolhe), a logo como recurso da versão HTML (citada por <c>cid:</c>) e o PDF
    /// anexado.
    /// </summary>
    public Task EnviarDocumentoAsync(
        OpcoesEmail opcoes, string destinatario, MensagemDeEmail mensagem, AnexoDeEmail anexo,
        CancellationToken ct = default)
        => EnviarAsync(opcoes, destinatario, mensagem.Assunto, mensagem.Texto, anexo, ct, mensagem);

    public Task EnviarMensagemAsync(
        OpcoesEmail opcoes, string destinatario, MensagemDeEmail mensagem, CancellationToken ct = default)
        => EnviarAsync(opcoes, destinatario, mensagem.Assunto, mensagem.Texto, null, ct, mensagem);

    private static async Task EnviarAsync(
        OpcoesEmail opcoes, string destinatario, string assunto, string corpo, AnexoDeEmail? anexo,
        CancellationToken ct, MensagemDeEmail? rica = null)
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
            SubjectEncoding = System.Text.Encoding.UTF8,
            Body = corpo,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = false,
            // A resposta do paciente ("não vou poder") volta para a clínica, não para o
            // servidor: sem isto, "responder" num remetente técnico cai no vazio.
            ReplyToList = { de }
        };

        if (rica?.Html is { } html)
        {
            // A ordem das versões importa: o leitor mostra a ÚLTIMA que ele entende.
            mensagem.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                corpo, System.Text.Encoding.UTF8, "text/plain"));
            var visaoHtml = AlternateView.CreateAlternateViewFromString(
                html, System.Text.Encoding.UTF8, "text/html");
            foreach (var img in rica.Imagens)
                visaoHtml.LinkedResources.Add(new LinkedResource(
                    new MemoryStream(img.Conteudo, writable: false), img.TipoMime) { ContentId = img.ContentId });
            mensagem.AlternateViews.Add(visaoHtml);
            mensagem.Body = string.Empty;
        }

        if (anexo is not null)
            mensagem.Attachments.Add(new Attachment(
                new MemoryStream(anexo.Conteudo, writable: false), anexo.NomeArquivo, anexo.TipoMime));

        await cliente.SendMailAsync(mensagem, ct);
    }
}
