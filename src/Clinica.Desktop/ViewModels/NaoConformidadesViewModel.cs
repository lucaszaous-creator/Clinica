using System.Collections.ObjectModel;
using System.Windows.Input;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Aba NC: lista todas as não conformidades (guias que, ao rodar as pendências, não puderam ser
/// baixadas e foram justificadas). Permite ler a justificativa e reabrir (voltar a ser pendência)
/// quando aparece uma solução.
/// </summary>
public partial class NaoConformidadesViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<NaoConformidadeItem> Itens { get; } = new();

    [ObservableProperty] private bool _temItens;

    public NaoConformidadesViewModel(IServiceScopeFactory scopeFactory, IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();

        Itens.Clear();
        foreach (var n in await rodada.NaoConformidadesAsync())
            Itens.Add(n);

        TemItens = Itens.Count > 0;
    }

    /// <summary>Mostra a justificativa completa da não conformidade (opção de leitura).</summary>
    [RelayCommand]
    private void VerJustificativa(NaoConformidadeItem? item)
    {
        if (item is null) return;
        _dialogo.Aviso($"Não conformidade — {item.PacienteNome}",
            string.IsNullOrWhiteSpace(item.Justificativa) ? "(sem justificativa registrada)" : item.Justificativa);
    }

    /// <summary>Reabre a não conformidade: a guia volta a ser pendência ativa e pode ser baixada.</summary>
    [RelayCommand]
    private async Task Reabrir(NaoConformidadeItem? item)
    {
        if (item is null) return;
        if (!_dialogo.Confirmar("Reabrir não conformidade",
                $"Reabrir a guia de {item.PacienteNome}? Ela volta a ser pendência ativa (aba Pendências).")) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
            await rodada.ReabrirNaoConformidadeAsync(item.CodigoId, Environment.UserName);
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Reabrir não conformidade", ex.Message);
        }

        await CarregarAsync();
    }

    [RelayCommand]
    private Task Atualizar() => CarregarAsync();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => AtualizarCommand;
}
