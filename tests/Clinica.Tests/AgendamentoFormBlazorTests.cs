using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using Bunit.Rendering;
using AngleSharp.Dom;
using Bunit;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Formularios;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os dois formulários da agenda migrados para Blazor (out/2026) — o horário
/// (<see cref="AgendamentoForm"/>) e a entrada na lista de espera (<see cref="ListaEsperaForm"/>) —
/// RENDERIZADOS (bUnit) sobre os serviços do app e o banco semeado das telas, e clicados de
/// verdade. A afirmação é sobre o BANCO, nunca sobre a frase: criar grava, editar só a
/// especialidade grava sem esbarrar no choque que já existia, choque é aviso e salva, sem
/// profissional recusa sem gravar, e a lista de espera ganha o pedido.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class AgendamentoFormBlazorTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(20);
    private readonly BancoDasTelas _banco;

    public AgendamentoFormBlazorTests(BancoDasTelas banco)
    {
        _banco = banco;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");
        System.Globalization.CultureInfo.CurrentCulture = ptBr;
        System.Globalization.CultureInfo.CurrentUICulture = ptBr;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    // ==================== o horário ====================

    [Fact]
    public void Criar_um_horario_com_profissional_grava_na_agenda()
    {
        var (prof, paciente, _) = Semear("Criar");
        var quando = DateTime.Today.AddDays(300).AddHours(10);
        using var ctx = Contexto();
        IReadOnlyList<string>? concluido = null;
        var form = ctx.RenderComponent<AgendamentoForm>(p => p
            .Add(x => x.Data, quando.Date).Add(x => x.Hora, "10:00")
            .Add(x => x.ProfissionalPreferidoId, prof.Id)
            .Add(x => x.AoConcluir, a => concluido = a));
        Esperar(() => form.FindAll("#agf-profissional").Count > 0);
        Assert.True(Contem(form, "Novo horário"));

        EscolherPaciente(form, paciente.Nome);
        Clicar(form, "Salvar horário");
        Esperar(() => concluido is not null);

        using var db = Db();
        var ag = db.Agendamentos.Single(a => a.PacienteId == paciente.Id);
        Assert.Equal(quando, ag.DataHora);
        Assert.Equal(prof.Id, ag.ProfissionalId);
        Assert.Equal(_banco.Gerente.Login, ag.CriadoPor);
    }

    [Fact]
    public void Editar_trocando_so_a_especialidade_grava_sem_esbarrar_no_choque_que_ja_existia()
    {
        var (prof, paciente, outro) = Semear("Editar");
        var quando = DateTime.Today.AddDays(301).AddHours(14);
        using var ctx = Contexto();
        int id;
        using (var escopo = ctx.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var agenda = escopo.ServiceProvider.GetRequiredService<AgendaService>();
            id = agenda.AgendarAsync(paciente.Id, quando, ModalidadeAtendimento.Consulta, "Importado · Consulta",
                modalidadeCodigo: nameof(ModalidadeAtendimento.Consulta),
                especialidadeConsultaCodigo: nameof(Especialidade.Psiquiatria),
                profissionalId: prof.Id).GetAwaiter().GetResult().Id;
            // O choque REAL: o mesmo profissional, o mesmo horário, outra pessoa.
            agenda.AgendarAsync(outro.Id, quando, ModalidadeAtendimento.Consulta, null,
                modalidadeCodigo: nameof(ModalidadeAtendimento.Consulta),
                especialidadeConsultaCodigo: nameof(Especialidade.Psiquiatria),
                profissionalId: prof.Id, encaixe: true).GetAwaiter().GetResult();
        }

        IReadOnlyList<string>? concluido = null;
        var form = ctx.RenderComponent<AgendamentoForm>(p => p
            .Add(x => x.AgendamentoId, id).Add(x => x.TituloDaEdicao, "Editar o horário")
            .Add(x => x.AoConcluir, a => concluido = a));
        Esperar(() => form.FindAll("#agf-especialidade").Count > 0);
        Assert.True(Contem(form, "Editar o horário"));
        // O choque que já existia é DITO, e nada impede.
        Esperar(() => Contem(form, AvisosDeChoque.Cabecalho));

        Agir(form, f => f.Find("#agf-especialidade"), e => e.ChangeAsync(new ChangeEventArgs { Value = nameof(Especialidade.Geriatria) }));
        Clicar(form, "Salvar horário");
        Esperar(() => concluido is not null);

        using var db = Db();
        var ag = db.Agendamentos.Single(a => a.Id == id);
        Assert.Equal(nameof(Especialidade.Geriatria), ag.EspecialidadeConsultaCodigo);
        Assert.Equal(quando, ag.DataHora);
        Assert.Equal(prof.Id, ag.ProfissionalId);
        Assert.Equal("Importado · Consulta", ag.Observacoes);
    }

    [Fact]
    public void Choque_aparece_como_aviso_e_salvar_marca_assim_mesmo()
    {
        var (prof, paciente, outro) = Semear("Choque");
        var quando = DateTime.Today.AddDays(302).AddHours(9);
        using var ctx = Contexto();
        using (var escopo = ctx.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            escopo.ServiceProvider.GetRequiredService<AgendaService>().AgendarAsync(outro.Id, quando,
                ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: prof.Id).GetAwaiter().GetResult();

        IReadOnlyList<string>? concluido = null;
        var form = ctx.RenderComponent<AgendamentoForm>(p => p
            .Add(x => x.Data, quando.Date).Add(x => x.Hora, "09:00")
            .Add(x => x.ProfissionalPreferidoId, prof.Id)
            .Add(x => x.AoConcluir, a => concluido = a));
        Esperar(() => form.FindAll("#agf-profissional").Count > 0);
        EscolherPaciente(form, paciente.Nome);

        Esperar(() => Contem(form, AvisosDeChoque.Cabecalho));
        Assert.NotEmpty(form.FindAll(".agf-traco.tom-aviso"));
        Assert.Contains(form.FindAll("button"), b => b.TextContent.Trim() == "Assumir encaixe");

        Clicar(form, "Salvar horário");
        Esperar(() => concluido is not null);

        using var db = Db();
        Assert.Equal(2, db.Agendamentos.Count(a => a.ProfissionalId == prof.Id && a.DataHora == quando));
    }

    [Fact]
    public void Sem_profissional_o_horario_novo_e_recusado_e_nada_grava()
    {
        var (_, paciente, _) = Semear("SemDono");
        using var ctx = Contexto();
        IReadOnlyList<string>? concluido = null;
        var form = ctx.RenderComponent<AgendamentoForm>(p => p
            .Add(x => x.Data, DateTime.Today.AddDays(303)).Add(x => x.Hora, "11:00")
            .Add(x => x.AoConcluir, a => concluido = a));
        Esperar(() => form.FindAll("#agf-profissional").Count > 0);
        EscolherPaciente(form, paciente.Nome);

        Clicar(form, "Salvar horário");
        Esperar(() => Contem(form, AgendamentoNaTela.RecusaSemProfissional));

        Assert.Null(concluido);
        using var db = Db();
        Assert.False(db.Agendamentos.Any(a => a.PacienteId == paciente.Id));
    }

    // ==================== a lista de espera ====================

    [Fact]
    public void Colocar_o_paciente_na_lista_de_espera_grava_o_pedido_com_o_turno()
    {
        var (prof, paciente, _) = Semear("Espera");
        using var ctx = Contexto();
        var concluiu = false;
        var form = ctx.RenderComponent<ListaEsperaForm>(p => p.Add(x => x.AoConcluir, () => concluiu = true));
        Esperar(() => form.FindAll("#lef-turno").Count > 0);
        Assert.True(Contem(form, "Manhã"));

        EscolherPaciente(form, paciente.Nome);
        Agir(form, f => f.Find("#lef-profissional"), e => e.ChangeAsync(new ChangeEventArgs { Value = prof.Id.ToString() }));
        Agir(form, f => f.Find("#lef-turno"), e => e.ChangeAsync(new ChangeEventArgs { Value = nameof(PeriodoPreferido.Manha) }));
        Agir(form, f => f.Find("#lef-prioritario"), e => e.ChangeAsync(new ChangeEventArgs { Value = true }));
        Clicar(form, "Entrar na lista");
        Esperar(() => concluiu);

        using var db = Db();
        var pedido = db.ListaEspera.Single(l => l.PacienteId == paciente.Id);
        Assert.Equal(PeriodoPreferido.Manha, pedido.Periodo);
        Assert.Equal(prof.Id, pedido.ProfissionalId);
        Assert.True(pedido.Prioritario);
        Assert.Equal(StatusListaEspera.Aguardando, pedido.Status);
    }

    [Fact]
    public void Lista_de_espera_sem_paciente_recusa_e_nada_grava()
    {
        using var ctx = Contexto();
        var concluiu = false;
        var form = ctx.RenderComponent<ListaEsperaForm>(p => p.Add(x => x.AoConcluir, () => concluiu = true));
        Esperar(() => form.FindAll("#lef-turno").Count > 0);
        Clicar(form, "Entrar na lista");
        Esperar(() => Contem(form, ListaEsperaNaTela.RecusaSemPaciente));
        Assert.False(concluiu);
    }

    // ==================== montagem ====================

    /// <summary>Um profissional e dois pacientes PRÓPRIOS do teste (nomes únicos), sem nada marcado.</summary>
    private (Profissional Prof, Paciente Paciente, Paciente Outro) Semear(string sufixo)
    {
        var marca = $"{sufixo}{Guid.NewGuid():N}"[..14];
        using var db = Db();
        var prof = new Profissional { Nome = $"Dr. Forms {marca}", NomeCurto = $"Forms {marca}" };
        var paciente = new Paciente { Nome = $"Zuleika Formteste {marca}", ConvenioCodigo = "Particular" };
        var outro = new Paciente { Nome = $"Outro Formteste {marca}", ConvenioCodigo = "Particular" };
        db.AddRange(prof, paciente, outro);
        db.SaveChanges();
        return (prof, paciente, outro);
    }

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

        // O catálogo como a clínica o tem (o que o app carrega na abertura), lido do banco semeado.
        using var escopo = ctx.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        escopo.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync().GetAwaiter().GetResult();
        escopo.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync().GetAwaiter().GetResult();
        return ctx;
    }

    /// <summary>
    /// Digita, espera o resultado e CLICA nele até o paciente ficar escolhido. O formulário
    /// re-renderiza sozinho (as conferências da abertura chamam StateHasChanged), e cada
    /// re-renderização dá à linha um id de evento novo: clicar no elemento achado antes dela
    /// manda o clique para um id que não existe mais, e o bUnit o descarta calado. Por isso a
    /// linha é achada DE NOVO a cada tentativa — o navegador sempre clica no DOM atual.
    /// </summary>
    private static void EscolherPaciente(IRenderedFragment form, string nome)
    {
        Agir(form, f => f.Find("input[placeholder='Buscar por nome ou CPF']"), e => e.InputAsync(new ChangeEventArgs { Value = nome }));
        try
        {
            Esperar(() =>
            {
                if (form.FindAll(".seletor-escolhido").Count > 0) return true;
                var linha = form.FindAll(".seletor-resultados li").FirstOrDefault(li => li.TextContent.Contains(nome));
                if (linha is null) return false;
                try { linha.ClickAsync(new MouseEventArgs()).GetAwaiter().GetResult(); } catch (UnknownEventHandlerIdException) { }
                return form.FindAll(".seletor-escolhido").Count > 0;
            });
        }
        catch (TimeoutException)
        {
            // Diz O QUE a tela mostra: "a busca não trouxe" e "a busca falhou" são causas diferentes.
            var texto = System.Text.RegularExpressions.Regex.Replace(form.FindAll(".modal").FirstOrDefault()?.TextContent ?? "(sem modal)", @"\s+", " ");
            throw new TimeoutException("O paciente não ficou escolhido. A tela dizia: " + texto[..Math.Min(600, texto.Length)]);
        }
    }

    /// <summary>O TEXTO do formulário (o Markup codifica acento e travessão; o texto não).</summary>
    private static bool Contem(IRenderedFragment form, string texto)
        => form.FindAll(".modal").Any(m => m.TextContent.Contains(texto, StringComparison.Ordinal));

    /// <summary>
    /// Age sobre o elemento pela versão ASSÍNCRONA do bUnit e repete SÓ quando o id de evento
    /// estava morto. O formulário re-renderiza sozinho (as conferências da abertura chamam
    /// StateHasChanged) e cada re-renderização troca o id do evento: o Click()/Change()
    /// síncrono descarta a Task e, com ela, a recusa — o gesto sumia calado. Repetir às cegas
    /// não serve (um Salvar gravaria duas vezes); repetir só na recusa do id é exato.
    /// </summary>
    private static void Agir(IRenderedFragment form, Func<IRenderedFragment, AngleSharp.Dom.IElement> achar,
        Func<AngleSharp.Dom.IElement, Task> acao)
    {
        Esperar(() =>
        {
            try { acao(achar(form)).GetAwaiter().GetResult(); return true; }
            catch (UnknownEventHandlerIdException) { return false; }
        });
    }

    private static void Clicar(IRenderedFragment form, string rotulo)
    {
        Esperar(() => form.FindAll("button").Any(b => b.TextContent.Trim() == rotulo && !b.HasAttribute("disabled")));
        Agir(form, f => f.FindAll("button").First(b => b.TextContent.Trim() == rotulo), e => e.ClickAsync(new MouseEventArgs()));
    }

    private static void Esperar(Func<bool> condicao,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condicao))] string? qual = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int linha = 0)
    {
        var prazo = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < prazo)
        {
            // A tela e o teste dividem a MESMA conexão em memória: ler no meio da gravação da
            // tela devolve "database is locked" (5/6) — isso é "ainda não", não falha.
            try { if (condicao()) return; }
            catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode is 5 or 6) { }
            catch (Bunit.ElementNotFoundException) { }
            Thread.Sleep(25);
        }
        throw new TimeoutException($"A condição não aconteceu a tempo (linha {linha}): {qual}");
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
