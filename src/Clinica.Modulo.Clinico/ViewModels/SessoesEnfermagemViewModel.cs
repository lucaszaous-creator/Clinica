using System.Collections.ObjectModel;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

public sealed partial class SessoesEnfermagemViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco) : ObservableObject
{
    public sealed record Opcao(string Codigo, string Nome);
    public IReadOnlyList<Opcao> Situacoes { get; } = [new("DiaEPendentes", "Hoje e pendentes anteriores"), new("Hoje", "Sessões de hoje"), new("Pendentes", "Evoluções pendentes"), new("Registradas", "Evoluções registradas"), new("Todas", "Todas no período")];
    public ObservableCollection<SessaoEnfermagemItem> Sessoes { get; } = [];
    [ObservableProperty] private string _paciente = "";
    [ObservableProperty] private string _medico = "";
    [ObservableProperty] private DateTime? _inicio;
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private string _situacao = "DiaEPendentes";
    [ObservableProperty] private string _mensagem = "";
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _mais;
    [ObservableProperty] private int _pagina;
    private int geracao;

    [RelayCommand] private async Task FiltrarAsync() { Pagina = 0; await CarregarAsync(); }
    [RelayCommand] private async Task AnteriorAsync() { if (Pagina > 0) { Pagina--; await CarregarAsync(); } }
    [RelayCommand] private async Task ProximaAsync() { if (Mais) { Pagina++; await CarregarAsync(); } }
    [RelayCommand] private async Task LimparAsync() { Paciente = Medico = ""; Inicio = Fim = null; Situacao = "DiaEPendentes"; await FiltrarAsync(); }
    [RelayCommand] public async Task CarregarAsync()
    {
        var carga = ++geracao;
        try
        {
            Carregando = true;
            using var scope = escopos.CreateScope();
            var resultado = await scope.ServiceProvider.GetRequiredService<SessoesEnfermagemService>().ListarAsync(
                SessaoUsuario.Atual.UsuarioId, new(Paciente, Inicio is {} i ? DateOnly.FromDateTime(i) : null,
                    Fim is {} f ? DateOnly.FromDateTime(f) : null, Medico, Situacao, Pagina), DateOnly.FromDateTime(DateTime.Today));
            if (carga != geracao) return;
            Sessoes.Clear(); foreach (var item in resultado.Itens) Sessoes.Add(item);
            Mais = resultado.Mais;
            Mensagem = Sessoes.Count == 0 ? "Nenhuma sessão encontrada com estes filtros." : $"Página {Pagina + 1} · {Sessoes.Count} sessões";
        }
        catch (Exception ex) { if (carga == geracao) { Sessoes.Clear(); Mais = false; Mensagem = "Não foi possível consultar as sessões: " + ex.Message; } }
        finally { if (carga == geracao) Carregando = false; }
    }
    [RelayCommand] private void Abrir(SessaoEnfermagemItem? item)
    {
        if (item is null || SessaoUsuario.Atual.Perfil != PerfilAcesso.Enfermagem) return;
        foco.Definir(item.PacienteId, item.Paciente, item.Id, item.AtendimentoId, DateOnly.FromDateTime(item.DataHora));
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimentoEnfermagem);
    }
}
