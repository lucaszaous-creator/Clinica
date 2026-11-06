using System.Net;
using System.Text;

namespace Clinica.Application.Assinatura.SafeID;

/// <summary>O que voltou da página de autorização do PSC.</summary>
/// <param name="Erro">
/// Preenchido quando a médica recusou no celular ou o PSC rejeitou o pedido. Recusa é
/// resposta legítima, não falha de sistema — quem chama mostra a frase e fecha, sem falar em
/// erro de comunicação.
/// </param>
public sealed record RetornoAutorizacao(string? Codigo, string? Erro, string? Descricao)
{
    public bool Autorizou => !string.IsNullOrEmpty(Codigo) && Erro is null;
}

/// <summary>
/// O servidor de um pedido só que recebe o <c>code</c> do OAuth de volta do navegador
/// (parcela 44).
///
/// Por que isto existe num aplicativo desktop
/// ------------------------------------------
/// O fluxo de autorização termina com o PSC redirecionando o NAVEGADOR para a
/// <c>redirect_uri</c>. Num sistema web isso cai no servidor; aqui não há servidor — a
/// clínica não tem endereço público, e nem deveria precisar de um para a médica assinar uma
/// receita. A saída padrão para aplicativo nativo (RFC 8252) é a própria aplicação escutar
/// em <c>127.0.0.1</c> por alguns segundos. A Safeweb aceita cadastrar loopback, o que
/// fechou esse caminho.
///
/// Duas decisões que parecem detalhe e não são:
///
/// <b>Escuta na RAIZ da porta, não no caminho.</b> O <c>HttpListener</c> exige prefixo
/// terminado em barra e casa por prefixo — registrar
/// <c>http://127.0.0.1:8123/safeid/retorno/</c> deixaria de casar o retorno para
/// <c>/safeid/retorno</c> (sem a barra), que é exatamente a forma cadastrada no portal. Quem
/// identifica a aplicação é a PORTA; o caminho é conferido aqui dentro.
///
/// <b>Só 127.0.0.1, nunca <c>+</c> ou <c>*</c>.</b> Um prefixo curinga abriria a porta para a
/// rede do consultório e ainda exigiria elevação no Windows. O que trafega aqui é um código
/// de autorização de assinatura — ele não sai desta máquina.
/// </summary>
public sealed class EscutaLoopback : IDisposable
{
    private readonly HttpListener _servidor;

    /// <summary>A URI em que esta execução está escutando — a que vai no <c>redirect_uri</c>.</summary>
    public Uri Endereco { get; }

    private EscutaLoopback(HttpListener servidor, Uri endereco)
    {
        _servidor = servidor;
        Endereco = endereco;
    }

    /// <summary>
    /// Abre a primeira porta livre entre as candidatas.
    ///
    /// A ordem é a do cadastro no portal, e a queda para a seguinte é silenciosa de
    /// propósito: porta ocupada por outro programa é problema do sistema operacional, não da
    /// médica, e travar a assinatura por causa dele obrigaria a clínica a mexer no portal da
    /// Safeweb com um paciente na sala.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nenhuma das portas cadastradas está livre.</exception>
    public static EscutaLoopback Abrir(IReadOnlyList<Uri>? candidatas)
    {
        if (candidatas is null || candidatas.Count == 0)
            throw new InvalidOperationException(
                "Nenhuma URL de retorno do SafeID está configurada. Defina "
                + $"{ConfiguracaoSafeID.ChaveRedirectUri} com as URLs cadastradas no portal.");

        var recusas = new List<string>();

        foreach (var candidata in candidatas)
        {
            if (!candidata.IsLoopback)
            {
                // URL pública na lista não é erro de digitação: é o fluxo CA, que não passa
                // por aqui. Pular em silêncio deixaria a clínica sem entender por que a
                // porta escolhida não foi a que ela configurou.
                recusas.Add($"{candidata} (não é loopback)");
                continue;
            }

            var servidor = new HttpListener();
            servidor.Prefixes.Add($"{candidata.Scheme}://{candidata.Host}:{candidata.Port}/");

            try
            {
                servidor.Start();
                return new EscutaLoopback(servidor, candidata);
            }
            catch (HttpListenerException ex)
            {
                servidor.Close();
                recusas.Add($"{candidata} ({ex.Message})");
            }
        }

        throw new InvalidOperationException(
            "Nenhuma das portas de retorno do SafeID pôde ser aberta nesta máquina: "
            + string.Join("; ", recusas));
    }

    /// <summary>
    /// Espera o navegador voltar com o código.
    ///
    /// Requisição que não bate com o <paramref name="estadoEsperado"/> é <b>ignorada e a
    /// escuta continua</b>, em vez de encerrar com erro: qualquer processo da máquina pode
    /// bater nesta porta, e deixar um pedido alheio derrubar a autorização daria a ele o
    /// poder de impedir a médica de assinar. O <c>state</c> é o que amarra o retorno ao
    /// pedido que ESTA janela fez (RFC 6749, 10.12).
    /// </summary>
    public async Task<RetornoAutorizacao> EsperarAsync(
        string estadoEsperado, CancellationToken ct = default)
    {
        using var registro = ct.Register(() => { try { _servidor.Stop(); } catch { } });

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            HttpListenerContext contexto;
            try
            {
                contexto = await _servidor.GetContextAsync();
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            var consulta = System.Web.HttpUtility.ParseQueryString(
                contexto.Request.Url?.Query ?? string.Empty);

            if (consulta["state"] != estadoEsperado)
            {
                await ResponderAsync(contexto, PaginaSimples(
                    "Pedido não reconhecido",
                    "Esta janela não corresponde à autorização em andamento."));
                continue;
            }

            var erro = consulta["error"];
            var retorno = new RetornoAutorizacao(
                consulta["code"], erro, consulta["error_description"]);

            await ResponderAsync(contexto, retorno.Autorizou
                ? PaginaSimples(
                    "Autorização concluída",
                    "Pode fechar esta aba e voltar ao sistema da clínica.")
                : PaginaSimples(
                    "Autorização não concluída",
                    retorno.Descricao ?? "O pedido foi recusado ou expirou."));

            return retorno;
        }
    }

    private static async Task ResponderAsync(HttpListenerContext contexto, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        contexto.Response.ContentType = "text/html; charset=utf-8";
        contexto.Response.ContentLength64 = bytes.Length;

        try
        {
            await contexto.Response.OutputStream.WriteAsync(bytes);
            contexto.Response.Close();
        }
        catch (Exception ex)
        {
            // O navegador pode ter fechado antes de ler a resposta. Isso não invalida o
            // código já recebido — mas some sem rastro se ninguém registrar.
            Diagnostico.Registrar("EscutaLoopback.Responder", ex);
        }
    }

    /// <summary>
    /// A página que a médica vê no navegador. Sem CSS externo e sem imagem: ela abre numa
    /// aba que vai ser fechada em cinco segundos, e qualquer recurso remoto aqui só
    /// adicionaria uma requisição que pode falhar.
    /// </summary>
    private static string PaginaSimples(string titulo, string texto) =>
        $"""
         <!doctype html>
         <html lang="pt-BR"><head><meta charset="utf-8"><title>{titulo}</title></head>
         <body style="font-family:system-ui,sans-serif;text-align:center;padding:48px;color:#1e293b">
         <h2 style="color:#0f172a">{titulo}</h2><p>{texto}</p>
         </body></html>
         """;

    public void Dispose()
    {
        try { _servidor.Close(); } catch { /* já fechado */ }
    }
}
