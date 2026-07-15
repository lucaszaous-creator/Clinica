using System.Collections.ObjectModel;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Cadastro de pacientes: convênio, se possui app e sexo (dados que dirigem as regras).</summary>
public partial class PacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Paciente> Pacientes { get; } = new();

    public Array Convenios => Enum.GetValues(typeof(Convenio));
    public Array Sexos => Enum.GetValues(typeof(Sexo));

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private Convenio _convenio = Convenio.UnimedIntercambio;
    [ObservableProperty] private bool _possuiApp;
    [ObservableProperty] private Sexo _sexo = Sexo.Feminino;
    [ObservableProperty] private string? _mensagem;

    public PacientesViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Pacientes.Clear();
        foreach (var p in await db.Pacientes.OrderBy(p => p.Nome).ToListAsync())
            Pacientes.Add(p);
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (string.IsNullOrWhiteSpace(Nome))
        {
            Mensagem = "Informe o nome do paciente.";
            return;
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            db.Pacientes.Add(new Paciente
            {
                Nome = Nome.Trim(),
                Documento = Documento,
                Telefone = Telefone,
                Convenio = Convenio,
                PossuiApp = PossuiApp,
                Sexo = Sexo
            });
            await db.SaveChangesAsync();
        }

        Mensagem = "Paciente salvo.";
        Nome = string.Empty; Documento = null; Telefone = null; PossuiApp = false;
        await CarregarAsync();
    }
}
