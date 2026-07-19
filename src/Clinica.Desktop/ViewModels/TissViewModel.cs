using System.Windows.Input;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Exportação do lote de guias no formato TISS (XML). Os dados do prestador e os
/// códigos TUSS são configurações globais (tela Configurações).
/// </summary>
public partial class TissViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    public TissViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1);
        _fim = _inicio.AddMonths(1).AddDays(-1);
    }

    public Task CarregarAsync() => Task.CompletedTask;

    [RelayCommand]
    private async Task Exportar()
    {
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            await ExportarInterno();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar o lote: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    private async Task ExportarInterno()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var tiss = scope.ServiceProvider.GetRequiredService<TissExportService>();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

        var inicio = DateOnly.FromDateTime(Inicio);
        var fim = DateOnly.FromDateTime(Fim);
        var codigos = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null && c.Atendimento!.Data >= inicio && c.Atendimento!.Data <= fim)
            .ToListAsync();

        if (codigos.Count == 0)
        {
            Mensagem = "Nenhuma guia faturada no período.";
            return;
        }

        // Pré-validação: operadoras rejeitam lotes com dados obrigatórios em branco.
        var dados = await parametros.ObterPrestadorAsync();
        var pendencias = tiss.ValidarPrestador(dados, codigos.Select(c => c.Tipo));
        if (pendencias.Count > 0 &&
            !_dialogo.ConfirmarPerigo("Dados incompletos para o TISS",
                "O lote será gerado com pendências que a operadora pode rejeitar:\n\n• " +
                string.Join("\n• ", pendencias) +
                "\n\nCorrija na tela Configurações (Clínica/prestador e Códigos TUSS) ou exporte mesmo assim.\n\nExportar mesmo assim?"))
        {
            Mensagem = "Exportação cancelada — complete as Configurações.";
            return;
        }

        // Número de lote SEQUENCIAL (exigência do padrão TISS; timestamp não serve).
        var numeroLote = await parametros.ObterProximoNumeroLoteTissAsync();
        var xml = tiss.GerarLoteXml(codigos, dados, numeroLote.ToString());

        var dialog = new SaveFileDialog
        {
            FileName = $"TISS-Lote-{numeroLote:000000}-{Fim:yyyy-MM}.xml",
            Filter = "Arquivo TISS (*.xml)|*.xml",
            DefaultExt = ".xml"
        };
        if (dialog.ShowDialog() == true)
        {
            await File.WriteAllTextAsync(dialog.FileName, xml);

            // Arquiva uma cópia do lote (histórico auditável do que foi enviado à operadora)
            // e só então consome o número da sequência.
            var copia = ArquivarCopiaLote(xml, numeroLote);
            await parametros.ConfirmarNumeroLoteTissAsync(numeroLote);

            Mensagem = $"Lote nº {numeroLote} gerado com {codigos.Count} guia(s): {dialog.FileName}" +
                       (copia is null ? string.Empty : $" (cópia arquivada em {copia})");
        }
    }

    /// <summary>Guarda uma cópia de cada lote exportado em %APPDATA%\ClinicaFaturamento\tiss.</summary>
    private static string? ArquivarCopiaLote(string xml, int numeroLote)
    {
        try
        {
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClinicaFaturamento", "tiss");
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, $"TISS-Lote-{numeroLote:000000}-{DateTime.Now:yyyy-MM-dd-HHmm}.xml");
            File.WriteAllText(arquivo, xml);
            return arquivo;
        }
        catch (Exception ex)
        {
            Configuracao.LogErros.Registrar("Arquivar cópia do lote TISS", ex);
            return null; // cópia é conveniência; a exportação principal já foi salva
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoImprimir => ExportarCommand;
}
