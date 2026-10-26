using Bunit;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
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
/// A tela da auditoria do atendimento (26/10), na MESMA <see cref="Raiz"/> e com o MESMO CRM de mentira da prévia:
/// abre com os cartões por secretária e a fila (o que espera a gerente primeiro), o detalhe nasce da sugestão do
/// @SemDor, concluir sem o resultado fica INLINE, o rascunho grava e entra no histórico, e os estados do 403 e do
/// CRM sem endereço.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class AuditoriaDoAtendimentoNaTelaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly TestContext _ctx = new();

    public AuditoriaDoAtendimentoNaTelaTests(BancoDasTelas banco)
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

    private IRenderedComponent<Raiz> Renderizar()
    {
        var tipo = typeof(IHostDaTela).Assembly.GetType("Clinica.Ui.Blazor.Telas.AuditoriaDoAtendimento")!;
        return _ctx.RenderComponent<Raiz>(p => p.Add(r => r.Tela, tipo));
    }

    private IRenderedComponent<Raiz> Abrir()
    {
        var tela = Renderizar();
        tela.WaitForAssertion(() => tela.FindAll(".aa-fila tbody tr").Should().NotBeEmpty(), Prazo);
        return tela;
    }

    private static void AbrirOPrimeiroParaConferir(IRenderedComponent<Raiz> tela)
    {
        tela.Clicar(() => tela.FindAll(".aa-fila tbody tr button").First(b => b.TextContent.Trim() == "Conferir"));
        tela.WaitForAssertion(() => tela.FindAll(".aa-criterio").Should().HaveCount(5), Prazo);
    }

    [Fact]
    public void Abre_com_a_faixa_os_cartoes_e_a_fila_com_o_que_espera_a_gerente_primeiro()
    {
        var tela = Abrir();

        tela.Find(".aa-faixa").TextContent.Should().Contain(AuditoriaNaTela.FaixaDaNota).And.Contain(AuditoriaNaTela.FaixaDaTransparencia);
        tela.FindAll(".aa-sec").Should().NotBeEmpty();
        tela.Find(".aa-sec").TextContent.Should().Contain("no prazo").And.Contain("Sem retorno do paciente").And.Contain("Transferências");

        var situacoes = tela.FindAll(".aa-fila tbody tr").Select(tr => tr.TextContent.Contains("Para conferir") ? 0 : 1).ToList();
        situacoes.Should().BeInAscendingOrder("as que esperam a gerente vêm antes das avaliadas");
    }

    [Fact]
    public void O_detalhe_nasce_da_sugestao_do_SemDor_e_concluir_sem_resultado_fica_inline()
    {
        var tela = Abrir();
        AbrirOPrimeiroParaConferir(tela);

        tela.Find(".aa-conversa").TextContent.Should().NotBeEmpty();
        tela.FindAll(".aa-m.da-avaliada").Should().NotBeEmpty("as mensagens da secretária avaliada ficam destacadas");
        tela.Markup.Should().Contain("Sugestão do @SemDor");

        tela.Clicar(() => tela.FindAll(".aa-botoes button").First(b => b.TextContent.Contains("Confirmar avaliação")));
        tela.WaitForAssertion(() => tela.Find(".aa-fecho .aviso").TextContent.Should().Contain("resultado"), Prazo);
    }

    [Fact]
    public void Guardar_rascunho_grava_no_CRM_e_entra_no_historico()
    {
        var tela = Abrir();
        AbrirOPrimeiroParaConferir(tela);
        var antes = tela.FindAll(".aa-historico li").Count;

        tela.Clicar(() => tela.FindAll(".aa-botoes button").First(b => b.TextContent.Contains("Guardar rascunho")));

        tela.WaitForAssertion(() => tela.FindAll(".aa-historico li").Count.Should().Be(antes + 1), Prazo);
        tela.FindAll(".aa-fecho .aviso.erro").Should().BeEmpty();
    }

    [Fact]
    public void Quem_nao_e_da_gestao_no_CRM_ve_o_estado_sem_permissao()
    {
        Crm.DaGestao = false;
        var tela = Renderizar();
        tela.WaitForAssertion(() => tela.Markup.Should().Contain(AuditoriaNaTela.TituloSemPermissao), Prazo);
        tela.Markup.Should().Contain(CrmDeMentira.SoGestao);
        tela.FindAll(".aa-fila").Should().BeEmpty();
    }

    [Fact]
    public void Sem_o_endereco_do_CRM_diz_que_ainda_nao_foi_ligado()
    {
        Crm.Modo = CrmDeMentira.ModoDoCrm.NaoConfigurado;
        var tela = Renderizar();
        tela.WaitForAssertion(() => tela.Markup.Should().Contain("ainda não foi ligado aqui"), Prazo);
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
