using Bunit;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O cadastro do paciente, a foto e a escolha do convênio "a definir" (Blazor, out/2026 — eram
/// janelas WPF do shell) clicados de verdade pela MESMA camada que a Raiz desenha: o fluxo
/// pede a <see cref="FormulariosDoPaciente"/>, espera, e a resposta só chega quando o BANCO
/// mudou — o vínculo gravado, a ficha criada, a edição salva.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class FormulariosDoPacienteTests : IDisposable
{
    private const string CpfValido = "111.444.777-35";
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);

    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly TestContext _ctx = new();

    public FormulariosDoPacienteTests()
    {
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;

        _conexao.Open();
        using (var db = Db())
        {
            db.Database.EnsureCreated();
            var gerente = new UsuarioSistema
            {
                Nome = "Direção (teste)", Login = "direcao", Perfil = PerfilAcesso.Gerente,
                SenhaHash = "-", SenhaSalt = "-", Ativo = true
            };
            db.Add(gerente);
            db.SaveChanges();
            new SessaoUsuario().Entrar(gerente);
        }

        // O cadastro lê o catálogo em memória (é o que as telas têm à mão, sem ida ao banco).
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio("UnimedPadrao", "Unimed", Convenio.UnimedPadrao, true),
            new EntradaConvenio(ConvenioCadastro.CodigoParticular, "Particular", Convenio.Personalizado, true, GeraGuia: false),
        ]);

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in _ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            _ctx.Services.Remove(d);
        _ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conexao));
        _ctx.Services.AddTelasBlazor();
        _ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());
    }

    public void Dispose()
    {
        CatalogoConvenios.Atualizar([]);
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _ctx.Dispose();
        _conexao.Dispose();
    }

    private FormulariosDoPaciente Formularios => _ctx.Services.GetRequiredService<FormulariosDoPaciente>();

    // ==================== escolha do convênio ====================

    [Fact]
    public async Task Ficha_que_ja_tem_convenio_nao_abre_nada()
    {
        var id = await PacienteAsync("Maria Unimed", "UnimedPadrao");
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();

        (await Formularios.EscolherConvenioAsync(id)).Should().BeTrue();
        camada.Markup.Trim().Should().BeEmpty("a pergunta só existe para a ficha 'a definir'");
    }

    [Fact]
    public async Task Sem_camada_montada_a_pergunta_responde_que_nao_abriu_em_vez_de_travar()
    {
        var id = await PacienteAsync("João Importado", ConvenioCadastro.CodigoADefinir);

        Formularios.Disponivel.Should().BeFalse();
        (await Formularios.EscolherConvenioAsync(id)).Should().BeFalse();
        (await Formularios.CadastrarAsync()).Should().BeNull();
        (await Formularios.EditarCadastroAsync(id)).Should().BeFalse();
    }

    [Fact]
    public async Task Escolher_o_convenio_grava_o_vinculo_e_so_entao_libera_o_fluxo()
    {
        var id = await PacienteAsync("João Importado", ConvenioCadastro.CodigoADefinir);
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();

        var pergunta = Formularios.EscolherConvenioAsync(id);
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Este paciente ainda não tem convênio"), Prazo);
        camada.Markup.Should().NotContain("A definir", "o 'a definir' é a pergunta, nunca uma resposta");
        Botao(camada, "Vincular convênio e continuar").HasAttribute("disabled").Should().BeTrue("nada vem pré-selecionado");

        camada.FindAll("button.esc-conv-linha").First(b => b.TextContent.Contains("Amil")).Click();
        camada.Find("input.campo").Change("CART-123");
        Botao(camada, "Vincular convênio e continuar").Click();

        (await pergunta.WaitAsync(Prazo)).Should().BeTrue();
        camada.WaitForAssertion(() => camada.Markup.Trim().Should().BeEmpty(), Prazo);
        using var db = Db();
        var p = await db.Pacientes.SingleAsync(x => x.Id == id);
        p.ConvenioCodigo.Should().Be("Amil");
        p.Carteirinha.Should().Be("CART-123");
    }

    [Fact]
    public async Task Cancelar_a_escolha_e_desistir_e_nada_e_gravado()
    {
        var id = await PacienteAsync("João Importado", ConvenioCadastro.CodigoADefinir);
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();

        var pergunta = Formularios.EscolherConvenioAsync(id);
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Vincular convênio e continuar"), Prazo);
        Botao(camada, "Cancelar").Click();

        (await pergunta.WaitAsync(Prazo)).Should().BeFalse();
        using var db = Db();
        (await db.Pacientes.SingleAsync(x => x.Id == id)).ConvenioCodigo.Should().Be(ConvenioCadastro.CodigoADefinir);
    }

    // ==================== cadastro ====================

    [Fact]
    public async Task Cadastro_novo_grava_e_devolve_quem_nasceu()
    {
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();
        var cadastro = Formularios.CadastrarAsync();
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Novo paciente"), Prazo);

        camada.Find("input.campo").Input("  Ana Nova  ");
        camada.Find("input[aria-label='CPF obrigatório']").Input(CpfValido);
        SelectDoConvenio(camada).Change(ConvenioCadastro.CodigoParticular);
        Botao(camada, "Salvar cadastro").Click();

        var novo = await cadastro.WaitAsync(Prazo);
        novo.Should().NotBeNull();
        novo!.Nome.Should().Be("Ana Nova");
        using var db = Db();
        var p = await db.Pacientes.SingleAsync(x => x.Id == novo.Id);
        p.Nome.Should().Be("Ana Nova");
        p.ConvenioCodigo.Should().Be(ConvenioCadastro.CodigoParticular);
    }

    [Fact]
    public async Task Cadastro_sem_convenio_e_com_CPF_invalido_fica_aberto_dizendo_o_que_falta()
    {
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();
        var cadastro = Formularios.CadastrarAsync();
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Novo paciente"), Prazo);

        camada.Find("input.campo").Input("Ana Nova");
        Botao(camada, "Salvar cadastro").Click();
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Escolha o convênio"), Prazo);

        SelectDoConvenio(camada).Change("UnimedPadrao");
        camada.Find("input[aria-label='CPF obrigatório']").Input("123");
        Botao(camada, "Salvar cadastro").Click();
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("CPF inválido"), Prazo);

        cadastro.IsCompleted.Should().BeFalse("o formulário fica aberto, com os dados preenchidos");
        using var db = Db();
        (await db.Pacientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Edicao_carrega_a_ficha_e_salva_por_cima()
    {
        var id = await PacienteAsync("Maria Unimed", "UnimedPadrao", CpfValido);
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();

        var edicao = Formularios.EditarCadastroAsync(id);
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Editar paciente").And.Contain("Salvar cadastro"), Prazo);
        camada.WaitForAssertion(() => camada.Find("input.campo").GetAttribute("value").Should().Be("Maria Unimed"), Prazo);

        camada.FindAll("input.campo").First(i => i.GetAttribute("maxlength") == "20").Change("(21) 98888-7777");
        Botao(camada, "Salvar cadastro").Click();

        (await edicao.WaitAsync(Prazo)).Should().BeTrue();
        using var db = Db();
        (await db.Pacientes.SingleAsync(x => x.Id == id)).Telefone.Should().Be("(21) 98888-7777");
    }

    [Fact]
    public void Tirar_foto_sem_camera_diz_que_a_saida_e_o_arquivo()
    {
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();
        _ = Formularios.CadastrarAsync();
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Tirar foto"), Prazo);

        Botao(camada, "Tirar foto").Click();

        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Escolher arquivo…"), Prazo);
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Use “Escolher arquivo…”"), Prazo);
        Botao(camada, "Capturar").HasAttribute("disabled").Should().BeTrue("sem câmera ligada não há quadro para congelar");
    }

    [Fact]
    public async Task Trocar_de_tela_desiste_do_formulario_aberto()
    {
        var camada = _ctx.RenderComponent<CamadaDoPaciente>();
        var cadastro = Formularios.CadastrarAsync();
        camada.WaitForAssertion(() => camada.Markup.Should().Contain("Novo paciente"), Prazo);

        Formularios.DesistirDeTodos();

        (await cadastro.WaitAsync(Prazo)).Should().BeNull();
        camada.WaitForAssertion(() => camada.Markup.Trim().Should().BeEmpty(), Prazo);
    }

    // ==================== apoio ====================

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);

    private async Task<int> PacienteAsync(string nome, string codigo, string? cpf = null)
    {
        using var db = Db();
        var p = new Paciente
        {
            Nome = nome, ConvenioCodigo = codigo, Documento = cpf, Sexo = Sexo.Feminino,
            Convenio = codigo == "UnimedPadrao" ? Convenio.UnimedPadrao : Convenio.Personalizado
        };
        db.Pacientes.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static AngleSharp.Dom.IElement Botao<T>(IRenderedComponent<T> c, string rotulo) where T : Microsoft.AspNetCore.Components.IComponent
        => c.FindAll("button").First(b => b.TextContent.Trim() == rotulo);

    private static AngleSharp.Dom.IElement SelectDoConvenio<T>(IRenderedComponent<T> c) where T : Microsoft.AspNetCore.Components.IComponent
        => c.FindAll("select").First(s => s.QuerySelectorAll("option").Any(o => o.TextContent.Contains("escolha")));

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
