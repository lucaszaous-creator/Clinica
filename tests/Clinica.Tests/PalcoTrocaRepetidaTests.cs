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
/// ficariam todos ali, acumulando. Aqui as telas Blazor reais, sobre as sementes da prévia,
/// são trocadas em cinco rodadas — em ordem, embaralhadas, intercaladas com "tela WPF à vista"
/// e às rajadas, sem esperar a leitura terminar.
///
/// ⚠️ EM LOTES (out/2026). O teste nasceu para as 14 telas do Financeiro, e a reflexão passou a
/// achar 75 com a migração para Blazor: ~375 trocas sobre 3 MB de HTML montado, 5–6 minutos
/// sozinho — o gargalo do CI. Agora cada caso é um palco com ~15 telas (o tamanho para o qual
/// as cinco rodadas foram desenhadas), e os lotes se espalham pelas fatias do CI
/// (<see cref="FatiarPorCasoAttribute"/>). Toda tela continua aberta, trocada e devolvida.
///
/// ⚠️ "Assentar" espera a tela PEDIDA à vista, não só o HTML parar de mudar: com o redesenho do
/// palco na fila atrás da leitura de outra tela, quatro leituras iguais (~60 ms) aconteciam
/// ANTES de ele entrar, e a conferência via a tela anterior — o "Assert.Same" intermitente do
/// PR 285, que não era defeito do palco.
///
/// ⚠️ E as consultas ao banco são contadas POR TELA (quem dispara a leitura carrega a tela no
/// contexto assíncrono). Com um contador só, a leitura de fundo de uma tela era creditada a
/// outra: a de Sessões de enfermagem — que, logada como direção, não lê nada — aparecia como
/// "leu ao abrir" e depois "não releu ao voltar", ao acaso.
///
/// ⚠️ O que este teste NÃO vê: o preto da janela nativa do WebView2, que só existe no
/// Windows. Esse é medido pelo `ValidarLayoutWindows --blazor`, que fotografa a tela real.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
[FatiarPorCaso]
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

    private const int Lotes = 5;

    /// <summary>A tela que disparou a leitura em curso — o contador credita a consulta a ela.</summary>
    private static readonly AsyncLocal<Type?> _telaEmCurso = new();

    public static IEnumerable<object[]> OsLotes() => Enumerable.Range(1, Lotes).Select(l => new object[] { l });

    private static Type[] Telas() => typeof(IHostDaTela).Assembly.GetTypes()
        .Where(t => t.Namespace == "Clinica.Ui.Blazor.Telas" && typeof(TelaBase).IsAssignableFrom(t) && !t.IsAbstract)
        .OrderBy(t => t.Name).ToArray();

    [Fact]
    public void Os_lotes_cobrem_todas_as_telas()
    {
        var todas = Telas();
        Assert.True(todas.Length >= 14);
        var cobertas = Enumerable.Range(1, Lotes).SelectMany(DoLote).ToList();
        Assert.Equal(todas.Length, cobertas.Count);
        Assert.Equal(todas.ToHashSet(), cobertas.ToHashSet());
    }

    private static Type[] DoLote(int lote) => Telas().Where((_, i) => i % Lotes == lote - 1).ToArray();

    [Theory]
    [MemberData(nameof(OsLotes))]
    public void Cinco_rodadas_pelas_telas_do_lote_nao_deixam_resto(int lote)
    {
        var ocorrencias = new List<(string, Exception)>();
        _ocorrencias.Value = ocorrencias;
        var consultas = new ContadorDeConsultas();
        using var ctx = Contexto(consultas);
        var palco = new Palco();
        var montagens = 0;
        palco.Montou += () => montagens++;
        var r = ctx.RenderComponent<PalcoDeTelas>(p => p.Add(x => x.Palco, palco));

        var telas = DoLote(lote);
        Assert.NotEmpty(telas);
        var sorteio = new Random(20261016 + lote);
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
                if (sorteio.Next(4) == 0) { palco.Desativar(); Assentar(r, palco); Conferir(r, palco, null, ids.Count); }

                var antes = consultas.De(tela);
                var eraAberta = ids.ContainsKey(tela);
                var eraAtual = palco.Atual is { } a && ids.TryGetValue(tela, out var idT) && idT == a;
                _telaEmCurso.Value = tela;
                palco.Ativar(tela);
                trocas++;
                Assentar(r, palco);

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
                        _telaEmCurso.Value = tela;
                        vis.QuerySelectorAll("button").First(b => b.TextContent.Contains("Abrir extrato")).Click();
                        Assentar(r, palco);
                    }
                    if (consultas.De(tela) > antes) leuAoAbrir.Add(tela);
                }

                Conferir(r, palco, tela, ids.Count);
                if (eraAberta && !eraAtual)
                {
                    voltas[tela] = voltas.GetValueOrDefault(tela) + 1;
                    if (leuAoAbrir.Contains(tela) && consultas.De(tela) == antes) semReleitura.Add(tela.Name);
                }
            }
        }

        // Rajada: vinte trocas sem esperar nenhuma leitura terminar — a releitura de uma tela
        // ainda no ar quando a próxima já assumiu.
        for (var i = 0; i < 20; i++) { var t = telas[sorteio.Next(telas.Length)]; _telaEmCurso.Value = t; palco.Ativar(t); trocas++; }
        Assentar(r, palco);
        Conferir(r, palco, palco.Telas.Single(e => e.Id == palco.Atual).Tela, ids.Count);

        Assert.Equal(1, montagens); // o hospedeiro é liberado UMA vez por janela
        Assert.DoesNotContain("Algo deu errado nesta tela", r.Markup);
        Assert.True(ocorrencias.Count == 0,
            "a troca de telas registrou no log: " + string.Join(" | ", ocorrencias.Select(o => $"{o.Item1}: {o.Item2.GetType().Name}: {o.Item2.Message}")));

        // Quase toda tela lê ao abrir; as que não leem (o extrato sem arquivo, a lista que é de
        // outro perfil) são a minoria. Menos da metade do lote é a CONTAGEM que quebrou — a
        // atribuição por tela deixou de chegar às consultas, e a checagem da volta ficaria cega.
        Assert.True(leuAoAbrir.Count >= telas.Length / 2, $"só {leuAoAbrir.Count} de {telas.Length} telas leram o banco ao abrir");
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

    /// <summary>
    /// Espera o redesenho ENTRAR e o palco parar: só conta a estabilidade depois de o
    /// documento mostrar o que o <see cref="Palco"/> pede (a tela atual à vista, ou nenhuma).
    /// </summary>
    private static void Assentar(IRenderedComponent<PalcoDeTelas> r, Palco palco)
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
            if (estavel >= 4)
            {
                if (MostraOPedido(r, palco)) return;
                estavel = 0; // estável, mas ainda o retrato de ANTES: o redesenho está na fila
            }
        }
        throw new TimeoutException($"em 20 s o palco não parou de redesenhar ou não mostrou a tela pedida ({palco.Telas.FirstOrDefault(e => e.Id == palco.Atual)?.Tela.Name ?? "nenhuma"})");
    }

    private static bool MostraOPedido(IRenderedComponent<PalcoDeTelas> r, Palco palco)
    {
        var conteineres = r.FindAll(".palco-tela");
        if (conteineres.Count != palco.Telas.Count) return false;
        var visiveis = conteineres.Where(c => !c.HasAttribute("hidden")).ToList();
        if (palco.Atual is not { } atual) return visiveis.Count == 0;
        var ordem = palco.Telas.ToList().FindIndex(e => e.Id == atual);
        return visiveis.Count == 1 && ReferenceEquals(conteineres[ordem], visiveis[0]);
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
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, int> _porTela = new();
        public int Total => Volatile.Read(ref _total);
        public int De(Type tela) => _porTela.TryGetValue(tela, out var n) ? n : 0;

        private void Contar()
        {
            Interlocked.Increment(ref _total);
            if (_telaEmCurso.Value is { } tela) _porTela.AddOrUpdate(tela, 1, (_, n) => n + 1);
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Contar();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Contar();
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
