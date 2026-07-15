using System.Collections.ObjectModel;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Ficha do paciente: dados + histórico completo de códigos, com estorno de baixas.</summary>
public partial class FichaPacienteViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private int _pacienteId;

    [ObservableProperty] private Paciente? _paciente;
    [ObservableProperty] private string _cpfFormatado = string.Empty;

    public ObservableCollection<CodigoFaturamento> Codigos { get; } = new();

    public event Action? Voltar;

    public FichaPacienteViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync(int pacienteId)
    {
        _pacienteId = pacienteId;
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        Paciente = await service.ObterComHistoricoAsync(pacienteId);
        CpfFormatado = Cpf.Formatar(Paciente?.Documento);

        Codigos.Clear();
        if (Paciente is not null)
        {
            var todos = Paciente.Atendimentos
                .SelectMany(a => a.Codigos)
                .OrderByDescending(c => c.DataPrevistaFaturamento);
            foreach (var c in todos) Codigos.Add(c);
        }
    }

    [RelayCommand]
    private async Task Estornar(CodigoFaturamento? codigo)
    {
        if (codigo is null || !codigo.Baixado) return;

        var confirma = MessageBox.Show(
            "Estornar a baixa desta guia? A pendência voltará a aparecer no painel.",
            "Confirmar estorno", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirma != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.EstornarBaixaAsync(codigo.Id, "estorno pela ficha do paciente", Environment.UserName);

        await CarregarAsync(_pacienteId);
    }

    [RelayCommand]
    private void FecharFicha() => Voltar?.Invoke();
}
