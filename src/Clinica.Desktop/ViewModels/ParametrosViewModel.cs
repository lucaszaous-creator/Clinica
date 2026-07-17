using System.Windows.Input;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Parâmetros editáveis dos convênios (dias de renovação e dias até o 2º código).</summary>
public partial class ParametrosViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<ParametroConvenio> Itens { get; } = new();

    [ObservableProperty] private string? _mensagem;

    public ParametrosViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
        var snap = await parametros.ObterAsync();

        Itens.Clear();
        foreach (var p in snap.Todos.OrderBy(p => p.Convenio))
            Itens.Add(p);
    }

    [RelayCommand]
    private async Task Salvar()
    {
        using var scope = _scopeFactory.CreateScope();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
        await parametros.SalvarAsync(Itens.ToList());
        Mensagem = "Parâmetros salvos. Passam a valer nos próximos lançamentos.";
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => SalvarCommand;
}
