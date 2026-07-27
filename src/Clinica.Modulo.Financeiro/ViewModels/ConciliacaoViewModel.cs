using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Guia efetivada no convênio esperando virar dinheiro no caixa.</summary>
public sealed partial class LinhaConciliacao : ObservableObject
{
    public required GuiaSemLancamento Guia { get; init; }
    public required string DataBaixa { get; init; }
    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string NumeroGuia { get; init; }
    public required string Tipo { get; init; }

    /// <summary>Valor a lançar, digitado pela operadora do financeiro.</summary>
    [ObservableProperty]
    private string _valor = string.Empty;
}

/// <summary>
/// Conciliação — a tela onde os dois módulos se encontram: lista as guias que o
/// faturamento já efetivou no convênio e que ainda não têm receita lançada.
/// Ao lançar, o vínculo fica gravado e a guia sai da lista.
/// </summary>
public sealed partial class ConciliacaoViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<LinhaConciliacao> Linhas { get; } = [];

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string _resumo = string.Empty;

    public ConciliacaoViewModel(FinanceiroService financeiro, ISnackbarService snackbar)
    {
        _financeiro = financeiro;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);

            var guias = await _financeiro.GuiasSemLancamentoAsync(inicio, fim);
            Linhas.Clear();
            foreach (var g in guias)
                Linhas.Add(new LinhaConciliacao
                {
                    Guia = g,
                    DataBaixa = g.DataBaixa.ToString("dd/MM/yyyy"),
                    Paciente = g.Paciente,
                    Convenio = g.Convenio.ToString(),
                    NumeroGuia = g.NumeroGuiaReal ?? "—",
                    Tipo = g.Tipo.ToString()
                });

            Resumo = Linhas.Count == 0
                ? "Nenhuma guia pendente de lançamento neste mês."
                : $"{Linhas.Count} guia(s) efetivada(s) sem receita lançada.";
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Não foi possível carregar a conciliação: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Lança a receita da guia com o valor informado na linha.</summary>
    [RelayCommand]
    private async Task LancarAsync(LinhaConciliacao? linha)
    {
        if (linha is null) return;

        if (!decimal.TryParse(linha.Valor, out var valor) || valor <= 0)
        {
            _snackbar.Erro("Informe um valor válido, maior que zero.");
            return;
        }

        try
        {
            await _financeiro.LancarReceitaDaGuiaAsync(linha.Guia, valor);
            _snackbar.Sucesso($"Receita de {valor:C} lançada para {linha.Paciente}.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);
}
