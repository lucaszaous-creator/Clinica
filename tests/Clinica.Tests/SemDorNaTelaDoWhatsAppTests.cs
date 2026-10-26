using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
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
/// O @SemDor NA TELA de WhatsApp (24/10/2026): a mesma <see cref="Raiz"/>, as mesmas sementes e o mesmo CRM de
/// mentira de <see cref="TelasBlazorAbremTests"/>, clicando o que a secretária clica. Afirma o que a tela MOSTRA
/// e o que chega ao CRM: a etiqueta na lista (a de atenção em vermelho), o ✨ que põe a sugestão no campo e a
/// revisão com o ↶, o resumo fechável — nada enviado ao paciente —, o desligado sem resquício e a falha com a frase.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class SemDorNaTelaDoWhatsAppTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly TestContext _ctx = new();

    public SemDorNaTelaDoWhatsAppTests(BancoDasTelas banco)
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

    private static void AbrirConversa(IRenderedComponent<Raiz> tela, string nome)
    {
        tela.Clicar(() => tela.FindAll(".wa-linha").First(e => e.TextContent.Contains(nome, StringComparison.Ordinal)));
        tela.WaitForAssertion(() => tela.Find(".wa-cab-quem h2").TextContent.Should().Contain(nome), Prazo);
    }

    private static string Campo(IRenderedComponent<Raiz> tela) => tela.Find(".wa-escrever textarea").GetAttribute("value") ?? "";

    private static IElement Linha(IRenderedComponent<Raiz> tela, string nome)
        => tela.FindAll(".wa-linha").First(e => e.TextContent.Contains(nome, StringComparison.Ordinal));

    [Fact]
    public async Task Ligado_mostra_as_etiquetas_e_o_brilho_poe_a_sugestao_no_campo_sem_enviar()
    {
        var tela = Abrir();

        // A etiqueta de atenção é vermelha; as outras, do @SemDor. Quem já recebeu resposta não tem etiqueta.
        tela.WaitForAssertion(() => Linha(tela, "Lúcia Andrade").QuerySelector(".wa-etiqueta.tom-perigo")!.TextContent.Should().Contain("Urgência clínica"), Prazo);
        Linha(tela, "Roberto Lima").QuerySelector(".wa-etiqueta").Should().BeNull("a última mensagem é da clínica");
        // A de assunto só onde acrescenta (25/10): o selo neutro da clínica ("Sem ficha") ganha "✨ Valores"; o que já
        // diz o assunto ("Não vem — remarcar", "2 fichas") não ganha "✨ Remarcar" nem "✨ Confirmou" ao lado.
        tela.FindAll(".wa-etiqueta.tom-semdor").Should().ContainSingle().Which.TextContent.Should().Contain("Valores");
        foreach (var linha in tela.FindAll(".wa-linha").Where(l => l.QuerySelector(".wa-etiqueta.tom-semdor") is not null))
            linha.QuerySelector(".wa-linha-rodape .selo")!.ClassList.Should().Contain("tom-neutro");
        Linha(tela, "Maria das Graças").QuerySelector(".wa-etiqueta").Should().BeNull("o selo da clínica já diz que ela não vem");

        var antes = (await Crm.AbrirConversaAsync(1, CrmDeMentira.Ids.Maria)).Messages.Length;
        AbrirConversa(tela, "Maria das Graças");
        Campo(tela).Should().BeEmpty();
        tela.Find(".wa-semdor-sugerir").GetAttribute("aria-label").Should().Be("Sugerir resposta com o @SemDor");

        // Campo vazio: a sugestão entra no campo, sem ↶ (não havia texto dela).
        tela.Clicar(() => tela.Find(".wa-semdor-sugerir"));
        tela.WaitForAssertion(() => Campo(tela).Should().StartWith("Combinado, Maria!"), Prazo);
        tela.FindAll(".wa-semdor-desfazer").Should().BeEmpty();

        // Com texto: a revisão entra, e o ↶ volta ao texto dela.
        tela.Digitar(() => tela.Find(".wa-escrever textarea"), "pode vir amanha as 14h");
        tela.Find(".wa-semdor-sugerir").GetAttribute("aria-label").Should().Be("Melhorar o texto com o @SemDor");
        tela.Clicar(() => tela.Find(".wa-semdor-sugerir"));
        tela.WaitForAssertion(() => Campo(tela).Should().Be("Pode vir amanhã às 14h."), Prazo);
        tela.Clicar(() => tela.Find(".wa-semdor-desfazer"));
        tela.WaitForAssertion(() => Campo(tela).Should().Be("pode vir amanha as 14h"), Prazo);
        tela.FindAll(".wa-semdor-desfazer").Should().BeEmpty();

        // Nada foi ao paciente.
        (await Crm.AbrirConversaAsync(1, CrmDeMentira.Ids.Maria)).Messages.Length.Should().Be(antes);
    }

    [Fact]
    public void Resumo_abre_com_quer_feito_e_falta_e_fecha()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Maria das Graças");

        tela.Clicar(() => tela.Find("[data-acao=resumo]"));
        tela.WaitForAssertion(() => tela.Find(".wa-semdor-resumo").TextContent.Should().Contain("O paciente quer").And.Contain("Remarcar a sessão de hoje")
            .And.Contain("Já foi feito").And.Contain("Falta"), Prazo);

        tela.Clicar(() => tela.Find(".wa-semdor-resumo header button"));
        tela.FindAll(".wa-semdor-resumo").Should().BeEmpty();
    }

    [Fact]
    public void Desligado_tira_as_etiquetas_o_brilho_e_o_resumo()
    {
        Crm.SemDor = CrmDeMentira.ModoDoSemDor.Desligado;
        var tela = Abrir();
        tela.WaitForAssertion(() => Linha(tela, "Lúcia Andrade").Should().NotBeNull(), Prazo);
        tela.FindAll(".wa-etiqueta").Should().BeEmpty();

        AbrirConversa(tela, "Maria das Graças");
        tela.FindAll(".wa-semdor-sugerir").Should().BeEmpty();
        tela.FindAll("[data-acao=resumo]").Should().BeEmpty();
        tela.Markup.Should().NotContain("@SemDor");
    }

    [Fact]
    public void Falhando_diz_a_frase_do_CRM_e_deixa_o_campo_como_estava()
    {
        Crm.SemDor = CrmDeMentira.ModoDoSemDor.Falhando;
        var tela = Abrir();
        // A gestão vê o motivo no topo da lista (a "ana" da prévia é supervisora).
        tela.WaitForAssertion(() => tela.Find(".wa-semdor-aviso").TextContent.Should().Contain("O @SemDor não está respondendo"), Prazo);

        AbrirConversa(tela, "Maria das Graças");
        tela.Digitar(() => tela.Find(".wa-escrever textarea"), "oi maria");
        tela.Clicar(() => tela.Find(".wa-semdor-sugerir"));
        tela.WaitForAssertion(() => tela.Find(".wa-recado").TextContent.Should().Contain(CrmDeMentira.SemDorSemResposta), Prazo);
        Campo(tela).Should().Be("oi maria");
        tela.Find(".wa-semdor-sugerir").HasAttribute("disabled").Should().BeFalse("ela pode tentar de novo");
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
