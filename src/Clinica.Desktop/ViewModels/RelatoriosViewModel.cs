using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Relatórios: taxa de baixa (métrica-chave), faturamento por convênio e envelhecimento das pendências.</summary>
public partial class RelatoriosViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private ResumoFaturamento? _resumo;

    public ObservableCollection<FaturamentoPorConvenio> PorConvenio { get; } = new();
    public ObservableCollection<FaixaEnvelhecimento> Envelhecimento { get; } = new();

    public RelatoriosViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1); // início do mês corrente
        _fim = _inicio.AddMonths(1).AddDays(-1);           // fim do mês corrente
    }

    public Task CarregarAsync() => Gerar();

    [RelayCommand]
    private async Task Gerar()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RelatorioService>();

        var rel = await service.GerarAsync(
            DateOnly.FromDateTime(Inicio),
            DateOnly.FromDateTime(Fim),
            DateOnly.FromDateTime(DateTime.Today));

        Resumo = rel.Resumo;

        PorConvenio.Clear();
        foreach (var c in rel.PorConvenio) PorConvenio.Add(c);

        Envelhecimento.Clear();
        foreach (var f in rel.Envelhecimento) Envelhecimento.Add(f);
    }

    /// <summary>Exporta o relatório atual em CSV (compatível com Excel pt-BR: ';' e BOM UTF-8).</summary>
    [RelayCommand]
    private async Task ExportarCsv()
    {
        if (Resumo is null || PorConvenio.Count == 0)
            await Gerar();

        var dialog = new SaveFileDialog
        {
            FileName = $"Relatorio-Faturamento-{Inicio:yyyy-MM-dd}-a-{Fim:yyyy-MM-dd}.csv",
            Filter = "CSV (*.csv)|*.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Relatório de faturamento;{Inicio:dd/MM/yyyy} a {Fim:dd/MM/yyyy}");
        sb.AppendLine();
        sb.AppendLine("Resumo;Códigos gerados;Baixados;Pendentes;Taxa de baixa (%)");
        sb.AppendLine($";{Resumo?.TotalCodigos};{Resumo?.Baixados};{Resumo?.Pendentes};{Resumo?.TaxaBaixa:0.#}");
        sb.AppendLine();
        sb.AppendLine("Faturamento por convênio");
        sb.AppendLine("Convênio;Gerados;Baixados;Pendentes;Taxa de baixa (%)");
        foreach (var c in PorConvenio)
            sb.AppendLine($"{ConvenioInfo.NomeExibicao(c.Convenio)};{c.TotalCodigos};{c.Baixados};{c.Pendentes};{c.TaxaBaixa:0.#}");
        sb.AppendLine();
        sb.AppendLine("Pendências em aberto (envelhecimento)");
        sb.AppendLine("Faixa;Quantidade");
        foreach (var f in Envelhecimento)
            sb.AppendLine($"{f.Faixa};{f.Quantidade}");

        // BOM garante acentos corretos ao abrir direto no Excel.
        await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => GerarCommand;
    public ICommand? AtalhoImprimir => ExportarCsvCommand;
}
