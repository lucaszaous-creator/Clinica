using AngleSharp.Dom;
using Bunit;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A TELA DE WHATSAPP DIZ A VERDADE (25/10/2026, §14 de docs/whatsapp-nativo-implementacao.md): a mesma
/// <see cref="Raiz"/>, as mesmas sementes e o mesmo CRM de mentira de <see cref="TelasBlazorAbremTests"/>, clicando o
/// que a secretária clica. Afirma o que a tela MOSTRA e o que chega ao CRM:
/// <list type="bullet">
/// <item>o pedido de transferência para quem recebe (selo na linha, aviso, Aceitar) e para quem pede (o formulário
/// sem nada marcado, "Aguardando o aceite", Cancelar);</item>
/// <item>uma palavra por estado ("Sem resposta" na linha, na coluna e no aviso) e uma porta por pergunta (o que o aviso
/// oferece não se repete no horário da coluna);</item>
/// <item>a conversa sem ficha conferida com o telefone no título e o nome do perfil rotulado.</item>
/// </list>
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class RegrasDoWhatsAppNaTelaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly TestContext _ctx = new();

    public RegrasDoWhatsAppNaTelaTests(BancoDasTelas banco)
    {
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in _ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            _ctx.Services.Remove(d);
        _ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(banco.Conexao));
        _ctx.Services.AddTelasBlazor();
        _ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());
        CrmDeMentira.Registrar(_ctx.Services);
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _ctx.Dispose();
    }

    private CrmDeMentira Crm => (CrmDeMentira)_ctx.Services.GetRequiredService<IClienteDoCrm>();

    private IRenderedComponent<Raiz> Abrir()
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.ConversasDoWhatsApp")!;
        var tela = _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        tela.WaitForAssertion(() => tela.FindAll(".wa-linha").Should().NotBeEmpty(), Prazo);
        tela.Clicar(() => tela.FindAll(".wa-filtros .segmentado button").First(b => b.TextContent.Trim().StartsWith("Todas", StringComparison.Ordinal)));
        return tela;
    }

    private static IElement Linha(IRenderedComponent<Raiz> tela, string texto)
        => tela.FindAll(".wa-linha").First(e => e.TextContent.Contains(texto, StringComparison.Ordinal));

    private static void AbrirConversa(IRenderedComponent<Raiz> tela, string daLinha, string noTitulo)
    {
        tela.Clicar(() => Linha(tela, daLinha));
        tela.WaitForAssertion(() => tela.Find(".wa-cab-quem h2").TextContent.Should().Contain(noTitulo), Prazo);
    }

    private static IElement Botao(IRenderedFragment onde, string css, string rotulo)
        => onde.FindAll(css).First(b => b.TextContent.Trim().EndsWith(rotulo, StringComparison.Ordinal));

    [Fact]
    public void Quem_recebe_o_pedido_ve_o_selo_o_aviso_e_aceita_e_a_conversa_passa_a_ser_dela()
    {
        var tela = Abrir();
        Linha(tela, PessoasDoWhatsApp.Lucia.Nome).QuerySelector(".wa-linha-rodape .selo")!.TextContent.Should().Be("Transferência para você");

        // Sem ficha conferida (item 16): o título é o telefone e o nome do perfil vem rotulado.
        AbrirConversa(tela, PessoasDoWhatsApp.Lucia.Nome, "(22) 99588-1414");
        tela.Find(".wa-cab-meta").TextContent.Should().Contain($"{PessoasDoWhatsApp.Lucia.Nome} (nome no WhatsApp)");
        tela.WaitForAssertion(() => tela.Find(".wa-acao strong").TextContent.Should().Be("Bruna Reis quer passar esta conversa para você."), Prazo);

        tela.Clicar(() => Botao(tela, ".wa-acao button", "Aceitar"));

        tela.WaitForAssertion(() => tela.FindAll(".wa-acao strong").Should().NotContain(e => e.TextContent.Contains("quer passar")), Prazo);
        tela.Find(".wa-faixa").TextContent.Should().Contain("Com você");
    }

    [Fact]
    public void Quem_pede_escolhe_sem_nada_marcado_e_ve_aguardando_o_aceite_ate_cancelar()
    {
        var tela = Abrir();
        // O Roberto está com a Ana e não tem aviso de confirmação (o "não vem" da Maria viria antes da espera).
        AbrirConversa(tela, PessoasDoWhatsApp.Roberto.Nome, PessoasDoWhatsApp.Roberto.Nome);
        tela.Clicar(() => tela.Find("button[data-acao='transferir']"));
        tela.WaitForAssertion(() => tela.Find(".modal .wa-opcoes"), Prazo);

        // Nada vem marcado (item 20): sem escolher, o botão não transfere.
        tela.FindAll(".modal input[type=radio]").Should().OnlyContain(r => !r.HasAttribute("checked"));
        Botao(tela, ".modal-rodape button", "Transferir").HasAttribute("disabled").Should().BeTrue();
        tela.InvokeAsync(() => tela.FindAll(".modal .wa-escolha").First(e => e.TextContent.Contains("Bruna Reis")).QuerySelector("input")!.Change(true))
            .GetAwaiter().GetResult();
        tela.Clicar(() => Botao(tela, ".modal-rodape button", "Enviar o pedido"));

        // A conversa continua com quem pediu, e o aviso diz que espera o aceite (R3/R7).
        tela.WaitForAssertion(() => tela.Find(".wa-acao strong").TextContent.Should().Be("Aguardando o aceite de Bruna Reis."), Prazo);
        tela.Find(".wa-faixa").TextContent.Should().Contain("Com você");
        tela.Find("button[data-acao='transferir']").HasAttribute("disabled").Should().BeTrue("um pedido por vez");

        tela.Clicar(() => Botao(tela, ".wa-acao button", "Cancelar o pedido"));
        tela.WaitForAssertion(() => tela.FindAll(".wa-acao strong").Should().NotContain(e => e.TextContent.Contains("Aguardando o aceite")), Prazo);
    }

    [Fact]
    public void Sem_resposta_e_a_mesma_palavra_na_linha_na_coluna_e_no_aviso_e_o_aviso_nao_se_repete_no_horario()
    {
        var tela = Abrir();
        Linha(tela, PessoasDoWhatsApp.Carlos.Nome).QuerySelector(".wa-linha-rodape .selo")!.TextContent.Should().StartWith("Sem resposta ·");

        AbrirConversa(tela, PessoasDoWhatsApp.Carlos.Nome, PessoasDoWhatsApp.Carlos.Nome);
        tela.WaitForAssertion(() => tela.Find(".wa-acao strong").TextContent.Should().Contain("sem resposta ao lembrete"), Prazo);
        var horario = tela.FindAll(".wa-horario").First(h => h.QuerySelector(".selo")!.TextContent == "Sem resposta");
        // O aviso já oferece Confirmar e Não vem para este horário: o cartão fica com o resto.
        var rotulos = horario.QuerySelectorAll("button").Select(b => b.TextContent.Trim()).ToList();
        rotulos.Should().NotContain(r => r.EndsWith("Confirmar") || r == "Não vem");
        rotulos.Should().Contain(r => r.EndsWith("Remarcar")).And.Contain("Cancelar");
        // A confirmação de hoje diz o canal certo: o automático do WhatsApp, não o SMS (item 15).
        tela.Find(".wa-ficha").TextContent.Should().Contain("WhatsApp enviado pelo número oficial (automático)").And.NotContain("SMS enviado");
    }

    private sealed class HostMudo : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
