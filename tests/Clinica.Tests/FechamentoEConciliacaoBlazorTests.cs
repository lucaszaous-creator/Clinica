using Bunit;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os formulários "Concluir atendimento" e "Conferir atendimentos" (Blazor, out/2026) clicados
/// de verdade — bUnit sobre os serviços do app (<c>AddClinica</c>) e um SQLite semeado aqui —,
/// afirmando o BANCO e não a frase: o pacote debitou, o lançamento nasceu, o fechamento com
/// aviso NÃO fecha o formulário, e a falta marcada pela conciliação vira falta.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class FechamentoEConciliacaoBlazorTests : IDisposable
{
    private const string CodigoParticular = "Particular";
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);

    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly TestContext _ctx = new();
    private readonly int _profissionalId;

    /// <summary>Cinco dias atrás: passou da carência da conciliação e o "pago hoje" não é futuro.</summary>
    private static readonly DateTime Sessao = DateTime.Today.AddDays(-5).AddHours(10);

    public FechamentoEConciliacaoBlazorTests()
    {
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;

        _conexao.Open();
        using (var db = Db())
        {
            db.Database.EnsureCreated();
            var prof = new Profissional { Nome = "Dra. Ana Teste" };
            db.Profissionais.Add(prof);
            var gerente = new UsuarioSistema
            {
                Nome = "Direção (teste)", Login = "direcao", Perfil = PerfilAcesso.Gerente,
                SenhaHash = "-", SenhaSalt = "-", Ativo = true
            };
            db.Add(gerente);
            db.SaveChanges();
            _profissionalId = prof.Id;
            new SessaoUsuario().Entrar(gerente);
        }

        // O "Particular" é o cadastro que NÃO gera guia (parcela 60); o catálogo é um cache
        // estático, então cada teste o monta e o devolve vazio no fim (a suíte não roda em paralelo).
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio(CodigoParticular, "Particular", Convenio.Personalizado, true,
                new ConfiguracaoRegraGenerica { FazEletro = true, TemSegundoCodigo = true },
                FormatoNumeroGuia.SemValidacao, GeraGuia: false)
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

    // ==================== Concluir atendimento ====================

    [Fact]
    public async Task Sessao_com_pacote_conclui_e_debita_uma_sessao()
    {
        var pacienteId = await PacienteAsync("Maria Pacote", Convenio.UnimedIntercambio, null);
        int pacoteId;
        using (var escopo = _ctx.Services.CreateScope())
        {
            var pacote = await escopo.ServiceProvider.GetRequiredService<PacoteService>().RegistrarVendaAsync(new PacotePaciente
            {
                PacienteId = pacienteId, Nome = "Pacote 10 sessões", Tipo = TipoPacote.Sessoes,
                SessoesContratadas = 10, Valor = 1000m, DataCompra = DateOnly.FromDateTime(Sessao).AddDays(-10)
            });
            pacoteId = pacote.Id;
        }
        var agendamentoId = await AgendarAsync(pacienteId, Sessao);

        ResultadoFechamento? concluido = null;
        var fechou = false;
        var form = _ctx.RenderComponent<FechamentoSessaoForm>(p => p
            .Add(x => x.AgendamentoId, agendamentoId)
            .Add(x => x.AoConcluir, (ResultadoFechamento r) => concluido = r)
            .Add(x => x.AoFechar, () => fechou = true));

        form.WaitForAssertion(() => form.Markup.Should().Contain("Debitar 1 sessão do pacote"), Prazo);
        form.Markup.Should().Contain("debita 1 sessão do pacote", "o resumo diz o que o botão vai fazer");
        Botao(form, "Concluir sessão").Click();

        form.WaitForAssertion(() => concluido.Should().NotBeNull(), Prazo);
        concluido!.TudoCerto.Should().BeTrue();
        concluido.Consumo.Should().NotBeNull();
        fechou.Should().BeFalse("quem fecha o formulário inteiro é a tela de trás, pelo AoConcluir");

        using var leitura = Db();
        leitura.ConsumosPacote.Count(c => c.PacotePacienteId == pacoteId && c.CanceladoEm == null).Should().Be(1);
        leitura.Agendamentos.Single(a => a.Id == agendamentoId).Status.Should().Be(StatusAgendamento.Realizado);
    }

    [Fact]
    public async Task Particular_pago_agora_faz_nascer_o_lancamento_da_sessao()
    {
        var pacienteId = await PacienteAsync("João Particular", Convenio.Personalizado, CodigoParticular);
        var agendamentoId = await AgendarAsync(pacienteId, Sessao);

        ResultadoFechamento? concluido = null;
        var form = _ctx.RenderComponent<FechamentoSessaoForm>(p => p
            .Add(x => x.AgendamentoId, agendamentoId)
            .Add(x => x.AoConcluir, (ResultadoFechamento r) => concluido = r));

        // Particular sem histórico e sem tabela: a pergunta é feita SEMPRE, e o vazio se explica.
        form.WaitForAssertion(() => form.Markup.Should().Contain("Atendimento PARTICULAR"), Prazo);
        form.Markup.Should().Contain("informe o valor combinado");
        ((AngleSharp.Html.Dom.IHtmlInputElement)form.Find("input[type=checkbox]")).IsChecked.Should().BeTrue("o particular nasce com a cobrança marcada");

        form.Find("input.campo.valor").Input("150,00");
        form.Find("select").Change(nameof(FormaPagamento.Pix));
        Botao(form, "Concluir sessão").Click();

        form.WaitForAssertion(() => concluido.Should().NotBeNull(), Prazo);
        concluido!.TudoCerto.Should().BeTrue();

        using var leitura = Db();
        var atendimentoId = leitura.Agendamentos.Single(a => a.Id == agendamentoId).AtendimentoId;
        var lancamento = leitura.Lancamentos.Single(l => l.AtendimentoId == atendimentoId);
        lancamento.Valor.Should().Be(150m);
        lancamento.Tipo.Should().Be(TipoLancamento.Entrada);
        lancamento.Status.Should().Be(StatusLancamento.Realizado);
        lancamento.FormaPagamento.Should().Be(FormaPagamento.Pix);
        lancamento.PacienteId.Should().Be(pacienteId);
    }

    [Fact]
    public async Task Fechamento_com_aviso_NAO_fecha_o_formulario_e_sai_por_Fechar()
    {
        var pacienteId = await PacienteAsync("Ana Aviso", Convenio.UnimedIntercambio, null);
        var agendamentoId = await AgendarAsync(pacienteId, Sessao);

        ResultadoFechamento? concluido = null;
        var fechou = false;
        var form = _ctx.RenderComponent<FechamentoSessaoForm>(p => p
            .Add(x => x.AgendamentoId, agendamentoId)
            .Add(x => x.AoConcluir, (ResultadoFechamento r) => concluido = r)
            .Add(x => x.AoFechar, () => fechou = true));
        form.WaitForAssertion(() => form.Markup.Should().Contain("Registrar como esta sessão foi paga"), Prazo);

        // Convênio sem histórico: a cobrança nasce DESMARCADA (receita fantasma, não). Quem
        // está no balcão marca, diz que fica a receber e erra o vencimento: antes da sessão.
        form.Find("input[type=checkbox]").Change(true);
        form.Find("input.campo.valor").Input("80");
        Botao(form, "Fica a receber").Click();
        form.Find("input[type=date]").Change(DateOnly.FromDateTime(Sessao).AddDays(-10).ToString("yyyy-MM-dd"));
        Botao(form, "Concluir sessão").Click();

        form.WaitForAssertion(() => form.Markup.Should().Contain("O atendimento foi concluído e a guia existe, MAS:"), Prazo);
        form.Markup.Should().Contain("A conta a receber NÃO foi registrada");
        concluido.Should().BeNull("o fechamento com aviso não é o que a janela devolvia como concluído");
        fechou.Should().BeFalse("o formulário fica aberto dizendo o que falhou");
        form.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Concluir sessão"),
            "concluir de novo criaria uma segunda guia para a mesma sessão");

        using (var leitura = Db())
        {
            var ag = leitura.Agendamentos.Single(a => a.Id == agendamentoId);
            ag.Status.Should().Be(StatusAgendamento.Realizado, "o atendimento aconteceu e a guia existe");
            leitura.Lancamentos.Count(l => l.AtendimentoId == ag.AtendimentoId).Should().Be(0);
        }

        Botao(form, "Fechar").Click();
        fechou.Should().BeTrue();
        concluido.Should().BeNull();
    }

    // ==================== Conferir atendimentos ====================

    [Fact]
    public async Task Conciliacao_marca_a_falta_e_nao_fecha()
    {
        var pacienteId = await PacienteAsync("Severino Parado", Convenio.UnimedIntercambio, null);
        var agendamentoId = await AgendarAsync(pacienteId, Sessao);

        var alterou = 0;
        var fechou = false;
        var form = _ctx.RenderComponent<ConciliacaoAgendaForm>(p => p
            .Add(x => x.AoAlterou, () => alterou++)
            .Add(x => x.AoFechar, () => fechou = true));

        form.WaitForAssertion(() => form.Markup.Should().Contain("Severino Parado"), Prazo);
        form.Markup.Should().Contain("Nenhuma sessão lançada neste dia para este paciente.");
        Botao(form, "Aconteceu — lançar").HasAttribute("disabled").Should().BeFalse("sem sessão no dia, lançar é uma das respostas");
        Botao(form, "Já foi lançada — encerrar").HasAttribute("disabled").Should().BeTrue("sem sessão no dia, não há o que apontar");

        Botao(form, "Foi falta").Click();
        var dialogos = _ctx.Services.GetRequiredService<Dialogos>();
        form.WaitForAssertion(() => dialogos.Atual.Should().NotBeNull(), Prazo);
        dialogos.Atual!.Titulo.Should().Be("Marcar falta?");
        await form.InvokeAsync(() => dialogos.Responder(dialogos.Atual!, true));

        form.WaitForAssertion(() => form.Markup.Should().Contain("Falta registrada."), Prazo);
        alterou.Should().Be(1);
        fechou.Should().BeFalse("a recepcionista percorre a lista inteira de uma vez");
        Botao(form, "Foi falta").HasAttribute("disabled").Should().BeTrue("a linha resolvida não responde de novo");

        using var leitura = Db();
        leitura.Agendamentos.Single(a => a.Id == agendamentoId).Status.Should().Be(StatusAgendamento.Faltou);
    }

    // ==================== cenário ====================

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);

    private async Task<int> PacienteAsync(string nome, Convenio convenio, string? codigo)
    {
        using var db = Db();
        var p = new Paciente { Nome = nome, Convenio = convenio, ConvenioCodigo = codigo, Sexo = Sexo.Feminino };
        db.Pacientes.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> AgendarAsync(int pacienteId, DateTime quando)
    {
        using var escopo = _ctx.Services.CreateScope();
        var ag = await escopo.ServiceProvider.GetRequiredService<AgendaService>().AgendarAsync(
            pacienteId, quando, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissionalId);
        return ag.Id;
    }

    private static AngleSharp.Dom.IElement Botao<T>(IRenderedComponent<T> form, string rotulo) where T : Microsoft.AspNetCore.Components.IComponent
        => form.FindAll("button").First(b => b.TextContent.Trim() == rotulo);

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
