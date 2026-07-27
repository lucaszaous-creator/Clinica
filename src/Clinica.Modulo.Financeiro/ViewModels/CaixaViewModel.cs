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

    /// <summary>Previsto: ainda dá para marcar como pago/recebido.</summary>
    public required bool PodeRealizar { get; init; }

    /// <summary>Cancelado não se cancela de novo (e continua no histórico).</summary>
    public required bool PodeCancelar { get; init; }
}

/// <summary>
/// Caixa do mês: entradas, saídas e saldo. Lançamentos vindos de guias do
/// faturamento aparecem marcados, para deixar visível o elo entre os módulos.
/// </summary>
public sealed partial class CaixaViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

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

    public CaixaViewModel(FinanceiroService financeiro, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _financeiro = financeiro;
        _snackbar = snackbar;
        _dialogo = dialogo;
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
                    EhEntrada = entrada,
                    PodeRealizar = l.Status == StatusLancamento.Previsto,
                    PodeCancelar = l.Status != StatusLancamento.Cancelado
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
            Clinica.Application.Diagnostico.Registrar("Financeiro — caixa não pôde ser carregado", ex);
            _snackbar.Erro($"Não foi possível carregar o caixa: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Abre o formulário do lançamento manual. É a entrada do dinheiro que o faturamento
    /// não conhece (aluguel, material, recebimento avulso) — o que vem de guia continua
    /// entrando pela Conciliação, já vinculado.
    /// </summary>
    [RelayCommand]
    private async Task NovoLancamentoAsync()
    {
        var janela = new Janelas.LancamentoWindow(new LancamentoEdicaoViewModel(_financeiro))
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Lançamento registrado.");
        await CarregarAsync();
    }

    /// <summary>Marca um lançamento previsto como efetivamente pago/recebido.</summary>
    [RelayCommand]
    private async Task RealizarAsync(LinhaCaixa? linha)
    {
        if (linha is null) return;

        try
        {
            await _financeiro.RealizarAsync(linha.Id, operador: Environment.UserName);
            _snackbar.Sucesso("Lançamento realizado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — lançamento não pôde ser realizado", ex);
            _snackbar.Erro($"Não foi possível realizar: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancela um lançamento. Nunca apaga — sai dos totais e fica no histórico com o
    /// motivo, que o serviço exige.
    /// </summary>
    [RelayCommand]
    private async Task CancelarAsync(LinhaCaixa? linha)
    {
        if (linha is null) return;

        var motivo = _dialogo.PerguntarTexto(
            "Cancelar lançamento",
            $"Por que \"{linha.Descricao}\" está sendo cancelado? O lançamento sai dos totais mas continua no histórico.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            await _financeiro.CancelarAsync(linha.Id, motivo, operador: Environment.UserName);
            _snackbar.Sucesso("Lançamento cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — lançamento não pôde ser cancelado", ex);
            _snackbar.Erro($"Não foi possível cancelar: {ex.Message}");
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
