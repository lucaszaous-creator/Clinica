using Bunit;
using Clinica.Application;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A tela "Comissão de recuperação" do Gerente (out/2026) clicada de verdade (bUnit, sobre os
/// serviços do app e as sementes da prévia), afirmando o BANCO e não a frase: validar pela
/// linha grava a decisão com o valor COPIADO da regra, e quem recebe não valida a própria.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class ComissaoRecuperacaoBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public ComissaoRecuperacaoBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void Validar_pela_linha_grava_a_decisao_com_o_valor_da_regra()
    {
        using var db = Db();
        var antes = db.ComissoesRecuperacao.Count(c => c.CanceladaEm == null && c.Decisao == DecisaoComissaoRecuperacao.Validada);

        using var ctx = Contexto();
        var tela = ctx.RenderComponent<Raiz>(p => p.Add(x => x.Tela, typeof(Clinica.Ui.Blazor.Telas.ComissaoRecuperacao)));
        Assentar(tela);
        var validar = tela.FindAll("tbody button").First(b => b.TextContent.Trim() == "Validar" && !b.HasAttribute("disabled"));
        validar.Click();
        Assentar(tela);
        tela.FindAll("[role=alertdialog] .modal-rodape button").Last().Click(); // "Validar" no diálogo
        Esperar(() => Db().ComissoesRecuperacao.Count(c => c.CanceladaEm == null && c.Decisao == DecisaoComissaoRecuperacao.Validada) == antes + 1);

        using var depois = Db();
        var nova = depois.ComissoesRecuperacao.OrderByDescending(c => c.Id).First();
        Assert.Equal(DecisaoComissaoRecuperacao.Validada, nova.Decisao);
        Assert.Equal(30m, nova.Valor); // a regra geral da semente — copiada, não referenciada
        Assert.Equal(_banco.Gerente.Login, nova.DecididaPor);
        Assert.Contains(depois.Auditoria.Select(a => a.Acao).ToList(), a => a == "ComissaoRecuperacaoValidada");
    }

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options);

    private TestContext Contexto()
    {
        var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            ctx.Services.Remove(d);
        ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_banco.Conexao));
        ctx.Services.AddTelasBlazor();
        ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());
        return ctx;
    }

    private static void Assentar(IRenderedComponent<Raiz> tela)
    {
        var prazo = DateTime.UtcNow + Prazo;
        var anterior = string.Empty;
        var estavel = 0;
        while (DateTime.UtcNow < prazo)
        {
            Thread.Sleep(50);
            var agora = tela.Markup;
            estavel = agora == anterior ? estavel + 1 : 0;
            anterior = agora;
            if (estavel >= 3 && !agora.Contains("class=\"esqueleto\"")) return;
        }
        throw new TimeoutException("A tela não terminou de carregar.");
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo) { if (condicao()) return; Thread.Sleep(50); }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    private sealed class HostMudo : IHostDaTela
    {
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
