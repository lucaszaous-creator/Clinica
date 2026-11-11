using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Servicos;
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
/// O FATURAMENTO EM BLAZOR FAZ O QUE O WPF FAZIA — os três atos que mais custam se algo se
/// perder na troca de tecnologia, exercitados clicando a tela de verdade (bUnit, sobre os
/// serviços do app e as sementes da prévia), e afirmando o ESTADO do banco, não a frase:
///
/// 1. desistir da janela de salvar o lote TISS NÃO cria lote nem gasta o número da sequência
///    (o WPF perguntava o destino antes; a primeira versão Blazor criava antes de perguntar);
/// 2. dar baixa pelo painel de pendências grava a baixa com o número digitado;
/// 3. os atalhos do teclado chegam à tela à vista pelo palco (Ctrl+P exporta o CSV, como o
///    IAtalhosDeTela do WPF) — e não agem por baixo de uma pergunta aberta.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class FaturamentoBlazorParidadeTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public FaturamentoBlazorParidadeTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    /// <summary>
    /// Os dois desfechos na MESMA sequência, porque a semente tem um conjunto só de guias sem
    /// lote: primeiro desiste da janela de salvar (nada pode ter sido criado, o número da
    /// sequência intacto), depois escolhe o destino e o lote nasce com o XML gravado lá.
    /// </summary>
    [Fact]
    public void Desistir_de_salvar_nao_cria_lote_e_escolher_o_destino_cria_e_grava_o_XML()
    {
        Assert.True(Candidatas() > 0, "a semente precisa de guias baixadas sem lote no mês corrente");

        // 1. Desistir.
        var desiste = new HostGravador { Destino = null, Pasta = null };
        using (var ctx = Contexto(desiste))
        {
            var (lotesAntes, sequenciaAntes) = Estado();
            var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.LotesTiss));
            Clicar(tela, "Gerar lote TISS (.xml)");
            Concluir(tela);

            Assert.True(desiste.PerguntouDestino, "a tela tinha de perguntar onde salvar ANTES de criar o lote");
            Assert.Contains("nenhum lote foi criado", tela.Markup);
            var (lotesDepois, sequenciaDepois) = Estado();
            Assert.Equal(lotesAntes, lotesDepois);
            Assert.Equal(sequenciaAntes, sequenciaDepois);
            Assert.Empty(desiste.Gravados);
        }

        // 2. Escolher o destino.
        var escolhe = new HostGravador { Destino = "/teste/lote.xml", Pasta = "/teste" };
        using (var ctx = Contexto(escolhe))
        {
            var (lotesAntes, _) = Estado();
            var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.LotesTiss));
            Clicar(tela, "Gerar lote TISS (.xml)");
            Concluir(tela);

            var (lotesDepois, _) = Estado();
            Assert.True(lotesDepois > lotesAntes, "o lote não foi criado");
            // Um XML por lote criado, cada um no destino escolhido.
            Assert.Equal(lotesDepois - lotesAntes, escolhe.Gravados.Count);
            Assert.All(escolhe.Gravados, g =>
            {
                Assert.StartsWith("/teste", g.Caminho);
                Assert.Contains("mensagemTISS", System.Text.Encoding.UTF8.GetString(g.Conteudo));
            });
        }
    }

    [Fact]
    public void Dar_baixa_pelo_painel_grava_a_baixa_com_o_numero_digitado()
    {
        using var ctx = Contexto(new HostGravador());
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        var abertasAntes = Contar(c => c.Status == StatusCodigo.Aberto);

        Clicar(tela, "Dar baixa");
        var campo = tela.Find("input.numero-guia");
        campo.Input("7654321");
        Assentar(tela);
        Clicar(tela, tela.FindAll(".modal-rodape button.primario").Last());
        ConfirmarDialogos(tela);

        Assert.Equal(1, Contar(c => c.NumeroGuiaReal == "7654321" && c.Status == StatusCodigo.Baixado));
        Assert.Equal(abertasAntes - 1, Contar(c => c.Status == StatusCodigo.Aberto));
    }

    /// <summary>
    /// A baixa de CONSULTA da Saúde Petrobras (nov/2026): o formulário abre com a guia ANS — os
    /// campos da "Guia de Consulta" do portal —, já com o que o sistema sabe; incompleta, não
    /// grava nada e diz por quê; completa, grava a baixa E a guia conferida.
    /// </summary>
    [Fact]
    public void Baixa_de_consulta_Petrobras_abre_a_guia_ANS_e_so_grava_com_ela_completa()
    {
        var marca = $"Petro{Guid.NewGuid():N}"[..12];
        int codigoId;
        using (var db = Db())
        {
            var repo = new ClinicaRepositorio(db);
            var paciente = new Paciente
            {
                Nome = $"Zenaide Guiateste {marca}", Convenio = Convenio.Petrobras, ConvenioCodigo = "Petrobras",
                Carteirinha = "010203040506", Sexo = Sexo.Feminino
            };
            var medica = new Profissional { Nome = $"Dra. Guia {marca}", RegistroConselho = "CRM-RJ 654321" };
            db.AddRange(paciente, medica);
            db.SaveChanges();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lancado = new AtendimentoService(repo).LancarAsync(paciente.Id, hoje, ModalidadeAtendimento.Consulta).GetAwaiter().GetResult();
            codigoId = lancado.Atendimento.Codigos.OrderBy(c => c.Ordem).First().Id;
            db.Add(new Agendamento
            {
                PacienteId = paciente.Id, DataHora = DateTime.Today.AddHours(8), ModalidadePrevista = ModalidadeAtendimento.Consulta,
                ProfissionalId = medica.Id, AtendimentoId = lancado.Atendimento.Id, Status = StatusAgendamento.Realizado
            });
            db.SaveChanges();
        }

        using var ctx = Contexto(new HostGravador());
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        var linha = tela.FindAll("tr").First(tr => tr.TextContent.Contains(marca));
        Clicar(tela, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dar baixa"));
        Esperar(() => tela.FindAll("[data-guia-ans]").Count > 0);

        // Abre com os blocos do portal e com o que o sistema sabe já preenchido.
        Assert.Contains("Dados do profissional executante", tela.Markup);
        Assert.Equal("010203040506", tela.Find("#ga-NumeroCarteira").GetAttribute("value"));
        Assert.Equal($"Dra. Guia {marca}", tela.Find("#ga-NomeProfissional").GetAttribute("value"));
        Assert.Equal("654321", tela.Find("#ga-NumeroConselho").GetAttribute("value"));
        Assert.Equal("9", tela.Find("#ga-IndicacaoAcidente").GetAttribute("value"));
        Assert.Equal("01", tela.Find("#ga-RegimeAtendimento").GetAttribute("value"));

        // Sem CBO a guia está incompleta: o botão nasce apagado, e o clique (que o bUnit faz
        // mesmo assim) recusa com a frase — nada é gravado, e nada estoura.
        tela.Find("input.numero-guia[placeholder='Copie do portal da operadora']").Input("PB778899");
        Assentar(tela);
        var confirmar = tela.FindAll(".modal-rodape button.primario").Last();
        Assert.True(confirmar.HasAttribute("disabled"));
        Clicar(tela, confirmar);
        Assert.Contains("Complete a guia ANS antes de dar baixa", tela.Markup);
        Assert.Equal(0, Contar(c => c.Id == codigoId && c.Status == StatusCodigo.Baixado));

        // Completa a guia (tudo explícito, para o teste não depender do que a semente cadastrou).
        void Texto(string campo, string valor) { tela.Find($"#ga-{campo}").Input(valor); Assentar(tela); }
        void Escolher(string campo, string valor) { tela.Find($"#ga-{campo}").Change(valor); Assentar(tela); }
        Texto("RegistroAns", "422631");
        Texto("CodigoPrestadorNaOperadora", "12345678000190");
        Texto("Cnes", "9999999");
        Escolher("ConselhoProfissional", "06");
        Escolher("UfConselho", "33");
        Escolher("Cbos", "225105");
        Escolher("TipoConsulta", "1");
        Escolher("CodigoTabela", "22");
        Texto("CodigoProcedimento", "10101012");
        Texto("ValorProcedimento", "139,00");
        Assert.DoesNotContain("campo-erro", tela.Find("[data-guia-ans]").InnerHtml);

        Clicar(tela, tela.FindAll(".modal-rodape button.primario").Last());
        ConfirmarDialogos(tela);
        Esperar(() => Contar(c => c.Id == codigoId && c.Status == StatusCodigo.Baixado) == 1);

        using var conferir = Db();
        var guia = conferir.GuiasTissConsulta.AsNoTracking().Single(g => g.CodigoId == codigoId);
        Assert.Equal("225105", guia.Cbos);
        Assert.Equal("33", guia.UfConselho);
        Assert.Equal("06", guia.ConselhoProfissional);
        Assert.Equal(139.00m, guia.ValorProcedimento);
        Assert.Equal("010203040506", guia.NumeroCarteira);
        Assert.Equal(_banco.Gerente.Login, guia.ConferidaPor);
        Assert.Equal("PB778899", conferir.Codigos.AsNoTracking().Single(c => c.Id == codigoId).NumeroGuiaReal);
    }

    /// <summary>A baixa dos OUTROS convênios não ganhou campo nenhum: continua número, data e observação.</summary>
    [Fact]
    public void Baixa_dos_outros_convenios_nao_mostra_a_guia_ANS()
    {
        using var ctx = Contexto(new HostGravador());
        var tela = Abrir(ctx, typeof(Clinica.Ui.Blazor.Telas.PendenciasFaturamento));
        var linha = tela.FindAll("tr").First(tr => tr.TextContent.Contains("Adriana Lopes Machado")
                                                   && tr.QuerySelectorAll("button").Any(b => b.TextContent.Trim() == "Dar baixa"));

        Clicar(tela, linha.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Dar baixa"));
        Esperar(() => tela.FindAll("input.numero-guia").Count > 0);

        Assert.Empty(tela.FindAll("[data-guia-ans]"));
        Assert.Single(tela.FindAll("input.numero-guia"));
    }

    [Fact]
    public async Task Ctrl_P_no_palco_exporta_o_CSV_da_tela_a_vista_e_nao_age_sob_uma_pergunta()
    {
        var host = new HostGravador();
        using var ctx = Contexto(host);
        var palco = new Palco();
        var r = ctx.RenderComponent<PalcoDeTelas>(p => p.Add(x => x.Palco, palco));
        palco.Ativar(typeof(Clinica.Ui.Blazor.Telas.GuiasFaturadas));
        Esperar(() => r.Markup.Contains("Exportar") && !r.Markup.Contains("class=\"esqueleto\""));

        await r.InvokeAsync(() => r.Instance.Atalho("Imprimir"));
        Esperar(() => host.Csvs > 0);
        Assert.Equal(1, host.Csvs);

        // Com um diálogo aberto o atalho não age por baixo dele.
        var dialogos = ctx.Services.GetRequiredService<Dialogos>();
        _ = r.InvokeAsync(() => dialogos.ConfirmarAsync("Pergunta", "Aberta"));
        await r.InvokeAsync(() => r.Instance.Atalho("Imprimir"));
        Thread.Sleep(300);
        Assert.Equal(1, host.Csvs);

        // Nome que não é atalho é ignorado (não é um Enum.Parse de qualquer coisa).
        await r.InvokeAsync(() => r.Instance.Atalho("2"));
        Assert.Equal(1, host.Csvs);
    }

    // ==================== apoio ====================

    private (int Lotes, int Sequencia) Estado()
    {
        using var db = Db();
        var parametros = new Clinica.Application.Servicos.ParametrosService(new ClinicaRepositorio(db));
        return (db.Set<LoteTiss>().Count(), parametros.ObterProximoNumeroLoteTissAsync().GetAwaiter().GetResult());
    }

    private int Candidatas()
    {
        using var db = Db();
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        return new Clinica.Application.Servicos.LoteTissService(new ClinicaRepositorio(db),
                new Clinica.Application.Servicos.ParametrosService(new ClinicaRepositorio(db)))
            .CandidatasAsync(inicio, inicio.AddMonths(1).AddDays(-1)).GetAwaiter().GetResult().Count;
    }

    private int Contar(Func<CodigoFaturamento, bool> filtro)
    {
        using var db = Db();
        return db.Set<CodigoFaturamento>().AsNoTracking().AsEnumerable().Count(filtro);
    }

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options);

    private TestContext Contexto(IHostDaTela host)
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
        ctx.Services.AddSingleton(host);
        return ctx;
    }

    private static IRenderedComponent<Raiz> Abrir(TestContext ctx, Type tela)
    {
        var r = ctx.RenderComponent<Raiz>(p => p.Add(x => x.Tela, tela));
        Assentar(r);
        return r;
    }

    private static void Clicar(IRenderedComponent<Raiz> tela, string rotulo)
        => Clicar(tela, tela.FindAll("button").First(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled")));

    private static void Clicar(IRenderedComponent<Raiz> tela, IElement botao)
    {
        botao.Click();
        Assentar(tela);
    }

    /// <summary>
    /// Responde "sim" a toda pergunta até a exportação TERMINAR (o botão sai de "Gerando…"):
    /// ela segue assíncrona depois de cada diálogo, e ler o banco no meio leria o lote pela metade.
    /// </summary>
    private static void Concluir(IRenderedComponent<Raiz> tela)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            ConfirmarDialogos(tela);
            if (!tela.Markup.Contains("Gerando…") && tela.FindAll("[role=alertdialog]").Count == 0) { Assentar(tela); return; }
            Thread.Sleep(50);
        }
        throw new TimeoutException("A exportação não terminou a tempo.");
    }

    /// <summary>Responde "sim" a toda pergunta que aparecer (operadoras, radar, confirmação da baixa).</summary>
    private static void ConfirmarDialogos(IRenderedComponent<Raiz> tela)
    {
        for (var i = 0; i < 6; i++)
        {
            var dialogo = tela.FindAll("[role=alertdialog] .modal-rodape button").LastOrDefault();
            if (dialogo is null) return;
            dialogo.Click();
            Assentar(tela);
        }
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

    /// <summary>Hospedeiro que GRAVA o que a tela pediu, para o teste conferir.</summary>
    private sealed class HostGravador : IHostDaTela
    {
        public string? Destino { get; init; } = "/teste/arquivo";
        public string? Pasta { get; init; } = "/teste";
        public bool PerguntouDestino { get; private set; }
        public int Csvs { get; private set; }
        public List<(string Caminho, byte[] Conteudo)> Gravados { get; } = [];

        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo)
        {
            Csvs++;
            return Task.FromResult<string?>(null);
        }
        public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) { PerguntouDestino = true; return Task.FromResult(Destino); }
        public Task<string?> PerguntarPastaAsync(string t) { PerguntouDestino = true; return Task.FromResult(Pasta); }
        public Task<string?> GravarArquivoAsync(string c, byte[] b) { Gravados.Add((c, b)); return Task.FromResult<string?>(null); }
    }
}
