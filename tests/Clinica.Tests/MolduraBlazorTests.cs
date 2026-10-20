using Bunit;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Clinica.Ui.Blazor.Moldura;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A MOLDURA DOS APPS EM BLAZOR (out/2026): a entrada, a configuração do banco e o cabeçalho
/// com menu, busca, sino e abas, que eram a LoginWindow, a SetupWindow e a ShellWindow do WPF.
/// A entrada roda sobre o AcessoService de verdade (senha, troca obrigatória, primeiro acesso,
/// recusa do app); a moldura, sobre um hospedeiro de mentira — quem decide o menu e a navegação
/// continua sendo o ShellViewModel, e o que se prova aqui é que a moldura DESENHA o que ele
/// diz e REPASSA o que a pessoa clica.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class MolduraBlazorTests : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");

    public MolduraBlazorTests()
    {
        _conexao.Open();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        db.Database.EnsureCreated();
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _conexao.Dispose();
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
        ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conexao));
        ctx.Services.AddTelasBlazor();
        return ctx;
    }

    private async Task<UsuarioSistema> CriarUsuarioAsync(TestContext ctx, string login, string senha,
        PerfilAcesso perfil = PerfilAcesso.Recepcao, bool deveTrocar = false)
    {
        using var escopo = ctx.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<AcessoService>()
            .CriarAsync("Pessoa de teste", login, senha, perfil, deveTrocarSenha: deveTrocar);
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condicao())
        {
            if (DateTime.UtcNow > prazo) throw new TimeoutException("a tela não chegou ao estado esperado");
            Thread.Sleep(15);
        }
    }

    // ==================== a entrada ====================

    private sealed class PortaDeEntrada(bool primeiroAcesso = false, Func<UsuarioSistema, string?>? recusar = null) : IEntradaDoApp
    {
        public UsuarioSistema? QuemEntrou { get; private set; }
        public bool Desistiu_ { get; private set; }
        public string NomeDoApp => "Recepção";
        public bool PrimeiroAcesso => primeiroAcesso;
        public string? Recusar(UsuarioSistema usuario) => recusar?.Invoke(usuario);
        public void Entrou(UsuarioSistema usuario) => QuemEntrou = usuario;
        public void Desistiu() => Desistiu_ = true;
    }

    private static void Digitar(IRenderedComponent<Entrada> tela, int indice, string texto)
        => tela.FindAll("input")[indice].Input(texto);

    [Fact]
    public async Task Senha_errada_fica_na_entrada_dizendo_o_erro_e_a_certa_entra()
    {
        using var ctx = Contexto();
        await CriarUsuarioAsync(ctx, "maria", "Senha#Forte2026");
        var porta = new PortaDeEntrada();
        var tela = ctx.RenderComponent<Entrada>(p => p.Add(x => x.Porta, porta));
        Assert.Contains("Entrar — Recepção", tela.Markup);

        Digitar(tela, 0, "maria");
        Digitar(tela, 1, "errada-123");
        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.NotEmpty(tela.FindAll(".aviso.erro")));
        Assert.Null(porta.QuemEntrou);

        Digitar(tela, 1, "Senha#Forte2026");
        tela.Find("form").Submit();
        Esperar(() => porta.QuemEntrou is not null);
        Assert.Equal("maria", porta.QuemEntrou!.Login);
    }

    [Fact]
    public async Task Senha_provisoria_obriga_a_trocar_antes_de_entrar()
    {
        using var ctx = Contexto();
        await CriarUsuarioAsync(ctx, "joao", "Provisoria#2026", deveTrocar: true);
        var porta = new PortaDeEntrada();
        var tela = ctx.RenderComponent<Entrada>(p => p.Add(x => x.Porta, porta));

        Digitar(tela, 0, "joao");
        Digitar(tela, 1, "Provisoria#2026");
        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.Contains("Defina uma nova senha", tela.Markup));
        Assert.Null(porta.QuemEntrou);

        // A repetição que não confere fica na tela — é conferência de digitação.
        Digitar(tela, 0, "Definitiva#2026");
        Digitar(tela, 1, "Outra#2026");
        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.Contains("não são iguais", tela.Markup));

        Digitar(tela, 1, "Definitiva#2026");
        tela.Find("form").Submit();
        Esperar(() => porta.QuemEntrou is not null);
        Assert.False(porta.QuemEntrou!.DeveTrocarSenha);

        using var escopo = ctx.Services.CreateScope();
        var resultado = await escopo.ServiceProvider.GetRequiredService<AcessoService>().AutenticarAsync("joao", "Definitiva#2026");
        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Base_sem_usuario_oferece_o_primeiro_acesso_que_nasce_gerente()
    {
        using var ctx = Contexto();
        var porta = new PortaDeEntrada(primeiroAcesso: true);
        var tela = ctx.RenderComponent<Entrada>(p => p.Add(x => x.Porta, porta));
        Assert.Contains("Primeiro acesso", tela.Markup);

        Digitar(tela, 0, "Direção da clínica");
        Digitar(tela, 1, "direcao");
        Digitar(tela, 2, "Senha#Forte2026");
        Digitar(tela, 3, "Senha#Forte2026");
        tela.Find("form").Submit();
        Esperar(() => porta.QuemEntrou is not null);
        Assert.Equal(PerfilAcesso.Gerente, porta.QuemEntrou!.Perfil);
    }

    [Fact]
    public async Task O_app_que_recusa_diz_na_porta_e_volta_a_pedir_usuario_e_senha()
    {
        using var ctx = Contexto();
        await CriarUsuarioAsync(ctx, "ana", "Senha#Forte2026");
        var porta = new PortaDeEntrada(recusar: u => $"O acesso de {u.Nome} não inclui o faturamento.");
        var tela = ctx.RenderComponent<Entrada>(p => p.Add(x => x.Porta, porta));

        Digitar(tela, 0, "ana");
        Digitar(tela, 1, "Senha#Forte2026");
        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.Contains("não inclui o faturamento", tela.Markup));
        Assert.Null(porta.QuemEntrou);
        Assert.Contains("Entrar — Recepção", tela.Markup);
    }

    [Fact]
    public void Sair_desiste_da_entrada()
    {
        using var ctx = Contexto();
        var porta = new PortaDeEntrada();
        var tela = ctx.RenderComponent<Entrada>(p => p.Add(x => x.Porta, porta));
        tela.FindAll("button").Single(b => b.TextContent.Trim() == "Sair").Click();
        Assert.True(porta.Desistiu_);
    }

    // ==================== a configuração do banco ====================

    private sealed class PortaDoBanco(bool conecta) : IConfiguracaoDoBanco
    {
        public string? Salva { get; private set; }
        public bool? Concluiu { get; private set; }
        public string NomeDoApp => "Consultório";
        public string Normalizar(string entrada) => entrada.Trim();
        public Task<(bool Ok, string Mensagem)> TestarAsync(string conexao)
            => Task.FromResult(conecta ? (true, "") : (false, "senha recusada"));
        public void Salvar(string conexao) => Salva = conexao;
        public void Concluir(bool salvou) => Concluiu = salvou;
    }

    [Fact]
    public void Salvar_so_libera_depois_do_teste_e_editar_invalida_o_teste()
    {
        using var ctx = Contexto();
        var porta = new PortaDoBanco(conecta: true);
        var tela = ctx.RenderComponent<ConfiguracaoDoBanco>(p => p.Add(x => x.Porta, porta));
        var salvar = () => tela.FindAll("button").Single(b => b.TextContent.Contains("Salvar e continuar"));
        Assert.True(salvar().HasAttribute("disabled"));

        tela.Find("textarea").Input("Host=banco;Database=clinica");
        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.False(salvar().HasAttribute("disabled")));

        // O que foi validado deixa de ser o que está na tela.
        tela.Find("textarea").Input("Host=outro;Database=clinica");
        Assert.True(salvar().HasAttribute("disabled"));

        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.False(salvar().HasAttribute("disabled")));
        salvar().Click();
        Assert.Equal("Host=outro;Database=clinica", porta.Salva);
        Assert.True(porta.Concluiu);
    }

    [Fact]
    public void Conexao_que_nao_abre_diz_por_que_e_nao_libera_o_salvar()
    {
        using var ctx = Contexto();
        var porta = new PortaDoBanco(conecta: false);
        var tela = ctx.RenderComponent<ConfiguracaoDoBanco>(p => p.Add(x => x.Porta, porta));
        tela.Find("textarea").Input("Host=banco");
        tela.Find("form").Submit();
        tela.WaitForAssertion(() => Assert.Contains("senha recusada", tela.Markup));
        Assert.True(tela.FindAll("button").Single(b => b.TextContent.Contains("Salvar")).HasAttribute("disabled"));
        Assert.Null(porta.Salva);
    }

    // ==================== a moldura ====================

    private sealed class MolduraDeMentira : IMolduraDoApp
    {
        public List<string> Navegou { get; } = [];
        public List<int> Abriu { get; } = [];
        public bool Ligou { get; private set; }
        public bool TrocouUsuario { get; private set; }
        public string NomeDoApp => "Recepção";
        public string UsuarioRotulo => "Maria Silva (Recepção)";
        public string DataHoje => "seg, 20/10/2026";
        public Palco Palco { get; } = new();
        public IReadOnlyList<GrupoDaBarra> Barra { get; set; } =
        [
            new("Agenda", new ItemDaBarra("agenda", "Agenda", null, true), []),
            new("Mais", null, [new ItemDaBarra("caixa", "Caixa", "O dinheiro do dia", false), new ItemDaBarra("pacotes", "Pacotes", null, false)])
        ];
        public string TituloTela => "Agenda";
        public IReadOnlyList<AbaDaTela> Abas { get; set; } = [];
        public string? FalhaDaTela { get; set; }
        public bool AtualizandoTela => false;
        public string TextoPesquisa { get; set; } = "";
        public bool PesquisaAberta => TextoPesquisa.Length > 0;
        public IReadOnlyList<ResultadoDaPesquisa> ResultadosPesquisa =>
            TextoPesquisa.Length > 0 ? [new ResultadoDaPesquisa("Fechamento", "Caixa", "alvo")] : [];
        public void AbrirResultado(ResultadoDaPesquisa? resultado) => Navegou.Add("resultado:" + (resultado?.Rotulo ?? "primeiro"));
        public void FecharPesquisa() => TextoPesquisa = "";
        public void Navegar(string chave) => Navegou.Add(chave);
        public void SelecionarAba(int indice) => Abriu.Add(indice);
        public void AbrirTreinamento() => Navegou.Add("treinamento");
        public int AssinaturasPendentes => 0;
        public string ResumoAssinaturas => "";
        public void AbrirFilaDeInfusao() { }
        public bool SinoAberto { get; set; }
        public string ContadorDoSino => "3";
        public string ResumoDoSino => "3 novos";
        public SecaoDoSino<PedidoNoSino> Pedidos => new(true, "1 novo", [new PedidoNoSino(7, "Até 1 atendimento a mais · BSV", "hoje 14:00–15:00", "Joana", true)], "");
        public SecaoDoSino<RespostaNoSino> Respostas => SecaoDoSino<RespostaNoSino>.Oculta;
        public IReadOnlyList<AvisoDaSessao> Historico => [new AvisoDaSessao("Salvo.", TomNotificacao.Sucesso, "09:12")];
        public void AlternarSino() { SinoAberto = !SinoAberto; Mudou?.Invoke(); }
        public void AbrirPedido(PedidoNoSino pedido) => Abriu.Add(-pedido.Id);
        public void AbrirTodosOsPedidos() { }
        public void AbrirResposta(RespostaNoSino resposta) { }
        public void AbrirConfirmacoes() { }
        public void FecharSobreposicoes() { SinoAberto = false; }
        public void TrocarUsuario() => TrocouUsuario = true;
        public void Ligar(Dialogos dialogos, Notificacoes notificacoes) => Ligou = true;
        public event Action? Mudou;
    }

    [Fact]
    public void A_moldura_desenha_o_menu_e_repassa_os_cliques_ao_shell()
    {
        using var ctx = Contexto();
        var moldura = new MolduraDeMentira();
        var tela = ctx.RenderComponent<MolduraDoApp>(p => p.Add(x => x.Moldura, moldura));
        Assert.True(moldura.Ligou); // os diálogos e as notificações deste navegador foram entregues
        Assert.Contains("Recepção", tela.Find(".moldura-app").TextContent);
        Assert.Contains("MS", tela.Find(".avatar").TextContent);

        // Entrada DIRETA navega no clique; categoria abre o submenu e o item navega.
        tela.FindAll(".moldura-grupo > button").Single(b => b.TextContent.Trim() == "Agenda").Click();
        Assert.Contains("agenda", moldura.Navegou);
        tela.FindAll(".moldura-grupo > button").Single(b => b.TextContent.Contains("Mais")).Click();
        var caixa = tela.FindAll(".moldura-submenu button").Single(b => b.TextContent.Contains("Caixa"));
        Assert.Contains("O dinheiro do dia", caixa.TextContent); // a descrição do item vai junto
        caixa.Click();
        Assert.Contains("caixa", moldura.Navegou);
        Assert.Empty(tela.FindAll(".moldura-submenu")); // navegar fecha o submenu

        tela.FindAll("button").Single(b => b.TextContent.Trim() == "Treinamento").Click();
        Assert.Contains("treinamento", moldura.Navegou);
    }

    [Fact]
    public void A_busca_mostra_o_caminho_e_Enter_abre_o_primeiro()
    {
        using var ctx = Contexto();
        var moldura = new MolduraDeMentira();
        var tela = ctx.RenderComponent<MolduraDoApp>(p => p.Add(x => x.Moldura, moldura));
        tela.Find("#pesquisa-global").Input("fecha");
        Assert.Equal("fecha", moldura.TextoPesquisa);
        tela.Render();
        Assert.Contains("— em Caixa", tela.Find(".moldura-resultados").TextContent);
        tela.Find("#pesquisa-global").KeyDown("Enter");
        Assert.Contains("resultado:primeiro", moldura.Navegou);
    }

    [Fact]
    public void Abas_so_aparecem_com_mais_de_uma_e_o_clique_escolhe_a_aba()
    {
        using var ctx = Contexto();
        var moldura = new MolduraDeMentira { Abas = [new AbaDaTela("Movimento", true)] };
        var tela = ctx.RenderComponent<MolduraDoApp>(p => p.Add(x => x.Moldura, moldura));
        Assert.Empty(tela.FindAll(".moldura-abas")); // UMA aba não é aba: é a tela

        moldura.Abas = [new AbaDaTela("Movimento", true), new AbaDaTela("Fechamento", false)];
        tela.Render();
        var abas = tela.FindAll(".moldura-abas button");
        Assert.Equal("true", abas[0].GetAttribute("aria-selected"));
        abas[1].Click();
        Assert.Equal([1], moldura.Abriu);
    }

    [Fact]
    public void O_sino_conta_lista_os_pedidos_e_o_historico_da_sessao()
    {
        using var ctx = Contexto();
        var moldura = new MolduraDeMentira();
        var tela = ctx.RenderComponent<MolduraDoApp>(p => p.Add(x => x.Moldura, moldura));
        Assert.Equal("3", tela.Find(".moldura-contador.sobre").TextContent);

        tela.Find(".moldura-sino button").Click();
        tela.WaitForAssertion(() => Assert.NotEmpty(tela.FindAll(".moldura-popover.sino")));
        Assert.Contains("Até 1 atendimento a mais", tela.Markup);
        Assert.Contains("Salvo.", tela.Markup);
        tela.FindAll(".moldura-aviso").First().Click();
        Assert.Contains(-7, moldura.Abriu);
    }

    [Fact]
    public void Tela_que_nao_abriu_diz_isso_no_lugar_dela()
    {
        using var ctx = Contexto();
        var moldura = new MolduraDeMentira { FalhaDaTela = "Não foi possível abrir esta tela." };
        var tela = ctx.RenderComponent<MolduraDoApp>(p => p.Add(x => x.Moldura, moldura));
        Assert.Contains("Não foi possível abrir esta tela.", tela.Find(".moldura-falha").TextContent);
    }

    [Fact]
    public void Trocar_usuario_pergunta_antes_e_so_reabre_se_confirmar()
    {
        using var ctx = Contexto();
        var moldura = new MolduraDeMentira();
        var tela = ctx.RenderComponent<MolduraDoApp>(p => p.Add(x => x.Moldura, moldura));
        tela.FindAll("button").Single(b => b.TextContent.Trim() == "Trocar usuário").Click();
        tela.WaitForAssertion(() => Assert.Contains("O sistema vai fechar", tela.Markup));
        Assert.False(moldura.TrocouUsuario);
        tela.FindAll(".modal-rodape button").Single(b => b.TextContent.Contains("Fechar e trocar")).Click();
        tela.WaitForAssertion(() => Assert.True(moldura.TrocouUsuario));
    }

    // ==================== a raiz da janela ====================

    [Fact]
    public void A_raiz_troca_da_entrada_para_a_moldura_no_mesmo_navegador()
    {
        using var ctx = Contexto();
        var estado = new EstadoDaJanela();
        var desenhou = false;
        estado.Desenhou += () => desenhou = true;
        estado.MostrarEntrada(new PortaDeEntrada());
        var raiz = ctx.RenderComponent<RaizDoApp>(p => p.Add(x => x.Estado, estado));
        Assert.True(desenhou); // é o que libera o navegador escondido da janela
        Assert.NotEmpty(raiz.FindAll(".entrada"));

        estado.MostrarMoldura(new MolduraDeMentira());
        raiz.WaitForAssertion(() => Assert.NotEmpty(raiz.FindAll(".moldura")));
        Assert.Empty(raiz.FindAll(".entrada"));
    }
}
