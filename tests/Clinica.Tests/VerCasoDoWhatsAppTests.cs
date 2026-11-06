using Bunit;
using Clinica.Application;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O "Ver caso" da conversa do WhatsApp (out/2026): o Acompanhamento abre JÁ na conversa do caso
/// daquela pessoa, na fila do tipo dele — e não na fila inteira para a recepção procurar de novo.
/// A mesma <see cref="Raiz"/> e as mesmas sementes de <see cref="TelasBlazorAbremTests"/>.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class VerCasoDoWhatsAppTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;
    private readonly TestContext _ctx = new();

    public VerCasoDoWhatsAppTests(BancoDasTelas banco)
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
        _ctx.Services.AddTelasBlazor();
        _ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _ctx.Dispose();
    }

    private PedidoDoAcompanhamento Pedido => _ctx.Services.GetRequiredService<PedidoDoAcompanhamento>();

    private IRenderedComponent<Raiz> AbrirAcompanhamento()
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.AcompanhamentoDePacientes")!;
        return _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
    }

    [Fact]
    public async Task O_caso_pedido_abre_a_conversa_dele_na_fila_do_tipo_dele()
    {
        // O Novo BSV de propósito: a fila que a tela abre por padrão é a do recall, então abrir o
        // caso prova também a troca de fila.
        (int Id, string Nome) caso;
        using (var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options))
            caso = await db.Acompanhamentos.AsNoTracking()
                .Where(a => a.Tipo == TipoAcompanhamento.NovoBsv)
                .OrderBy(a => a.Id)
                .Select(a => new ValueTuple<int, string>(a.Id, a.Paciente!.Nome))
                .FirstAsync();

        Pedido.PedirCaso(caso.Id);
        var tela = AbrirAcompanhamento();

        tela.WaitForAssertion(() => tela.Find(".modal").GetAttribute("aria-label").Should().Be(caso.Nome), Prazo);
        Pedido.ConsumirCaso().Should().BeNull("ler o pedido o limpa: voltar à aba não reabre o caso");
    }

    [Fact]
    public void Caso_que_saiu_da_fila_nao_abre_nada()
    {
        Pedido.PedirCaso(int.MaxValue);
        var tela = AbrirAcompanhamento();

        tela.WaitForAssertion(() => tela.Markup.Should().Contain("acomp-linha"), Prazo);
        tela.FindAll(".modal").Should().BeEmpty();
        Pedido.ConsumirCaso().Should().BeNull();
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
