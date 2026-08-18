using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<ResumoMensal> Meses { get; } = [];

    /// <summary>Quantos meses o comparativo cobre.</summary>
    [ObservableProperty]
    private int _janelaMeses = 6;

    [ObservableProperty]
    private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty]
    private bool _naoVerificado;

    [ObservableProperty]
    private int _totalCodigos;

    [ObservableProperty]
    private int _totalBaixados;

    [ObservableProperty]
    private int _totalPendentes;

    [ObservableProperty]
    private string _taxaBaixaFormatada = "—";

    /// <summary>
    /// ⚠️ Nada de serviço SCOPED no construtor — o shell resolve esta tela do provedor
    /// RAIZ, e Scoped pedido à raiz vive pela vida inteira do app, com o `DbContext`
    /// junto (parcela 69). Escopo por operação. Ver a checagem 37 do verificar-suite.
    /// </summary>
    public ProducaoViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    partial void OnJanelaMesesChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): alternar 6 ↔ 12 meses rápido
    /// deixaria os totais de uma janela sob o botão marcado da outra — num banco remoto
    /// a leitura mais velha pode responder por último.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            using var escopo = _escopos.CreateScope();
            var meses = await escopo.ServiceProvider.GetRequiredService<RelatorioService>()
                .ComparativoMensalAsync(hoje, JanelaMeses);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

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
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            _snackbar.Erro($"Não foi possível carregar a produção: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    [RelayCommand]
    private void Ultimos6Meses() => JanelaMeses = 6;

    [RelayCommand]
    private void Ultimos12Meses() => JanelaMeses = 12;
}
