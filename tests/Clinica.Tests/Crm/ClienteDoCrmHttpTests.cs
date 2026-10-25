using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Crm;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O cliente HTTP da API do CRM (PR 1 do WhatsApp nativo, docs/whatsapp-nativo-implementacao.md).
/// O CRM aqui é de mentira, mas faz o que o de verdade faz no que importa ao cliente: cookie
/// antiforgery + csrf anônimo, bilhete conferido pelo MESMO <see cref="EntradaNoCrmService"/> que a
/// ponte usa (uso único, no banco), cookie de sessão, csrf novo depois do login, 401 sem sessão e
/// 400 quando a escrita chega sem o csrf da sessão. O bilhete é emitido e conferido de verdade.
/// </summary>
public sealed class ClienteDoCrmHttpTests : IDisposable
{
    const string EnderecoA = "https://crm.teste";
    const string EnderecoB = "https://crm-novo.teste";
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // O CRM exige Guid "D" (com hífens) no Id do envio e da nota: "N" volta 400 (testado no servidor real).
    static readonly string IdDoEnvio = Guid.NewGuid().ToString("D");
    static readonly string IdDaNota = Guid.NewGuid().ToString("D");

    readonly SqliteConnection conexao = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ServiceProvider servicos;
    readonly CrmDeMentira crm;
    readonly RelogioDeTeste relogio = new();
    readonly ClienteDoCrmHttp cliente;

    public ClienteDoCrmHttpTests()
    {
        conexao.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        // O bilhete aponta para o usuário (FK): a Ana (7) e a Bia (8) dividem o PC do balcão.
        foreach (var (id, login) in new[] { (7, "ana"), (8, "bia") })
        {
            var (hash, sal) = HashSenha.Gerar("senha-do-balcao");
            db.Usuarios.Add(new UsuarioSistema { Id = id, Login = login, Nome = login, Perfil = PerfilAcesso.Recepcao, Ativo = true,
                SenhaHash = hash, SenhaSalt = sal });
        }
        db.SaveChanges();
        var colecao = new ServiceCollection();
        colecao.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(conexao));
        colecao.AddScoped<IClinicaRepositorio, ClinicaRepositorio>();
        colecao.AddScoped<ParametrosService>();
        colecao.AddScoped<IEntradaNoCrmService, EntradaNoCrmService>();
        servicos = colecao.BuildServiceProvider();
        crm = new CrmDeMentira(new EntradaNoCrmService(new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options)));
        cliente = Cliente(TimeSpan.FromSeconds(10));
    }

    ClienteDoCrmHttp Cliente(TimeSpan prazo)
        => new(servicos.GetRequiredService<IServiceScopeFactory>(), c => new Cano(crm, c), relogio,
            new ClienteDoCrmHttp.Prazos(prazo, prazo, prazo));

    async Task Configurar(string endereco)
        => await new ParametrosService(new ClinicaRepositorio(db)).SalvarEnderecoDoCrmAsync(endereco);

    int Bilhetes() => db.BilhetesEntradaCrm.AsNoTracking().Count();

    static AppConversation Conversa(string id, int versao = 1) => new(id, "Maria", "5511999990000", "whatsapp", "clinica",
        "recepcao", null, false, versao, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, "", [], [], [], [], null);

    static HttpResponseMessage Responder(object corpo, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(corpo, Json), Encoding.UTF8, "application/json") };

    static HttpResponseMessage Erro(int status, string frase) => Responder(new AppError(frase), (HttpStatusCode)status);

    public void Dispose()
    {
        cliente.Dispose();
        servicos.Dispose();
        db.Dispose();
        conexao.Dispose();
    }

    [Fact] public async Task Entra_com_o_bilhete_na_ordem_certa_e_escreve_com_o_csrf_novo()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho == "/api/conversations" ? Responder(Array.Empty<AppConversationSummary>()) : Responder(Conversa("c1"));

        Assert.Empty(await cliente.ListarConversasAsync(7));
        await cliente.AssumirAsync(7, "c1", new AppChange(3, null, null));

        Assert.Equal(
        [
            "GET /api/session csrf=",
            "POST /api/login/ticket csrf=anon-1",
            "GET /api/session csrf=",
            "GET /api/conversations csrf=",
            "POST /api/conversations/c1/claim csrf=csrf-s1",
        ], crm.Pedidos.Select(p => $"{p.Metodo} {p.Caminho} csrf={p.Csrf}"));
        Assert.Equal("u7", crm.Pedidos.Last().Agente);
        // O bilhete foi gasto pela conferência da ponte: uso único, e não serve de novo.
        Assert.NotNull((await db.BilhetesEntradaCrm.AsNoTracking().SingleAsync()).UsadoEm);
        Assert.Contains("\"version\":3", crm.Pedidos.Last().Corpo);
    }

    [Fact] public async Task Sessao_vencida_renova_uma_vez_e_repete_a_chamada()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Conversa("c1"));
        await cliente.AbrirConversaAsync(7, "c1");
        crm.Revogar();

        var conversa = await cliente.ResponderAsync(7, "c1", new AppReply(1, IdDoEnvio, "Bom dia"));

        Assert.Equal("c1", conversa.Id);
        Assert.Equal(2, Bilhetes());
        var reply = crm.Pedidos.Where(p => p.Caminho == "/api/conversations/c1/reply").ToList();
        Assert.Equal([401, 200], reply.Select(p => p.Status));
        Assert.Equal(["csrf-s1", "csrf-s2"], reply.Select(p => p.Csrf));
    }

    [Fact] public async Task Dois_401_seguidos_viram_CrmIndisponivel_e_pausam_as_entradas()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);
        crm.RecusarToda401 = true;

        var e = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.ListarConversasAsync(7));
        Assert.Equal(CrmIndisponivel.AcessoRecusado, e.Message);
        Assert.Equal(2, Bilhetes()); // a primeira sessão + UMA renovação, não um laço

        // Pausado: a releitura de 7 s não gasta bilhete nem o limite de login (que é por IP).
        var antes = crm.Pedidos.Count;
        Assert.Equal(CrmIndisponivel.AcessoRecusado, (await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.ListarConversasAsync(7))).Message);
        Assert.Equal(antes, crm.Pedidos.Count);

        relogio.Agora += ClienteDoCrmHttp.PausaDepoisDaRecusa + TimeSpan.FromSeconds(1);
        crm.RecusarToda401 = false;
        await cliente.ListarConversasAsync(7);
        Assert.Equal(3, Bilhetes());
    }

    [Fact] public async Task Sessao_nova_que_toma_401_nao_renova_de_novo()
    {
        await Configurar(EnderecoA);
        crm.RecusarToda401 = true;

        var e = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.ListarConversasAsync(7));

        Assert.Equal(CrmIndisponivel.AcessoRecusado, e.Message);
        Assert.Equal(1, Bilhetes());
    }

    [Fact] public async Task Bilhete_recusado_e_acesso_recusado_nao_fora_do_ar()
    {
        await Configurar(EnderecoA);
        crm.RecusarBilhetes = true;

        var e = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.SessaoAsync(7));

        Assert.Equal(CrmIndisponivel.AcessoRecusado, e.Message);
        Assert.DoesNotContain(crm.Pedidos, p => p.Caminho == "/api/conversations");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Erro_4xx_do_CRM_vira_RecusaDoCrm_com_a_frase_e_o_status(int status)
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Erro(status, "Esta conversa está com outra pessoa.");

        var e = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.AssumirAsync(7, "c1", new AppChange(1, null, null)));

        Assert.Equal("Esta conversa está com outra pessoa.", e.Message);
        Assert.Equal(status, e.Status);
        Assert.Equal(status == 409, e.Conflito);
        Assert.IsAssignableFrom<InvalidOperationException>(e); // recusa de regra, como as da Application
        Assert.Equal(1, Bilhetes()); // recusa não é sessão vencida: nada de bilhete novo
    }

    [Fact] public async Task Recusa_sem_frase_ainda_diz_o_codigo()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound);

        var e = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.AbrirConversaAsync(7, "c1"));

        Assert.Equal("O CRM recusou o pedido (código 404).", e.Message);
    }

    [Fact] public async Task Erro_5xx_vira_CrmIndisponivel_e_a_escrita_nao_e_repetida()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho.EndsWith("/close")
            ? Erro(503, "O CRM está reiniciando.")
            : new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>502</html>") };

        var semFrase = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.ListarConversasAsync(7));
        var comFrase = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.EncerrarAsync(7, "c1", new AppClose(2, "resolved", "ok")));

        Assert.Equal(ClienteDoCrmHttp.ForaDoAr, semFrase.Message);
        Assert.Equal("O CRM está reiniciando.", comFrase.Message);
        Assert.Single(crm.Pedidos, p => p.Caminho == "/api/conversations/c1/close");
    }

    [Fact] public async Task Demora_e_rede_caida_viram_CrmIndisponivel()
    {
        await Configurar(EnderecoA);
        using var apressado = Cliente(TimeSpan.FromMilliseconds(300));
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await apressado.ListarConversasAsync(7);

        crm.Demora = TimeSpan.FromSeconds(5);
        var demorou = await Assert.ThrowsAsync<CrmIndisponivel>(() => apressado.ResponderAsync(7, "c1", new AppReply(1, IdDoEnvio, "Oi")));
        Assert.Equal(ClienteDoCrmHttp.Demorou, demorou.Message);
        Assert.Single(crm.Pedidos, p => p.Caminho == "/api/conversations/c1/reply"); // sem retry em escrita

        crm.Demora = TimeSpan.Zero;
        crm.Rota = (_, _) => throw new HttpRequestException("rede caiu");
        var caiu = await Assert.ThrowsAsync<CrmIndisponivel>(() => apressado.ListarConversasAsync(7));
        Assert.Equal(ClienteDoCrmHttp.ForaDoAr, caiu.Message);
        Assert.IsType<HttpRequestException>(caiu.InnerException);
    }

    [Fact] public async Task Quem_cancela_recebe_o_cancelamento_e_nao_CRM_fora_do_ar()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);
        crm.Demora = TimeSpan.FromSeconds(5);
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.ListarConversasAsync(7, cancelar.Token));
    }

    [Fact] public async Task Resposta_em_formato_desconhecido_vira_CrmIndisponivel()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") };

        var e = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.ListarDesdeAsync(7, DateTimeOffset.UnixEpoch));

        Assert.Equal(ClienteDoCrmHttp.FormatoDesconhecido, e.Message);
    }

    [Fact] public async Task Toda_escrita_leva_o_csrf_da_sessao_e_leitura_nao()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho switch
        {
            "/api/conversations/c1/attachments" => Responder(new AppAttachment("a1", "c1", "exame.pdf", "application/pdf", "DOCUMENT", 3)),
            "/api/conversations/c1/read" => new HttpResponseMessage(HttpStatusCode.OK),
            "/api/workspace" => Responder(new AppWorkspace([], new() { ["resolved"] = "Resolvido" })),
            "/api/templates/%2B5511" => Responder(Array.Empty<AppTemplate>()),
            _ when p.Caminho.StartsWith("/api/conversations?desde=") => Responder(new AppConversationsSince([], [], DateTimeOffset.UnixEpoch)),
            "/api/conversations" when p.Metodo == "GET" => Responder(Array.Empty<AppConversationSummary>()),
            _ => Responder(Conversa("c1")),
        };
        var mudanca = new AppChange(1, "u2", "recepcao");

        await cliente.NovaConversaAsync(7, new AppNewConversation("clinica", "5511999990000", "Maria"));
        await cliente.ResponderAsync(7, "c1", new AppReply(1, IdDoEnvio, "Oi"));
        await cliente.AnotarAsync(7, "c1", new AppNoteRequest(1, IdDaNota, "Ligar depois"));
        await cliente.MarcarLidaAsync(7, "c1", new AppReadMarker(2));
        await cliente.AssumirAsync(7, "c1", mudanca);
        await cliente.TransferirAsync(7, "c1", mudanca);
        await cliente.ReabrirAsync(7, "c1", mudanca);
        await cliente.DevolverAFilaAsync(7, "c1", mudanca with { AgentId = null });
        await cliente.EncerrarAsync(7, "c1", new AppClose(1, "resolved", "Remarcado"));
        await cliente.LigarFichaAsync(7, "c1", new AppPatientLink(42, "Maria", 1));
        var anexo = await cliente.AnexarAsync(7, "c1", new MemoryStream([1, 2, 3]), "exame.pdf", "application/pdf");
        await cliente.ListarConversasAsync(7);
        await cliente.ListarDesdeAsync(7, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        await cliente.AbrirConversaAsync(7, "c1");
        await cliente.ModelosAsync(7, "+5511");
        var area = await cliente.AreaDeTrabalhoAsync(7);
        await cliente.SessaoAsync(7);

        var depoisDaEntrada = crm.Pedidos.Skip(3).ToList();
        var escritas = depoisDaEntrada.Where(p => p.Metodo == "POST").ToList();
        Assert.Equal(11, escritas.Count);
        Assert.All(escritas, p => Assert.Equal("csrf-s1", p.Csrf));
        Assert.All(depoisDaEntrada.Where(p => p.Metodo == "GET"), p => Assert.Equal("", p.Csrf));
        Assert.All(depoisDaEntrada, p => Assert.Equal(200, p.Status));
        Assert.Equal(1, Bilhetes());
        Assert.Equal("a1", anexo.Id);
        Assert.Equal("Resolvido", area.Outcomes["resolved"]);
        // O "+" do fuso chega como "+", não como espaço; o "+" do remetente também.
        Assert.Contains(depoisDaEntrada, p => p.Caminho == "/api/conversations?desde=2026-10-24T12%3A00%3A00.0000000%2B00%3A00");
        var envio = escritas.Single(p => p.Caminho.EndsWith("/attachments"));
        Assert.StartsWith("multipart/form-data", envio.TipoDoCorpo);
        Assert.Contains("filename=exame.pdf", envio.Corpo);
        Assert.Contains("name=file", envio.Corpo);
        Assert.Contains("\"agentId\":null", escritas.Single(p => p.Caminho.EndsWith("/reassign")).Corpo);
    }

    // ---------- A transferência para uma pessoa: aceitar, recusar, cancelar (25/10) ----------

    [Fact] public async Task A_decisao_da_transferencia_vai_pela_rota_do_pedido_com_o_csrf_e_o_motivo()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho.StartsWith("/api/transfers/", StringComparison.Ordinal)
            ? Responder(new AppTransferDecided("c1"))
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        var aceito = await cliente.DecidirTransferenciaAsync(7, "p 1", "accept", new AppTransferDecision());
        await cliente.DecidirTransferenciaAsync(7, "p 1", "reject", new AppTransferDecision("Estou no caixa agora."));
        await cliente.DecidirTransferenciaAsync(7, "p 1", "cancel", new AppTransferDecision());

        Assert.Equal("c1", aceito.ConversationId);
        var depois = crm.Pedidos.Skip(3).ToList();
        Assert.Equal(["POST /api/transfers/p%201/accept csrf=csrf-s1", "POST /api/transfers/p%201/reject csrf=csrf-s1",
            "POST /api/transfers/p%201/cancel csrf=csrf-s1"], depois.Select(p => $"{p.Metodo} {p.Caminho} csrf={p.Csrf}"));
        Assert.Contains("\"reason\":\"Estou no caixa agora.\"", depois[1].Corpo);
        // Ação que o CRM não conhece nem sai daqui (nada da tela vira outro segmento de rota).
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cliente.DecidirTransferenciaAsync(7, "p1", "../reassign", new AppTransferDecision()));
        Assert.Equal(3, crm.Pedidos.Skip(3).Count());
    }

    [Fact] public async Task A_recusa_da_transferencia_chega_com_a_frase_do_CRM()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Erro(409, "Esta transferência não está mais pendente.");
        var erro = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.DecidirTransferenciaAsync(7, "p1", "accept", new AppTransferDecision()));
        Assert.Equal(("Esta transferência não está mais pendente.", 409), (erro.Message, erro.Status));
    }

    // ---------- O @SemDor (24/10) ----------

    [Fact] public async Task O_SemDor_vai_pelo_caminho_autenticado_com_o_csrf_e_volta_nos_records_do_contrato()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho switch
        {
            "/api/semdor/estado" => Responder(new AppSemDorState(true, true, DateTimeOffset.UnixEpoch, null, "")),
            "/api/conversations/c%201/semdor/sugestao" => Responder(new AppSemDorSuggestion("Olá, Maria! Vou verificar na agenda.", "sugestao")),
            "/api/conversations/c%201/semdor/resumo" => Responder(new AppSemDorSummary("Remarcar", "Ofereceu dois horários", "Confirmar o novo")),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

        var estado = await cliente.EstadoDoSemDorAsync(7);
        var sugestao = await cliente.SugerirComSemDorAsync(7, "c 1", new AppSemDorRequest(null));
        await cliente.SugerirComSemDorAsync(7, "c 1", new AppSemDorRequest("pode vir quinta"));
        var resumo = await cliente.ResumirComSemDorAsync(7, "c 1");

        Assert.True(estado.Ligada);
        Assert.Equal("sugestao", sugestao.Modo);
        Assert.Equal(("Remarcar", "Confirmar o novo"), (resumo.Quer, resumo.Falta));
        var depois = crm.Pedidos.Skip(3).ToList();
        Assert.Equal(["GET /api/semdor/estado csrf=", "POST /api/conversations/c%201/semdor/sugestao csrf=csrf-s1",
            "POST /api/conversations/c%201/semdor/sugestao csrf=csrf-s1", "POST /api/conversations/c%201/semdor/resumo csrf=csrf-s1"],
            depois.Select(p => $"{p.Metodo} {p.Caminho} csrf={p.Csrf}"));
        // Campo vazio vai como null (é o que o CRM lê como "sugira"); o rascunho vai como está; o resumo leva "{}".
        Assert.Contains("\"rascunho\":null", depois[1].Corpo);
        Assert.Contains("\"rascunho\":\"pode vir quinta\"", depois[2].Corpo);
        Assert.Equal("{}", depois[3].Corpo);
        Assert.Equal(1, Bilhetes());
    }

    [Fact] public async Task O_SemDor_desligado_e_sem_resposta_chegam_com_a_frase_do_CRM()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho.EndsWith("/sugestao")
            ? Erro(409, "O @SemDor está desligado neste CRM.")
            : Erro(502, "O @SemDor não conseguiu resumir agora. Tente de novo em instantes.");

        var desligado = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.SugerirComSemDorAsync(7, "c1", new AppSemDorRequest(null)));
        var falhou = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.ResumirComSemDorAsync(7, "c1"));

        Assert.Equal(("O @SemDor está desligado neste CRM.", 409), (desligado.Message, desligado.Status));
        Assert.Equal("O @SemDor não conseguiu resumir agora. Tente de novo em instantes.", falhou.Message);
        Assert.Single(crm.Pedidos, p => p.Caminho.EndsWith("/resumo")); // 502 não se repete: cada pedido é uma chamada paga à IA
    }

    [Fact] public async Task O_SemDor_espera_a_IA_mais_que_uma_escrita_comum()
    {
        // O CRM espera a Anthropic por até 30 s: com o prazo da escrita (20 s) o app diria "demorou" com a resposta a caminho.
        Assert.True(ClienteDoCrmHttp.Prazos.Padrao.IA > TimeSpan.FromSeconds(30));
        Assert.True(ClienteDoCrmHttp.Prazos.Padrao.IA > ClienteDoCrmHttp.Prazos.Padrao.Escrita);

        await Configurar(EnderecoA);
        using var curto = new ClienteDoCrmHttp(servicos.GetRequiredService<IServiceScopeFactory>(), c => new Cano(crm, c), relogio,
            new ClienteDoCrmHttp.Prazos(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(10)));
        crm.Rota = (p, _) => p.Caminho.EndsWith("/sugestao") ? Responder(new AppSemDorSuggestion("Olá!", "sugestao")) : Responder(Conversa("c1"));
        await curto.AbrirConversaAsync(7, "c1");

        crm.Demora = TimeSpan.FromSeconds(1);
        Assert.Equal("Olá!", (await curto.SugerirComSemDorAsync(7, "c1", new AppSemDorRequest(null))).Texto);
        Assert.Equal(ClienteDoCrmHttp.Demorou, (await Assert.ThrowsAsync<CrmIndisponivel>(() => curto.AssumirAsync(7, "c1", new AppChange(1, null, null)))).Message);
    }

    // ---------- A presença do app (25/10, D12) ----------

    [Fact] public async Task O_pulso_de_presenca_vai_com_o_csrf_e_renova_a_sessao_vencida()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => p.Caminho == "/api/presence" ? Responder(new AppPresence(true, 120)) : new HttpResponseMessage(HttpStatusCode.NotFound);

        var pulso = await cliente.PulsoDePresencaAsync(7);

        Assert.Equal((true, 120), (pulso.Connected, pulso.LeaseSeconds));
        var pedido = crm.Pedidos.Last();
        Assert.Equal(("POST", "/api/presence", "csrf-s1", "u7", "{}"), (pedido.Metodo, pedido.Caminho, pedido.Csrf, pedido.Agente, pedido.Corpo));

        // A sessão do CRM venceu (2 h) com a pessoa ainda logada no app: entra de novo e repete UMA vez.
        crm.Revogar();
        Assert.True((await cliente.PulsoDePresencaAsync(7)).Connected);
        Assert.Equal([401, 200], crm.Pedidos.Where(p => p.Caminho == "/api/presence").Skip(1).Select(p => p.Status));
        Assert.Equal(2, Bilhetes());
    }

    [Fact] public async Task O_anexo_e_reenviado_inteiro_quando_a_sessao_venceu()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(new AppAttachment("a1", "c1", "foto.jpg", "image/jpeg", "IMAGE", 4));
        await cliente.SessaoAsync(7);
        crm.Revogar();

        await cliente.AnexarAsync(7, "c1", new MemoryStream("JPEG"u8.ToArray()), "foto.jpg", "image/jpeg");

        var envios = crm.Pedidos.Where(p => p.Caminho.EndsWith("/attachments")).ToList();
        Assert.Equal([401, 200], envios.Select(p => p.Status));
        Assert.All(envios, p => Assert.Contains("JPEG", p.Corpo));
    }

    // ---------- A mídia (24/10/2026) ----------

    static HttpResponseMessage Arquivo(byte[] bytes, string mime, string? nome = null)
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        r.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        if (nome is not null)
            r.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "\"" + nome + "\"", FileNameStar = nome };
        return r;
    }

    static AppMessage Mensagem(string direcao, string tipo, string id = "wamid.1", string? anexo = null)
        => new(id, direcao, "", tipo, null, DateTimeOffset.UnixEpoch, direcao == "in" ? "received" : "seen", AttachmentId: anexo);

    [Fact] public async Task A_midia_recebida_vem_da_rota_da_mensagem_sem_csrf_com_o_tipo_e_o_nome()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => Arquivo([137, 80, 78, 71], "image/png", "Pedido médico.png");

        var a = await cliente.MidiaAsync(7, "c1", Mensagem("in", "IMAGE"));

        Assert.Equal([137, 80, 78, 71], a.Conteudo);
        Assert.Equal("image/png", a.Mime);
        Assert.Equal("Pedido médico.png", a.Nome);
        var pedido = crm.Pedidos.Last();
        Assert.Equal("GET /api/conversations/c1/media/wamid.1?download=true", $"{pedido.Metodo} {pedido.Caminho}");
        Assert.Equal("", pedido.Csrf);
    }

    [Fact] public async Task A_midia_enviada_vem_da_rota_do_anexo()
    {
        await Configurar(EnderecoA);
        crm.Rota = (p, _) => Arquivo("%PDF-"u8.ToArray(), "application/pdf");

        var a = await cliente.MidiaAsync(7, "c1", Mensagem("out", "DOCUMENT", anexo: "a9"));

        Assert.Equal("/api/attachments/a9?download=true", crm.Pedidos.Last().Caminho);
        Assert.Null(a.Nome);
    }

    [Fact] public async Task Mensagem_sem_arquivo_e_recusada_sem_ir_ao_CRM()
    {
        await Configurar(EnderecoA);

        var e = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.MidiaAsync(7, "c1", Mensagem("in", "TEXT")));

        Assert.Equal(MidiaDaConversa.SemArquivo, e.Message);
        Assert.Empty(crm.Pedidos);
    }

    [Fact] public async Task A_midia_renova_a_sessao_vencida_e_repete()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Arquivo([1, 2], "audio/ogg");
        await cliente.SessaoAsync(7);
        crm.Revogar();

        var a = await cliente.MidiaAsync(7, "c1", Mensagem("in", "AUDIO"));

        Assert.Equal([1, 2], a.Conteudo);
        Assert.Equal([401, 200], crm.Pedidos.Where(p => p.Caminho.Contains("/media/")).Select(p => p.Status));
        Assert.Equal(2, Bilhetes());
    }

    [Fact] public async Task Arquivo_maior_que_o_teto_e_recusado_e_vazio_e_falha()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Arquivo(new byte[MidiaDaConversa.Teto + 1], "video/mp4");
        var grande = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.MidiaAsync(7, "c1", Mensagem("in", "VIDEO")));
        Assert.Equal(MidiaDaConversa.GrandeDemais, grande.Message);

        crm.Rota = (_, _) => Arquivo([], "video/mp4");
        var vazio = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.MidiaAsync(7, "c1", Mensagem("in", "VIDEO")));
        Assert.Equal(ClienteDoCrmHttp.ArquivoVazio, vazio.Message);
    }

    [Fact] public async Task A_recusa_do_CRM_na_midia_chega_com_a_frase_dele()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Erro(409, "Este anexo não possui um endereço de mídia compatível com o provedor do WhatsApp.");

        var e = await Assert.ThrowsAsync<RecusaDoCrm>(() => cliente.MidiaAsync(7, "c1", Mensagem("in", "VIDEO")));

        Assert.Equal(409, e.Status);
        Assert.StartsWith("Este anexo não possui", e.Message);
    }

    [Fact] public async Task A_gravacao_sobe_com_recording_true_e_o_anexo_comum_nao()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(new AppAttachment("a1", "c1", "gravacao.ogg", "audio/ogg", "AUDIO", 3));

        await cliente.AnexarAsync(7, "c1", new MemoryStream([1, 2, 3]), "exame.pdf", "application/pdf");
        await cliente.AnexarAsync(7, "c1", new MemoryStream([4, 5, 6]), "gravacao.webm", "audio/webm;codecs=opus", gravacao: true);

        var envios = crm.Pedidos.Where(p => p.Caminho.EndsWith("/attachments")).ToList();
        Assert.Equal(2, envios.Count);
        Assert.DoesNotContain("name=recording", envios[0].Corpo);
        Assert.Contains("name=recording", envios[1].Corpo);
        Assert.Contains("filename=gravacao.webm", envios[1].Corpo);
        Assert.All(envios, p => Assert.Equal("csrf-s1", p.Csrf));
    }

    [Fact] public async Task Dois_usuarios_no_mesmo_PC_nao_dividem_a_sessao()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());

        await cliente.ListarConversasAsync(7);
        await cliente.ListarConversasAsync(8);
        await cliente.ListarConversasAsync(7);
        await cliente.ListarConversasAsync(8);

        var lista = crm.Pedidos.Where(p => p.Caminho == "/api/conversations").ToList();
        Assert.Equal(["u7", "u8", "u7", "u8"], lista.Select(p => p.Agente));
        Assert.Equal(2, Bilhetes());

        // Logout de uma pessoa não derruba a outra.
        cliente.EsquecerSessao(7);
        await cliente.UltimaSaida;
        await cliente.ListarConversasAsync(8);
        Assert.Equal(2, Bilhetes());
        await cliente.ListarConversasAsync(7);
        Assert.Equal(3, Bilhetes());
        Assert.Equal("u7", crm.Pedidos.Last().Agente);
    }

    [Fact] public async Task Csrf_recusado_renova_a_sessao_e_repete_a_escrita_uma_vez()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Conversa("c1"));
        await cliente.AbrirConversaAsync(7, "c1");
        crm.VencerCsrf();

        await cliente.AssumirAsync(7, "c1", new AppChange(1, null, null));

        var claims = crm.Pedidos.Where(p => p.Caminho == "/api/conversations/c1/claim").ToList();
        Assert.Equal([400, 200], claims.Select(p => p.Status));
        Assert.Equal(["csrf-s1", "csrf-s2"], claims.Select(p => p.Csrf));
        Assert.Equal(2, Bilhetes());
    }

    [Fact] public async Task Segundo_csrf_recusado_vira_CrmIndisponivel_e_pausa()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Conversa("c1"));
        await cliente.AbrirConversaAsync(7, "c1");
        crm.RecusarCsrfSempre = true;

        var e = await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.AssumirAsync(7, "c1", new AppChange(1, null, null)));

        Assert.Equal(CrmIndisponivel.AcessoRecusado, e.Message);
        Assert.Equal(2, Bilhetes());
        var antes = crm.Pedidos.Count;
        await Assert.ThrowsAsync<CrmIndisponivel>(() => cliente.AssumirAsync(7, "c1", new AppChange(1, null, null)));
        Assert.Equal(antes, crm.Pedidos.Count);
    }

    [Fact] public async Task Logout_avisa_o_CRM_e_a_falha_do_aviso_nao_propaga()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);
        await cliente.ListarConversasAsync(8);

        cliente.EsquecerSessao(7);
        await cliente.UltimaSaida;
        var saida = Assert.Single(crm.Pedidos, p => p.Caminho == "/api/logout");
        Assert.Equal(("POST", "csrf-s1", "u7", 200), (saida.Metodo, saida.Csrf, saida.Agente, saida.Status));

        crm.FalharSaida = true;
        cliente.EsquecerSessao(8); // não lança
        await cliente.UltimaSaida;  // e o aviso que falhou não deixa tarefa com exceção
        Assert.True(cliente.UltimaSaida.IsCompletedSuccessfully);
        Assert.Equal(2, crm.Pedidos.Count(p => p.Caminho == "/api/logout"));

        cliente.EsquecerSessao(9); // quem nunca entrou não avisa nada
        await cliente.UltimaSaida;
        Assert.Equal(2, crm.Pedidos.Count(p => p.Caminho == "/api/logout"));
    }

    /// <summary>
    /// O fechamento do app (Sair ou Trocar usuário) é o ponto único de saída, e o processo morre
    /// logo depois: a saída tem de ter CHEGADO ao CRM quando <see cref="SaidaDoCrm.AoFecharOApp"/>
    /// volta — sem esperar o <c>UltimaSaida</c> aqui no teste, que é o que o app não faz.
    /// </summary>
    [Fact] public async Task Fechar_o_app_tira_do_CRM_quem_estava_logado_antes_de_voltar()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);

        Assert.True(SaidaDoCrm.AoFecharOApp(cliente, 7, TimeSpan.FromSeconds(5)));

        var saida = Assert.Single(crm.Pedidos, p => p.Caminho == "/api/logout");
        Assert.Equal(("u7", 200), (saida.Agente, saida.Status));

        // Ninguém logado (desistiu na entrada) e app sem cliente: nada a avisar, e nada lança.
        Assert.False(SaidaDoCrm.AoFecharOApp(cliente, 0, TimeSpan.FromSeconds(5)));
        Assert.False(SaidaDoCrm.AoFecharOApp(null, 7, TimeSpan.FromSeconds(5)));
        Assert.Single(crm.Pedidos, p => p.Caminho == "/api/logout");
    }

    [Fact] public async Task Fechar_o_app_com_o_CRM_fora_do_ar_nao_lanca()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);
        crm.FalharSaida = true;

        Assert.True(SaidaDoCrm.AoFecharOApp(cliente, 7, TimeSpan.FromSeconds(5)));
        Assert.True(cliente.UltimaSaida.IsCompleted);
    }

    [Fact] public async Task Duas_chamadas_simultaneas_que_tomam_401_emitem_um_bilhete()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);
        crm.Revogar();
        crm.SegurarOs401Ate(2); // os dois pedidos tomam 401 juntos, antes de qualquer renovação

        await Task.WhenAll(cliente.ListarConversasAsync(7), cliente.ListarConversasAsync(7));

        Assert.Equal(2, Bilhetes()); // o da primeira sessão + UM da renovação
        Assert.Equal(2, crm.Pedidos.Count(p => p.Caminho == "/api/conversations" && p.Status == 401));
        Assert.Equal(3, crm.Pedidos.Count(p => p.Caminho == "/api/conversations" && p.Status == 200));
    }

    [Fact] public async Task Sem_endereco_e_nao_configurado_e_nao_fora_do_ar()
    {
        Assert.False(await cliente.ConfiguradoAsync());
        var e = await Assert.ThrowsAsync<CrmNaoConfigurado>(() => cliente.ListarConversasAsync(7));
        Assert.IsAssignableFrom<CrmIndisponivel>(e); // a tela que só conhece "fora do ar" ainda não quebra
        Assert.Empty(crm.Pedidos);
        Assert.Equal(0, Bilhetes());

        await Configurar(EnderecoA);
        Assert.True(await cliente.ConfiguradoAsync());
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);
    }

    [Fact] public async Task Endereco_novo_e_sessao_nova_no_CRM_novo()
    {
        await Configurar(EnderecoA);
        crm.Rota = (_, _) => Responder(Array.Empty<AppConversationSummary>());
        await cliente.ListarConversasAsync(7);

        await Configurar(EnderecoB);
        await cliente.ListarConversasAsync(7);
        Assert.Equal("crm.teste", crm.Pedidos.Last().Host); // dentro da validade, o endereço lido continua

        relogio.Agora += ClienteDoCrmHttp.ValidadeDoEndereco + TimeSpan.FromSeconds(1);
        await cliente.ListarConversasAsync(7);

        Assert.Equal(2, Bilhetes());
        var noNovo = crm.Pedidos.Where(p => p.Host == "crm-novo.teste").ToList();
        Assert.Equal(["/api/session", "/api/login/ticket", "/api/session", "/api/conversations"], noNovo.Select(p => p.Caminho));
        Assert.DoesNotContain("sessao=", noNovo[0].Cookies); // os cookies do CRM antigo não vão ao novo
    }

    // ---------- O CRM de mentira ----------

    sealed record Pedido(string Host, string Metodo, string Caminho, string Csrf, string Cookies, string? Agente, string Corpo,
        string TipoDoCorpo)
    {
        public int Status { get; set; }
    }

    sealed class RelogioDeTeste : TimeProvider
    {
        public DateTimeOffset Agora = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    /// <summary>O cano HTTP de uma sessão: entrega ao CRM de mentira os cookies DESTE container e guarda os que ele devolve.</summary>
    sealed class Cano(CrmDeMentira crm, CookieContainer cookies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var resposta = await crm.Atender(request, cookies.GetCookieHeader(uri), ct);
            if (resposta.Headers.TryGetValues("Set-Cookie", out var novos))
                foreach (var c in novos) cookies.SetCookies(uri, c);
            return resposta;
        }
    }

    sealed class CrmDeMentira(EntradaNoCrmService conferente)
    {
        readonly ConcurrentQueue<Pedido> pedidos = new();
        readonly ConcurrentDictionary<string, string> antiforgery = new(); // csrf anônimo → cookie af
        readonly ConcurrentDictionary<string, string> sessoes = new();     // cookie sessao → agente
        readonly SemaphoreSlim conferencia = new(1, 1);                    // um DbContext só do lado de cá
        int numero;
        int abertas;
        int segurar;
        TaskCompletionSource? barreira;

        public IReadOnlyList<Pedido> Pedidos => pedidos.ToList();
        public Func<Pedido, string, HttpResponseMessage> Rota { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
        public bool RecusarToda401 { get; set; }
        public bool RecusarBilhetes { get; set; }
        public TimeSpan Demora { get; set; }
        public bool RecusarCsrfSempre { get; set; }
        public bool FalharSaida { get; set; }
        readonly ConcurrentDictionary<string, bool> csrfVencido = new();

        /// <summary>O csrf das sessões abertas deixa de valer com o cookie delas (o CRM trocou as chaves).</summary>
        public void VencerCsrf() { foreach (var k in sessoes.Keys) csrfVencido[k] = true; }

        /// <summary>Todas as sessões abertas vencem (o cookie de 2 h, ou o CRM reiniciou).</summary>
        public void Revogar() => sessoes.Clear();

        public void SegurarOs401Ate(int quantos)
        {
            segurar = quantos;
            barreira = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<HttpResponseMessage> Atender(HttpRequestMessage request, string cabecalhoDeCookies, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var corpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var cookies = cabecalhoDeCookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.Split('=', 2)).ToDictionary(c => c[0], c => c[1]);
            var csrf = request.Headers.TryGetValues("X-CSRF-TOKEN", out var v) ? v.Single() : "";
            var agente = cookies.TryGetValue("sessao", out var s) && sessoes.TryGetValue(s, out var a) ? a : null;
            var pedido = new Pedido(uri.Host, request.Method.Method, uri.PathAndQuery, csrf, cabecalhoDeCookies, agente, corpo,
                request.Content?.Headers.ContentType?.MediaType ?? "");
            pedidos.Enqueue(pedido);
            var resposta = await Decidir(pedido, cookies, s, ct);
            pedido.Status = (int)resposta.StatusCode;
            return resposta;
        }

        async Task<HttpResponseMessage> Decidir(Pedido p, Dictionary<string, string> cookies, string? sessao, CancellationToken ct)
        {
            if (p.Caminho == "/api/session")
            {
                if (p.Agente is not null)
                    return Responder(new AppSession(p.Agente, "csrf-" + sessao, [new("u7", "Ana")], [new("clinica", "Clínica")], ["recepcao"], true, true, false));
                var n = Interlocked.Increment(ref numero);
                antiforgery["anon-" + n] = "af-" + n;
                var anonima = Responder(new AppSession(null, "anon-" + n, [], [], ["recepcao"], false, true, false));
                anonima.Headers.Add("Set-Cookie", $"af=af-{n}; path=/; secure; httponly");
                return anonima;
            }
            if (p.Caminho == "/api/login/ticket")
            {
                if (!antiforgery.TryGetValue(p.Csrf, out var af) || cookies.GetValueOrDefault("af") != af)
                    return Erro(400, ClienteDoCrmHttp.FraseDoCsrfRecusado);
                var bilhete = JsonSerializer.Deserialize<AppTicketLogin>(p.Corpo, Json)!.Ticket;
                int? usuario;
                await conferencia.WaitAsync(ct);
                try { usuario = await conferente.ConferirAsync(bilhete, ct); }
                finally { conferencia.Release(); }
                if (usuario is null || RecusarBilhetes) return Erro(401, "Não foi possível entrar pela Recepção. Entre com seu usuário e senha.");
                var nova = "s" + Interlocked.Increment(ref abertas);
                sessoes[nova] = "u" + usuario;
                var ok = new HttpResponseMessage(HttpStatusCode.OK);
                ok.Headers.Add("Set-Cookie", $"sessao={nova}; path=/; secure; httponly");
                return ok;
            }
            if (p.Agente is null || RecusarToda401)
            {
                if (barreira is { } b && Interlocked.Decrement(ref segurar) >= 0)
                {
                    if (Volatile.Read(ref segurar) <= 0) b.TrySetResult();
                    await b.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                }
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            // O 400 do antiforgery do CRM: a mesma frase do middleware dele.
            if (p.Metodo == "POST" && (p.Csrf != "csrf-" + sessao || RecusarCsrfSempre || csrfVencido.ContainsKey(sessao!)))
                return Erro(400, ClienteDoCrmHttp.FraseDoCsrfRecusado);
            if (p.Caminho == "/api/logout")
            {
                if (FalharSaida) throw new HttpRequestException("o CRM caiu no meio do logout");
                sessoes.TryRemove(sessao!, out _);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (Demora > TimeSpan.Zero) await Task.Delay(Demora, ct);
            return Rota(p, p.Agente);
        }
    }
}
