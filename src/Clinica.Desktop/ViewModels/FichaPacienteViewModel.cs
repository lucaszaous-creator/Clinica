using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private readonly Controls.IDialogoService _dialogo;
    private int _pacienteId;

    [ObservableProperty] private Paciente? _paciente;
    [ObservableProperty] private string _cpfFormatado = string.Empty;
    [ObservableProperty] private string? _nomeConvenio;
    [ObservableProperty] private string? _mensagem;

    // Indicadores da ficha
    [ObservableProperty] private int _totalSessoes;
    [ObservableProperty] private int _totalBaixados;
    [ObservableProperty] private int _totalPendentes;
    [ObservableProperty] private string _ultimaSessao = "—";

    /// <summary>Nome da clínica (assinatura da mensagem de WhatsApp).</summary>
    private string? _nomeClinica;

    public ObservableCollection<CodigoFaturamento> Codigos { get; } = new();

    public event Action? Voltar;

    public FichaPacienteViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

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

            NomeConvenio = Domain.Regras.ConvenioInfo.NomeExibicao(Paciente.Convenio);

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            TotalSessoes = Paciente.Atendimentos.Count;
            TotalBaixados = Codigos.Count(c => c.Baixado);
            TotalPendentes = Codigos.Count(c => c.EstaPendente(hoje));
            UltimaSessao = Paciente.Atendimentos.Count > 0
                ? Paciente.Atendimentos.Max(a => a.Data).ToString("dd/MM/yyyy")
                : "—";
        }

        try
        {
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            _nomeClinica = string.IsNullOrWhiteSpace(prestador.NomeFantasia) ? prestador.RazaoSocial : prestador.NomeFantasia;
        }
        catch
        {
            // Sem assinatura na mensagem; não impede a ficha.
        }
    }

    /// <summary>Abre a conversa de WhatsApp (wa.me) com o paciente.</summary>
    [RelayCommand]
    private void Whatsapp()
    {
        if (Paciente is null) return;

        var fone = Domain.Telefone.Normalizar(Paciente.Telefone);
        if (fone.Length is < 10 or > 13)
        {
            Mensagem = "Telefone ausente ou inválido no cadastro (edite em Pacientes).";
            return;
        }
        if (fone.Length is 10 or 11)
            fone = "55" + fone; // wa.me exige DDI

        var primeiroNome = Paciente.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Paciente.Nome;
        var texto = $"Olá, {primeiroNome}!" +
                    (string.IsNullOrWhiteSpace(_nomeClinica) ? string.Empty : $" Aqui é da {_nomeClinica}.");

        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{fone}?text={Uri.EscapeDataString(texto)}")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível abrir o WhatsApp: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Estornar(CodigoFaturamento? codigo)
    {
        if (codigo is null || !codigo.Baixado) return;

        if (!_dialogo.Confirmar("Confirmar estorno",
            "Estornar a baixa desta guia? A pendência voltará a aparecer no painel.")) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.EstornarBaixaAsync(codigo.Id, "estorno pela ficha do paciente", Environment.UserName);

        await CarregarAsync(_pacienteId);
    }

    [RelayCommand]
    private void FecharFicha() => Voltar?.Invoke();
}
