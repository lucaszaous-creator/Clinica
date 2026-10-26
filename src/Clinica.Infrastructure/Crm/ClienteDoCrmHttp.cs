using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure.Crm;

/// <summary>
/// O <see cref="IClienteDoCrm"/> de verdade: HTTPS até a API do CRM (out/2026, PR 1 do WhatsApp
/// nativo — docs/whatsapp-nativo-implementacao.md; o fluxo de entrada em
/// semdor-crm/docs/api-para-os-apps.md §4).
///
/// SINGLETON, um por processo: a aba vive o dia inteiro e abre escopo por operação, e as sessões
/// precisam sobreviver a esses escopos. Por isso os dois serviços que leem o banco
/// (<see cref="IEntradaNoCrmService"/> e <see cref="ParametrosService"/>, ambos Scoped) chegam por
/// <see cref="IServiceScopeFactory"/>, um escopo por uso — nunca pelo construtor.
///
/// Cada usuário do app tem a SUA sessão (cookies, csrf): duas pessoas dividem o PC do balcão, e o
/// CRM aplica a cada uma o acesso dela. A sessão nasce de novo a cada entrada (cookies limpos) e
/// quando o endereço configurado muda.
///
/// ⚠️ Três decisões que não são óbvias:
/// <list type="bullet">
/// <item>A entrada é SERIALIZADA por usuário: duas leituras que tomam 401 juntas emitem UM bilhete
/// (a segunda vê que a sessão já mudou e só repete).</item>
/// <item>401 → entra de novo e repete UMA vez, inclusive escrita (o 401 vem da autorização, antes de
/// o CRM executar qualquer coisa). Nenhuma outra falha é repetida: escrita que deu timeout pode ter
/// sido gravada, e a idempotência é do CRM (Version e o Id do envio).</item>
/// <item>Acesso recusado (bilhete recusado, ou a sessão nova também tomou 401) PAUSA as entradas
/// daquela pessoa por <see cref="PausaDepoisDaRecusa"/>: o limite de login do CRM é por IP, e todos os
/// PCs da clínica saem pelo mesmo IP — a releitura de 7 s de uma pessoa recusada esgotaria o limite
/// e barraria a entrada de todas as outras.</item>
/// </list>
/// </summary>
public sealed class ClienteDoCrmHttp : IClienteDoCrm, IDisposable
{
    /// <summary>Quanto tempo o endereço lido das Configurações vale antes de ser relido (a Gerência pode trocá-lo).</summary>
    public static readonly TimeSpan ValidadeDoEndereco = TimeSpan.FromSeconds(30);

    /// <summary>Depois de o CRM recusar o acesso, a pessoa não tenta entrar de novo por este tempo.</summary>
    public static readonly TimeSpan PausaDepoisDaRecusa = TimeSpan.FromMinutes(1);

    /// <summary>O teto de anexo do CRM (MediaStore.Limit, 16 MB). Aqui só evita carregar na memória o que ele recusaria.</summary>
    public const int TetoDoAnexo = 16 * 1024 * 1024;

    public const string ForaDoAr = "O CRM não respondeu. Tente de novo em instantes.";
    public const string Demorou = "O CRM demorou demais para responder. Tente de novo em instantes.";
    public const string FormatoDesconhecido = "O CRM respondeu num formato que este app não conhece. Fale com a direção para atualizar o app.";
    public const string ArquivoVazio = "O CRM devolveu o arquivo vazio. Tente de novo em instantes.";
    public const string TentativasDemais = "O CRM recebeu tentativas demais de entrada. Espere um minuto e tente de novo.";

    /// <summary>A frase do 400 do antiforgery do CRM (ver <see cref="CsrfRecusadoAsync"/>).</summary>
    public const string FraseDoCsrfRecusado = "Sessão desatualizada. Recarregue a página.";

    /// <summary>Prazo do aviso de saída ao CRM no logout: encerra a SESSÃO no servidor (que valeria 2 h) e, com ela, a presença que o pulso do app mantinha (<see cref="PulsoDePresencaAsync"/>). Não pode segurar nada.</summary>
    public static readonly TimeSpan PrazoDaSaida = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Prazos por natureza de chamada: leitura 10 s, escrita 20 s, envio de arquivo 60 s (e o arquivo de uma
    /// mensagem, que pode ter 16 MB, também), e o @SemDor 35 s (o CRM espera a Anthropic por até 30 s; com 20 s
    /// o app diria "demorou" enquanto a sugestão ainda vinha). <c>IA</c> nulo = o da escrita.
    /// </summary>
    internal sealed record Prazos(TimeSpan Leitura, TimeSpan Escrita, TimeSpan Envio, TimeSpan? IA = null)
    {
        public static readonly Prazos Padrao = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(35));
    }

    /// <summary>Midia: um GET (sem csrf) com o prazo do envio — o CRM busca a recebida no provedor na primeira vez, e o arquivo pode ter 16 MB.</summary>
    private enum Natureza { Leitura, Escrita, Envio, IA, Midia }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory escopos;
    private readonly Func<CookieContainer, HttpMessageHandler> criarCano;
    private readonly TimeProvider relogio;
    private readonly Prazos prazos;

    private readonly SemaphoreSlim leituraDoEndereco = new(1, 1);
    private (string? Endereco, DateTimeOffset LidoEm)? endereco;

    private readonly object trava = new();
    private readonly Dictionary<int, SessaoNoCrm> sessoes = new();

    public ClienteDoCrmHttp(IServiceScopeFactory escopos) : this(escopos, CanoDeVerdade, TimeProvider.System, Prazos.Padrao) { }

    /// <summary>Para os testes: o cano HTTP (recebe o CookieContainer da sessão), o relógio e os prazos.</summary>
    internal ClienteDoCrmHttp(IServiceScopeFactory escopos, Func<CookieContainer, HttpMessageHandler> criarCano,
        TimeProvider relogio, Prazos prazos)
    {
        this.escopos = escopos;
        this.criarCano = criarCano;
        this.relogio = relogio;
        this.prazos = prazos;
    }

    private static HttpMessageHandler CanoDeVerdade(CookieContainer cookies) => new SocketsHttpHandler
    {
        CookieContainer = cookies,
        UseCookies = true,
        // Redirecionar levaria o cookie da sessão para outro endereço: 3xx é falha, não caminho.
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };

    // ---------- As rotas ----------

    public async Task<bool> ConfiguradoAsync(CancellationToken ct = default)
        => await EnderecoAsync(forcar: true, ct) is not null;

    public Task<AppSession> SessaoAsync(int usuarioId, CancellationToken ct = default)
        => LerAsync<AppSession>(usuarioId, "/api/session", ct);

    public Task<AppConversationSummary[]> ListarConversasAsync(int usuarioId, CancellationToken ct = default)
        => LerAsync<AppConversationSummary[]>(usuarioId, "/api/conversations", ct);

    // "O" tem o fuso (+00:00): sem o escape, o "+" chegaria ao CRM como espaço.
    public Task<AppConversationsSince> ListarDesdeAsync(int usuarioId, DateTimeOffset desde, CancellationToken ct = default)
        => LerAsync<AppConversationsSince>(usuarioId, "/api/conversations?desde=" + Uri.EscapeDataString(desde.ToString("O")), ct);

    public Task<AppConversation> AbrirConversaAsync(int usuarioId, string conversaId, CancellationToken ct = default)
        => LerAsync<AppConversation>(usuarioId, Conversa(conversaId), ct);

    public Task<AppConversation> NovaConversaAsync(int usuarioId, AppNewConversation pedido, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, "/api/conversations", pedido, ct);

    public Task<AppConversation> ResponderAsync(int usuarioId, string conversaId, AppReply resposta, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "reply"), resposta, ct);

    public Task<AppConversation> AnotarAsync(int usuarioId, string conversaId, AppNoteRequest nota, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "notes"), nota, ct);

    public Task MarcarLidaAsync(int usuarioId, string conversaId, AppReadMarker marcador, CancellationToken ct = default)
        => ChamarAsync(usuarioId, Natureza.Escrita, () => Post(Conversa(conversaId, "read"), JsonContent.Create(marcador, options: Json)),
            (_, _) => Task.FromResult(true), ct);

    public Task MarcarNaoLidaAsync(int usuarioId, string conversaId, CancellationToken ct = default)
        => ChamarAsync(usuarioId, Natureza.Escrita, () => Post(Conversa(conversaId, "unread"), JsonContent.Create(new { }, options: Json)),
            (_, _) => Task.FromResult(true), ct);

    public Task<AppConversation> AssumirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "claim"), mudanca, ct);

    public Task<AppConversation> TransferirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "transfer"), mudanca, ct);

    // A ação vai no caminho, só as três que o CRM conhece: nada que a tela mande vira outro segmento de rota.
    public Task<AppTransferDecided> DecidirTransferenciaAsync(int usuarioId, string pedidoId, string acao, AppTransferDecision decisao,
        CancellationToken ct = default)
    {
        if (acao is not ("accept" or "reject" or "cancel"))
            return Task.FromException<AppTransferDecided>(new ArgumentOutOfRangeException(nameof(acao), acao, "accept, reject ou cancel."));
        return EscreverAsync<AppTransferDecided>(usuarioId, "/api/transfers/" + Uri.EscapeDataString(pedidoId) + "/" + acao, decisao, ct);
    }

    public Task<AppConversation> ReabrirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "reopen"), mudanca, ct);

    public Task<AppConversation> DevolverAFilaAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "reassign"), mudanca, ct);

    public Task<AppConversation> EncerrarAsync(int usuarioId, string conversaId, AppClose encerramento, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "close"), encerramento, ct);

    public Task<AppConversation> LigarFichaAsync(int usuarioId, string conversaId, AppPatientLink ficha, CancellationToken ct = default)
        => EscreverAsync<AppConversation>(usuarioId, Conversa(conversaId, "patient"), ficha, ct);

    public async Task<AppAttachment> AnexarAsync(int usuarioId, string conversaId, Stream arquivo, string nome, string mime, bool gravacao = false,
        CancellationToken ct = default)
    {
        // Lido uma vez para a memória: o 401 repete o envio, e o Stream de quem chama só se lê uma vez.
        var bytes = await LerArquivoAsync(arquivo, ct);
        var tipo = MediaTypeHeaderValue.TryParse(mime, out var m) ? m : new MediaTypeHeaderValue("application/octet-stream");
        return await ChamarAsync(usuarioId, Natureza.Envio, () =>
        {
            var parte = new ByteArrayContent(bytes);
            parte.Headers.ContentType = tipo;
            var corpo = new MultipartFormDataContent { { parte, "file", nome } };
            // O mesmo campo do CRM web (app.js, upload(..., recording)): o CRM converte a gravação para OGG/Opus.
            if (gravacao) corpo.Add(new StringContent("true"), "recording");
            return Post(Conversa(conversaId, "attachments"), corpo);
        }, Desserializar<AppAttachment>, ct);
    }

    public Task<ArquivoDaMidia> MidiaAsync(int usuarioId, string conversaId, AppMessage mensagem, CancellationToken ct = default)
    {
        if (MidiaDaConversa.Rota(conversaId, mensagem) is not { } rota)
            return Task.FromException<ArquivoDaMidia>(new RecusaDoCrm(MidiaDaConversa.SemArquivo, 404));
        return ChamarAsync(usuarioId, Natureza.Midia, () => new HttpRequestMessage(HttpMethod.Get, rota), LerMidiaAsync, ct);
    }

    /// <summary>
    /// Os bytes, o tipo e o nome (Content-Disposition, que o CRM manda com <c>download=true</c>). ⚠️ O
    /// corpo já chegou inteiro quando isto roda (o envio lê com ResponseContentRead, dentro do prazo):
    /// o teto aqui recusa o que passou dele, não evita a leitura — quem garante o tamanho é o CRM,
    /// que não guarda arquivo maior (<c>MediaStore.Get</c>).
    /// </summary>
    private static async Task<ArquivoDaMidia> LerMidiaAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        if (resposta.Content.Headers.ContentLength > MidiaDaConversa.Teto) throw new RecusaDoCrm(MidiaDaConversa.GrandeDemais, 413);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > MidiaDaConversa.Teto) throw new RecusaDoCrm(MidiaDaConversa.GrandeDemais, 413);
        if (bytes.Length == 0) throw new CrmIndisponivel(ArquivoVazio);
        var disposicao = resposta.Content.Headers.ContentDisposition;
        var nome = (disposicao?.FileNameStar ?? disposicao?.FileName)?.Trim().Trim('"');
        return new ArquivoDaMidia(bytes, resposta.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            string.IsNullOrWhiteSpace(nome) ? null : nome);
    }

    public Task<AppTemplate[]> ModelosAsync(int usuarioId, string remetente, CancellationToken ct = default)
        => LerAsync<AppTemplate[]>(usuarioId, "/api/templates/" + Uri.EscapeDataString(remetente), ct);

    public Task<AppWorkspace> AreaDeTrabalhoAsync(int usuarioId, CancellationToken ct = default)
        => LerAsync<AppWorkspace>(usuarioId, "/api/workspace", ct);

    public Task<AppSemDorState> EstadoDoSemDorAsync(int usuarioId, CancellationToken ct = default)
        => LerAsync<AppSemDorState>(usuarioId, "/api/semdor/estado", ct);

    // POST com o csrf, como escrita, mas com o prazo da IA. Repetir no 401 é seguro: o CRM recusa antes de chamar a IA.
    public Task<AppSemDorSuggestion> SugerirComSemDorAsync(int usuarioId, string conversaId, AppSemDorRequest pedido, CancellationToken ct = default)
        => ChamarAsync(usuarioId, Natureza.IA, () => Post(Conversa(conversaId, "semdor/sugestao"), JsonContent.Create(pedido, options: Json)),
            Desserializar<AppSemDorSuggestion>, ct);

    // A rota não lê corpo; vai "{}", como o CRM web manda.
    public Task<AppSemDorSummary> ResumirComSemDorAsync(int usuarioId, string conversaId, CancellationToken ct = default)
        => ChamarAsync(usuarioId, Natureza.IA, () => Post(Conversa(conversaId, "semdor/resumo"), new StringContent("{}", System.Text.Encoding.UTF8, "application/json")),
            Desserializar<AppSemDorSummary>, ct);

    // O pulso de presença (25/10): POST com o csrf e "{}", como o CRM web (reception-presence.js) manda. Escrita
    // comum: o 401 renova e repete UMA vez — a sessão do app venceu, a pessoa continua logada aqui.
    public Task<AppPresence> PulsoDePresencaAsync(int usuarioId, CancellationToken ct = default)
        => ChamarAsync(usuarioId, Natureza.Escrita, () => Post("/api/presence", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")),
            Desserializar<AppPresence>, ct);

    public void EsquecerSessao(int usuarioId)
    {
        // O HttpClient antigo não é descartado aqui: uma chamada dele pode estar em andamento
        // (a releitura de 7 s), e descartá-lo a derrubaria com uma exceção que não é do CRM.
        // As conexões dele fecham sozinhas (PooledConnectionIdleTimeout).
        Acesso? aberto;
        lock (trava)
        {
            aberto = sessoes.TryGetValue(usuarioId, out var sessao) ? sessao.Atual : null;
            sessoes.Remove(usuarioId);
        }
        // Avisa o CRM para a sessão acabar na hora (a presença acaba com ela: o CRM só conta sessão viva) — sem esperar e sem exceção: o logout do app
        // não pode depender do CRM estar no ar. Se o aviso se perder, a sessão vence sozinha (2 h).
        if (aberto is not null) UltimaSaida = Task.Run(() => SairAsync(aberto));
    }

    /// <summary>O último aviso de saída (para os testes esperarem por ele). Nunca falha.</summary>
    internal Task UltimaSaida { get; private set; } = Task.CompletedTask;

    private async Task SairAsync(Acesso acesso)
    {
        try
        {
            using var pedido = Post("/api/logout", new StringContent(""));
            pedido.Headers.Add("X-CSRF-TOKEN", acesso.Csrf);
            using var resposta = await EnviarAsync(acesso.Http, pedido, PrazoDaSaida, CancellationToken.None);
        }
        catch (Exception e)
        {
            Clinica.Application.Diagnostico.Registrar("CRM — aviso de saída no logout (a sessão vence sozinha)", e);
        }
    }

    public void Dispose()
    {
        lock (trava)
        {
            foreach (var s in sessoes.Values) s.Atual?.Http.Dispose();
            sessoes.Clear();
        }
        leituraDoEndereco.Dispose();
    }

    private static string Conversa(string id, string? acao = null)
        => "/api/conversations/" + Uri.EscapeDataString(id) + (acao is null ? "" : "/" + acao);

    private static HttpRequestMessage Post(string caminho, HttpContent corpo)
        => new(HttpMethod.Post, caminho) { Content = corpo };

    private Task<T> LerAsync<T>(int usuarioId, string caminho, CancellationToken ct)
        => ChamarAsync(usuarioId, Natureza.Leitura, () => new HttpRequestMessage(HttpMethod.Get, caminho), Desserializar<T>, ct);

    private Task<T> EscreverAsync<T>(int usuarioId, string caminho, object corpo, CancellationToken ct)
        => ChamarAsync(usuarioId, Natureza.Escrita, () => Post(caminho, JsonContent.Create(corpo, corpo.GetType(), options: Json)),
            Desserializar<T>, ct);

    // ---------- O ciclo de uma chamada ----------

    private async Task<T> ChamarAsync<T>(int usuarioId, Natureza natureza, Func<HttpRequestMessage> montar,
        Func<HttpResponseMessage, CancellationToken, Task<T>> ler, CancellationToken ct)
    {
        var sessao = await SessaoDoUsuarioAsync(usuarioId, ct);
        var acesso = sessao.Atual;
        var entrouNestaChamada = false;
        if (acesso is null) (acesso, entrouNestaChamada) = await EntrarAsync(sessao, null, ct);

        for (var renovou = false; ; renovou = true)
        {
            using var pedido = montar();
            if (natureza is Natureza.Escrita or Natureza.Envio or Natureza.IA) pedido.Headers.Add("X-CSRF-TOKEN", acesso.Csrf);
            using var resposta = await EnviarAsync(acesso.Http, pedido, Prazo(natureza), ct);
            if (resposta.StatusCode != HttpStatusCode.Unauthorized && !await CsrfRecusadoAsync(resposta, ct))
                return await TratarAsync(resposta, ler, ct);

            // A sessão que esta chamada acabou de abrir, ou a renovada, também foi recusada: não é
            // sessão vencida, é o CRM recusando a pessoa. Renovar de novo só gastaria bilhete.
            if (renovou || entrouNestaChamada)
            {
                sessao.Recusar(acesso, relogio.GetUtcNow() + PausaDepoisDaRecusa);
                throw new CrmIndisponivel(CrmIndisponivel.AcessoRecusado);
            }
            (acesso, _) = await EntrarAsync(sessao, acesso, ct);
        }
    }

    /// <summary>
    /// O 400 do antiforgery: o csrf não vale mais para o cookie da sessão (o CRM trocou as chaves,
    /// reiniciou sem elas). É sessão vencida, não recusa — e é seguro repetir, porque o filtro do CRM
    /// valida o csrf ANTES de executar a rota. ⚠️ O único sinal que o CRM dá é a frase: o middleware
    /// dele (Program.cs, <c>catch (AntiforgeryValidationException)</c>) devolve 400 com exatamente
    /// <see cref="FraseDoCsrfRecusado"/>, sem cabeçalho nem código próprio. Se a frase mudar lá, este
    /// reconhecimento para (o teste do CRM de mentira usa a mesma constante) e o 400 vira
    /// <see cref="RecusaDoCrm"/> com a frase — nada quebra, só deixa de renovar sozinho.
    /// </summary>
    private static async Task<bool> CsrfRecusadoAsync(HttpResponseMessage resposta, CancellationToken ct)
        => resposta.StatusCode == HttpStatusCode.BadRequest
           && await FraseDoErroAsync(resposta, ct) == FraseDoCsrfRecusado;

    private TimeSpan Prazo(Natureza n) => n switch
    {
        Natureza.Leitura => prazos.Leitura,
        Natureza.Escrita => prazos.Escrita,
        Natureza.IA => prazos.IA ?? prazos.Escrita,
        _ => prazos.Envio, // Envio e Midia
    };

    /// <summary>Envia com prazo próprio. O corpo é lido dentro do prazo (ResponseContentRead).</summary>
    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, HttpRequestMessage pedido, TimeSpan prazo, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(prazo);
        try
        {
            return await http.SendAsync(pedido, HttpCompletionOption.ResponseContentRead, limite.Token);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new CrmIndisponivel(Demorou, e);
        }
        catch (HttpRequestException e)
        {
            throw new CrmIndisponivel(ForaDoAr, e);
        }
    }

    private static async Task<T> TratarAsync<T>(HttpResponseMessage resposta,
        Func<HttpResponseMessage, CancellationToken, Task<T>> ler, CancellationToken ct)
    {
        if (resposta.IsSuccessStatusCode) return await ler(resposta, ct);
        var status = (int)resposta.StatusCode;
        var frase = await FraseDoErroAsync(resposta, ct);
        // 408 e 429 não são "não" do CRM: são "agora não".
        if (status is >= 400 and < 500 and not 408 and not 429)
            throw new RecusaDoCrm(frase ?? $"O CRM recusou o pedido (código {status}).", status);
        throw new CrmIndisponivel(frase ?? ForaDoAr);
    }

    private static async Task<T> Desserializar<T>(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            return await resposta.Content.ReadFromJsonAsync<T>(Json, ct)
                   ?? throw new CrmIndisponivel(FormatoDesconhecido);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            throw new CrmIndisponivel(FormatoDesconhecido, e);
        }
    }

    /// <summary>A frase de <c>{ "error": "…" }</c>, ou <c>null</c> quando o corpo não é isso (página do proxy, vazio).</summary>
    private static async Task<string?> FraseDoErroAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var erro = JsonSerializer.Deserialize<AppError>(texto, Json);
            return string.IsNullOrWhiteSpace(erro?.Error) ? null : erro.Error.Trim();
        }
        catch (JsonException) { return null; }
    }

    private static async Task<byte[]> LerArquivoAsync(Stream arquivo, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        var bloco = new byte[81920];
        int lidos;
        while ((lidos = await arquivo.ReadAsync(bloco, ct)) > 0)
        {
            if (memoria.Length + lidos > TetoDoAnexo) throw new RecusaDoCrm("Selecione um arquivo de até 16 MB.", 413);
            memoria.Write(bloco, 0, lidos);
        }
        return memoria.ToArray();
    }

    // ---------- Endereço e sessões ----------

    private async Task<string?> EnderecoAsync(bool forcar, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        if (!forcar && endereco is { } lido && agora - lido.LidoEm < ValidadeDoEndereco) return lido.Endereco;
        await leituraDoEndereco.WaitAsync(ct);
        try
        {
            // Quem esperou a vaga encontra o endereço que o outro acabou de ler.
            if (!forcar && endereco is { } recente && relogio.GetUtcNow() - recente.LidoEm < ValidadeDoEndereco) return recente.Endereco;
            await using var escopo = escopos.CreateAsyncScope();
            var valor = await escopo.ServiceProvider.GetRequiredService<ParametrosService>().ObterEnderecoDoCrmAsync(ct);
            endereco = (valor, relogio.GetUtcNow());
            return valor;
        }
        finally { leituraDoEndereco.Release(); }
    }

    private async Task<SessaoNoCrm> SessaoDoUsuarioAsync(int usuarioId, CancellationToken ct)
    {
        var atual = await EnderecoAsync(forcar: false, ct) ?? throw new CrmNaoConfigurado();
        lock (trava)
        {
            // Endereço novo = CRM novo: os cookies do antigo não servem lá (e não devem ir para lá).
            if (!sessoes.TryGetValue(usuarioId, out var sessao) || sessao.Endereco != atual)
                sessoes[usuarioId] = sessao = new SessaoNoCrm(usuarioId, atual);
            return sessao;
        }
    }

    /// <summary>
    /// Entra no CRM pelo bilhete, uma entrada por vez por pessoa. <paramref name="vista"/> é a sessão
    /// com que a chamada tomou 401 (ou <c>null</c> se não havia): se outra chamada já a trocou, não
    /// entra de novo — usa a nova.
    /// </summary>
    private async Task<(Acesso Acesso, bool Entrou)> EntrarAsync(SessaoNoCrm sessao, Acesso? vista, CancellationToken ct)
    {
        await sessao.Entrada.WaitAsync(ct);
        try
        {
            if (sessao.Atual is { } atual && !ReferenceEquals(atual, vista)) return (atual, false);
            if (sessao.PausadaAte is { } ate && relogio.GetUtcNow() < ate) throw new CrmIndisponivel(sessao.MotivoDaPausa);
            try
            {
                var acesso = await FazerEntradaAsync(sessao, ct);
                // O HttpClient da sessão anterior não é descartado: outra chamada pode estar nele (ver EsquecerSessao).
                sessao.Atual = acesso;
                return (acesso, true);
            }
            catch (AcessoNegado negado)
            {
                sessao.Recusar(vista, relogio.GetUtcNow() + PausaDepoisDaRecusa, negado.Message);
                throw new CrmIndisponivel(negado.Message);
            }
        }
        finally { sessao.Entrada.Release(); }
    }

    /// <summary>session (csrf anônimo + cookie antiforgery) → bilhete → login/ticket → session (o csrf NOVO).</summary>
    private async Task<Acesso> FazerEntradaAsync(SessaoNoCrm sessao, CancellationToken ct)
    {
        // Cookies limpos a cada entrada: nada da sessão recusada vai junto.
        var http = new HttpClient(criarCano(new CookieContainer()), disposeHandler: true)
        {
            BaseAddress = new Uri(sessao.Endereco + "/"),
            Timeout = Timeout.InfiniteTimeSpan, // o prazo é por chamada (EnviarAsync)
        };
        try
        {
            var anonima = await SessaoCruaAsync(http, ct);

            string bilhete;
            await using (var escopo = escopos.CreateAsyncScope())
                bilhete = await escopo.ServiceProvider.GetRequiredService<IEntradaNoCrmService>().EmitirAsync(sessao.UsuarioId, ct);

            using (var pedido = Post("/api/login/ticket", JsonContent.Create(new AppTicketLogin(bilhete), options: Json)))
            {
                pedido.Headers.Add("X-CSRF-TOKEN", anonima.Csrf);
                using var resposta = await EnviarAsync(http, pedido, prazos.Escrita, ct);
                if (!resposta.IsSuccessStatusCode)
                {
                    var status = (int)resposta.StatusCode;
                    if (status == 429) throw new AcessoNegado(TentativasDemais);
                    // 400 (csrf), 401 (bilhete), 403 e 409 (CRM sem o login do sistema): o acesso, não a rede.
                    if (status is >= 400 and < 500 and not 408) throw new AcessoNegado(CrmIndisponivel.AcessoRecusado);
                    throw new CrmIndisponivel(await FraseDoErroAsync(resposta, ct) ?? ForaDoAr);
                }
            }

            var logada = await SessaoCruaAsync(http, ct);
            if (string.IsNullOrEmpty(logada.AgentId)) throw new AcessoNegado(CrmIndisponivel.AcessoRecusado);
            return new Acesso(http, logada.Csrf);
        }
        catch
        {
            http.Dispose();
            throw;
        }
    }

    private async Task<AppSession> SessaoCruaAsync(HttpClient http, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/session");
        using var resposta = await EnviarAsync(http, pedido, prazos.Leitura, ct);
        var sessao = await TratarAsync(resposta, Desserializar<AppSession>, ct);
        return string.IsNullOrEmpty(sessao.Csrf) ? throw new CrmIndisponivel(FormatoDesconhecido) : sessao;
    }

    /// <summary>Uma sessão aberta: o HttpClient com os cookies dela e o csrf que vale para ela.</summary>
    private sealed record Acesso(HttpClient Http, string Csrf);

    /// <summary>A recusa de acesso durante a entrada (vira <see cref="CrmIndisponivel"/> e pausa as entradas).</summary>
    private sealed class AcessoNegado(string mensagem) : Exception(mensagem);

    private sealed class SessaoNoCrm(int usuarioId, string endereco)
    {
        public int UsuarioId { get; } = usuarioId;
        public string Endereco { get; } = endereco;
        public SemaphoreSlim Entrada { get; } = new(1, 1);

        private readonly object trava = new();
        private Acesso? atual;
        private DateTimeOffset? pausadaAte;
        private string motivoDaPausa = CrmIndisponivel.AcessoRecusado;

        public Acesso? Atual
        {
            get { lock (trava) return atual; }
            set { lock (trava) atual = value; }
        }

        public DateTimeOffset? PausadaAte { get { lock (trava) return pausadaAte; } }
        public string MotivoDaPausa { get { lock (trava) return motivoDaPausa; } }

        /// <summary>Descarta a sessão recusada (se ainda é a atual) e pausa as entradas até <paramref name="ate"/>.</summary>
        public void Recusar(Acesso? recusado, DateTimeOffset ate, string motivo = CrmIndisponivel.AcessoRecusado)
        {
            lock (trava)
            {
                if (recusado is null || ReferenceEquals(atual, recusado)) atual = null;
                pausadaAte = ate;
                motivoDaPausa = motivo;
            }
        }
    }
}
