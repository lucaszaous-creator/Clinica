using Bunit;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes.SecoesDoPaciente;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A seção "Atendimento de enfermagem" (out/2026, Blazor) montada como a tela do paciente a monta —
/// com o paciente e a tela em CASCATA — sobre os serviços do app e a semente da enfermagem, e
/// clicada nos botões principais. Afirma o BANCO quando o botão grava, e a NAVEGAÇÃO quando o botão
/// só leva a outra seção: elo partido aqui não estoura, vira um clique que não faz nada.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class EnfermagemBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public EnfermagemBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        // A direção com uma ficha de profissional COM conselho: registro de enfermagem sem COREN é
        // recusado pelo serviço (parcela 72), e o teste quer a gravação, não a recusa.
        new SessaoUsuario().Entrar(new UsuarioSistema
        {
            Id = banco.Gerente.Id, Nome = "Joana Enfermeira (teste)", Login = banco.Gerente.Login,
            Perfil = PerfilAcesso.Gerente, Ativo = true, SenhaHash = "-", SenhaSalt = "-",
            Profissional = new Profissional { Nome = "Joana Enfermeira (teste)", RegistroConselho = "COREN-RJ 123456" }
        });
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    [Fact]
    public void A_secao_navega_avisa_a_ponte_imprime_e_grava_a_passagem()
    {
        var (pacienteId, nome, agendamentoId) = Marta();
        var tela = new TelaGravada();
        using var ctx = Contexto();
        var corte = ctx.RenderComponent<CascadingValue<ContextoDoPaciente>>(p => p
            .Add(x => x.Value, new ContextoDoPaciente(pacienteId, nome, agendamentoId, null, DateOnly.FromDateTime(DateTime.Today), 1))
            .AddChildContent<CascadingValue<ITelaDoPaciente>>(q => q
                .Add(x => x.Value, tela)
                .AddChildContent<SecaoAtendimentoDeEnfermagem>()));
        Assentar(corte);

        // Os atalhos da porta levam à seção — e nada mais.
        Botao(corte, "Consultar ficha").Click();
        Botao(corte, "Exames e anexos").Click();
        Assert.Equal([TelaDoPacienteNaTela.FichaDoPaciente, TelaDoPacienteNaTela.ExamesEAnexos], tela.Idas);

        // "Registrar infusão" abre o formulário Blazor NA PRÓPRIA PÁGINA (out/2026: a janela WPF
        // saiu) — pela camada de formulários do navegador, nunca uma janela por cima.
        Botao(corte, "Registrar infusão").Click();
        Assentar(corte);
        var janelas = ctx.Services.GetRequiredService<JanelasNaTela>();
        Assert.Contains(janelas.Abertas, j => j.Tipo == typeof(Clinica.Ui.Blazor.Formularios.Infusao.InfusaoExternaForm));
        janelas.DesistirDeTodos();
        Assentar(corte);

        // Imprimir com o compositor em branco: o papel sai (numerado, na lista do paciente).
        int documentosAntes;
        using (var db = Db()) documentosAntes = db.DocumentosClinicos.Count(d => d.PacienteId == pacienteId);
        Botao(corte, "Imprimir a passagem").Click();
        Esperar(() => { using var db = Db(); return db.DocumentosClinicos.Count(d => d.PacienteId == pacienteId) == documentosAntes + 1; });
        Assentar(corte);
        Assert.Contains("enf-sucesso", corte.Find("footer.enf-rodape p").ClassName);

        // Escrever e registrar: a passagem entra no prontuário, ligada a ESTA sessão e com o COREN.
        int antes;
        using (var db = Db()) antes = db.EvolucoesEnfermagem.Count(e => e.PacienteId == pacienteId);
        corte.Find("textarea.enf-texto").Change("Paciente tranquila, acesso pérvio, sem queixas na admissão.");
        Assentar(corte);
        Botao(corte, "Salvar sessão").Click();
        Esperar(() => { using var db = Db(); return db.EvolucoesEnfermagem.Count(e => e.PacienteId == pacienteId) == antes + 1; });

        using var depois = Db();
        var nova = depois.EvolucoesEnfermagem.Where(e => e.PacienteId == pacienteId).OrderByDescending(e => e.Id).First();
        Assert.Equal("Paciente tranquila, acesso pérvio, sem queixas na admissão.", nova.Texto);
        Assert.Equal(agendamentoId, nova.AgendamentoId);
        Assert.Equal("COREN-RJ 123456", nova.AutorConselho);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), nova.Data);
    }

    [Fact]
    public void Sem_VerProntuario_a_secao_diz_por_que_e_nao_le_nada()
    {
        var (pacienteId, nome, agendamentoId) = Marta();
        new SessaoUsuario().Entrar(new UsuarioSistema
        {
            Id = _banco.Gerente.Id, Nome = "Balcão (teste)", Login = "balcao", Perfil = PerfilAcesso.Recepcao,
            Ativo = true, SenhaHash = "-", SenhaSalt = "-"
        });
        using var ctx = Contexto();
        var corte = ctx.RenderComponent<CascadingValue<ContextoDoPaciente>>(p => p
            .Add(x => x.Value, new ContextoDoPaciente(pacienteId, nome, agendamentoId, null, null, 1))
            .AddChildContent<CascadingValue<ITelaDoPaciente>>(q => q
                .Add(x => x.Value, new TelaGravada())
                .AddChildContent<SecaoAtendimentoDeEnfermagem>()));
        Assentar(corte);

        Assert.Contains(EnfermagemNaTela.SemProntuario, corte.Markup);
        Assert.Empty(corte.FindAll("textarea.enf-texto"));
    }

    [Fact]
    public void A_passagem_so_e_branca_quando_nada_que_grava_foi_digitado()
    {
        // O "Finalizar" do cabeçalho pergunta por ela (IPassagemAberta): a PA sozinha NÃO é branco.
        var estado = new Clinica.Ui.Blazor.Componentes.SecoesDoPaciente.Enfermagem.EstadoDaPassagem(null!, 1, "Marta");
        Assert.True(estado.PassagemEmBranco);
        estado.PressaoArterial = "120/80";
        Assert.False(estado.PassagemEmBranco);
    }

    private (int Id, string Nome, int AgendamentoId) Marta()
    {
        using var db = Db();
        var marta = db.Pacientes.Single(p => p.Nome == "Marta Vasconcelos Rocha");
        var hoje = DateTime.Today;
        var sessao = db.Agendamentos.Single(a => a.PacienteId == marta.Id && a.DataHora >= hoje && a.DataHora < hoje.AddDays(1));
        return (marta.Id, marta.Nome, sessao.Id);
    }

    private static AngleSharp.Dom.IElement Botao(IRenderedFragment corte, string rotulo)
        => corte.FindAll("button").First(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled"));

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
        throw new TimeoutException("A seção não terminou de carregar.");
    }

    private static void Esperar(Func<bool> condicao)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            // A seção e o teste dividem a MESMA conexão em memória: "database is locked" é "ainda não".
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            Thread.Sleep(50);
        }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    /// <summary>A tela do paciente de mentira: grava para onde a seção pediu para ir.</summary>
    private sealed class TelaGravada : ITelaDoPaciente
    {
        public List<string> Idas { get; } = [];
        public Task RelerCabecalhoAsync() => Task.CompletedTask;
        public void IrPara(string secao, int subAba = 0) => Idas.Add(secao);
        private readonly Dictionary<string, object> _guardados = [];
        public T Guardado<T>(string chave, Func<T> criar) where T : class
        {
            if (_guardados.TryGetValue(chave, out var v)) return (T)v;
            var novo = criar();
            _guardados[chave] = novo;
            return novo;
        }
        public void AvisarMudanca() { }
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
