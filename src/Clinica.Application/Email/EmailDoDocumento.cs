using System.Text;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;

namespace Clinica.Application.Email;

/// <summary>Uma imagem que viaja DENTRO da mensagem e é citada no HTML por <c>cid:</c>.</summary>
public sealed record ImagemEmbutida(string ContentId, byte[] Conteudo, string TipoMime);

/// <summary>
/// Um e-mail com as duas versões: o TEXTO (o que todo leitor mostra, e o que o filtro de spam
/// confere contra o HTML) e o HTML com a identidade da clínica. <see cref="Html"/> nulo é
/// "só texto".
/// </summary>
public sealed record MensagemDeEmail(string Assunto, string Texto, string? Html, IReadOnlyList<ImagemEmbutida> Imagens);

/// <summary>
/// O e-mail que leva o DOCUMENTO ao paciente (out/2026 — decisão da direção: e-mail é só para
/// documento; confirmação de sessão não sai por e-mail, porque a clínica não colhe o endereço
/// no cadastro). Até aqui ele era texto puro, sem marca nenhuma — o paciente recebia um anexo
/// de um remetente que não parecia a clínica.
///
/// As decisões, e por quê:
/// <list type="bullet">
/// <item><b>A logo vai EMBUTIDA</b> (<c>cid:</c>), nunca por endereço: os leitores de e-mail
///   bloqueiam imagem remota por padrão, e a mensagem chegaria com um quadrado vazio no topo.</item>
/// <item><b>Tabela e estilo em linha</b>: é o único HTML que Gmail, Outlook e o app do celular
///   desenham igual — folha de estilo no &lt;head&gt; é descartada por metade deles.</item>
/// <item><b>Sempre com a versão em texto ao lado</b> (o <see cref="TextoDoEnvio.CorpoEmail"/> de
///   sempre): e-mail só HTML é o que o filtro de spam marca primeiro.</item>
/// <item><b>Mensagem automática (no-reply)</b>: não convida a responder nem explica a
///   assinatura do PDF (decisão da direção, out/2026) — a dúvida vai aos contatos da clínica.</item>
/// <item><b>Nada clínico no corpo</b>: diz QUAL papel (o rótulo e o número que estão impressos
///   nele) e mais nada — caixa de e-mail é lida por outras pessoas, e o conteúdo é o anexo.</item>
/// <item><b>Todo texto que vem do banco passa pelo escape</b>: o nome do paciente e o endereço da
///   clínica são digitados por gente.</item>
/// </list>
/// Mora na Application, e não no enviador, porque é o que o e-mail AFIRMA — e o que a tela ou
/// a mensagem afirma precisa morar onde o <c>dotnet test</c> alcança.
/// </summary>
public static class EmailDoDocumento
{
    public const string CidLogo = "logo-clinica-semdor";

    // Cores: as da marca (MarcaSemDor) e os neutros do design system (tokens/colors.css).
    private const string Fundo = "#F8FAFC";      // --cinza-50
    private const string Superficie = "#FFFFFF"; // --superficie
    private const string Borda = "#E5E7EB";      // --cinza-200
    private const string Texto = "#111827";      // --cinza-900
    private const string TextoSecundario = "#6B7280"; // --cinza-500
    private const string Fonte = "'Segoe UI',Inter,system-ui,-apple-system,'Helvetica Neue',Arial,sans-serif";

    public static MensagemDeEmail Montar(
        string nomePaciente, string rotulo, string numero,
        DadosPrestador prestador, byte[]? logo)
    {
        var nomeClinica = NomeDaClinica(prestador);
        var assunto = TextoDoEnvio.AssuntoEmail(nomeClinica);
        var texto = TextoDoEnvio.CorpoEmail(nomePaciente, nomeClinica)
                    + Rodape(prestador);
        var imagens = logo is { Length: > 0 }
            ? new[] { new ImagemEmbutida(CidLogo, logo, "image/png") }
            : Array.Empty<ImagemEmbutida>();
        var html = Html(nomePaciente, rotulo, numero, prestador, nomeClinica, imagens.Length > 0);
        return new MensagemDeEmail(assunto, texto, html, imagens);
    }

    private static string? NomeDaClinica(DadosPrestador p)
        => !string.IsNullOrWhiteSpace(p.NomeFantasia) ? p.NomeFantasia!.Trim()
         : !string.IsNullOrWhiteSpace(p.RazaoSocial) ? p.RazaoSocial!.Trim() : null;

    /// <summary>
    /// Escapa só o que muda o HTML (<c>&lt; &gt; &amp; " '</c>). O <c>WebUtility.HtmlEncode</c>
    /// também troca TODO acento por entidade ("Clínica" vira <c>Cl&amp;#237;nica</c>): o leitor
    /// desenha igual, mas o HTML fica ilegível e maior — e o resto do corpo já sai com acento.
    /// </summary>
    private static string E(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => c.ToString()
            });
        return sb.ToString();
    }

    /// <summary>Endereço · telefone · e-mail da clínica, só o que estiver cadastrado.</summary>
    private static IReadOnlyList<string> Contatos(DadosPrestador p)
        => new[] { p.Endereco, p.Telefone, p.Email }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();

    private static string Rodape(DadosPrestador p)
    {
        var contatos = Contatos(p);
        return contatos.Count == 0 ? string.Empty : "\n" + string.Join(" · ", contatos);
    }

    private static string Html(
        string nomePaciente, string rotulo, string numero,
        DadosPrestador prestador, string? nomeClinica, bool temLogo)
    {
        var quem = nomeClinica ?? "a clínica";
        var primeiro = TextoDoEnvio.PrimeiroNome(nomePaciente);
        var contatos = Contatos(prestador);
        var sb = new StringBuilder();

        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<title>").Append(E(TextoDoEnvio.AssuntoEmail(nomeClinica))).Append("</title></head>")
          .Append($"<body style=\"margin:0;padding:0;background:{Fundo};\">")
          // Pré-cabeçalho: a linha que o leitor mostra ao lado do assunto, antes de abrir.
          .Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">")
          .Append(E($"{rotulo} {numero} em anexo, em PDF.")).Append("</div>")
          .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{Fundo};\"><tr><td align=\"center\" style=\"padding:24px 12px;\">")
          .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;background:{Superficie};border:1px solid {Borda};border-radius:12px;overflow:hidden;font-family:{Fonte};color:{Texto};\">")
          // Faixa da marca.
          .Append($"<tr><td style=\"height:6px;background:{MarcaSemDor.Navy};font-size:0;line-height:0;\">&nbsp;</td></tr>")
          .Append("<tr><td style=\"padding:24px 32px 8px;\">");
        if (temLogo)
            sb.Append($"<img src=\"cid:{CidLogo}\" alt=\"{E(quem)}\" height=\"44\" style=\"display:block;height:44px;width:auto;border:0;\">");
        else
            sb.Append($"<div style=\"font-size:20px;font-weight:700;color:{MarcaSemDor.Navy};\">{E(quem)}</div>");
        sb.Append("</td></tr>")
          // Saudação e a frase do envio.
          .Append("<tr><td style=\"padding:16px 32px 0;font-size:16px;line-height:1.55;\">")
          .Append($"<p style=\"margin:0 0 12px;font-size:18px;font-weight:600;\">Olá, {E(primeiro)}!</p>")
          .Append($"<p style=\"margin:0;\">Segue em anexo o documento que {E(quem)} preparou para você.</p>")
          .Append("</td></tr>")
          // O papel: o que está impresso nele, e nada além.
          .Append("<tr><td style=\"padding:20px 32px 0;\">")
          .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border:1px solid {Borda};border-left:4px solid {MarcaSemDor.Navy};border-radius:8px;\"><tr><td style=\"padding:14px 16px;\">")
          .Append($"<div style=\"font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;color:{TextoSecundario};\">Documento em anexo (PDF)</div>")
          .Append($"<div style=\"margin-top:4px;font-size:17px;font-weight:700;color:{MarcaSemDor.Navy};\">{E(rotulo)}</div>")
          .Append($"<div style=\"margin-top:2px;font-size:14px;color:{TextoSecundario};\">Nº {E(numero)}</div>")
          .Append("</td></tr></table></td></tr>");

        sb.Append("<tr><td style=\"padding:20px 32px 28px;font-size:15px;line-height:1.55;\">")
          .Append($"<p style=\"margin:0;font-weight:600;\">{E(quem)}</p>")
          .Append("</td></tr>")
          // Rodapé: quem é a clínica e por que esta mensagem chegou.
          .Append($"<tr><td style=\"padding:16px 32px 20px;background:{Fundo};border-top:1px solid {Borda};font-size:12px;line-height:1.5;color:{TextoSecundario};\">");
        if (contatos.Count > 0)
            sb.Append("<p style=\"margin:0 0 6px;\">").Append(string.Join(" · ", contatos.Select(E))).Append("</p>");
        sb.Append("<p style=\"margin:0;\">Esta é uma mensagem automática — não responda. ")
          .Append(contatos.Count > 0 ? "Em caso de dúvida, fale com a clínica pelos contatos acima. " : string.Empty)
          .Append("Se ela chegou a você por engano, apague-a.</p>")
          .Append("</td></tr></table></td></tr></table></body></html>");
        return sb.ToString();
    }
}
