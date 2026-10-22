using Bunit;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Clinica.Ui.Blazor.Moldura;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A TELA do chat da equipe sobre o serviço de verdade (SQLite): o que a pessoa vê e clica —
/// o texto longo que vem inteiro só no "Ver inteiro", a sugestão de @, a busca que abre a
/// mensagem antiga, o Esc, a direta só leitura e a prévia sem botão dentro de botão.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class ChatDaEquipeBlazorTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly UsuarioSistema ana = new() { Nome = "Ana Souza", Login = "ana", Perfil = PerfilAcesso.Recepcao, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema paulo = new() { Nome = "Paulo Lima", Login = "paulo", Perfil = PerfilAcesso.Profissional, CriadoEm = new(2026, 1, 1) };
    private readonly UsuarioSistema joana = new() { Nome = "Joana Reis", Login = "joana", Perfil = PerfilAcesso.Enfermagem, CriadoEm = new(2026, 1, 1) };
    private readonly HostQueConta host = new();
    private readonly AvisosDeTeste avisos = new();

    public ChatDaEquipeBlazorTests()
    {
        _conexao.Open();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        db.Database.EnsureCreated();
        db.Usuarios.AddRange(ana, paulo, joana);
        db.SaveChanges();
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(ana);
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _conexao.Dispose();
    }

    private TestContext Contexto(string salvo)
    {
        var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string?>("clinicaChat.ler", _ => true).SetResult(salvo);
        ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            ctx.Services.Remove(d);
        ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conexao));
        ctx.Services.AddTelasBlazor();
        ctx.Services.AddSingleton<IHostDaTela>(host);
        ctx.Services.AddSingleton<IAvisosDoBanco>(avisos);
        return ctx;
    }

    private async Task<int> EnviarAsync(TestContext ctx, UsuarioSistema autor, string conversa, string texto)
    {
        using var escopo = ctx.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<IChatEquipeService>().EnviarAsync(autor.Id, conversa, new(texto));
    }

    private static void Esperar(Func<bool> condicao, string oQue)
    {
        // O chat espera 2 s antes da primeira leitura (a abertura do app é da tela de trabalho).
        var prazo = DateTime.UtcNow + TimeSpan.FromSeconds(12);
        while (!condicao())
        {
            if (DateTime.UtcNow > prazo) throw new TimeoutException($"a tela não chegou a: {oQue}");
            Thread.Sleep(20);
        }
    }

    [Fact]
    public async Task Conversa_aberta_texto_longo_citacao_busca_e_Esc()
    {
        using var ctx = Contexto("janela|canal:geral|1|1|1|0");
        var laudo = string.Join("\n", Enumerable.Range(1, 80).Select(i => $"Linha {i} do laudo da paciente em acompanhamento."));
        await EnviarAsync(ctx, paulo, ChatEquipeRegras.CanalGeral, "O exame da Lúcia ficou pronto");
        await EnviarAsync(ctx, joana, ChatEquipeRegras.CanalGeral, laudo);

        var tela = ctx.RenderComponent<ChatDaEquipe>();
        Esperar(() => tela.Markup.Contains("O exame da Lúcia ficou pronto"), "a conversa aberta");
        tela.FindAll("button button").Should().BeEmpty("botão dentro de botão não é HTML válido");
        tela.Find(".chat-anuncio").GetAttribute("aria-live").Should().Be("polite");

        // O laudo veio só pelo começo; "Ver inteiro" busca o resto.
        tela.Markup.Should().NotContain("Linha 80 do laudo");
        tela.FindAll(".chat-rodape-longa button").First(b => b.TextContent.Contains("Ver inteiro")).Click();
        Esperar(() => tela.Markup.Contains("Linha 80 do laudo"), "o laudo inteiro");

        // @ abre a sugestão com as pessoas da conversa; escolher escreve a citação.
        tela.Find("textarea.chat-campo").Input("Pode ver, @pa");
        tela.FindAll(".chat-mencao").Select(b => b.TextContent.Trim()).Should().Contain(t => t.EndsWith("Paulo Lima"));
        tela.FindAll(".chat-mencao").First(b => b.TextContent.Contains("Paulo Lima")).Click();
        tela.Find("textarea.chat-campo").GetAttribute("value").Should().Be("Pode ver, @Paulo ");
        tela.FindAll(".chat-mencoes").Should().BeEmpty();

        // Esc fecha a sugestão aberta, uma coisa de cada vez.
        tela.Find("textarea.chat-campo").Input("@jo");
        tela.FindAll(".chat-mencoes").Should().ContainSingle();
        await tela.InvokeAsync(() => tela.Instance.Escape());
        tela.FindAll(".chat-mencoes").Should().BeEmpty();

        // Enter envia a citação: a mensagem aparece e quem foi citado é avisado.
        tela.Find("textarea.chat-campo").Input("@Paulo pode ver o exame?");
        await tela.InvokeAsync(() => tela.Instance.EnviarPeloEnter());
        Esperar(() => tela.Markup.Contains("@Paulo pode ver o exame?"), "a mensagem enviada");

        // A busca acha sem acento e abre a conversa NA mensagem, destacada.
        tela.Find("button[aria-label='Procurar nas mensagens']").Click();
        tela.Find(".chat-busca input").Input("lucia");
        Esperar(() => tela.FindAll(".chat-resultado").Count == 1, "o resultado da busca");
        tela.Find(".chat-resultado").Click();
        Esperar(() => tela.FindAll(".chat-historico").Count == 1, "a mensagem achada");
        tela.FindAll(".chat-m.achada").Should().ContainSingle().Which.TextContent.Should().Contain("O exame da Lúcia");
        tela.Find(".chat-historico button").Click();
        Esperar(() => tela.FindAll(".chat-historico").Count == 0 && tela.Markup.Contains("@Paulo pode ver o exame?"), "a volta ao fim");
    }

    [Fact]
    public async Task Direta_com_quem_saiu_da_equipe_mostra_sem_campo_de_escrever()
    {
        var direta = ChatEquipeRegras.Direta(ana.Id, paulo.Id);
        using var ctx = Contexto($"janela|{direta}|1|1|1|0");
        await EnviarAsync(ctx, paulo, direta, "Até amanhã!");
        using (var escopo = ctx.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            (await db.Usuarios.SingleAsync(u => u.Id == paulo.Id)).Ativo = false;
            await db.SaveChangesAsync();
        }

        var tela = ctx.RenderComponent<ChatDaEquipe>();
        Esperar(() => tela.Markup.Contains("Até amanhã!"), "a direta");
        tela.Find(".chat-so-leitura").TextContent.Should().Contain("saiu da equipe");
        tela.FindAll("textarea.chat-campo").Should().BeEmpty();
        Esperar(() => tela.FindAll(".chat-conv.saiu").Count == 1, "a lista com quem saiu");
    }

    [Fact]
    public async Task Mensagem_direta_com_o_chat_fechado_mostra_a_previa_toca_e_pisca_a_janela()
    {
        using var ctx = Contexto("fechado||1|1|1|0");
        await EnviarAsync(ctx, paulo, ChatEquipeRegras.CanalGeral, "bom dia");
        var tela = ctx.RenderComponent<ChatDaEquipe>();
        // Espera a primeira leitura (o pulso aprende onde a conversa está).
        Esperar(() => ctx.JSInterop.Invocations.Any(i => i.Identifier == "clinicaChat.ligarCampo"), "a ligação das teclas");
        Thread.Sleep(2600);
        await EnviarAsync(ctx, paulo, ChatEquipeRegras.Direta(ana.Id, paulo.Id), "Pode me ligar?");
        // O gatilho do banco avisa na hora: o chat responde com o pulso, sem esperar a batida de 16 s.
        avisos.Disparar("MensagensEquipe");
        Esperar(() => tela.FindAll(".chat-previa").Count == 1, "a prévia da direta");
        tela.FindAll("button button").Should().BeEmpty();
        tela.Find(".chat-previa-abrir").TextContent.Should().Contain("Pode me ligar?");
        tela.Find(".chat-anuncio").TextContent.Should().Contain("Paulo Lima");
        host.Atencoes.Should().BeGreaterThan(0, "direta com a janela atrás de outro programa pisca na barra de tarefas");
        ctx.JSInterop.Invocations.Should().Contain(i => i.Identifier == "clinicaChat.tocar");
    }

    [Fact]
    public async Task Ampliar_aumenta_o_proprio_chat_sem_janela_por_cima()
    {
        using var ctx = Contexto("janela|canal:geral|1|1|1|0");
        await EnviarAsync(ctx, paulo, ChatEquipeRegras.CanalGeral, "bom dia");
        var tela = ctx.RenderComponent<ChatDaEquipe>();
        Esperar(() => tela.Markup.Contains("bom dia"), "a conversa aberta");

        tela.FindAll(".chat-linha button").First(b => b.TextContent.Contains("Ampliar")).Click();
        tela.FindAll(".chat-painel.ampliado").Should().ContainSingle("o próprio chat cresce");
        tela.FindAll(".modal-fundo, .chat-editor").Should().BeEmpty("nada de janela por cima do chat (ela ficava atrás do painel e escondia o Enviar)");
        tela.Find("textarea.chat-campo").GetAttribute("rows").Should().Be("14");
        tela.Find(".chat-dica").TextContent.Should().Contain("Ctrl+Enter envia");
        tela.FindAll(".chat-linha button").Should().Contain(b => b.TextContent.Contains("Reduzir"));
        tela.Find(".chat-enviar").Should().NotBeNull("o Enviar continua à vista");

        // A sugestão de @ funciona ampliado (o campo é o mesmo).
        tela.Find("textarea.chat-campo").Input("@pa");
        tela.FindAll(".chat-mencoes").Should().ContainSingle();
        await tela.InvokeAsync(() => tela.Instance.Escape());
        await tela.InvokeAsync(() => tela.Instance.Escape());
        tela.FindAll(".chat-painel.ampliado").Should().BeEmpty("Esc fecha a sugestão e depois reduz");
    }

    [Fact]
    public async Task A_bolinha_arrastada_fica_onde_foi_deixada()
    {
        using var ctx = Contexto("fechado||1|1|1|0|30,40|");
        var tela = ctx.RenderComponent<ChatDaEquipe>();
        Esperar(() => ctx.JSInterop.Invocations.Any(i => i.Identifier == "clinicaChat.ligarCampo"), "a ligação das teclas");
        tela.Find(".chat-flutuante").GetAttribute("style").Should().Contain("min(30px").And.Contain("min(40px").And.Contain("--chat-piso");

        // A página avisa onde a pessoa soltou: o componente desenha e guarda.
        await tela.InvokeAsync(() => tela.Instance.Posicionar("bola", 120, 300));
        tela.Find(".chat-flutuante").GetAttribute("style").Should().Contain("min(120px").And.Contain("min(300px");
        ctx.JSInterop.Invocations.Where(i => i.Identifier == "clinicaChat.gravar").Last().Arguments[1]
            .Should().Be("fechado||1|1|1|0|120,300|");

        // Aberto em janelinha, a janela tem a sua própria posição; ampliado, não usa nenhuma.
        await tela.InvokeAsync(() => tela.Instance.Posicionar("janela", 50, 60));
        tela.Find(".chat-bola").Click();
        Esperar(() => tela.FindAll(".chat-painel.janela").Count == 1, "a janelinha");
        tela.Find(".chat-painel").GetAttribute("style").Should().Contain("min(50px");
        await tela.InvokeAsync(() => tela.Instance.Posicionar("outra", 1, 1));
        tela.Find(".chat-painel").GetAttribute("style").Should().Contain("min(50px", "posição de coisa desconhecida é ignorada");
    }

    private sealed class AvisosDeTeste : IAvisosDoBanco
    {
        public event Action<string>? Mudou;
        public void Disparar(string tabela) => Mudou?.Invoke(tabela);
    }

    private sealed class HostQueConta : IHostDaTela
    {
        public int Atencoes;
        public Task ChamarAtencaoAsync() { Interlocked.Increment(ref Atencoes); return Task.CompletedTask; }
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> c, IReadOnlyList<IReadOnlyList<string>> l, string n) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] c, string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string f, string t) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
