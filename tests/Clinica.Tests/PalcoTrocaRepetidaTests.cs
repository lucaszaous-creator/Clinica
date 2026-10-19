using System.Data.Common;
using Bunit;
using Clinica.Application;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor.Sementes;
using Xunit;
using Xunit.Abstractions;

namespace Clinica.Tests;

/// <summary>
/// TROCAR DE TELA MUITAS VEZES NÃO DEIXA RESTO (out/2026). O palco mantém toda tela aberta
/// montada e só a atual à vista — o que tirou o flash preto. O preço desse desenho é que um
/// defeito de troca não some ao fechar a tela, porque ela não fecha: duas telas à vista ao
/// mesmo tempo, a mesma tela montada duas vezes, a camada de diálogo duplicada, a tela que
/// volta vazia, a que não relê ao voltar e a releitura que estoura por cima da troca seguinte
/// ficariam todos ali, acumulando. Aqui as 14 telas reais do Financeiro, sobre as sementes da
/// prévia, são trocadas em rodadas — em ordem, embaralhadas, intercaladas com "tela WPF à
/// vista" e às rajadas, sem esperar a leitura terminar.
///
/// ⚠️ O que este teste NÃO vê: o preto da janela nativa do WebView2, que só existe no
/// Windows. Esse é medido pelo `ValidarLayoutWindows --blazor`, que fotografa a tela real.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class PalcoTrocaRepetidaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly AsyncLocal<List<(string Contexto, Exception Erro)>?> _ocorrencias = new();
    private readonly Action<string, Exception>? _sinkOriginal = Diagnostico.Sink;
    private readonly BancoDasTelas _banco;
    private readonly ITestOutputHelper _saida;

    public PalcoTrocaRepetidaTests(BancoDasTelas banco, ITestOutputHelper saida)
    {
        _banco = banco;
        _saida = saida;
        var anterior = _sinkOriginal;
        Diagnostico.Sink = (c, e) => { _ocorrencias.Value?.Add((c, e)); anterior?.Invoke(c, e); };
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose()
    {
        Diagnostico.Sink = _sinkOriginal;
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    private static Type[] Telas() => typeof(IHostDaTela).Assembly.GetTypes()
        .Where(t => t.Namespace == "Clinica.Ui.Blazor.Telas" && typeof(TelaBase).IsAssignableFrom(t) && !t.IsAbstract)
        .OrderBy(t => t.Name).ToArray();

    [Fact]
    public void Cinco_rodadas_pelas_quatorze_telas_nao_deixam_resto()
    {
        var ocorrencias = new List<(string, Exception)>();
        _ocorrencias.Value = ocorrencias;
        var consultas = new ContadorDeConsultas();
        using var ctx = Contexto(consultas);
        var palco = new Palco();
        var montagens = 0;
        palco.Montou += () => montagens++;
        var r = ctx.RenderComponent<PalcoDeTelas>(p => p.Add(x => x.Palco, palco));

        var telas = Telas();
        Assert.True(telas.Length >= 14);
        var sorteio = new Random(20261016);
        var ids = new Dictionary<Type, int>();
        var voltas = new Dictionary<Type, int>();
        var semReleitura = new HashSet<string>();
        var leuAoAbrir = new HashSet<Type>(); // tela que não lê nada ao abrir (o extrato sem arquivo) não tem o que reler
        var trocas = 0;

        for (var rodada = 0; rodada < 5; rodada++)
        {
            // Rodada 0 em ordem (a primeira abertura de cada); as outras, embaralhadas.
            var ordem = rodada == 0 ? telas : telas.OrderBy(_ => sorteio.Next()).ToArray();
            foreach (var tela in ordem)
            {
                // De vez em quando o shell mostra uma tela WPF no meio (Pacotes, Configurações).
                // O palco redesenha por InvokeAsync: se uma tela ainda está terminando uma leitura
                // no despachante, o redesenho entra na fila — o mesmo Assentar da troca para uma
                // tela Blazor, senão a conferência lê o retrato de ANTES (o CI do Linux pegou).
                if (sorteio.Next(4) == 0) { palco.Desativar(); Assentar(r); Conferir(r, palco, null, ids.Count); }

                var antes = consultas.Total;
                var eraAberta = ids.ContainsKey(tela);
                var eraAtual = palco.Atual is { } a && ids.TryGetValue(tela, out var idT) && idT == a;
                palco.Ativar(tela);
                trocas++;
                Assentar(r);

                var entrada = palco.Telas.Single(e => e.Tela == tela);
                if (ids.TryGetValue(tela, out var id)) Assert.Equal(id, entrada.Id); // nunca recriada
                else
                {
                    ids[tela] = entrada.Id;
                    // O extrato só lê o banco depois de abrir um arquivo: abre o OFX da semente,
                    // para a volta a ele ter o que recruzar.
                    if (tela.Name == "ExtratoDoBanco")
                    {
                        var vis = r.FindAll(".palco-tela").Single(c => !c.HasAttribute("hidden"));
                        vis.QuerySelectorAll("button").First(b => b.TextContent.Contains("Abrir extrato")).Click();
                        Assentar(r);
                    }
                    if (consultas.Total > antes) leuAoAbrir.Add(tela);
                }

                Conferir(r, palco, tela, ids.Count);
                if (eraAberta && !eraAtual)
                {
                    voltas[tela] = voltas.GetValueOrDefault(tela) + 1;
                    if (leuAoAbrir.Contains(tela) && consultas.Total == antes) semReleitura.Add(tela.Name);
                }
            }
        }

        // Rajada: vinte trocas sem esperar nenhuma leitura terminar — a releitura de uma tela
        // ainda no ar quando a próxima já assumiu.
        for (var i = 0; i < 20; i++) { palco.Ativar(telas[sorteio.Next(telas.Length)]); trocas++; }
        Assentar(r);
        Conferir(r, palco, palco.Telas.Single(e => e.Id == palco.Atual).Tela, ids.Count);

        Assert.Equal(1, montagens); // o hospedeiro é liberado UMA vez por janela
        Assert.DoesNotContain("Algo deu errado nesta tela", r.Markup);
        Assert.True(ocorrencias.Count == 0,
            "a troca de telas registrou no log: " + string.Join(" | ", ocorrencias.Select(o => $"{o.Item1}: {o.Item2.GetType().Name}: {o.Item2.Message}")));

        Assert.True(leuAoAbrir.Count >= 14, $"só {leuAoAbrir.Count} telas leram o banco ao abrir");
        _saida.WriteLine($"{trocas} trocas · {ids.Count} telas montadas · {voltas.Values.Sum()} voltas · {consultas.Total} consultas ao banco");
        _saida.WriteLine("telas que NÃO releem ao voltar: " + (semReleitura.Count == 0 ? "nenhuma" : string.Join(", ", semReleitura.Order())));
        Assert.True(semReleitura.Count == 0,
            "voltar a estas telas não releu o banco — elas mostrariam o que havia quando a pessoa saiu: " + string.Join(", ", semReleitura.Order()));
    }

    /// <summary>
    /// O retrato depois de cada troca: no máximo UMA tela à vista (e é a pedida), cada tela
    /// montada UMA vez, a tela à vista com conteúdo, e UMA camada de diálogo e de avisos.
    /// </summary>
    private static void Conferir(IRenderedComponent<PalcoDeTelas> r, Palco palco, Type? atual, int abertas)
    {
        var conteineres = r.FindAll(".palco-tela");
        Assert.Equal(abertas, conteineres.Count);
        var visiveis = conteineres.Where(c => !c.HasAttribute("hidden")).ToList();
        if (atual is null) { Assert.Empty(visiveis); return; }
        Assert.True(visiveis.Count == 1, $"{atual.Name}: {visiveis.Count} telas à vista depois da troca (esperava 1)");
        var ordem = palco.Telas.ToList().FindIndex(e => e.Tela == atual);
        Assert.Same(conteineres[ordem], visiveis[0]);
        Assert.NotEmpty(visiveis[0].QuerySelectorAll(".pagina"));
        // Diz QUAL tela caiu no terceiro estado e o que ela escreveu: "Assert.Empty falhou" sem o
        // nome da tela obrigou a reproduzir no escuro uma falha que só aparece com o tempo do CI.
        var falha = visiveis[0].QuerySelectorAll(".vazio-estado.falha");
        Assert.True(falha.Length == 0, $"{atual.Name} mostrou falha de leitura depois da troca: {falha.FirstOrDefault()?.TextContent?.Trim()}");
        Assert.Single(r.FindAll(".notificacoes"));          // a pilha de avisos é uma por navegador
        Assert.Empty(r.FindAll(".palco-tela .notificacoes")); // e nunca dentro de uma tela
    }

    private static void Assentar(IRenderedComponent<PalcoDeTelas> r)
    {
        var prazo = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        var anterior = string.Empty;
        var estavel = 0;
        while (DateTime.UtcNow < prazo)
        {
            Thread.Sleep(15); // a mesma batida do TelasBlazorAbremTests.Assentar
            var agora = r.Markup;
            estavel = agora == anterior ? estavel + 1 : 0;
            anterior = agora;
            if (estavel >= 4) return;
        }
        throw new TimeoutException("o palco não parou de redesenhar em 20 s");
    }

    private TestContext Contexto(ContadorDeConsultas consultas)
    {
        var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            ctx.Services.Remove(d);
        ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_banco.Conexao).AddInterceptors(consultas));
        ctx.Services.AddTelasBlazor();
        ctx.Services.AddSingleton<IHostDaTela, HostSemDisco>();
        return ctx;
    }

    private sealed class ContadorDeConsultas : DbCommandInterceptor
    {
        private int _total;
        public int Total => Volatile.Read(ref _total);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _total);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _total);
            return base.ReaderExecuting(command, eventData, result);
        }
    }

    private sealed class HostSemDisco : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> c, IReadOnlyList<IReadOnlyList<string>> l, string n) => Task.FromResult<string?>("teste");
        public Task<string?> SalvarEAbrirAsync(byte[] c, string n, string f, string e) => Task.FromResult<string?>("teste");
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string f, string t)
            => Task.FromResult(SementeTaxasExtrato.ExemploOfx is { } ofx
                ? new ArquivoEscolhido("extrato-exemplo.ofx", System.Text.Encoding.Latin1.GetBytes(ofx))
                : null);
        public Task<bool> CopiarAsync(string t) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string c) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string u) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int id) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>("/teste/" + n);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>("/teste");
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }

}
