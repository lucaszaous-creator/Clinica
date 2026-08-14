using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>
/// Produção do período: volume de códigos por mês, quanto foi efetivado (baixado)
/// e quanto continua pendente.
///
/// É a base de conferência do faturamento e a matéria-prima do repasse por
/// profissional. Trabalha só com CONTAGENS — nenhum valor monetário, coerente com
/// o domínio atual (ver CLAUDE.md: faturamento ≠ recebíveis).
/// </summary>
public sealed partial class ProducaoViewModel : ObservableObject
{
    private readonly RelatorioService _relatorios;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<ResumoMensal> Meses { get; } = [];

    /// <summary>Quantos meses o comparativo cobre.</summary>
    [ObservableProperty]
    private int _janelaMeses = 6;

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private int _totalCodigos;

    [ObservableProperty]
    private int _totalBaixados;

    [ObservableProperty]
    private int _totalPendentes;

    [ObservableProperty]
    private string _taxaBaixaFormatada = "—";

    public ProducaoViewModel(RelatorioService relatorios, ISnackbarService snackbar)
    {
        _relatorios = relatorios;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    partial void OnJanelaMesesChanged(int value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var meses = await _relatorios.ComparativoMensalAsync(hoje, JanelaMeses);

            Meses.Clear();
            foreach (var m in meses) Meses.Add(m);

            TotalCodigos = meses.Sum(m => m.TotalCodigos);
            TotalBaixados = meses.Sum(m => m.Baixados);
            TotalPendentes = meses.Sum(m => m.Pendentes);
            TaxaBaixaFormatada = TotalCodigos > 0
                ? $"{(double)TotalBaixados / TotalCodigos:P1}"
                : "—";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — produção não pôde ser carregada", ex);
            _snackbar.Erro($"Não foi possível carregar a produção: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void Ultimos6Meses() => JanelaMeses = 6;

    [RelayCommand]
    private void Ultimos12Meses() => JanelaMeses = 12;
}
