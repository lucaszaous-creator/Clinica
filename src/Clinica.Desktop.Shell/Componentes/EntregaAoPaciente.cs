using System.Collections.Specialized;
using System.IO;
using Clinica.Application.Servicos;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>O que a entrega conseguiu fazer, já na frase que a tela mostra.</summary>
/// <param name="Caminho">Onde o arquivo ficou gravado. Serve mesmo quando o resto falha.</param>
/// <param name="NaAreaDeTransferencia">
/// O arquivo está copiado e um Ctrl+V no WhatsApp o anexa. Quando falso, a pessoa anexa
/// pelo clipe, e a mensagem diz o caminho — nunca fica sem saída.
/// </param>
public sealed record ResultadoEntrega(
    string Caminho, bool NaAreaDeTransferencia, string Frase, bool EhErro);

/// <summary>
/// Entregar ao paciente o ARQUIVO assinado — a metade que faltava para a receita
/// eletrônica servir para alguma coisa (parcela 43, 2ª rodada).
///
/// O problema que isto resolve
/// ---------------------------
/// A assinatura vive nos BYTES do arquivo. Quem leva só a impressão leva um documento sem
/// a garantia que o sistema produziu, e a farmácia recusa — com razão. Até aqui o sistema
/// salvava o PDF e abria o leitor, e a partir daí a secretária tinha de achar o arquivo,
/// abrir o WhatsApp, procurar o paciente e anexar. Na prática isso significa que, num dia
/// cheio, o paciente sai com o papel e o arquivo fica no computador da clínica — que é
/// exatamente o caso em que a assinatura não serviu para nada.
///
/// O que ele faz, e por que não faz mais
/// -------------------------------------
/// Grava numa pasta ESTÁVEL (e não num temporário que o Windows limpa), põe o arquivo na
/// área de transferência — no WhatsApp Web ou no aplicativo, Ctrl+V anexa — e abre a
/// conversa do paciente com a mensagem pronta. O anexo em si não é automático porque
/// mandar arquivo por WhatsApp sem intervenção exige a API oficial da Meta (conta
/// Business, custo por mensagem); prometer envio automático com o <c>wa.me</c> seria
/// prometer o que ele não faz.
///
/// A mensagem ensina o paciente a usar o arquivo: sem essa frase ele encaminha a foto do
/// papel, que é o erro mais comum e o mais caro — a farmácia devolve e ele volta à clínica.
/// </summary>
public static class EntregaAoPaciente
{
    /// <summary>Onde os documentos entregues ficam, para poderem ser reenviados depois.</summary>
    public static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Clinica", "Documentos assinados");

    /// <summary>
    /// Grava, copia e abre a conversa. Nunca lança: toda falha vira frase para a tela,
    /// porque aqui já existe um documento assinado e válido — perder a entrega não pode
    /// virar erro que faça alguém achar que precisa emitir de novo.
    /// </summary>
    public static ResultadoEntrega Entregar(
        byte[] pdf, string nomeArquivo, string? telefone, string nomePaciente,
        string tipoDocumento, string? nomeClinica = null)
    {
        string caminho;

        try
        {
            Directory.CreateDirectory(Pasta);
            caminho = Path.Combine(Pasta, ImpressaoPdf.NomeSeguro(nomeArquivo));
            File.WriteAllBytes(caminho, pdf);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Suíte — documento assinado não pôde ser gravado para entrega", ex);

            return new ResultadoEntrega(
                string.Empty, false,
                $"Não foi possível gravar o arquivo para enviar: {ex.Message}", EhErro: true);
        }

        var copiado = CopiarParaAreaDeTransferencia(caminho);

        var erroWhatsapp = Whatsapp.Abrir(
            telefone, nomePaciente, Mensagem(nomePaciente, tipoDocumento, nomeClinica));

        // Três desfechos, e os três precisam de frase própria: sem telefone o arquivo
        // continua gravado e pode ir por e-mail; sem área de transferência o anexo é pelo
        // clipe; com tudo certo, o que falta é um Ctrl+V — e dizer isso é o que faz a
        // secretária concluir a entrega em vez de fechar a janela achando que já foi.
        if (erroWhatsapp is not null)
            return new ResultadoEntrega(
                caminho, copiado,
                $"{erroWhatsapp} O arquivo está salvo em {caminho} — envie por e-mail ou "
                + "abra o WhatsApp manualmente.", EhErro: true);

        return new ResultadoEntrega(
            caminho, copiado,
            copiado
                ? "WhatsApp aberto e arquivo copiado: cole com Ctrl+V para anexar. "
                  + $"Ele também está salvo em {caminho}."
                : $"WhatsApp aberto. Anexe o arquivo salvo em {caminho}.",
            EhErro: false);
    }

    /// <summary>
    /// A mensagem que vai ao paciente.
    ///
    /// Ela diz a única coisa que ele precisa saber e que ninguém lhe conta: <b>é o ARQUIVO
    /// que vale</b>. Sem isso ele imprime, ou manda foto da tela, e a farmácia recusa.
    /// </summary>
    public static string Mensagem(string nomePaciente, string tipoDocumento, string? nomeClinica)
    {
        var primeiroNome = nomePaciente
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? nomePaciente;

        var artigo = tipoDocumento.StartsWith("Receita", StringComparison.OrdinalIgnoreCase)
            ? "sua" : "seu";

        return $"Olá, {primeiroNome}! Segue {artigo} {tipoDocumento.ToLowerInvariant()} "
             + "assinado(a) digitalmente. "
             + "Apresente o ARQUIVO PDF (não a foto nem a impressão) — é nele que fica a "
             + $"assinatura. A farmácia confere em {DocumentosClinicosPdfService.ValidadorOficial}."
             + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }

    /// <summary>
    /// Põe o ARQUIVO (não o texto do caminho) na área de transferência, para o Ctrl+V do
    /// WhatsApp virar anexo.
    ///
    /// A área de transferência do Windows é um recurso disputado: outro programa pode
    /// estar segurando-a no exato instante do clique, e a chamada falha. Uma segunda
    /// tentativa resolve o caso comum, e falhar de vez não é problema — a frase muda e a
    /// pessoa anexa pelo clipe.
    /// </summary>
    private static bool CopiarParaAreaDeTransferencia(string caminho)
    {
        var arquivos = new StringCollection { caminho };

        for (var tentativa = 1; tentativa <= 2; tentativa++)
        {
            try
            {
                System.Windows.Clipboard.SetFileDropList(arquivos);
                return true;
            }
            catch (Exception ex) when (tentativa == 1)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Suíte — área de transferência ocupada na 1ª tentativa", ex);
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Suíte — arquivo não pôde ser copiado para a área de transferência", ex);
            }
        }

        return false;
    }
}
