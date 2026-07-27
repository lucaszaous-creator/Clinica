using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma linha do caixa, já formatada para a tela.</summary>
public sealed class LinhaCaixa
{
    public required int Id { get; init; }
    public required string Data { get; init; }
    public required string Descricao { get; init; }
    public required string Categoria { get; init; }
    public required string ValorFormatado { get; init; }
    public required string StatusRotulo { get; init; }

    /// <summary>Vínculo com o faturamento, quando a receita nasceu de uma guia.</summary>
    public required bool VeioDoFaturamento { get; init; }

    public required bool EhEntrada { get; init; }
}

/// <summary>
/// Caixa do mês: entradas, saídas e saldo. Lançamentos vindos de guias do
/// faturamento aparecem marcados, para deixar visível o elo entre os módulos.
/// </summary>
public sealed partial class CaixaViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<LinhaCaixa> Linhas { get; } = [];

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string _entradas = "—";

    [ObservableProperty]
    private string _saidas = "—";

    [ObservableProperty]
    private string _saldo = "—";

    [ObservableProperty]
    private string _previsto = "—";

    public CaixaViewModel(FinanceiroService financeiro, ISnackbarService snackbar)
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

            var lancamentos = await _financeiro.DoPeriodoAsync(inicio, fim);
            Linhas.Clear();
            foreach (var l in lancamentos)
            {
                var entrada = l.Tipo == TipoLancamento.Entrada;
                Linhas.Add(new LinhaCaixa
                {
                    Id = l.Id,
                    Data = l.Data.ToString("dd/MM"),
                    Descricao = l.Descricao,
                    Categoria = l.Categoria?.Nome ?? "—",
                    ValorFormatado = $"{(entrada ? "+" : "-")} {l.Valor:C}",
                    StatusRotulo = Rotular(l.Status),
                    VeioDoFaturamento = l.CodigoFaturamentoId is not null,
                    EhEntrada = entrada
                });
            }

            var resumo = await _financeiro.ResumoAsync(inicio, fim);
            Entradas = $"{resumo.EntradasRealizadas:C}";
            Saidas = $"{resumo.SaidasRealizadas:C}";
            Saldo = $"{resumo.SaldoRealizado:C}";
            Previsto = $"{resumo.SaldoPrevisto:C}";
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Não foi possível carregar o caixa: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);

    private static string Rotular(StatusLancamento status) => status switch
    {
        StatusLancamento.Previsto => "Previsto",
        StatusLancamento.Realizado => "Realizado",
        StatusLancamento.Cancelado => "Cancelado",
        _ => status.ToString()
    };
}
