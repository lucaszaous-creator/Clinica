using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Windows.Tests;

public sealed class PaginacaoPacienteTests : IDisposable
{
    private readonly ServiceProvider _sp = new ServiceCollection()
        .AddClinica("Host=127.0.0.1;Database=nao_usado;Username=nao_usado").BuildServiceProvider();

    [Fact]
    public async Task Abertura_ociosa_e_paginas_limitadas_sem_perder_acesso_a_ultima()
    {
        var leituras = new List<(int, int)>();
        var pacientes = Enumerable.Range(1, 241).Select(i => new Paciente { Id = i, Nome = $"Fictício {i}" }).ToList();
        var vm = new SeletorPacienteViewModel(_sp.GetRequiredService<IServiceScopeFactory>())
        {
            SemBuscaInicial = true, TamanhoPagina = 100,
            ConsultaPagina = (_, ignorar, quantidade, _) =>
            {
                leituras.Add((ignorar, quantidade));
                return Task.FromResult<IReadOnlyList<Paciente>>(pacientes.Skip(ignorar).Take(quantidade).ToList());
            }
        };
        await vm.BuscarAsync(true);
        Assert.Empty(leituras);
        await vm.DesligarSugestaoCommand.ExecuteAsync(null);
        Assert.Equal(100, vm.Resultados.Count);
        Assert.True(vm.TemProximaPagina);
        await vm.ProximaPaginaCommand.ExecuteAsync(null);
        Assert.Equal(101, vm.Resultados[0].Id);
        await vm.ProximaPaginaCommand.ExecuteAsync(null);
        Assert.Equal(41, vm.Resultados.Count);
        Assert.False(vm.TemProximaPagina);
        await vm.PaginaAnteriorCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.PaginaAtual);
        Assert.Equal(new[] { (0, 101), (100, 101), (200, 101), (100, 101) }, leituras);
    }

    [Fact]
    public async Task Digitacao_rapida_agrupa_consulta_e_reinicia_pagina()
    {
        var leituras = new List<string?>();
        var terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new SeletorPacienteViewModel(_sp.GetRequiredService<IServiceScopeFactory>())
        {
            TamanhoPagina = 100,
            ConsultaPagina = (termo, ignorar, _, _) =>
            {
                Assert.Equal(0, ignorar);
                leituras.Add(termo);
                return Task.FromResult<IReadOnlyList<Paciente>>([new Paciente { Id = 8, Nome = "Alvo" }]);
            }
        };
        vm.Atualizou += () => terminou.TrySetResult();
        vm.Termo = "A"; vm.Termo = "Al"; vm.Termo = "Alvo";
        await terminou.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "Alvo" }, leituras);
        Assert.Equal(1, vm.PaginaAtual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resposta_ou_erro_antigo_nao_substitui_busca_nova(bool falha)
    {
        var antigo = new TaskCompletionSource<IReadOnlyList<Paciente>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new SeletorPacienteViewModel(_sp.GetRequiredService<IServiceScopeFactory>())
        {
            TamanhoPagina = 100,
            ConsultaPagina = (termo, _, _, _) => termo == "antigo" ? antigo.Task
                : Task.FromResult<IReadOnlyList<Paciente>>([new Paciente { Id = 9, Nome = "Atual" }])
        };
        vm.Termo = "antigo";
        var leituraAntiga = vm.BuscarAsync(true);
        vm.Termo = "novo";
        await vm.BuscarAsync(true);
        if (falha) antigo.SetException(new InvalidOperationException("Falha antiga"));
        else antigo.SetResult([new Paciente { Id = 1, Nome = "Antigo" }]);
        await leituraAntiga;
        Assert.Equal(9, Assert.Single(vm.Resultados).Id);
        Assert.Null(vm.Erro);
        Assert.False(vm.Buscando);
    }

    public void Dispose() => _sp.Dispose();
}
