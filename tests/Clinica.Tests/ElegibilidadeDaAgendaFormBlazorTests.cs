using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O painel "Consultar elegibilidade" da agenda (nov/2026), RENDERIZADO (bUnit) sobre os serviços
/// do app e o banco semeado das telas, e clicado de verdade. A afirmação é sobre a FILA, nunca só
/// sobre a frase: escolher um paciente da agenda e consultar põe na fila a carteirinha DO CADASTRO;
/// o avulso põe a carteirinha digitada; o CPF acha a ficha e consulta a carteirinha dela; quem não
/// pode ser consultado aparece desabilitado, com o motivo. Nada vai à operadora: não há conector aqui.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class ElegibilidadeDaAgendaFormBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public ElegibilidadeDaAgendaFormBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void Paciente_da_agenda_consulta_a_carteirinha_do_cadastro_e_o_sem_consulta_fica_desabilitado()
    {
        var (dia, comConsulta, semConsulta) = Semear();
        using var ctx = Contexto();
        var form = ctx.RenderComponent<ElegibilidadeDaAgendaForm>(p => p.Add(x => x.Dia, dia));
        Esperar(() => form.FindAll("[data-paciente-agenda]").Count >= 2);

        var bloqueado = form.Find($"[data-paciente-agenda='{semConsulta.AgendamentoId}']");
        Assert.True(bloqueado.HasAttribute("disabled"));
        Assert.Contains(ElegibilidadeNaAgendaService.SemConsultaAutomatica, bloqueado.TextContent);

        Agir(form, f => f.Find($"[data-paciente-agenda='{comConsulta.AgendamentoId}']"), e => e.ClickAsync(new MouseEventArgs()));
        Esperar(() => form.FindAll("[data-consulta-da-agenda] [data-consultar-elegibilidade]").Count == 1);
        Agir(form, f => f.Find("[data-consulta-da-agenda] [data-consultar-elegibilidade]"), e => e.ClickAsync(new MouseEventArgs()));

        Esperar(() => Pedidos(comConsulta.Carteira).Count == 1);
        var pedido = Pedidos(comConsulta.Carteira).Single();
        Assert.Equal("Petrobras", pedido.ConvenioCodigo);
        Assert.Equal(comConsulta.PacienteId, pedido.PacienteId);
        Assert.Equal(EstadoTransacaoTiss.Pendente, pedido.Estado);
        // Sem conector, o pedido fica na fila: a tela diz "consultando", nunca "não elegível".
        Assert.Contains("Consultando", form.Find("[data-consulta-da-agenda]").TextContent);
        Assert.DoesNotContain("Não elegível", form.Find("[data-consulta-da-agenda]").TextContent);
    }

    [Fact]
    public void Avulso_pela_carteirinha_digitada_poe_ela_na_fila_do_convenio_escolhido()
    {
        var (dia, _, _) = Semear();
        var carteira = "AV" + Guid.NewGuid().ToString("N")[..10];
        using var ctx = Contexto();
        var form = ctx.RenderComponent<ElegibilidadeDaAgendaForm>(p => p.Add(x => x.Dia, dia));
        Esperar(() => form.FindAll("[data-aba-avulso]").Count == 1 && form.FindAll(".esqueleto").Count == 0);
        Agir(form, f => f.Find("[data-aba-avulso]"), e => e.ClickAsync(new MouseEventArgs()));
        Esperar(() => form.FindAll("#ea-Convenio").Count == 1);
        Agir(form, f => f.Find("#ea-Convenio"), e => e.ChangeAsync(new ChangeEventArgs { Value = "Petrobras" }));
        Agir(form, f => f.Find("#ea-Numero"), e => e.InputAsync(new ChangeEventArgs { Value = carteira }));
        Esperar(() => form.FindAll("[data-consulta-avulsa] [data-consultar-elegibilidade]").Count == 1);
        Agir(form, f => f.Find("[data-consulta-avulsa] [data-consultar-elegibilidade]"), e => e.ClickAsync(new MouseEventArgs()));

        Esperar(() => Pedidos(carteira).Count == 1);
        Assert.Equal("Petrobras", Pedidos(carteira).Single().ConvenioCodigo);
    }

    [Fact]
    public void Avulso_pelo_CPF_acha_a_ficha_e_consulta_a_carteirinha_dela()
    {
        var (dia, comConsulta, _) = Semear();
        using var ctx = Contexto();
        var form = ctx.RenderComponent<ElegibilidadeDaAgendaForm>(p => p.Add(x => x.Dia, dia));
        Esperar(() => form.FindAll("[data-aba-avulso]").Count == 1 && form.FindAll(".esqueleto").Count == 0);
        Agir(form, f => f.Find("[data-aba-avulso]"), e => e.ClickAsync(new MouseEventArgs()));
        Esperar(() => form.FindAll("#ea-Tipo").Count == 1);
        Agir(form, f => f.Find("#ea-Convenio"), e => e.ChangeAsync(new ChangeEventArgs { Value = "Petrobras" }));
        Agir(form, f => f.Find("#ea-Tipo"), e => e.ChangeAsync(new ChangeEventArgs { Value = "cpf" }));
        Agir(form, f => f.Find("#ea-Numero"), e => e.InputAsync(new ChangeEventArgs { Value = comConsulta.Cpf }));
        Agir(form, f => f.Find("[data-buscar-cpf]"), e => e.ClickAsync(new MouseEventArgs()));

        Esperar(() => form.FindAll("[data-consulta-avulsa] [data-consultar-elegibilidade]").Count == 1);
        Assert.Contains(comConsulta.Carteira, form.Find("[data-consulta-avulsa]").TextContent);
        Agir(form, f => f.Find("[data-consulta-avulsa] [data-consultar-elegibilidade]"), e => e.ClickAsync(new MouseEventArgs()));
        Esperar(() => Pedidos(comConsulta.Carteira).Count == 1);
        Assert.Equal(comConsulta.PacienteId, Pedidos(comConsulta.Carteira).Single().PacienteId);
    }

    [Fact]
    public void CPF_sem_ficha_diz_o_que_fazer_e_nao_poe_nada_na_fila()
    {
        var (dia, _, _) = Semear();
        var antes = TodosOsPedidos();
        using var ctx = Contexto();
        var form = ctx.RenderComponent<ElegibilidadeDaAgendaForm>(p => p.Add(x => x.Dia, dia));
        Esperar(() => form.FindAll("[data-aba-avulso]").Count == 1 && form.FindAll(".esqueleto").Count == 0);
        Agir(form, f => f.Find("[data-aba-avulso]"), e => e.ClickAsync(new MouseEventArgs()));
        Esperar(() => form.FindAll("#ea-Tipo").Count == 1);
        Agir(form, f => f.Find("#ea-Tipo"), e => e.ChangeAsync(new ChangeEventArgs { Value = "cpf" }));
        // Um CPF válido e novo (fictício, derivado de uma marca aleatória): nenhuma ficha o tem.
        var cpf = CpfFicticio(Guid.NewGuid().ToString("N")[..8]);
        Agir(form, f => f.Find("#ea-Numero"), e => e.InputAsync(new ChangeEventArgs { Value = cpf }));
        Esperar(() => !form.Find("[data-buscar-cpf]").HasAttribute("disabled"));
        Agir(form, f => f.Find("[data-buscar-cpf]"), e => e.ClickAsync(new MouseEventArgs()));

        Esperar(() => form.Find(".modal").TextContent.Contains(ElegibilidadeNaAgendaService.CpfNaoEncontrado));
        Assert.Empty(form.FindAll("[data-consulta-avulsa]"));
        Assert.Equal(antes, TodosOsPedidos());
    }

    [Fact]
    public void Dia_sem_ninguem_na_agenda_diz_isso_e_aponta_o_avulso()
    {
        using var ctx = Contexto();
        var form = ctx.RenderComponent<ElegibilidadeDaAgendaForm>(p => p.Add(x => x.Dia, new DateOnly(2099, 1, 2)));
        Esperar(() => form.Markup.Contains("Ninguém na agenda deste dia"));
        Assert.Contains("aba Avulso", form.Markup);
    }

    /// <summary>
    /// O botão mora na aba "Dia" da Agenda (a tela que os apps MOSTRAM) e abre o painel com o dia
    /// que ela mostra. Esteve na Grade de horários (#361), que o menu dos apps esconde.
    /// </summary>
    [Fact]
    public void A_aba_Dia_mostra_o_botao_e_abre_o_painel_do_dia_mostrado()
    {
        using var ctx = Contexto();
        var tela = ctx.RenderComponent<Clinica.Ui.Blazor.Telas.AgendaDoDia>();
        Esperar(() => tela.FindAll("[data-consultar-elegibilidade-agenda]").Count == 1);
        Assert.False(tela.Find("[data-consultar-elegibilidade-agenda]").HasAttribute("disabled"));

        Agir(tela, t => t.Find("[data-consultar-elegibilidade-agenda]"), e => e.ClickAsync(new MouseEventArgs()));
        Esperar(() => tela.FindAll("[data-aba-agenda]").Count == 1);
        Assert.Contains(DateTime.Today.ToString("dd/MM"), tela.Find("[data-aba-agenda]").TextContent);
    }

    /// <summary>Sem nenhum dos bits da elegibilidade, o botão nasce desabilitado e diz por quê.</summary>
    [Fact]
    public void Sem_permissao_o_botao_da_aba_Dia_nasce_desabilitado()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema { Id = 9, Nome = "Balcão sem consulta", Perfil = PerfilAcesso.Recepcao,
            PermissoesNegadas = EnvioTissNaTela.PermissoesDaElegibilidade });
        using var ctx = Contexto();
        var tela = ctx.RenderComponent<Clinica.Ui.Blazor.Telas.AgendaDoDia>();
        Esperar(() => tela.FindAll("[data-consultar-elegibilidade-agenda]").Count == 1);
        var botao = tela.Find("[data-consultar-elegibilidade-agenda]");
        Assert.True(botao.HasAttribute("disabled"));
        Assert.Contains("não permite", botao.GetAttribute("title"));
    }

    /// <summary>
    /// UMA PORTA POR PERGUNTA, e a porta tem de estar no menu: os .razor que têm o botão são
    /// EXATAMENTE os declarados em TelasComOBotao — a lista que a verificação Windows do CI procura
    /// nas abas visíveis do Recepção e do Gerente (tools/Clinica.Verificacao.Desktop). Botão posto
    /// numa tela sem declarar reprova aqui; tela declarada fora do menu reprova lá.
    /// </summary>
    [Fact]
    public void As_telas_com_o_botao_sao_as_declaradas_para_a_conferencia_do_menu()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "Clinica.sln"))) raiz = raiz.Parent;
        Assert.NotNull(raiz);
        var comBotao = Directory.GetFiles(Path.Combine(raiz!.FullName, "src", "Clinica.Ui.Blazor"), "*.razor", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("data-consultar-elegibilidade-agenda"))
            .Select(f => Path.GetFileNameWithoutExtension(f)!).OrderBy(n => n).ToList();
        var declaradas = ElegibilidadeDaAgendaForm.TelasComOBotao.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(declaradas, comBotao);
        Assert.DoesNotContain("GradeDaAgenda", comBotao);
    }

    // ==================== peças ====================

    private sealed record PacienteSemeado(int AgendamentoId, int PacienteId, string Carteira, string Cpf);

    /// <summary>
    /// Um dia longe de tudo, com dois pacientes FICTÍCIOS: um da Saúde Petrobras (consultável: a
    /// semente das telas grava a credencial) e um da Unimed (sem consulta automática).
    /// </summary>
    private (DateOnly Dia, PacienteSemeado ComConsulta, PacienteSemeado SemConsulta) Semear()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var dia = new DateOnly(2098, 1, 1).AddDays(Math.Abs(marca.GetHashCode()) % 3000);
        var cpf = CpfFicticio(marca);
        using var db = Db();
        var petro = new Paciente
        {
            Nome = $"Elegível Agenda {marca}", Convenio = Convenio.Petrobras, ConvenioCodigo = "Petrobras",
            Carteirinha = "PB" + marca.ToUpperInvariant(), Documento = cpf
        };
        var unimed = new Paciente { Nome = $"Unimed Agenda {marca}", Convenio = Convenio.UnimedPadrao, ConvenioCodigo = "UnimedPadrao", Carteirinha = "UN" + marca };
        db.AddRange(petro, unimed);
        db.SaveChanges();
        var a1 = new Agendamento { PacienteId = petro.Id, DataHora = dia.ToDateTime(new TimeOnly(9, 0)), ModalidadePrevista = ModalidadeAtendimento.Consulta, CriadoPor = "teste" };
        var a2 = new Agendamento { PacienteId = unimed.Id, DataHora = dia.ToDateTime(new TimeOnly(10, 0)), ModalidadePrevista = ModalidadeAtendimento.Consulta, CriadoPor = "teste" };
        db.AddRange(a1, a2);
        db.SaveChanges();
        return (dia, new(a1.Id, petro.Id, petro.Carteirinha, cpf), new(a2.Id, unimed.Id, unimed.Carteirinha, ""));
    }

    /// <summary>Um CPF válido derivado da marca do teste (fictício, só para o dígito conferir).</summary>
    private static string CpfFicticio(string marca)
    {
        var baseNove = string.Concat(marca.Select(c => (char)('0' + c % 10))).PadRight(9, '7')[..9];
        if (baseNove.Distinct().Count() == 1) baseNove = "12345678" + baseNove[0];
        int Dv(string n, int peso) { var s = n.Select((c, i) => (c - '0') * (peso - i)).Sum() % 11; return s < 2 ? 0 : 11 - s; }
        var d1 = Dv(baseNove, 10);
        var d2 = Dv(baseNove + d1, 11);
        return baseNove + d1 + d2;
    }

    private List<TransacaoTiss> Pedidos(string carteira)
    {
        using var db = Db();
        return db.TransacoesTiss.AsNoTracking().Where(t => t.Tipo == TipoTransacaoTiss.Elegibilidade && t.NumeroCarteira == carteira).ToList();
    }

    private int TodosOsPedidos()
    {
        using var db = Db();
        return db.TransacoesTiss.Count();
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
        // SEM recarregar o catálogo de convênios: ele é estático, e recarregá-lo daqui mudaria as
        // regras da Petrobras para os outros testes do mesmo processo.
        return ctx;
    }

    /// <summary>
    /// Age pela versão ASSÍNCRONA do bUnit e repete SÓ quando o id do evento morreu numa
    /// re-renderização (o painel re-renderiza sozinho ao ler a agenda).
    /// </summary>
    private static void Agir(IRenderedFragment form, Func<IRenderedFragment, IElement> achar, Func<IElement, Task> gesto)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (true)
        {
            try { gesto(achar(form)).GetAwaiter().GetResult(); return; }
            catch (UnknownEventHandlerIdException) when (DateTime.UtcNow < prazo) { Thread.Sleep(25); }
            catch (ElementNotFoundException) when (DateTime.UtcNow < prazo) { Thread.Sleep(25); }
        }
    }

    private static void Esperar(Func<bool> condicao,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condicao))] string? qual = null)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            catch (ElementNotFoundException) { }
            Thread.Sleep(25);
        }
        throw new TimeoutException($"A condição não aconteceu a tempo: {qual}");
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
