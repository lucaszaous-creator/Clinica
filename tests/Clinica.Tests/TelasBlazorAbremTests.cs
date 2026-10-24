using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor.Sementes;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// AS TELAS BLAZOR ABREM — e cada botão delas também (out/2026). Antes, a conferência de que
/// a tela renderiza era a prévia fotografada à mão (`tools/PreviaBlazor/foto.js`): um PR podia
/// ficar verde sem nenhuma tela ter sido desenhada. Aqui cada tela do Financeiro é montada
/// pela MESMA <see cref="Raiz"/> que o WPF hospeda, sobre os MESMOS serviços do app
/// (<c>AddClinica</c>) e as MESMAS sementes fictícias da prévia, dentro do `dotnet test` —
/// sem navegador, sem job a mais no GitHub.
///
/// O que reprova, e por que cada um:
/// - o cartão "Algo deu errado nesta tela" do ErrorBoundary: exceção que escapou da tela;
/// - o terceiro estado (<c>FalhaDeLeitura</c>) na abertura: a leitura falhou com dados válidos;
/// - QUALQUER ocorrência no <see cref="Diagnostico"/> na abertura: as telas engolem a exceção
///   numa notificação para não derrubar o circuito — e é justamente por isso que só o log as vê;
/// - depois de um clique, exceção que NÃO é recusa de regra (NullReference, consulta que o EF
///   não traduz, gravação que o banco recusa). Recusa de regra ("informe o valor") é a tela
///   funcionando: o formulário vazio deve mesmo dizer não.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
[FatiarPorCaso]
public sealed class TelasBlazorAbremTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);

    // O Sink é global e a suíte roda em paralelo: só conta o que aconteceu DENTRO deste teste.
    private static readonly AsyncLocal<List<(string Contexto, Exception Erro)>?> _ocorrencias = new();
    private readonly Action<string, Exception>? _sinkOriginal = Diagnostico.Sink;

    private readonly BancoDasTelas _banco;

    public TelasBlazorAbremTests(BancoDasTelas banco)
    {
        _banco = banco;
        var anterior = _sinkOriginal;
        Diagnostico.Sink = (c, e) =>
        {
            _ocorrencias.Value?.Add((c, e));
            anterior?.Invoke(c, e);
        };

        // A cultura da clínica: os serviços formatam mensagens na cultura do processo.
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;

        // No app o login é obrigatório: a tela abre com quem ENTROU, e o serviço confere a
        // pessoa no banco (os materiais do Estoque recusam quem não existe). A direção vê tudo,
        // então nenhum botão nasce apagado por falta de bit.
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose()
    {
        Diagnostico.Sink = _sinkOriginal;
        // De volta a "ninguém entrou" — o mesmo estado com que a suíte começa.
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    /// <summary>Toda tela de <c>Clinica.Ui.Blazor.Telas</c> — achada por reflexão, para a próxima cobrar a si mesma.</summary>
    public static IEnumerable<object[]> Telas() => typeof(IHostDaTela).Assembly.GetTypes()
        .Where(t => t.Namespace == "Clinica.Ui.Blazor.Telas" && typeof(TelaBase).IsAssignableFrom(t) && !t.IsAbstract)
        .OrderBy(t => t.Name)
        .Select(t => new object[] { t.Name });

    [Fact]
    public void As_quatorze_telas_do_Financeiro_estao_na_lista()
        => Assert.True(Telas().Count() >= 14, $"esperava 14 telas, achei {Telas().Count()}");

    [Theory]
    [MemberData(nameof(Telas))]
    public void A_tela_abre_com_dados_sem_erro_nem_falha_de_leitura(string nome)
    {
        var ocorrencias = Vigiar();
        using var ctx = Contexto();
        var tela = Abrir(ctx, nome);

        Assert.DoesNotContain("Algo deu errado nesta tela", tela.Markup);
        Assert.Empty(tela.FindAll(".vazio-estado.falha"));
        Assert.True(ocorrencias.Count == 0,
            $"{nome} registrou no log ao abrir: " + string.Join(" | ", ocorrencias.Select(o => $"{o.Contexto}: {o.Erro.Message}")));
    }

    [Theory]
    [MemberData(nameof(Telas))]
    public void Cada_botao_da_tela_e_dos_formularios_que_ele_abre_responde_sem_quebrar(string nome)
    {
        var ocorrencias = Vigiar();
        using var ctx = Contexto();
        Explorar(ctx, nome, [], new HashSet<string>(), new Dictionary<string, int>(), ocorrencias);
    }

    /// <summary>
    /// Quantos botões de uma mesma FAMÍLIA de dado se clicam por tela. O rótulo de um botão de
    /// dado é o próprio dado ("08:15 Marina Teixeira", "Seg 12", o nome do profissional), então
    /// "um por rótulo" virava um por LINHA: a Visão geral da agenda chegava a 882 cliques e
    /// quatro minutos sozinha, medindo o tamanho da semente e não a tela (out/2026).
    /// </summary>
    private const int PorFamilia = 4;

    /// <summary>Classes de ESTILO de botão (as de ação: o rótulo é a ação, e cada um conta).</summary>
    private static readonly HashSet<string> ClassesDeEstilo =
    [
        "botao", "mini", "icone", "primario", "secundario", "perigo", "contorno-perigo", "fantasma",
        "link", "compacto", "fechar", "chip", "selecionado", "ativo", "kpi-menu-botao",
    ];

    /// <summary>Prefixos de VARIANTE de dado (a família da sessão, o calor da célula, a etapa).</summary>
    private static readonly string[] Variantes = ["fam-", "calor-", "etapa-", "estado-", "grupo-", "tom-"];

    /// <summary>
    /// A família de um botão de dado: as classes próprias dele, sem as de estilo e sem as de
    /// variante. Nulo para botão de ação, que continua sendo testado um por rótulo.
    /// </summary>
    private static string? Familia(IElement botao)
    {
        var proprias = (botao.GetAttribute("class") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(c => !ClassesDeEstilo.Contains(c) && !Variantes.Any(v => c.StartsWith(v, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();
        return proprias.Count == 0 ? null : string.Join(' ', proprias);
    }

    /// <summary>
    /// Clica cada botão uma vez POR RÓTULO — a linha da tabela repete "Editar" em cada conta, e
    /// clicar todas mediria o tamanho da semente, não a tela. O que o clique faz APARECER (a aba
    /// que abriu, o formulário que surgiu, a pergunta do diálogo) é explorado do mesmo jeito,
    /// até dois níveis: é assim que "Novo lançamento → Salvar" e "Particulares → Lançar" entram.
    /// Cada clique parte de uma tela NOVA, refazendo o caminho até ele. Botão de DADO (célula,
    /// bloco de horário, cartão de paciente) conta pela família, até <see cref="PorFamilia"/>.
    /// </summary>
    private void Explorar(TestContext ctx, string nome, List<(int Indice, string Rotulo)> caminho,
        HashSet<string> testados, Dictionary<string, int> porFamilia, List<(string Contexto, Exception Erro)> ocorrencias)
    {
        var aqui = Percorrer(ctx, nome, caminho);
        if (aqui is null) return;
        var candidatos = new List<(int Indice, string Rotulo)>();
        foreach (var (botao, indice) in aqui.FindAll("button").Select((b, k) => (b, k)))
        {
            if (botao.HasAttribute("disabled")) continue;
            var rotulo = Rotulo(botao);
            if (testados.Contains(rotulo)) continue;
            if (Familia(botao) is { } familia)
            {
                if (porFamilia.GetValueOrDefault(familia) >= PorFamilia) continue;
                porFamilia[familia] = porFamilia.GetValueOrDefault(familia) + 1;
            }
            testados.Add(rotulo);
            candidatos.Add((indice, rotulo));
        }

        foreach (var c in candidatos)
        {
            var passos = new List<(int, string)>(caminho) { (c.Indice, c.Rotulo) };
            var depois = Percorrer(ctx, nome, passos);
            if (depois is null) continue;
            var trilha = string.Join(" → ", passos.Select(p => p.Item2));
            ExigirSemQuebra(depois, nome, trilha, ocorrencias);
            if (passos.Count < 3) Explorar(ctx, nome, passos, testados, porFamilia, ocorrencias);
        }
    }

    /// <summary>Abre a tela e refaz os cliques; nulo quando o caminho já não existe (os dados mudaram).</summary>
    private static IRenderedComponent<Raiz>? Percorrer(TestContext ctx, string nome, List<(int Indice, string Rotulo)> caminho)
    {
        var tela = Abrir(ctx, nome);
        foreach (var (indice, rotulo) in caminho)
        {
            var botao = tela.FindAll("button").ElementAtOrDefault(indice);
            if (botao is null || Rotulo(botao) != rotulo || !Clicar(botao)) return null;
            Assentar(tela);
        }
        return tela;
    }

    // ==================== montagem ====================

    private static List<(string Contexto, Exception Erro)> Vigiar()
    {
        var lista = new List<(string, Exception)>();
        _ocorrencias.Value = lista;
        return lista;
    }

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
        // O CRM de mentira da prévia (as conversas da semente do WhatsApp): o HTTP de verdade nunca é chamado em teste.
        PreviaBlazor.CrmDeMentira.Registrar(ctx.Services);
        ctx.Services.AddTelasBlazor();
        ctx.Services.AddSingleton<IHostDaTela, HostDoTeste>();
        return ctx;
    }

    private static IRenderedComponent<Raiz> Abrir(TestContext ctx, string nome)
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas." + nome)
                   ?? throw new InvalidOperationException($"Tela {nome} não existe.");
        var tela = ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        Assentar(tela);
        return tela;
    }

    /// <summary>
    /// Espera a leitura terminar: sem esqueleto e com o HTML parado por três batidas. É um
    /// laço de verdade, e não o WaitForState — aquele só reavalia a cada render, e a carga
    /// que termina SEM mudar nada nunca o reavaliaria.
    /// </summary>
    private static void Assentar(IRenderedComponent<Raiz> tela)
    {
        var prazo = DateTime.UtcNow + Prazo;
        var anterior = string.Empty;
        var estavel = 0;
        // 15 ms × 4 batidas (60 ms parado) em vez de 50 × 3: cada clique da exploração passa
        // por aqui, e o mínimo de 150 ms por passo era mais da metade do tempo da suíte no CI.
        while (DateTime.UtcNow < prazo)
        {
            Thread.Sleep(15);
            var agora = tela.Markup;
            estavel = agora == anterior ? estavel + 1 : 0;
            anterior = agora;
            if (estavel >= 4 && !agora.Contains("class=\"esqueleto\"")) return;
        }
        throw new TimeoutException("A tela não terminou de carregar em " + Prazo.TotalSeconds + " s:\n" + anterior[..Math.Min(anterior.Length, 2000)]);
    }

    private static bool Clicar(IElement botao)
    {
        if (botao.HasAttribute("disabled")) return false;
        try
        {
            botao.Click();
            return true;
        }
        catch (Bunit.MissingEventHandlerException)
        {
            return false; // botão sem @onclick (rótulo de seletor, submit de formulário)
        }
    }

    private static void ExigirSemQuebra(IRenderedComponent<Raiz> tela, string nome, string rotulo,
        List<(string Contexto, Exception Erro)> ocorrencias)
    {
        Assert.False(tela.Markup.Contains("Algo deu errado nesta tela"),
            $"{nome}: o botão «{rotulo}» derrubou a tela.");
        var defeitos = ocorrencias.Where(o => !RecusaDeRegra(o.Erro)).ToList();
        Assert.True(defeitos.Count == 0,
            $"{nome}: o botão «{rotulo}» registrou: " + string.Join(" | ", defeitos.Select(o => $"{o.Contexto}: {o.Erro.GetType().Name}: {o.Erro.Message}")));
        ocorrencias.Clear();
    }

    /// <summary>
    /// Recusa de regra é a tela funcionando (o formulário vazio diz não). Consulta que o EF não
    /// traduz também chega como InvalidOperationException — e essa é DEFEITO.
    /// </summary>
    private static bool RecusaDeRegra(Exception e)
        => e is InvalidOperationException or ArgumentException
           && !e.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase)
           && e.GetType().Assembly != typeof(DbContext).Assembly;

    private static string Rotulo(IElement botao)
    {
        var texto = botao.TextContent.Trim();
        return texto.Length > 0 ? texto : botao.GetAttribute("aria-label") ?? botao.GetAttribute("title") ?? "(sem rótulo)";
    }

    /// <summary>Sem disco nem shell: cada pedido devolve uma frase, como na prévia.</summary>
    private sealed class HostDoTeste : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho,
            IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo)
            => Task.FromResult<string?>($"teste: {linhas.Count} linhas");

        public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao)
            => Task.FromResult<string?>($"teste: {nomeSugerido}");

        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo)
            => Task.FromResult(SementeImportacaoDePacientes.ExemploPara(filtro)
                ?? (SementeTaxasExtrato.ExemploOfx is { } ofx
                    ? new ArquivoEscolhido("extrato-exemplo.ofx", System.Text.Encoding.Latin1.GetBytes(ofx))
                    : null));

        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>("/teste/" + n);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>("/teste");
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}

/// <summary>
/// O <see cref="Diagnostico.Sink"/> e o <see cref="SessaoUsuario.Atual"/> são globais: quem os
/// troca não corre em paralelo com quem os troca (o `Entrar` de uma sessão muda o Atual).
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoDiagnostico
{
    public const string Nome = "Diagnostico global";
}

/// <summary>
/// O banco fictício das telas, semeado UMA vez para a classe: são 24 meses de caixa, e semear
/// por caso de teste custaria minutos. O que um clique grava fica — e está certo: a tela
/// seguinte abre com o que a anterior fez, como na clínica.
/// </summary>
public sealed class BancoDasTelas : IDisposable
{
    public SqliteConnection Conexao { get; } = new("Data Source=:memory:");

    /// <summary>Quem "entrou" no app durante o teste: a direção, ativa e com a senha definida.</summary>
    public UsuarioSistema Gerente { get; }

    public BancoDasTelas()
    {
        Conexao.Open();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(Conexao).Options);
        db.Database.EnsureCreated();
        foreach (var semente in typeof(ISemente).Assembly.GetTypes()
                     .Where(t => typeof(ISemente).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                     .Select(t => (ISemente)Activator.CreateInstance(t)!)
                     .OrderBy(s => s.Ordem))
        {
            semente.Semear(db);
            db.ChangeTracker.Clear();
        }

        Gerente = new UsuarioSistema
        {
            Nome = "Direção (teste)", Login = "direcao", Perfil = PerfilAcesso.Gerente,
            SenhaHash = "-", SenhaSalt = "-", Ativo = true
        };
        db.Add(Gerente);
        db.SaveChanges();
    }

    /// <summary>
    /// Fecha o banco da classe. Uma leitura de fundo de uma tela (o relógio de releitura, a
    /// carga que o último clique disparou) pode ainda estar no ar quando o último caso termina,
    /// e fechar a conexão por baixo dela faz o SQLite reclamar ("Collection was modified")
    /// numa LIMPEZA que reprova a classe inteira. A leitura termina em milissegundos: tenta-se
    /// de novo por até 2 s, e só a última tentativa deixa o erro subir.
    /// </summary>
    public void Dispose()
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try { Conexao.Dispose(); return; }
            catch (InvalidOperationException) when (tentativa < 40) { Thread.Sleep(50); }
        }
    }
}
