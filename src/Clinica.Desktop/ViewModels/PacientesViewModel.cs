using System.Collections.ObjectModel;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Cadastro, busca (nome/CPF), edição, exclusão de pacientes e acesso à ficha.</summary>
public partial class PacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Paciente> Pacientes { get; } = new();

    public Array Convenios => Enum.GetValues(typeof(Convenio));
    public Array Sexos => Enum.GetValues(typeof(Sexo));

    [ObservableProperty] private string? _busca;

    // Formulário
    [ObservableProperty] private int? _editandoId;
    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private Convenio _convenio = Convenio.UnimedIntercambio;
    [ObservableProperty] private bool _possuiApp;
    [ObservableProperty] private Sexo _sexo = Sexo.Feminino;
    [ObservableProperty] private string? _mensagem;

    public string TituloFormulario => EditandoId is null ? "Novo paciente" : "Editar paciente";

    /// <summary>Pede ao shell para abrir a ficha de um paciente.</summary>
    public event Action<int>? FichaSolicitada;

    public PacientesViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    partial void OnEditandoIdChanged(int? value) => OnPropertyChanged(nameof(TituloFormulario));

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        Pacientes.Clear();
        foreach (var p in await service.BuscarAsync(Busca))
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
        if (!string.IsNullOrWhiteSpace(Documento) && !Cpf.Valido(Documento))
        {
            Mensagem = "CPF inválido. Verifique os dígitos.";
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();

            if (EditandoId is int id)
            {
                var p = await db.Pacientes.FirstOrDefaultAsync(x => x.Id == id);
                if (p is null) { Mensagem = "Paciente não encontrado."; return; }
                Aplicar(p);
                await service.AtualizarAsync(p);
                Mensagem = "Paciente atualizado.";
            }
            else
            {
                var p = new Paciente();
                Aplicar(p);
                await service.SalvarNovoAsync(p);
                Mensagem = "Paciente salvo.";
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            return;
        }

        Limpar();
        await Buscar();
    }

    private void Aplicar(Paciente p)
    {
        p.Nome = Nome.Trim();
        p.Documento = Documento;
        p.Telefone = Telefone;
        p.Convenio = Convenio;
        p.PossuiApp = PossuiApp;
        p.Sexo = Sexo;
    }

    [RelayCommand]
    private void Editar(Paciente? p)
    {
        if (p is null) return;
        EditandoId = p.Id;
        Nome = p.Nome;
        Documento = Cpf.Formatar(p.Documento);
        Telefone = p.Telefone;
        Convenio = p.Convenio;
        PossuiApp = p.PossuiApp;
        Sexo = p.Sexo;
        Mensagem = null;
    }

    [RelayCommand]
    private void Novo() => Limpar();

    [RelayCommand]
    private async Task Excluir(Paciente? p)
    {
        if (p is null) return;
        var confirma = MessageBox.Show(
            $"Excluir o paciente \"{p.Nome}\"?\nTodos os atendimentos e códigos dele também serão removidos.",
            "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirma != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        await service.RemoverAsync(p.Id);
        if (EditandoId == p.Id) Limpar();
        await Buscar();
    }

    [RelayCommand]
    private void AbrirFicha(Paciente? p)
    {
        if (p is not null) FichaSolicitada?.Invoke(p.Id);
    }

    private void Limpar()
    {
        EditandoId = null;
        Nome = string.Empty;
        Documento = null;
        Telefone = null;
        PossuiApp = false;
        Convenio = Convenio.UnimedIntercambio;
        Sexo = Sexo.Feminino;
    }
}
