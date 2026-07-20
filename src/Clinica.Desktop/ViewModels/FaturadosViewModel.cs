using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Alertas;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Guias já baixadas no período, com a opção de ESTORNAR (reabrir a pendência).</summary>
public partial class FaturadosViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<CodigoFaturamento> Baixados { get; } = new();

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private string? _mensagem;

    public FaturadosViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1);
        _fim = _inicio.AddMonths(1).AddDays(-1);
    }

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var lista = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null
                        && c.DataBaixa >= DateOnly.FromDateTime(Inicio)
                        && c.DataBaixa <= DateOnly.FromDateTime(Fim))
            .OrderByDescending(c => c.DataBaixa)
            .ToListAsync();

        Baixados.Clear();
        foreach (var c in lista) Baixados.Add(c);
    }

    [RelayCommand]
    private async Task Estornar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        if (!_dialogo.Confirmar("Confirmar estorno",
            $"Estornar a baixa desta guia de {codigo.Atendimento?.Paciente?.Nome}?\n\n" +
            "A pendência voltará a aparecer no painel para ser faturada novamente.")) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.EstornarBaixaAsync(codigo.Id, "estorno pela tela de Faturados", Environment.UserName);

        await Buscar();
    }

    [RelayCommand]
    private async Task Glosar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        var descricao = $"{codigo.Atendimento?.Paciente?.Nome} — {codigo.Tipo} (guia {codigo.NumeroGuiaReal})";
        int prazo;
        using (var scopePrazo = _scopeFactory.CreateScope())
            prazo = await scopePrazo.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrazoRecursoGlosaAsync();

        var dialog = new GlosaWindow(descricao, prazo) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        using var scope = _scopeFactory.CreateScope();
        var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
        await glosas.RegistrarAsync(codigo.Id, dialog.DataGlosa, dialog.Motivo, dialog.MotivoCodigo, Environment.UserName);

        await Buscar();
    }

    /// <summary>Guia TISS impressa (PDF, leiaute ANS) do atendimento desta guia.</summary>
    [RelayCommand]
    private async Task GuiaPdf(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        try
        {
            byte[] pdf;
            using (var scope = _scopeFactory.CreateScope())
            {
                var guia = scope.ServiceProvider.GetRequiredService<GuiaTissPdfService>();
                var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
                pdf = await guia.GerarPdfAsync(codigo.AtendimentoId, prestador);
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"Guia-TISS-{codigo.Atendimento?.Numero ?? codigo.AtendimentoId.ToString()}-{codigo.Atendimento?.Data:yyyy-MM-dd}.pdf",
                Filter = "PDF (*.pdf)|*.pdf",
                DefaultExt = ".pdf"
            };
            if (dialog.ShowDialog() != true) return;

            await File.WriteAllBytesAsync(dialog.FileName, pdf);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar a guia: {ex.Message}";
        }
    }

    /// <summary>Reimpressão da capa de faturamento do atendimento desta guia (estado atual).</summary>
    [RelayCommand]
    private async Task Capa(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        await Configuracao.CapaImpressao.GerarESalvarAsync(
            _scopeFactory, codigo.AtendimentoId, codigo.Atendimento?.Numero, codigo.Atendimento?.Data ?? default);
    }

    /// <summary>Exporta as guias do período em CSV (Excel pt-BR: ';' e BOM UTF-8) para conferência com o convênio.</summary>
    [RelayCommand]
    private async Task ExportarCsv()
    {
        if (Baixados.Count == 0)
        {
            Mensagem = "Nada a exportar — busque um período com guias baixadas.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"Faturados-{Inicio:yyyy-MM-dd}-a-{Fim:yyyy-MM-dd}.csv",
            Filter = "CSV (*.csv)|*.csv",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Paciente;Convênio;Código;Ordem;Nº guia;Baixado em;Glosa");
            foreach (var c in Baixados)
                sb.AppendLine(string.Join(';',
                    c.Atendimento?.Paciente?.Nome,
                    ConvenioInfo.NomeExibicao(c.Atendimento?.Paciente?.Convenio ?? default),
                    c.Tipo,
                    c.Ordem,
                    c.NumeroGuiaReal,
                    c.DataBaixa?.ToString("dd/MM/yyyy"),
                    c.Glosa));

            // BOM garante acentos corretos ao abrir direto no Excel.
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            Mensagem = $"Exportado: {Baixados.Count} guia(s) em {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível exportar: {ex.Message}";
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
    public ICommand? AtalhoImprimir => ExportarCsvCommand;
}
