using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinica.Tests.Ui;

/// <summary>Snackbar de teste: guarda o que a tela mandou dizer, em vez de desenhar.</summary>
public sealed class SnackbarDeTeste : ISnackbarService
{
    public List<string> Sucessos { get; } = [];
    public List<string> Erros { get; } = [];
    public List<string> Infos { get; } = [];

    public void Sucesso(string mensagem) => Sucessos.Add(mensagem);
    public void Erro(string mensagem) => Erros.Add(mensagem);
    public void Info(string mensagem) => Infos.Add(mensagem);
}

/// <summary>
/// Diálogo de teste. Responde o que o teste mandar e REGISTRA o que foi perguntado —
/// é o aviso do balcão, e o que ele diz importa tanto quanto o que a ação grava.
/// </summary>
public sealed class DialogoDeTeste : IDialogoService
{
    public List<(string Titulo, string Mensagem)> Avisos { get; } = [];
    public List<(string Titulo, string Mensagem)> Confirmacoes { get; } = [];

    /// <summary>O que responder a Confirmar/ConfirmarPerigo (padrão: sim).</summary>
    public bool Resposta { get; set; } = true;

    /// <summary>O que devolver em PerguntarTexto. Null = o usuário desistiu.</summary>
    public string? TextoRespondido { get; set; } = "motivo do teste";

    public bool Confirmar(string titulo, string mensagem)
    {
        Confirmacoes.Add((titulo, mensagem));
        return Resposta;
    }

    public bool ConfirmarPerigo(string titulo, string mensagem)
    {
        Confirmacoes.Add((titulo, mensagem));
        return Resposta;
    }

    public void Aviso(string titulo, string mensagem) => Avisos.Add((titulo, mensagem));

    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null)
        => TextoRespondido;
}

/// <summary>
/// O ambiente mínimo em que um ViewModel da suíte roda: banco SQLite em memória, o mesmo
/// contêiner de serviços do app real e os dois canais de feedback substituídos por dublês.
///
/// O contêiner é montado com o <c>AddClinica</c> DE VERDADE e só o provider do banco é
/// trocado. Reescrever a lista de serviços aqui daria um grafo paralelo que envelhece:
/// serviço novo registrado no app não apareceria no teste, e o teste passaria a provar
/// uma montagem que não existe.
/// </summary>
public sealed class AmbienteDeTela : IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly ServiceProvider _provedor;

    public IServiceScopeFactory Escopos { get; }
    public SnackbarDeTeste Snackbar { get; } = new();
    public DialogoDeTeste Dialogo { get; } = new();

    /// <summary>Contexto direto, para semear o banco antes de a tela abrir.</summary>
    public ClinicaDbContext Db { get; }

    public AmbienteDeTela()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        var servicos = new ServiceCollection();

        // A connection string do Npgsql nunca é usada: o provider é trocado logo abaixo.
        // O que interessa aqui são os ~60 AddScoped que vêm junto.
        servicos.AddClinica("Host=nao-usado;Database=x;Username=x;Password=x");

        servicos.RemoveAll<DbContextOptions<ClinicaDbContext>>();
        servicos.RemoveAll<DbContextOptions>();
        servicos.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conexao));

        servicos.AddSingleton<ISnackbarService>(Snackbar);
        servicos.AddSingleton<IDialogoService>(Dialogo);

        _provedor = servicos.BuildServiceProvider();
        Escopos = _provedor.GetRequiredService<IServiceScopeFactory>();

        Db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        Db.Database.EnsureCreated();
    }

    public T Servico<T>() where T : notnull
    {
        var escopo = _provedor.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<T>();
    }

    // ==================== Semeadura ====================

    public async Task<Paciente> PacienteAsync(
        string nome = "Maria", Convenio convenio = Convenio.UnimedIntercambio)
    {
        var p = new Paciente { Nome = nome, Convenio = convenio, Sexo = Sexo.Feminino };
        Db.Pacientes.Add(p);
        await Db.SaveChangesAsync();
        return p;
    }

    public async Task<Profissional> ProfissionalAsync(string nome = "Dra. Ana")
    {
        var p = new Profissional { Nome = nome, Ativo = true };
        Db.Profissionais.Add(p);
        await Db.SaveChangesAsync();
        return p;
    }

    public async Task<Agendamento> AgendamentoAsync(
        int pacienteId, DateTime quando, int? profissionalId = null,
        DateTime? chegada = null, DateTime? inicio = null,
        StatusAgendamento status = StatusAgendamento.Agendado,
        int? duracaoMinutos = null)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = quando,
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            ModalidadeCodigo = ModalidadeAtendimento.AcupunturaSimples.ToString(),
            ProfissionalId = profissionalId,
            ChegadaEm = chegada,
            InicioAtendimentoEm = inicio,
            Status = status,
            DuracaoMinutos = duracaoMinutos
        };
        Db.Agendamentos.Add(ag);
        await Db.SaveChangesAsync();
        return ag;
    }

    /// <summary>
    /// Espera a carga assíncrona que o ViewModel dispara sozinho terminar.
    ///
    /// Os ViewModels da suíte carregam no construtor (`_ = CarregarAsync()`), como a tela
    /// real faz. Chamar `CarregarAsync` de novo por fora rodaria DUAS consultas no mesmo
    /// DbContext ao mesmo tempo, que o EF recusa — então o teste semeia o banco ANTES de
    /// construir a tela e espera a carga que ela mesma fez.
    ///
    /// A parte síncrona de `CarregarAsync` (até o primeiro await) já roda no construtor e
    /// deixa `Carregando = true`, então não há corrida na primeira leitura.
    /// </summary>
    public static async Task AssentarAsync(Func<bool> carregando, int limiteMs = 15000)
    {
        // Defensivo: hoje o `Carregando = true` de todos estes ViewModels está ANTES do
        // primeiro await, então quando o construtor (ou o setter) devolve, a carga já se
        // anunciou. Se um dia alguém mover essa linha para depois de um await, um poll
        // imediato leria "já terminou" sobre uma carga que nem começou — e o teste
        // passaria a afirmar sobre a tela vazia.
        await Task.Delay(50);

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        while (carregando() && relogio.ElapsedMilliseconds < limiteMs)
            await Task.Delay(15);

        if (carregando())
            throw new TimeoutException(
                "A tela não terminou de carregar dentro do limite — provável travamento.");
    }

    public void Dispose()
    {
        Db.Dispose();
        _provedor.Dispose();
        _conexao.Dispose();
    }
}

/// <summary>Atalho de leitura para os ViewModels que expõem `Carregando`.</summary>
public static class EsperaDeTela
{
    public static Task AssentarAsync(this ObservableObject vm, Func<bool> carregando)
        => AmbienteDeTela.AssentarAsync(carregando);
}
