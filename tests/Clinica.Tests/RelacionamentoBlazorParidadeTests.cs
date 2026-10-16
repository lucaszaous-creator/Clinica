using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O ACOMPANHAMENTO EM BLAZOR FAZ O QUE PROMETE (out/2026, desenho A) — os dois atos do pedido
/// da cliente ("como a usuária assume e registra o contato?"), clicando a tela de verdade (bUnit,
/// sobre os serviços do app e a semente da prévia) e afirmando o BANCO, não a frase:
///
/// 1. "Assumir" na própria linha grava quem fez login como responsável, sem texto;
/// 2. "Não respondeu" pela conversa grava a etapa Sem resposta, conta uma tentativa a mais e
///    marca o próximo contato para dois dias ÚTEIS.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class RelacionamentoBlazorParidadeTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public RelacionamentoBlazorParidadeTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void Assumir_na_linha_grava_quem_fez_login_como_responsavel()
    {
        var caso = Caso("Carlos Eduardo Ramos");
        Assert.Null(caso.ResponsavelId);

        using var ctx = Contexto();
        var tela = Abrir(ctx);
        var botao = Linha(tela, "Carlos Eduardo Ramos").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Assumir");
        botao.Click();
        Assentar(tela);

        var depois = Caso("Carlos Eduardo Ramos");
        Assert.Equal(_banco.Gerente.Id, depois.ResponsavelId);
        Assert.Equal(caso.Etapa, depois.Etapa);
        using var db = Db();
        Assert.Contains(db.ContatosAcompanhamento.AsNoTracking().Where(c => c.AcompanhamentoPacienteId == caso.Id).ToList(),
            c => c.Resultado.StartsWith("Assumiu") && c.Operador == _banco.Gerente.Login && c.Canal == null);
    }

    [Fact]
    public void Nao_respondeu_pela_conversa_grava_sem_resposta_uma_tentativa_a_mais_e_dois_dias_uteis()
    {
        var caso = Caso("Paulo Sérgio Nunes");
        var tentativasAntes = Tentativas(caso.Id);

        using var ctx = Contexto();
        var tela = Abrir(ctx);
        Linha(tela, "Paulo Sérgio Nunes").Click();
        Assentar(tela);
        Clicar(tela, tela.FindAll(".acomp-desfecho").Single(b => b.QuerySelector("b")!.TextContent == "Não respondeu"));
        Clicar(tela, tela.FindAll(".acomp-canais button").Single(b => b.TextContent.Trim() == "WhatsApp"));
        Clicar(tela, tela.FindAll(".modal-rodape button").Single(b => b.TextContent.Contains("Registrar")));

        var depois = Caso("Paulo Sérgio Nunes");
        Assert.Equal(EtapaAcompanhamento.SemResposta, depois.Etapa);
        Assert.Equal(tentativasAntes + 1, Tentativas(caso.Id));
        Assert.Equal(AcompanhamentoNaTela.DiasUteis(DateOnly.FromDateTime(DateTime.Today), 2), depois.ProximoContato);
        Assert.Equal(_banco.Gerente.Id, depois.ResponsavelId);
        Assert.Empty(tela.FindAll(".acomp-conversa")); // gravou e fechou
    }

    [Fact]
    public void Nao_respondeu_sem_dizer_por_onde_nao_grava_e_diz_o_que_falta()
    {
        var caso = Caso("Lúcia Prado");
        var tentativasAntes = Tentativas(caso.Id);

        using var ctx = Contexto();
        var tela = Abrir(ctx);
        Clicar(tela, tela.FindAll(".segmentado button").Single(b => b.TextContent.Trim() == "Toda a equipe"));
        Linha(tela, "Lúcia Prado").Click();
        Assentar(tela);
        Clicar(tela, tela.FindAll(".acomp-desfecho").Single(b => b.QuerySelector("b")!.TextContent == "Não respondeu"));
        Clicar(tela, tela.FindAll(".modal-rodape button").Single(b => b.TextContent.Contains("Registrar")));

        Assert.Contains("por onde foi o contato", tela.Markup);
        Assert.Equal(tentativasAntes, Tentativas(caso.Id));
        Assert.Equal(caso.Versao, Caso("Lúcia Prado").Versao);
    }

    // ==================== apoio ====================

    private AcompanhamentoPaciente Caso(string paciente)
    {
        using var db = Db();
        return db.Acompanhamentos.AsNoTracking().Include(c => c.Paciente)
            .Single(c => c.Paciente!.Nome == paciente && c.Tipo == TipoAcompanhamento.Recall);
    }

    private int Tentativas(int id)
    {
        using var db = Db();
        return db.ContatosAcompanhamento.AsNoTracking().Count(c => c.AcompanhamentoPacienteId == id && c.Canal != null);
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
        ctx.Services.AddSingleton<IHostDaTela, HostQueNaoAbre>();
        return ctx;
    }

    private static IRenderedComponent<Raiz> Abrir(TestContext ctx)
    {
        var r = ctx.RenderComponent<Raiz>(p => p.Add(x => x.Tela, typeof(Clinica.Ui.Blazor.Telas.AcompanhamentoDePacientes)));
        Assentar(r);
        return r;
    }

    private static IElement Linha(IRenderedComponent<Raiz> tela, string paciente)
        => tela.FindAll("tr.acomp-linha").Single(tr => tr.QuerySelector(".principal")!.TextContent == paciente);

    private static void Clicar(IRenderedComponent<Raiz> tela, IElement botao)
    {
        botao.Click();
        Assentar(tela);
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

    /// <summary>Sem shell: o WhatsApp e a ficha não abrem, a cópia "dá certo".</summary>
    private sealed class HostQueNaoAbre : IHostDaTela
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
