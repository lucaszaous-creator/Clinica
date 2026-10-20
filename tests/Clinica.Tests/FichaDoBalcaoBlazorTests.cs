using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Formularios.Recepcao;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A FICHA DO BALCÃO em Blazor (out/2026 — era a FichaBalcaoView WPF numa janela por cima) e o
/// ORÇAMENTO LIVRE (era a OrcamentoWindow): as regras que a ViewModel WPF escrevia à mão (agora
/// em <see cref="FichaDoBalcao"/> e <see cref="OrcamentoLivreNaTela"/>, na Application) e as
/// telas RENDERIZADAS (bUnit) sobre o banco das telas, clicadas de verdade.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class FichaDoBalcaoBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private static readonly DateOnly Hoje = new(2026, 10, 20);

    private readonly BancoDasTelas _banco;
    private readonly Action<string, Exception>? _sinkOriginal = Diagnostico.Sink;
    private readonly List<(string Contexto, Exception Erro)> _ocorrencias = [];

    public FichaDoBalcaoBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        var anterior = _sinkOriginal;
        Diagnostico.Sink = (c, e) => { lock (_ocorrencias) _ocorrencias.Add((c, e)); anterior?.Invoke(c, e); };
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose()
    {
        Diagnostico.Sink = _sinkOriginal;
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    // ==================== As regras da ficha (Application) ====================

    [Fact]
    public void Cabecalho_diz_o_que_falta_com_traco_e_a_idade_impossivel_com_a_frase_de_conferir()
    {
        var completo = new Paciente
        {
            Nome = "Maria", Documento = "12345678900", Telefone = "21999990000", DataNascimento = new DateOnly(1980, 5, 2),
            Carteirinha = "000123", ValidadeCarteirinha = new DateOnly(2026, 12, 31), Email = " maria@exemplo.com ",
            Endereco = "Rua A, 10"
        };
        var c = FichaDoBalcao.Cabecalho(completo, Hoje);
        c.Documento.Should().Be("123.456.789-00");
        c.Idade.Should().Contain("46 anos").And.Contain("02/05/1980");
        c.Carteirinha.Should().Be("000123 · vence 31/12/2026");
        c.Email.Should().Be("maria@exemplo.com");
        c.PacienteDesde.Should().Be("nenhuma sessão realizada ainda");

        var vazio = FichaDoBalcao.Cabecalho(new Paciente { Nome = "Sem nada" }, Hoje);
        vazio.Documento.Should().Be("—");
        vazio.Idade.Should().Be("—");
        vazio.Telefone.Should().Be("—");
        vazio.Carteirinha.Should().BeNull("sem carteirinha, a linha não fala de carteirinha");
        vazio.Email.Should().Be("—");

        FichaDoBalcao.Cabecalho(new Paciente { Nome = "Data errada", DataNascimento = new DateOnly(1851, 1, 1) }, Hoje)
            .Idade.Should().NotContain("anos").And.Contain("conferir");
    }

    [Fact]
    public void Pacote_diz_uso_fracao_e_situacao_e_o_sem_contrato_nao_tem_barra()
    {
        var pacote = new PacotePaciente
        {
            Nome = "10 sessões", SessoesContratadas = 10, DataCompra = new DateOnly(2026, 9, 1), ValidoAte = new DateOnly(2026, 12, 1),
            Consumos = Enumerable.Range(0, 7).Select(_ => new ConsumoPacote()).ToList()
        };
        var linha = FichaDoBalcao.PacoteDe(pacote, Hoje);
        linha.Uso.Should().Be("7 de 10 usadas");
        linha.Fracao.Should().BeApproximately(0.7, 1e-9);
        linha.TemFracao.Should().BeTrue();
        linha.Ativo.Should().BeTrue();
        linha.Situacao.Should().Be("Vence em 01/12/2026 · comprado em 01/09/2026");

        var livre = FichaDoBalcao.PacoteDe(new PacotePaciente { Nome = "Livre", DataCompra = new DateOnly(2026, 1, 1) }, Hoje);
        livre.TemFracao.Should().BeFalse();
        livre.Uso.Should().Be("0 sessões usadas");
    }

    [Fact]
    public void Autorizacao_mostra_a_vigente_e_sem_nenhuma_diz_que_nao_ha()
    {
        FichaDoBalcao.Autorizacao([], Hoje).Should().Be(("Nenhuma autorização registrada", "—"));

        var vencida = new SaldoAutorizacao(new AutorizacaoSessoes
        {
            Numero = "111", DataEmissao = new DateOnly(2026, 1, 1), DataValidade = new DateOnly(2026, 3, 1), QuantidadeAutorizada = 10
        }, 10, 0, Vencida: true);
        var vigente = new SaldoAutorizacao(new AutorizacaoSessoes
        {
            Numero = "88123", DataEmissao = new DateOnly(2026, 9, 1), DataValidade = new DateOnly(2026, 11, 30), QuantidadeAutorizada = 10
        }, 7, 3, Vencida: false);

        FichaDoBalcao.Autorizacao([vencida, vigente], Hoje)
            .Should().Be(("Senha 88123 · 7 de 10 sessões usadas", "até 30/11/2026"));
        FichaDoBalcao.Autorizacao([vencida], Hoje).Validade.Should().Be("vencida em 01/03/2026");
    }

    [Fact]
    public void Guias_em_aberto_nao_contam_o_particular_nem_o_estornado()
    {
        var atendimentos = new[]
        {
            new Atendimento { Codigos = [new() { Status = StatusCodigo.Aberto }, new() { Status = StatusCodigo.Baixado, DataBaixa = Hoje }] },
            new Atendimento { Codigos = [new() { Status = StatusCodigo.NaoAplicavel }] },
            new Atendimento { EstornadoEm = DateTime.Today, Codigos = [new() { Status = StatusCodigo.Aberto }] }
        };
        FichaDoBalcao.GuiasEmAberto(atendimentos).Should().Be(1);
    }

    [Fact]
    public void Comunicacao_le_o_consentimento_de_mensagens()
    {
        FichaDoBalcao.Comunicacao([]).Should().Be("—");
        FichaDoBalcao.Comunicacao([new FichaAdministrativaNaTela.Consentimento(FinalidadeConsentimento.ComunicacaoEMarketing, "Mensagens", "Concedido", true, 1)])
            .Should().Be("Mensagens autorizadas");
        FichaDoBalcao.Comunicacao([new FichaAdministrativaNaTela.Consentimento(FinalidadeConsentimento.ComunicacaoEMarketing, "Mensagens", "Revogado em 01/10/2026", false, 1)])
            .Should().Be("Revogado em 01/10/2026");
    }

    // ==================== O orçamento livre (Application) ====================

    [Fact]
    public void Orcamento_soma_so_as_linhas_completas_e_vazio_e_traco_nao_zero()
    {
        var linhas = new List<LinhaDoOrcamentoLivre>
        {
            new() { Descricao = "Sessão de acupuntura", Quantidade = "10", ValorUnitario = "180,00" },
            new() { Descricao = "Avaliação", Quantidade = "1", ValorUnitario = "1.250,50" },
            new() { Descricao = "", Quantidade = "1", ValorUnitario = "999" }
        };
        OrcamentoLivreNaTela.TotalDaLinha(linhas[0]).Should().Be("R$ 1.800,00");
        OrcamentoLivreNaTela.TotalGeral(linhas).Should().Be("R$ 3.050,50");
        OrcamentoLivreNaTela.TotalGeral([new LinhaDoOrcamentoLivre()]).Should().Be("—");
        OrcamentoLivreNaTela.TotalDaLinha(new LinhaDoOrcamentoLivre { Descricao = "x", Quantidade = "abc", ValorUnitario = "10" }).Should().Be("—");
    }

    [Fact]
    public void Orcamento_recusa_o_que_nao_se_le_e_monta_os_itens_na_ordem()
    {
        var (itens, erro) = OrcamentoLivreNaTela.Montar(
            [new() { Descricao = " Sessão ", Quantidade = "2", ValorUnitario = "100" }, new() { Descricao = "", ValorUnitario = "lixo" },
             new() { Descricao = "Pacote", Quantidade = "1,5", ValorUnitario = "R$ 40,00" }], Hoje, Hoje.AddDays(30));
        erro.Should().BeNull();
        itens.Select(i => (i.Ordem, i.Descricao, i.Quantidade, i.ValorUnitario))
            .Should().Equal((1, "Sessão", 2m, 100m), (2, "Pacote", 1.5m, 40m));

        OrcamentoLivreNaTela.Montar([new() { Descricao = "Sessão", Quantidade = "0", ValorUnitario = "10" }], Hoje, Hoje).Erro
            .Should().Contain("Quantidade inválida em \"Sessão\"");
        OrcamentoLivreNaTela.Montar([new() { Descricao = "Sessão", Quantidade = "1", ValorUnitario = "dez" }], Hoje, Hoje).Erro
            .Should().Contain("Valor inválido em \"Sessão\"");
        OrcamentoLivreNaTela.Montar([new LinhaDoOrcamentoLivre()], Hoje, Hoje).Erro.Should().Contain("ao menos um item");
        OrcamentoLivreNaTela.Montar([new() { Descricao = "Sessão", Quantidade = "1", ValorUnitario = "10" }], Hoje, Hoje.AddDays(-1)).Erro
            .Should().Contain("validade é anterior");
    }

    // ==================== O pedido da ficha ====================

    [Fact]
    public void O_mesmo_pedido_nao_muda_a_versao_e_outro_muda()
    {
        var pedido = new PedidoDaFichaDoBalcao();
        pedido.Versao.Should().Be(0);
        pedido.Definir(1, "Ana");
        pedido.Versao.Should().Be(1);
        pedido.Definir(1, "Ana");
        pedido.Versao.Should().Be(1, "abrir de novo a ficha que já está aberta não recarrega tudo");
        pedido.Definir(2, "Bia");
        pedido.Ler().Should().Be((new PedidoDaFichaDoBalcao.Pedido(2, "Bia", 0), 2));
    }

    [Fact]
    public async Task Sem_a_ficha_do_balcao_a_porta_cai_na_ficha_do_host()
    {
        var servicos = new ServiceCollection().AddSingleton<PedidoDaFichaDoBalcao>().BuildServiceProvider();
        var host = new HostDoTeste { NavegaPara = null, AbreFicha = true };
        (await PedidoDaFichaDoBalcao.AbrirAsync(servicos, host, 7, "Carla")).Should().BeTrue();
        host.FichaAberta.Should().Be(7);

        var comFicha = new HostDoTeste { NavegaPara = PedidoDaFichaDoBalcao.Chave };
        (await PedidoDaFichaDoBalcao.AbrirAsync(servicos, comFicha, 8, "Dora")).Should().BeTrue();
        comFicha.FichaAberta.Should().BeNull("a ficha do balcão abriu — o host não foi chamado");
        servicos.GetRequiredService<PedidoDaFichaDoBalcao>().Ler().Pedido!.PacienteId.Should().Be(8);
    }

    // ==================== As telas renderizadas ====================

    [Fact]
    public void A_ficha_abre_no_paciente_pedido_e_cada_aba_e_botao_responde_sem_quebrar()
    {
        var paciente = Semear();
        using var ctx = Contexto();
        ctx.Services.GetRequiredService<PedidoDaFichaDoBalcao>().Definir(paciente.Id, paciente.Nome);
        var tela = ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, typeof(Clinica.Ui.Blazor.Telas.FichaDoBalcao)));

        Esperar(() => tela.Markup.Contains(paciente.Nome) && tela.Markup.Contains("Paciente desde") && !tela.Markup.Contains("class=\"esqueleto\""));
        Assert.Contains("Paciente desde", tela.Markup);
        Assert.Contains("2026 · 1 sessão", tela.Markup);

        foreach (var aba in new[] { "Agenda e sessões", "Convênio, contatos e LGPD", "Pacotes e pagamentos", "Documentos", "Resumo" })
        {
            Clicar(tela, aba);
            ExigirSemQuebra(tela, aba);
            if (aba == "Pacotes e pagamentos") Esperar(() => tela.Markup.Contains("7 de 10 usadas"));
        }

        // Cada botão de ação do cabeçalho responde (na prévia, as pontes dizem que é do aplicativo).
        foreach (var rotulo in new[] { "Prontuário", "Editar cadastro", "Emitir documento", "Marcar" })
        {
            Clicar(tela, rotulo);
            ExigirSemQuebra(tela, rotulo);
        }
    }

    [Fact]
    public void Sem_pedido_a_ficha_diz_que_nao_ha_paciente_aberto()
    {
        using var ctx = Contexto();
        var tela = ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, typeof(Clinica.Ui.Blazor.Telas.FichaDoBalcao)));
        Esperar(() => tela.Markup.Contains("Nenhum paciente aberto"));
    }

    [Fact]
    public void O_orcamento_livre_emite_o_documento_numerado_com_os_itens_digitados()
    {
        var paciente = Semear();
        using var ctx = Contexto();
        string? numero = null;
        var form = ctx.RenderComponent<OrcamentoLivreForm>(p => p
            .Add(f => f.PacienteId, paciente.Id)
            .Add(f => f.Paciente, paciente.Nome)
            .Add(f => f.AoConcluir, (string n) => numero = n));

        var campos = form.FindAll(".orc-livre-itens input");
        campos[0].Input("Sessão de acupuntura");
        form.FindAll(".orc-livre-itens input")[1].Input("4");
        form.FindAll(".orc-livre-itens input")[2].Input("150,00");
        Assert.Contains("R$ 600,00", form.Markup);
        form.FindAll("button").First(b => b.TextContent.Contains("Emitir e imprimir")).Click();

        Esperar(() => numero is not null);
        using var db = Db();
        var doc = db.DocumentosFinanceiros.Include(d => d.Itens).Single(d => d.Numero == numero);
        doc.Tipo.Should().Be(TipoDocumentoFinanceiro.Orcamento);
        doc.PacienteId.Should().Be(paciente.Id);
        doc.Destinatario.Should().Be(paciente.Nome);
        doc.Itens.Single().Should().Match<ItemDocumentoFinanceiro>(i => i.Quantidade == 4m && i.ValorUnitario == 150m);
    }

    // ==================== apoio ====================

    private Paciente Semear()
    {
        var marca = Guid.NewGuid().ToString("N")[..10];
        using var db = Db();
        var paciente = new Paciente { Nome = $"Zuleica Fichateste {marca}", ConvenioCodigo = "Particular", Telefone = "21988887777" };
        db.Add(paciente);
        db.SaveChanges();
        db.Add(new Atendimento
        {
            PacienteId = paciente.Id, Data = new DateOnly(2026, 10, 1), RealizadoEm = new DateTime(2026, 10, 1, 10, 0, 0),
            Modalidade = ModalidadeAtendimento.AcupunturaSimples, ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples)
        });
        var pacote = new PacotePaciente
        {
            PacienteId = paciente.Id, Nome = "10 sessões", SessoesContratadas = 10, Valor = 1500m,
            DataCompra = new DateOnly(2026, 9, 1), ValidoAte = DateOnly.FromDateTime(DateTime.Today).AddDays(60)
        };
        for (var i = 0; i < 7; i++) pacote.Consumos.Add(new ConsumoPacote { Data = new DateOnly(2026, 9, 2 + i) });
        db.Add(pacote);
        db.SaveChanges();
        return paciente;
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
        ctx.Services.AddSingleton<IHostDaTela>(new HostDoTeste());
        return ctx;
    }

    private static void Clicar(IRenderedFragment tela, string rotulo)
    {
        Esperar(() => tela.FindAll("button").Any(b => b.TextContent.Trim().EndsWith(rotulo, StringComparison.Ordinal) && !b.HasAttribute("disabled")));
        tela.FindAll("button").First(b => b.TextContent.Trim().EndsWith(rotulo, StringComparison.Ordinal)).Click();
        Thread.Sleep(80);
    }

    private void ExigirSemQuebra(IRenderedFragment tela, string rotulo)
    {
        Assert.False(tela.Markup.Contains("Algo deu errado nesta tela"), $"«{rotulo}» derrubou a ficha.");
        List<(string Contexto, Exception Erro)> defeitos;
        lock (_ocorrencias) defeitos = _ocorrencias.Where(o => o.Erro is not (InvalidOperationException or ArgumentException)).ToList();
        Assert.True(defeitos.Count == 0, $"«{rotulo}» registrou: " + string.Join(" | ", defeitos.Select(o => $"{o.Contexto}: {o.Erro.Message}")));
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            catch (ElementNotFoundException) { }
            Thread.Sleep(25);
        }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    private sealed class HostDoTeste : IHostDaTela
    {
        public string? NavegaPara { get; init; }
        public bool AbreFicha { get; init; }
        public int? FichaAberta { get; private set; }

        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> c, IReadOnlyList<IReadOnlyList<string>> l, string n) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] c, string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string f, string t) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(chave == NavegaPara);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId)
        {
            if (AbreFicha) FichaAberta = pacienteId;
            return Task.FromResult(AbreFicha);
        }
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
