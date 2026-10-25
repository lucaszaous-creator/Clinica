using AngleSharp.Dom;
using Bunit;
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
/// A CONVERSA RESOLVE, NA TELA (25/10 — docs/whatsapp-nativo-implementacao.md §16), de ponta a ponta na MESMA
/// <see cref="Raiz"/> e com o MESMO CRM de mentira da prévia: a foto do Roberto vai aos arquivos da ficha dele
/// (e a nota fica na conversa), a mensagem da Lúcia vai ao profissional pelo chat da equipe, "Passar o turno"
/// devolve todas as conversas da Ana à fila, o "Marcar horário" abre o formulário da agenda com a ficha
/// travada, e cancelar deixa a mensagem do que a clínica fez no campo — nada sai sem o Enviar.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class ConversaResolveNaTelaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    /// <summary>
    /// O que o clique faz aparece NA HORA, não na releitura de 7 s: os formulários chamam a tela por <c>Func</c>, que
    /// não a redesenha sozinha (lição de 24/10) — com o prazo comum, a releitura escondia o formulário que não fechava.
    /// </summary>
    private static readonly TimeSpan NaHora = TimeSpan.FromSeconds(3);
    private readonly TestContext _ctx = new();
    private readonly BancoDasTelas _banco;

    public ConversaResolveNaTelaTests(BancoDasTelas banco)
    {
        _banco = banco;
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
        CrmDeMentira.Registrar(_ctx.Services);
        _ctx.Services.AddTelasBlazor();
        _ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _ctx.Dispose();
    }

    private CrmDeMentira Crm => (CrmDeMentira)_ctx.Services.GetRequiredService<IClienteDoCrm>();

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options);

    private IRenderedComponent<Raiz> AbrirTela()
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.ConversasDoWhatsApp")!;
        var tela = _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        tela.WaitForAssertion(() => tela.FindAll(".wa-filtros button").Should().Contain(b => b.TextContent.Contains("Minhas")), Prazo);
        return tela;
    }

    private static void Filtro(IRenderedComponent<Raiz> tela, string nome)
        => tela.Clicar(() => tela.FindAll(".wa-filtros button").First(b => b.TextContent.Contains(nome)));

    private static void Abrir(IRenderedComponent<Raiz> tela, string nome)
    {
        tela.WaitForAssertion(() => tela.FindAll(".wa-linha").Should().Contain(l => l.TextContent.Contains(nome)), Prazo);
        tela.Clicar(() => tela.FindAll(".wa-linha").First(l => l.TextContent.Contains(nome)));
        tela.WaitForAssertion(() => tela.Find(".wa-cab h2").TextContent.Should().Contain(nome), Prazo);
    }

    private static void Mudar(IRenderedFragment tela, Func<IElement> achar, object valor)
        => tela.InvokeAsync(() => achar().Change(valor)).GetAwaiter().GetResult();

    [Fact]
    public async Task A_foto_recebida_vai_aos_arquivos_da_ficha_e_a_nota_fica_na_conversa()
    {
        var tela = AbrirTela();
        Filtro(tela, "Minhas");
        Abrir(tela, "Roberto");
        tela.WaitForAssertion(() => tela.Find(".wa-foto img"), Prazo);

        tela.Clicar(() => tela.Find(".wa-midia-guardar button"));
        tela.WaitForAssertion(() => tela.Find(".modal").TextContent.Should().Contain("Guardar na ficha"), Prazo);
        // Sem o título e a data, o botão não grava.
        tela.FindAll(".modal-rodape button").Last().HasAttribute("disabled").Should().BeTrue();
        Mudar(tela, () => tela.FindAll(".wa-guardar-titulos input").Single(i => i.ParentElement!.TextContent.Contains("Pedido médico")), "on");
        var dia = DateTime.Today.AddDays(-2);
        Mudar(tela, () => tela.Find(".modal input[type=date]"), dia.ToString("yyyy-MM-dd"));
        tela.Clicar(() => tela.FindAll(".modal-rodape button").Last());

        tela.WaitForAssertion(() => tela.FindAll(".modal").Should().BeEmpty(), NaHora);
        using (var db = Db())
        {
            var anexo = await db.AnexosPaciente.AsNoTracking().Include(a => a.Paciente).SingleAsync(a => a.Titulo == "Pedido médico");
            anexo.Paciente!.Nome.Should().Be(PessoasDoWhatsApp.Roberto.Nome);
            anexo.Data.Should().Be(DateOnly.FromDateTime(dia));
            anexo.CriadoPor.Should().Be(_banco.Gerente.Login);
            (await db.ArquivosAnexoPaciente.AsNoTracking().SingleAsync(a => a.AnexoPacienteId == anexo.Id)).Conteudo.Should().Equal(MidiaDeExemplo.Foto.Conteudo);
        }
        var conversa = await Crm.AbrirConversaAsync(0, CrmDeMentira.Ids.Roberto);
        conversa.Notes.Should().Contain(n => n.Text.StartsWith("Guardado na ficha por ") && n.Text.Contains("Pedido médico"));
        conversa.Messages.Should().NotContain(m => m.Text.Contains("Guardado"), "a nota é interna: nada vai ao paciente");
    }

    [Fact]
    public async Task A_mensagem_do_paciente_vai_ao_profissional_pelo_chat_da_equipe()
    {
        var tela = AbrirTela();
        Filtro(tela, "Todas");
        Abrir(tela, "Lúcia");
        tela.WaitForAssertion(() => tela.FindAll(".wa-m.recebida .wa-m-acoes button").Should().NotBeEmpty(), Prazo);

        tela.Clicar(() => tela.FindAll(".wa-m.recebida").Last(m => m.TextContent.Contains("braço")).QuerySelector(".wa-m-acoes button")!);
        tela.WaitForAssertion(() => tela.Find(".modal").TextContent.Should().Contain("Perguntar ao profissional"), Prazo);
        tela.Find(".modal .wa-citacao").TextContent.Should().Contain("o braço ficou inchado");
        tela.Find(".modal").TextContent.Should().Contain("Não entra no prontuário");
        // A Lúcia não tem ficha conferida: vai o nome do WhatsApp, sem paciente anexado. Escolhe o primeiro profissional.
        Mudar(tela, () => tela.FindAll(".wa-perguntar-quem input").First(), "on");
        tela.Clicar(() => tela.FindAll(".modal-rodape button").Last());

        tela.WaitForAssertion(() => tela.FindAll(".modal").Should().BeEmpty(), NaHora);
        using var db = Db();
        var msg = await db.MensagensEquipe.AsNoTracking().OrderBy(m => m.Id).LastAsync();
        msg.AutorId.Should().Be(_banco.Gerente.Id);
        msg.Conversa.Should().StartWith("direta:");
        msg.PacienteId.Should().BeNull();
        msg.Texto.Should().Contain("ficha ainda não conferida").And.Contain("> Bom dia. Depois da acupuntura");
    }

    [Fact]
    public async Task Passar_o_turno_devolve_todas_as_conversas_da_pessoa_a_fila_com_a_nota()
    {
        var tela = AbrirTela();
        Filtro(tela, "Minhas");
        tela.WaitForAssertion(() => tela.Find(".wa-turno").TextContent.Should().Contain("4 conversas com você"), Prazo);

        tela.Clicar(() => tela.Find(".wa-turno button"));
        tela.WaitForAssertion(() => tela.FindAll(".modal .wa-turno-lista li").Should().HaveCount(4), Prazo);
        tela.FindAll(".modal-rodape button").Last().HasAttribute("disabled").Should().BeTrue("a nota é obrigatória");
        tela.Digitar(() => tela.Find(".modal textarea"), "Fim do turno da manhã; a Maria espera o horário de terça.");
        tela.Clicar(() => tela.FindAll(".modal-rodape button").Last());

        tela.WaitForAssertion(() => tela.FindAll(".modal").Should().BeEmpty(), NaHora);
        var lista = await Crm.ListarConversasAsync(0);
        lista.Should().NotContain(c => !c.Closed && c.AgentId == CrmDeMentira.Eu);
        var maria = await Crm.AbrirConversaAsync(0, CrmDeMentira.Ids.Maria);
        maria.Notes.Should().Contain(n => n.Text.Contains("Fim do turno da manhã"));
        tela.WaitForAssertion(() => tela.FindAll(".wa-turno").Should().BeEmpty(), NaHora);
    }

    [Fact]
    public void Marcar_horario_abre_o_formulario_da_agenda_com_a_ficha_travada()
    {
        var tela = AbrirTela();
        Filtro(tela, "Minhas");
        Abrir(tela, "Roberto");
        tela.WaitForAssertion(() => tela.FindAll(".wa-marcar button").Should().ContainSingle(), Prazo);

        tela.Clicar(() => tela.Find(".wa-marcar button"));

        tela.WaitForAssertion(() => tela.Find(".modal").TextContent.Should().Contain($"Marcar horário para {PessoasDoWhatsApp.Roberto.Nome}"), Prazo);
        tela.WaitForAssertion(() => tela.Find(".agf-paciente-fixo").TextContent.Should().Contain(PessoasDoWhatsApp.Roberto.Nome), Prazo);
    }

    [Fact]
    public void Cancelar_pela_conversa_deixa_a_mensagem_do_que_a_clinica_fez_no_campo_sem_enviar()
    {
        var tela = AbrirTela();
        Filtro(tela, "Minhas");
        Abrir(tela, "Maria das Graças");
        tela.WaitForAssertion(() => tela.FindAll(".wa-horario").Should().NotBeEmpty(), Prazo);
        var enviadasAntes = Crm.AbrirConversaAsync(0, CrmDeMentira.Ids.Maria).GetAwaiter().GetResult().Messages.Length;

        tela.Clicar(() => tela.FindAll(".wa-horario button").First(b => b.TextContent.Trim() == "Cancelar"));
        tela.WaitForAssertion(() => tela.Find(".modal select"), Prazo);
        Mudar(tela, () => tela.Find(".modal select"), "Profissional ausente");
        tela.Clicar(() => tela.FindAll(".modal-rodape button").Last());

        tela.WaitForAssertion(() => tela.Find(".wa-escrever textarea").GetAttribute("value").Should().StartWith("Maria, cancelei o seu horário de"), Prazo);
        Crm.AbrirConversaAsync(0, CrmDeMentira.Ids.Maria).GetAwaiter().GetResult().Messages.Length.Should().Be(enviadasAntes, "o rascunho espera o Enviar");
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
