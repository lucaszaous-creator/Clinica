using Bunit;
using Clinica.Application;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreviaBlazor;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A CONVERSA AO LADO (out/2026, fase 2 do WhatsApp nativo): o botão "WhatsApp" da Ficha do balcão abre
/// a conversa DAQUELE paciente num painel à direita, sem trocar de tela; o Esc fecha; o paciente sem
/// conversa ganha o "Começar com um modelo" (a mesma Nova conversa da aba, já preenchida); duas conversas
/// abrem a mais recente com o seletor; e onde o painel não serve (app sem a aba WhatsApp) o botão some. A mesma <see cref="Raiz"/>, as mesmas sementes e o mesmo CRM de mentira de
/// <see cref="TelasBlazorAbremTests"/>.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class ConversaAoLadoTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;
    private readonly TestContext _ctx = new();
    private readonly HostDoPainel _host = new();

    public ConversaAoLadoTests(BancoDasTelas banco)
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
        _ctx.Services.AddSingleton<IHostDaTela>(_host);
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _ctx.Dispose();
    }

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options);

    private async Task<(int Id, string Nome, string Telefone)> PacienteAsync(string nome)
    {
        using var db = Db();
        var p = await db.Pacientes.AsNoTracking().FirstAsync(x => x.Nome == nome);
        return (p.Id, p.Nome, p.Telefone!);
    }

    /// <summary>A Ficha do balcão aberta no paciente (o pedido que a agenda e a lista de Pacientes deixam).</summary>
    private IRenderedComponent<Raiz> AbrirFicha(int pacienteId, string nome)
    {
        _ctx.Services.GetRequiredService<PedidoDaFichaDoBalcao>().Definir(pacienteId, nome);
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.FichaDoBalcao")!;
        var tela = _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        tela.WaitForAssertion(() => tela.Find(".fb-acoes .wa-lado-abrir"), Prazo);
        return tela;
    }

    [Fact]
    public async Task O_WhatsApp_da_ficha_abre_a_conversa_do_paciente_ao_lado_e_o_Esc_fecha()
    {
        var maria = await PacienteAsync(PessoasDoWhatsApp.Maria.Nome);
        var tela = AbrirFicha(maria.Id, maria.Nome);

        tela.Find(".fb-acoes .wa-lado-abrir").Click();

        tela.WaitForAssertion(() => tela.Find(".wa-lado .wa-cab h2").TextContent.Should().Be(maria.Nome), Prazo);
        tela.Find(".wa-lado .pagina.wa").ClassList.Should().Contain("wa-ao-lado");
        tela.Find(".wa-lado .wa-lista").Should().NotBeNull("a lista existe no HTML, escondida pelo CSS do modo painel");
        tela.Find(".tp-nome").TextContent.Should().Be(maria.Nome, "a ficha continua à vista: não trocou de tela");
        tela.FindAll(".wa-lado-faixa").Should().BeEmpty("uma conversa, ligada à ficha: nada a conferir nem a escolher");

        tela.Find(".wa-lado").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        tela.WaitForAssertion(() => tela.FindAll(".wa-lado").Should().BeEmpty(), Prazo);
        tela.Find(".tp-nome").TextContent.Should().Be(maria.Nome);
    }

    [Fact]
    public async Task O_rascunho_sobrevive_a_fechar_a_gaveta_e_reabrir()
    {
        // O rascunho é da janela (RascunhosDoWhatsApp), não da gaveta: fechar não apaga o que ela escrevia.
        var maria = await PacienteAsync(PessoasDoWhatsApp.Maria.Nome);
        var tela = AbrirFicha(maria.Id, maria.Nome);

        tela.Find(".fb-acoes .wa-lado-abrir").Click();
        tela.WaitForAssertion(() => tela.Find(".wa-lado .wa-escrever textarea"), Prazo);
        tela.Find(".wa-lado .wa-escrever textarea").Input("Maria, confirmo terça às 14h");
        tela.Find(".wa-lado").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        tela.WaitForAssertion(() => tela.FindAll(".wa-lado").Should().BeEmpty(), Prazo);

        tela.Find(".fb-acoes .wa-lado-abrir").Click();
        tela.WaitForAssertion(() => tela.Find(".wa-lado .wa-escrever textarea").GetAttribute("value")
            .Should().Be("Maria, confirmo terça às 14h"), Prazo);
    }

    [Fact]
    public async Task Paciente_sem_conversa_oferece_comecar_por_modelo_com_o_telefone_ja_preenchido()
    {
        int id;
        using (var db = Db())
        {
            var p = new Paciente { Nome = "Paciente Sem Conversa Teste", Telefone = "(21) 98777-1234" };
            db.Pacientes.Add(p);
            await db.SaveChangesAsync();
            id = p.Id;
        }
        var tela = AbrirFicha(id, "Paciente Sem Conversa Teste");

        tela.Find(".fb-acoes .wa-lado-abrir").Click();

        tela.WaitForAssertion(() => tela.Find(".wa-lado").TextContent.Should().Contain("Ainda não há conversa com Paciente"), Prazo);
        tela.FindAll(".wa-lado button").Single(b => b.TextContent.Contains(ConversaAoLadoNaTela.Comecar)).Click();

        tela.WaitForAssertion(() => tela.Find(".wa-lado .modal").GetAttribute("aria-label").Should().Be("Nova conversa"), Prazo);
        tela.WaitForAssertion(() => tela.Find(".wa-lado .modal input[inputmode='tel']").GetAttribute("value").Should().Be("(21) 98777-1234"), Prazo);
        tela.Find(".wa-lado .modal input[maxlength='80']").GetAttribute("value").Should().Be("Paciente Sem Conversa Teste");
    }

    [Fact]
    public async Task Duas_conversas_da_ficha_abrem_a_mais_recente_e_o_seletor_troca()
    {
        var maria = await PacienteAsync(PessoasDoWhatsApp.Maria.Nome);
        // Uma segunda conversa da Maria, por SMS, criada agora (a mais recente) e ligada à ficha.
        var crm = (CrmDeMentira)_ctx.Services.GetRequiredService<IClienteDoCrm>();
        var sms = await crm.NovaConversaAsync(1, new AppNewConversation(CrmDeMentira.RemetenteSms, maria.Telefone, maria.Nome));
        await crm.LigarFichaAsync(1, sms.Id, new AppPatientLink(maria.Id, maria.Nome));
        var tela = AbrirFicha(maria.Id, maria.Nome);

        tela.Find(".fb-acoes .wa-lado-abrir").Click();

        tela.WaitForAssertion(() => tela.Find(".wa-lado .wa-lado-seletor select").GetAttribute("value").Should().Be(sms.Id), Prazo);
        tela.FindAll(".wa-lado .wa-lado-seletor option").Should().HaveCount(2);
        tela.Find(".wa-lado .wa-cab-meta").TextContent.Should().Contain("SMS");

        tela.Find(".wa-lado .wa-lado-seletor select").Change(CrmDeMentira.Ids.Maria);

        tela.WaitForAssertion(() => tela.Find(".wa-lado .wa-cab-meta").TextContent.Should().Contain("WhatsApp"), Prazo);
        tela.Find(".wa-lado .wa-lado-seletor select").GetAttribute("value").Should().Be(CrmDeMentira.Ids.Maria);
    }

    [Fact]
    public async Task O_botao_some_no_app_sem_a_aba_WhatsApp()
    {
        var maria = await PacienteAsync(PessoasDoWhatsApp.Maria.Nome);
        _ctx.Services.GetRequiredService<PedidoDaFichaDoBalcao>().Definir(maria.Id, maria.Nome);
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.FichaDoBalcao")!;

        _host.TemAba = false;
        var semAba = _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        semAba.WaitForAssertion(() => semAba.Find(".fb-acoes"), Prazo);
        semAba.FindAll(".wa-lado-abrir").Should().BeEmpty("o Faturamento e o Consultório não têm a aba WhatsApp");
        _host.TemAba = true;

    }

    [Fact]
    public async Task Sem_o_bit_da_aba_o_botao_nasce_apagado()
    {
        var maria = await PacienteAsync(PessoasDoWhatsApp.Maria.Nome);
        new SessaoUsuario().Entrar(new UsuarioSistema
        {
            Id = 9999, Nome = "Sem agenda (teste)", Login = "sem-agenda", Perfil = PerfilAcesso.Recepcao,
            PermissoesNegadas = Permissao.EditarAgenda, SenhaHash = "-", SenhaSalt = "-", Ativo = true
        });

        var tela = AbrirFicha(maria.Id, maria.Nome);

        tela.Find(".fb-acoes .wa-lado-abrir").HasAttribute("disabled").Should().BeTrue();
    }

    /// <summary>O hospedeiro do teste: a aba WhatsApp existe (como na Recepção), a não ser que o teste a tire.</summary>
    private sealed class HostDoPainel : IHostDaTela
    {
        public bool TemAba { get; set; } = true;
        public bool TemTela(string chave) => TemAba && chave == ConversaNoCrm.ChaveDaAba;
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
