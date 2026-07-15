using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Registra a BAIXA de uma guia: data, número real da guia e forma de obtenção. Não trata recebíveis.</summary>
public partial class BaixaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private int _codigoId;

    [ObservableProperty] private CodigoFaturamento? _codigo;
    [ObservableProperty] private string _pacienteNome = string.Empty;
    [ObservableProperty] private DateTime _dataBaixa = DateTime.Today;
    [ObservableProperty] private string? _numeroGuia;
    [ObservableProperty] private string? _observacao;
    [ObservableProperty] private string? _mensagem;

    public event Action? BaixaConcluida;
    public event Action? Cancelado;

    public BaixaViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync(int codigoId)
    {
        _codigoId = codigoId;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Codigo = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .FirstOrDefaultAsync(c => c.Id == codigoId);
        PacienteNome = Codigo?.Atendimento?.Paciente?.Nome ?? string.Empty;
    }

    [RelayCommand]
    private async Task Confirmar()
    {
        if (string.IsNullOrWhiteSpace(NumeroGuia))
        {
            Mensagem = "Informe o número da guia gerada no sistema do convênio.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.DarBaixaAsync(_codigoId, DateOnly.FromDateTime(DataBaixa),
            NumeroGuia, Environment.UserName, Observacao);

        BaixaConcluida?.Invoke();
    }

    [RelayCommand]
    private void Cancelar() => Cancelado?.Invoke();
}
