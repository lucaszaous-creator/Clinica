using System.Collections.Concurrent;
using System.Data.Common;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Clinica.Windows.Tests;

public sealed class CarregamentoProntuarioTests : IDisposable
{
    private readonly SqliteConnection _manterBanco;
    private readonly ServiceProvider _servicos;
    private readonly Medidor _medidor = new();
    private readonly PacienteEmFoco _foco = new();
    private readonly ProntuarioClinicoViewModel _vm;
    private readonly int _primeiro, _segundo;

    public CarregamentoProntuarioTests()
    {
        // Banco fictício compartilhado; cada escopo recebe conexão/DbContext próprios.
        var cs = $"Data Source=desempenho-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _manterBanco = new SqliteConnection(cs);
        _manterBanco.Open();
        var services = new ServiceCollection();
        services.AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado");
        services.RemoveAll<ClinicaDbContext>();
        services.AddScoped(_ => new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(cs).AddInterceptors(_medidor).Options));
        _servicos = services.BuildServiceProvider();
        using var scope = _servicos.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        db.Database.EnsureCreated();
        var p1 = new Paciente { Nome = "Fictício A" };
        var p2 = new Paciente { Nome = "Fictício B" };
        db.Pacientes.AddRange(p1, p2);
        db.SaveChanges();
        _primeiro = p1.Id; _segundo = p2.Id;
        db.Evolucoes.AddRange(
            new Evolucao { PacienteId = p1.Id, Data = new DateOnly(2026, 10, 1), QueixaPrincipal = "Dor lombar" },
            new Evolucao { PacienteId = p1.Id, Data = new DateOnly(2026, 10, 2), QueixaPrincipal = "Dor no ombro" },
            new Evolucao { PacienteId = p2.Id, Data = new DateOnly(2026, 10, 3), QueixaPrincipal = "Somente paciente B" });
        db.SaveChanges();
        new SessaoUsuario().Entrar(new UsuarioSistema()); // sessão vazia dos testes
        _vm = new ProntuarioClinicoViewModel(_servicos.GetRequiredService<IServiceScopeFactory>(), _foco);
        _foco.Definir(_primeiro, "Fictício A");
    }

    [Fact]
    public async Task Digitar_filtro_nao_faz_consultas_nem_recarrega_linha_do_tempo()
    {
        await CarregarEEsperarAsync();
        Assert.Equal(2, _vm.Sessoes.Count);
        var consultas = _medidor.Consultas.Count;
        foreach (var termo in new[] { "l", "lo", "lomb", "lombar" }) _vm.TermoSessao = termo;
        Assert.Single(_vm.Sessoes);
        Assert.Equal("Dor lombar", _vm.Sessoes[0].Queixa);
        Assert.Equal(consultas, _medidor.Consultas.Count);
        _vm.TermoSessao = "";
        Assert.Equal(2, _vm.Sessoes.Count);
    }

    [Fact]
    public async Task Atualizar_descarta_cache_e_enxerga_registro_novo()
    {
        await CarregarEEsperarAsync();
        using (var scope = _servicos.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            db.Evolucoes.Add(new Evolucao { PacienteId = _primeiro, Data = new DateOnly(2026, 10, 4), QueixaPrincipal = "Registro novo" });
            await db.SaveChangesAsync();
        }
        await CarregarEEsperarAsync();
        Assert.Equal(3, _vm.Sessoes.Count);
    }

    [Fact]
    public async Task Filtro_digitado_durante_a_carga_e_aplicado_ao_resultado()
    {
        _medidor.EsperarEvolucoes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var carga = _vm.CarregarAsync();
        await _medidor.EvolucoesIniciadas.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _vm.TermoSessao = "ombro";
        _medidor.EsperarEvolucoes.SetResult();
        await carga;
        await EsperarLinhaDoTempoAsync();
        Assert.Single(_vm.Sessoes);
        Assert.Equal("Dor no ombro", _vm.Sessoes[0].Queixa);
    }

    [Fact]
    public async Task Retorno_atrasado_de_outro_paciente_nao_substitui_o_atual()
    {
        _medidor.EsperarEvolucoes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var antiga = _vm.CarregarAsync();
        await _medidor.EvolucoesIniciadas.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var liberarAntiga = _medidor.EsperarEvolucoes;
        _medidor.EsperarEvolucoes = null;
        _foco.Definir(_segundo, "Fictício B");
        await CarregarEEsperarAsync();
        liberarAntiga.SetResult();
        await antiga;
        Assert.Single(_vm.Sessoes);
        Assert.Equal("Somente paciente B", _vm.Sessoes[0].Queixa);
        _vm.TermoSessao = "lombar";
        Assert.Empty(_vm.Sessoes);
    }

    [Fact]
    public async Task Falha_ao_atualizar_nao_reapresenta_snapshot_antigo_pelo_filtro()
    {
        await CarregarEEsperarAsync();
        _medidor.FalharEvolucoes = true;
        await CarregarEEsperarAsync();
        Assert.True(_vm.NaoVerificado);
        _vm.TermoSessao = "lombar";
        Assert.Empty(_vm.Sessoes);
    }

    [Fact]
    public async Task Limpar_paciente_esvazia_cache_e_impede_filtro_de_reexibir_dados()
    {
        await CarregarEEsperarAsync();
        _foco.Limpar();
        await CarregarEEsperarAsync();
        _vm.TermoSessao = "lombar";
        Assert.Empty(_vm.Sessoes);
        Assert.False(_vm.Carregando);
    }

    [Fact]
    public async Task Atendimento_carrega_apoio_enquanto_alertas_aguardam_sem_compartilhar_contexto()
    {
        _foco.Limpar();
        var atendimento = new AtendimentoViewModel(_servicos.GetRequiredService<IServiceScopeFactory>(),
            new AvisosFicticios(), _foco);
        _foco.Definir(_primeiro, "Fictício A");
        _medidor.EsperarProblemas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var carga = atendimento.CarregarAsync();
        try
        {
            await _medidor.ProblemasIniciados.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (atendimento.Mapa?.CargaConcluida != true) await Task.Delay(10, limite.Token);
            Assert.False(carga.IsCompleted, "o mapa deve aparecer mesmo com os alertas ainda aguardando");
        }
        finally { _medidor.EsperarProblemas.TrySetResult(); await carga; }

        Assert.False(atendimento.NaoVerificado, atendimento.Mensagem);
        Assert.DoesNotContain(atendimento.AlertasClinicos, a => a.Texto.Contains("Não foi possível"));
        Assert.DoesNotContain(atendimento.Alertas, a => a.Texto.Contains("Não foi possível"));
        Assert.Equal(1, _medidor.Consultas.Count(q => q.Contains("FROM \"Evolucoes\"")));
        Assert.False(atendimento.Carregando);
    }

    private sealed class AvisosFicticios : ISnackbarService
    {
        public void Sucesso(string mensagem) { }
        public void Erro(string mensagem) { }
        public void Info(string mensagem) { }
    }

    private async Task CarregarEEsperarAsync()
    {
        await _vm.CarregarAsync();
        await EsperarLinhaDoTempoAsync();
    }

    private async Task EsperarLinhaDoTempoAsync()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (_vm.LinhaDoTempo.Carregando) await Task.Delay(10, limite.Token);
    }

    private sealed class Medidor : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Consultas { get; } = new();
        public TaskCompletionSource? EsperarEvolucoes { get; set; }
        public TaskCompletionSource EvolucoesIniciadas { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FalharEvolucoes { get; set; }
        public TaskCompletionSource? EsperarProblemas { get; set; }
        public TaskCompletionSource ProblemasIniciados { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Consultas.Enqueue(command.CommandText);
            if (command.CommandText.Contains("FROM \"ProblemasPaciente\"") && EsperarProblemas is { } problemas)
            {
                ProblemasIniciados.TrySetResult();
                await problemas.Task.WaitAsync(cancellationToken);
            }
            if (command.CommandText.Contains("FROM \"Evolucoes\""))
            {
                if (FalharEvolucoes) throw new InvalidOperationException("Falha simulada de rede");
                var espera = EsperarEvolucoes;
                if (espera is not null)
                {
                    EvolucoesIniciadas.TrySetResult();
                    await espera.Task.WaitAsync(cancellationToken);
                }
            }
            await Task.Delay(10, cancellationToken);
            return result;
        }
    }

    public void Dispose() { _servicos.Dispose(); _manterBanco.Dispose(); }
}
