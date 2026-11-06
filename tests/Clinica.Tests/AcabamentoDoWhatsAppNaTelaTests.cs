using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Abstracoes;
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
/// O acabamento profissional da tela do WhatsApp (25/10/2026, docs/whatsapp-nativo-implementacao.md §15), na
/// MESMA <see cref="Raiz"/> e com o MESMO CRM de mentira da prévia: o teclado do WhatsApp Web (Enter envia,
/// Shift+Enter não, Esc fecha a busca e os popovers), o cabeçalho que diz a ação, o Transferir sem ninguém
/// marcado, os botões do modelo sob o balão, a velocidade do áudio, o vazio por modo e a ligação que cai.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class AcabamentoDoWhatsAppNaTelaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly TestContext _ctx = new();

    public AcabamentoDoWhatsAppNaTelaTests(BancoDasTelas banco)
    {
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

    private IRenderedComponent<Raiz> Abrir()
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.ConversasDoWhatsApp")!;
        var tela = _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        tela.WaitForAssertion(() => tela.FindAll(".wa-linha").Should().NotBeEmpty(), Prazo);
        return tela;
    }

    private static void AbrirConversa(IRenderedComponent<Raiz> tela, string nome)
    {
        tela.Clicar(() => tela.FindAll(".wa-filtros .segmentado button").First(b => b.TextContent.Trim().StartsWith("Todas", StringComparison.Ordinal)));
        tela.Clicar(() => tela.FindAll(".wa-linha").First(e => e.TextContent.Contains(nome, StringComparison.Ordinal)));
        tela.WaitForAssertion(() => tela.Find(".wa-cab-quem h2").TextContent.Should().Contain(nome), Prazo);
    }

    private static void Tecla(IRenderedComponent<Raiz> tela, Func<IElement> achar, string tecla, bool shift = false)
        => tela.InvokeAsync(() => achar().KeyDown(new KeyboardEventArgs { Key = tecla, ShiftKey = shift })).GetAwaiter().GetResult();

    private static string Campo(IRenderedComponent<Raiz> tela) => tela.Find(".wa-escrever textarea").GetAttribute("value") ?? "";

    [Fact]
    public async Task Enter_envia_e_Shift_Enter_nao_como_no_WhatsApp_Web()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Maria das Graças");
        var antes = (await Crm.AbrirConversaAsync(1, CrmDeMentira.Ids.Maria)).Messages.Length;
        tela.Find(".wa-escrever textarea").GetAttribute("placeholder").Should().Contain("Enter envia, Shift+Enter quebra a linha");

        // Shift+Enter é a quebra de linha: nada sai e o texto fica.
        tela.Digitar(() => tela.Find(".wa-escrever textarea"), "Combinado, Maria!");
        Tecla(tela, () => tela.Find(".wa-escrever textarea"), "Enter", shift: true);
        Campo(tela).Should().Be("Combinado, Maria!");
        (await Crm.AbrirConversaAsync(1, CrmDeMentira.Ids.Maria)).Messages.Length.Should().Be(antes);

        // Enter envia: o balão aparece e o campo esvazia.
        Tecla(tela, () => tela.Find(".wa-escrever textarea"), "Enter");
        tela.WaitForAssertion(() => tela.FindAll(".wa-m.enviada").Should().Contain(m => m.TextContent.Contains("Combinado, Maria!")), Prazo);
        tela.WaitForAssertion(() => Campo(tela).Should().BeEmpty(), Prazo);
        (await Crm.AbrirConversaAsync(1, CrmDeMentira.Ids.Maria)).Messages.Length.Should().Be(antes + 1);
    }

    [Fact]
    public void Esc_fecha_a_busca_e_os_popovers()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Maria das Graças");

        tela.Clicar(() => tela.Find("[data-acao=buscar]"));
        tela.WaitForAssertion(() => tela.Find(".wa-busca-conversa input"), Prazo);
        Tecla(tela, () => tela.Find(".wa-busca-conversa input"), "Escape");
        tela.FindAll(".wa-busca-conversa").Should().BeEmpty();

        tela.Clicar(() => tela.FindAll(".wa-escrever-rodape button").First(b => b.TextContent.Contains("Respostas rápidas")));
        tela.WaitForAssertion(() => tela.Find(".wa-popover"), Prazo);
        Tecla(tela, () => tela.Find(".wa-popover"), "Escape");
        tela.FindAll(".wa-popover").Should().BeEmpty();

        // Com o popover aberto, o Esc no próprio campo também fecha.
        tela.Clicar(() => tela.FindAll(".wa-escrever-rodape button").First(b => b.TextContent.Contains("Respostas rápidas")));
        tela.WaitForAssertion(() => tela.Find(".wa-popover"), Prazo);
        Tecla(tela, () => tela.Find(".wa-escrever textarea"), "Escape");
        tela.FindAll(".wa-popover").Should().BeEmpty();
    }

    [Fact]
    public void O_cabecalho_diz_a_acao_e_o_Transferir_nao_vem_com_ninguem_marcado()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Maria das Graças");

        tela.Find("[data-acao=devolver]").TextContent.Should().Contain("Devolver à fila");
        tela.Find(".wa-cab-acoes").TextContent.Should().NotContain("Com você");
        tela.Find("[data-acao=encerrar]").TextContent.Should().Contain("Encerrar");

        tela.Clicar(() => tela.Find("[data-acao=transferir]"));
        tela.WaitForAssertion(() => tela.Find(".modal"), Prazo);
        tela.FindAll(".modal input[type=radio]").Should().NotBeEmpty().And.OnlyContain(r => !r.HasAttribute("checked"));
        // O botão principal: "Transferir" para o setor, "Enviar o pedido" para uma pessoa (§14) — o rótulo muda com a escolha.
        var transferir = () => tela.FindAll(".modal-rodape button.primario").Single();
        transferir().HasAttribute("disabled").Should().BeTrue("um Enter não pode passar a conversa a quem não foi escolhido");
        tela.InvokeAsync(() => tela.FindAll(".modal input[type=radio]")[0].Change(true)).GetAwaiter().GetResult();
        tela.WaitForAssertion(() => transferir().HasAttribute("disabled").Should().BeFalse(), Prazo);
        transferir().TextContent.Should().Contain("Enviar o pedido", "a primeira escolha é uma pessoa: vai o pedido, não a conversa");
    }

    [Fact]
    public void Os_botoes_do_modelo_aparecem_sob_o_balao_do_lembrete()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Carlos Eduardo");
        // O lembrete das 06:00 saiu pelo modelo com Confirmar, Remarcar e Cancelar: a recepção vê o que a pessoa recebeu.
        tela.WaitForAssertion(() => tela.Find(".wa-m-botoes").TextContent.Should().Contain("Confirmar").And.Contain("Remarcar").And.Contain("Cancelar"), Prazo);
    }

    [Fact]
    public void O_audio_tem_a_velocidade_a_vista_e_o_rodape_nao_diz_KB()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Roberto Lima");
        tela.WaitForAssertion(() => tela.Find(".wa-velocidade").TextContent.Trim().Should().Be("1×"), Prazo);
        tela.Find(".wa-midia-audio .wa-midia-linha small").TextContent.Should().NotContain("KB");
        tela.Clicar(() => tela.Find(".wa-velocidade"));
        tela.WaitForAssertion(() => tela.Find(".wa-velocidade").TextContent.Trim().Should().Be("1,5×"), Prazo);
        tela.Clicar(() => tela.Find(".wa-velocidade"));
        tela.WaitForAssertion(() => tela.Find(".wa-velocidade").TextContent.Trim().Should().Be("2×"), Prazo);
    }

    [Fact]
    public void Sem_conversa_aberta_a_ficha_nao_ocupa_lugar_e_o_vazio_e_um_por_modo()
    {
        var tela = Abrir();
        tela.Find(".pagina.wa").ClassList.Should().Contain("sem-conversa");
        tela.Find(".wa-vazia").TextContent.Should().Contain("Escolha uma conversa").And.NotContain("segundos");

        tela.Clicar(() => tela.FindAll(".wa-modo button").First(b => b.TextContent.Contains("Retenção")));
        tela.WaitForAssertion(() => tela.Find(".wa-vazia").TextContent.Should().Contain("Escolha um paciente para preparar o convite"), Prazo);
        // O rodapé é do estado da ligação também na Retenção; a explicação vai para o ⓘ, e o Esc a fecha.
        tela.Find(".wa-lista-rodape").TextContent.Should().Contain("Conectado");
        tela.Clicar(() => tela.Find(".wa-quem-aparece"));
        tela.Find(".wa-quem-aparece-texto").TextContent.Should().Contain("não há disparo em massa");
        Tecla(tela, () => tela.Find(".wa-quem-aparece"), "Escape");
        tela.FindAll(".wa-quem-aparece-texto").Should().BeEmpty();

        tela.Clicar(() => tela.FindAll(".wa-modo button").First(b => b.TextContent.Contains("Conversas")));
        AbrirConversa(tela, "Maria das Graças");
        tela.Find(".pagina.wa").ClassList.Should().NotContain("sem-conversa");
    }

    [Fact]
    public void A_ligacao_que_cai_no_meio_do_uso_vira_a_faixa_amarela_e_a_frase_sem_CRM()
    {
        var tela = Abrir();
        AbrirConversa(tela, "Maria das Graças");
        tela.FindAll(".wa-sem-ligacao").Should().BeEmpty();

        Crm.Modo = CrmDeMentira.ModoDoCrm.ForaDoAr;
        tela.Digitar(() => tela.Find(".wa-escrever textarea"), "Pode ser terça às 14h");
        Tecla(tela, () => tela.Find(".wa-escrever textarea"), "Enter");

        tela.WaitForAssertion(() => tela.Find(".wa-sem-ligacao").TextContent.Should().Contain("Sem ligação com o WhatsApp desde")
            .And.Contain("O que você enviar agora pode não sair"), Prazo);
        tela.Find(".wa-recado").TextContent.Should().Contain("Sem ligação com o WhatsApp da clínica agora. Nada foi alterado").And.NotContain("CRM");
        tela.Find(".wa-lista-rodape").TextContent.Should().Contain("Sem ligação desde");
        Campo(tela).Should().Be("Pode ser terça às 14h", "o que ela escreveu não se perde");
    }

    private sealed class HostMudo : IHostDaTela
    {
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
