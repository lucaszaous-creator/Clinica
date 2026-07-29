using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
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

    /// <summary>
    /// O que a operadora vai reter deste valor, calculado ENQUANTO se digita (parcela 18).
    /// Aparece antes de gravar porque é aí que dá para conferir contra o demonstrativo —
    /// descobrir a retenção depois transformaria a conferência num estorno.
    /// </summary>
    [ObservableProperty]
    private string? _retencao;
}

/// <summary>
/// Conciliação — a tela onde os dois módulos se encontram: lista as guias que o
/// faturamento já efetivou no convênio e que ainda não têm receita lançada.
/// Ao lançar, o vínculo fica gravado e a guia sai da lista.
/// </summary>
public sealed partial class ConciliacaoViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;
    private readonly TaxaService _taxas;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<LinhaConciliacao> Linhas { get; } = [];

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string _resumo = string.Empty;

    public ConciliacaoViewModel(
        FinanceiroService financeiro, TaxaService taxas, ISnackbarService snackbar)
    {
        _financeiro = financeiro;
        _taxas = taxas;
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

        // Valores.TentarLerDecimal, e não decimal.TryParse: é o leitor do projeto, que
        // aceita "1.250,00" e "1250.00" sem depender da cultura da máquina.
        if (!Valores.TentarLerDecimal(linha.Valor, out var valor) || valor <= 0)
        {
            _snackbar.Erro("Informe um valor válido, maior que zero.");
            return;
        }

        try
        {
            // A RETENÇÃO na fonte da operadora (parcela 18). O valor do lançamento
            // continua sendo o BRUTO da guia; o retido é dedução ao lado, e o líquido é
            // calculado — mesma regra da maquininha.
            var deducoes = await _taxas.CalcularAsync(
                valor, linha.Guia.DataBaixa, FormaPagamento.Convenio,
                reterImposto: true, convenioCodigo: linha.Guia.ConvenioCodigo);

            await _financeiro.LancarReceitaDaGuiaAsync(linha.Guia, valor, deducoes: deducoes);

            _snackbar.Sucesso(deducoes.ValorImposto is { } retido and > 0m
                ? $"Receita de {valor:C} lançada para {linha.Paciente} — {retido:C} retidos na fonte."
                : $"Receita de {valor:C} lançada para {linha.Paciente}.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    /// <summary>
    /// Calcula a retenção da linha sem gravar nada — a mesma conta que o Lançar vai usar,
    /// para não haver duas respostas para a mesma pergunta.
    /// </summary>
    [RelayCommand]
    private async Task PreverAsync(LinhaConciliacao? linha)
    {
        if (linha is null) return;

        try
        {
            linha.Retencao = null;
            if (!Valores.TentarLerDecimal(linha.Valor, out var valor) || valor <= 0) return;

            var d = await _taxas.CalcularAsync(
                valor, linha.Guia.DataBaixa, FormaPagamento.Convenio,
                reterImposto: true, convenioCodigo: linha.Guia.ConvenioCodigo);

            // Sem retenção cadastrada não se inventa desconto — mas a linha DIZ que não
            // achou, senão o líquido igual ao bruto passaria por retenção zero.
            linha.Retencao = d.ValorImposto is { } retido and > 0m
                ? $"retém {retido:C} ({d.DetalheImposto}) · líquido {valor - retido:C}"
                : "sem retenção cadastrada para este convênio";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — prévia da retenção falhou", ex);
            linha.Retencao = null;
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);
}
