using Bunit;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Formularios.Prontuario;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os formulários Blazor que substituíram as janelas WPF da cobrança no balcão, do envio ao
/// paciente, do quick-view do prontuário e da escolha de paciente (out/2026), montados sobre os
/// serviços do app e as sementes da prévia, com a direção logada. Afirma o BANCO quando o botão
/// grava (o recebimento sai do alerta de dívida) e o DESFECHO quando ele só pergunta.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class FormulariosDoConsultorioBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public FormulariosDoConsultorioBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void A_cobranca_recebe_a_conta_vencida_e_avisa_quem_abriu_que_mudou()
    {
        int pacienteId, lancamentoId;
        using (var db = Db())
        {
            var paciente = new Paciente { Nome = "Devedora da Cobrança (teste)", Telefone = "(21) 99999-0001" };
            db.Pacientes.Add(paciente);
            db.SaveChanges();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var conta = new LancamentoFinanceiro
            {
                Data = hoje.AddDays(-40), Tipo = TipoLancamento.Entrada, Descricao = "Parcela 1/3 do pacote",
                Valor = 180m, Status = StatusLancamento.Previsto, DataVencimento = hoje.AddDays(-30), PacienteId = paciente.Id
            };
            db.Add(conta);
            db.SaveChanges();
            (pacienteId, lancamentoId) = (paciente.Id, conta.Id);
        }

        bool? mudou = null;
        using var ctx = Contexto();
        var corte = ctx.RenderComponent<CobrancaDoPacienteForm>(p => p
            .Add(x => x.PacienteId, pacienteId)
            .Add(x => x.Paciente, "Devedora da Cobrança (teste)")
            .Add(x => x.Telefone, "(21) 99999-0001")
            .Add(x => x.AoFechar, m => mudou = m));
        Assentar(corte);
        Assert.Contains("1 conta(s) vencida(s)", corte.Markup);
        Assert.Contains("vencida há 30 dias", corte.Markup);

        // A forma é escolhida na LINHA e viaja até o recebimento.
        corte.Find("table.lista select").Change(FormaPagamento.Dinheiro.ToString());
        Botao(corte, "Receber").Click();
        Assentar(corte);
        Assert.Contains("Receber pagamento do paciente", corte.Markup);
        Botao(corte, "Confirmar recebimento").Click();
        Esperar(() => { using var db = Db(); return db.Set<LancamentoFinanceiro>().Single(l => l.Id == lancamentoId).Status == StatusLancamento.Realizado; });
        Assentar(corte);

        using (var db = Db())
            Assert.Equal(FormaPagamento.Dinheiro, db.Set<LancamentoFinanceiro>().Single(l => l.Id == lancamentoId).FormaPagamento);
        Assert.Contains("Recebimento registrado", corte.Markup);
        Assert.Contains("Nenhuma conta vencida", corte.Markup);

        Botao(corte, "Fechar").Click();
        Assert.True(mudou);
    }

    [Fact]
    public void O_envio_oferece_so_o_WhatsApp_e_o_local_diz_que_e_do_aplicativo()
    {
        int documentoId;
        using (var db = Db())
            documentoId = db.DocumentosClinicos.AsEnumerable()
                .First(d => d.Tipo == TipoDocumentoClinico.Receita && !d.Cancelado && d.AssinadoEletronicamente).Id;

        ResultadoDoAto? desfecho = null;
        using var ctx = Contexto();
        var corte = ctx.RenderComponent<EnvioAoPacienteForm>(p => p
            .Add(x => x.DocumentoId, documentoId)
            .Add(x => x.AoConcluir, r => desfecho = r));
        Assentar(corte);

        var opcoes = corte.FindAll("label.con-envio-opcao").Select(l => l.TextContent).ToList();
        // Sem e-mail (out/2026, decisão da cliente): só os dois caminhos do WhatsApp.
        Assert.Equal(2, opcoes.Count);
        Assert.Contains("WhatsApp (pelo CRM)", opcoes[0]);
        Assert.Contains("deste computador", opcoes[1]);
        Assert.DoesNotContain(opcoes, o => o.Contains("E-mail"));

        // O WhatsApp deste computador é o único caminho que precisa do aplicativo: sem a ponte, a frase.
        var local = corte.FindAll("input[type=radio]")[1];
        if (!local.HasAttribute("disabled"))
        {
            local.Change(true);
            Botao(corte, "Enviar").Click();
            Esperar(() => desfecho is not null);
            Assert.True(desfecho!.EhErro);
            Assert.Equal(EntregaAoPacienteIndisponivel.Frase, desfecho.Frase);
        }
    }

    [Fact]
    public void O_quick_view_abre_a_sessao_no_lugar_do_resumo_e_volta_a_ele()
    {
        int pacienteId;
        string nome;
        using (var db = Db())
        {
            var ev = db.Evolucoes.AsNoTracking().Include(e => e.Paciente).AsEnumerable().First(e => e.CanceladaEm is null);
            (pacienteId, nome) = (ev.PacienteId, ev.Paciente!.Nome);
        }

        using var ctx = Contexto();
        var corte = ctx.RenderComponent<ResumoProntuarioForm>(p => p
            .Add(x => x.PacienteId, pacienteId)
            .Add(x => x.Paciente, nome));
        Assentar(corte);
        Assert.Contains("Diagnósticos recentes", corte.Markup);
        Assert.DoesNotContain("2ª via da anamnese", corte.Markup); // só quem veio de uma anamnese emitida

        Botao(corte, "Abrir sessão").Click();
        Assentar(corte);
        Assert.DoesNotContain("Diagnósticos recentes", corte.Markup); // um modal por vez, nunca empilhado
        Assert.Contains("Copiar registro", corte.Markup);

        corte.FindAll("button").First(b => b.TextContent.Trim() == "Fechar").Click();
        Assentar(corte);
        Assert.Contains("Diagnósticos recentes", corte.Markup);
    }

    [Fact]
    public void Escolher_paciente_so_acende_com_a_escolha_e_devolve_quem_foi_escolhido()
    {
        string nome;
        using (var db = Db()) nome = db.Pacientes.AsNoTracking().OrderBy(p => p.Id).First().Nome;

        Paciente? escolhido = null;
        using var ctx = Contexto();
        var corte = ctx.RenderComponent<EscolherPacienteForm>(p => p
            .Add(x => x.Assunto, "Novo pedido de exame — para quem?")
            .Add(x => x.AoEscolher, pac => escolhido = pac));
        Assert.True(corte.FindAll("button").Single(b => b.TextContent.Trim() == "Escolher").HasAttribute("disabled"));

        corte.Find("input.campo").Input(nome);
        Esperar(() => corte.FindAll("ul.seletor-resultados li").Count > 0);
        corte.FindAll("ul.seletor-resultados li").First(li => li.TextContent.Contains(nome)).Click();
        Botao(corte, "Escolher").Click();
        Assert.Equal(nome, escolhido?.Nome);
    }

    private static AngleSharp.Dom.IElement Botao(IRenderedFragment corte, string rotulo)
        => corte.FindAll("button").Last(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled"));

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
        return ctx;
    }

    private static void Assentar(IRenderedFragment tela)
    {
        var prazo = DateTime.UtcNow + Prazo;
        var anterior = string.Empty;
        var estavel = 0;
        while (DateTime.UtcNow < prazo)
        {
            Thread.Sleep(15);
            var agora = tela.Markup;
            estavel = agora == anterior ? estavel + 1 : 0;
            anterior = agora;
            if (estavel >= 4 && !agora.Contains("class=\"esqueleto\"")) return;
        }
        throw new TimeoutException("O formulário não terminou de carregar.");
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            Thread.Sleep(50);
        }
        throw new TimeoutException("A condição não aconteceu a tempo.");
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
