using Bunit;
using Clinica.Application;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
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
/// A mídia na tela nativa do WhatsApp (24/10/2026, pedido da direção: "ver imagens, ouvir áudios e
/// baixar itens enviados dentro da tela"), de ponta a ponta na MESMA <see cref="Raiz"/> e com o
/// MESMO CRM de mentira da prévia: a foto e a nota de voz do Roberto aparecem sozinhas como
/// <c>data:</c>, o PDF enviado baixa com o nome e os bytes certos, a foto amplia, o vídeo que o
/// provedor não entrega mostra a falha com "Tentar de novo", e o áudio gravado sobe como gravação e
/// sai como resposta com o anexo. O navegador (o MediaRecorder) é simulado pelo bUnit.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class MidiaDoWhatsAppNaTelaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly TestContext _ctx = new();
    private readonly HostQueGuarda _host = new();

    public MidiaDoWhatsAppNaTelaTests(BancoDasTelas banco)
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
        _ctx.Services.AddSingleton<IHostDaTela>(_host);
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _ctx.Dispose();
    }

    private CrmDeMentira Crm => (CrmDeMentira)_ctx.Services.GetRequiredService<IClienteDoCrm>();

    /// <summary>Abre a tela, vai a "Minhas" e abre a conversa de quem tem o nome.</summary>
    private IRenderedComponent<Raiz> AbrirConversa(string nome)
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.ConversasDoWhatsApp")!;
        var tela = _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
        tela.WaitForAssertion(() => tela.FindAll(".wa-filtros button").Should().Contain(b => b.TextContent.Contains("Minhas")), Prazo);
        tela.FindAll(".wa-filtros button").First(b => b.TextContent.Contains("Minhas")).Click();
        tela.WaitForAssertion(() => tela.FindAll(".wa-linha").Should().Contain(l => l.TextContent.Contains(nome)), Prazo);
        tela.FindAll(".wa-linha").First(l => l.TextContent.Contains(nome)).Click();
        tela.WaitForAssertion(() => tela.FindAll(".wa-m").Should().NotBeEmpty(), Prazo);
        return tela;
    }

    [Fact]
    public void A_foto_e_a_nota_de_voz_aparecem_sozinhas_e_o_PDF_enviado_baixa_inteiro()
    {
        var tela = AbrirConversa("Roberto");

        tela.WaitForAssertion(() =>
        {
            tela.Find(".wa-foto img").GetAttribute("src").Should().StartWith("data:image/png;base64,");
            tela.Find(".wa-audio audio").GetAttribute("src").Should().StartWith("data:audio/wav;base64,");
        }, Prazo);
        tela.Find(".wa-midia-audio").TextContent.Should().Contain("Áudio recebido · 0:06");
        tela.Markup.Should().NotContain("Mensagem sem texto");

        var doc = tela.FindAll(".wa-doc").Single(d => d.TextContent.Contains("orientacoes-bsv.pdf"));
        doc.TextContent.Should().Contain("PDF enviado");
        doc.QuerySelectorAll("button").First(b => b.TextContent.Contains("Baixar")).Click();

        tela.WaitForAssertion(() => _host.Gravados.Should().ContainSingle(), Prazo);
        _host.Gravados[0].Caminho.Should().Be("/teste/orientacoes-bsv.pdf");
        _host.Gravados[0].Bytes.Should().Equal(MidiaDeExemplo.Orientacoes.Conteudo);
        _host.Pedidos.Should().ContainSingle().Which.Should().Be(("orientacoes-bsv.pdf", "Arquivo PDF (*.pdf)|*.pdf", ".pdf"));
    }

    [Fact]
    public void O_clique_na_foto_abre_a_foto_grande_com_Baixar()
    {
        var tela = AbrirConversa("Roberto");
        tela.WaitForAssertion(() => tela.Find(".wa-foto img"), Prazo);

        tela.Find(".wa-foto").Click();

        tela.WaitForAssertion(() => tela.Find(".modal .wa-ampliada img").GetAttribute("src").Should().StartWith("data:image/png"), Prazo);
        tela.Find(".modal").TextContent.Should().Contain("Esse é o pedido do médico");
        tela.FindAll(".modal-rodape button").Single(b => b.TextContent.Contains("Baixar")).Click();
        tela.WaitForAssertion(() => _host.Gravados.Should().ContainSingle(), Prazo);
        _host.Gravados[0].Caminho.Should().MatchRegex(@"^/teste/foto-\d{4}-\d{2}-\d{2}-\d{4}\.png$");
    }

    [Fact]
    public void O_documento_recebido_abre_e_o_video_que_nao_veio_mostra_a_falha_com_tentar_de_novo()
    {
        var tela = AbrirConversa("João Batista");

        var pedido = tela.FindAll(".wa-doc").Single(d => d.TextContent.Contains("Pedido medico"));
        pedido.QuerySelectorAll("button").First(b => b.TextContent.Contains("Abrir")).Click();
        tela.WaitForAssertion(() => _host.Abertos.Should().ContainSingle().Which.Should().Be("Pedido medico - Joao.pdf"), Prazo);

        tela.FindAll(".wa-video-capa").Single().Click();
        tela.WaitForAssertion(() => tela.Find(".wa-midia-falha").TextContent.Should()
            .Contain("Não foi possível carregar o vídeo: Este anexo não possui um endereço de mídia compatível"), Prazo);
        tela.Find(".wa-midia-falha button").TextContent.Should().Contain("Tentar de novo");
    }

    [Fact]
    public async Task O_audio_gravado_sobe_como_gravacao_e_sai_como_resposta_com_o_anexo()
    {
        byte[] gravado = [0x1A, 0x45, 0xDF, 0xA3, 1, 2, 3];
        var js = _ctx.JSInterop.SetupModule("./js/gravacao-de-audio.js");
        js.Setup<GravadorDeAudio.ResultadoDoInicio?>("iniciar", _ => true).SetResult(new("gravando", "audio/webm", null));
        js.Setup<GravadorDeAudio.SituacaoDaGravacao?>("situacao", _ => true).SetResult(new(3.2, true, false, false));
        js.Setup<Microsoft.JSInterop.IJSStreamReference?>("parar", _ => true).SetResult(new FluxoDoNavegador(gravado));
        js.SetupVoid("cancelar", _ => true).SetVoidResult();
        var tela = AbrirConversa("Roberto");

        tela.FindAll(".wa-escrever-rodape button").Single(b => b.GetAttribute("aria-label") == "Gravar áudio").Click();
        tela.WaitForAssertion(() => tela.Find(".wa-gravando .wa-gravando-tempo").TextContent.Should().Be("0:03"), Prazo);
        tela.FindAll(".wa-gravando button").Single(b => b.TextContent.Contains("Enviar áudio")).Click();

        tela.WaitForAssertion(() => tela.FindAll(".wa-gravando").Should().BeEmpty(), Prazo);
        // Na hora, e não na releitura de 7 s: o gravador chama a tela por Func, que não a redesenha sozinha.
        tela.WaitForAssertion(() => tela.FindAll(".wa-m.enviada .wa-audio audio").Should().ContainSingle()
            .Which.GetAttribute("src").Should().StartWith("data:audio/webm;base64,"), TimeSpan.FromSeconds(3));
        var conversa = await Crm.AbrirConversaAsync(0, CrmDeMentira.Ids.Roberto);
        var audio = conversa.Messages.Last();
        audio.Direction.Should().Be("out");
        audio.Type.Should().Be("AUDIO");
        audio.FileName.Should().Be("gravacao.webm");
        Crm.BytesDoAnexo(audio.AttachmentId!).Should().Equal(gravado);
        conversa.Messages.Count(m => m.AttachmentId == audio.AttachmentId).Should().Be(1, "um clique é um envio");
    }

    [Fact]
    public void Sem_microfone_a_tela_diz_o_que_fazer_e_nada_quebra()
    {
        var js = _ctx.JSInterop.SetupModule("./js/gravacao-de-audio.js");
        js.Setup<GravadorDeAudio.ResultadoDoInicio?>("iniciar", _ => true).SetResult(new("negado", null, "Permission denied"));
        var tela = AbrirConversa("Roberto");

        tela.FindAll(".wa-escrever-rodape button").Single(b => b.GetAttribute("aria-label") == "Gravar áudio").Click();

        tela.WaitForAssertion(() => tela.Find(".wa-recado").TextContent.Should().Contain("O microfone não foi liberado"), Prazo);
        tela.FindAll(".wa-gravando").Should().BeEmpty();
    }

    /// <summary>Os bytes que o navegador devolve em fluxo (o <c>parar()</c> do js da gravação).</summary>
    private sealed class FluxoDoNavegador(byte[] bytes) : Microsoft.JSInterop.IJSStreamReference
    {
        public long Length => bytes.Length;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Stream>(new MemoryStream(bytes));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Guarda o que a tela pediu ao disco (onde salvar, o que gravou, o que abriu).</summary>
    private sealed class HostQueGuarda : IHostDaTela
    {
        public List<(string Nome, string Filtro, string Extensao)> Pedidos { get; } = [];
        public List<(string Caminho, byte[] Bytes)> Gravados { get; } = [];
        public List<string> Abertos { get; } = [];

        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao)
        {
            Abertos.Add(nomeSugerido);
            return Task.FromResult<string?>(null);
        }
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e)
        {
            Pedidos.Add((n, f, e));
            return Task.FromResult<string?>("/teste/" + n);
        }
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b)
        {
            Gravados.Add((c, b));
            return Task.FromResult<string?>(null);
        }
    }
}
