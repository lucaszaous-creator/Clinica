using System.Diagnostics;
using System.IO;
using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// Fluxo compartilhado de (re)impressão da capa de faturamento: gera o PDF do
/// atendimento no estado ATUAL (em andamento ou concluída), pergunta onde salvar
/// e abre o arquivo. Usado no lançamento, na baixa e nas telas de reimpressão
/// (Faturados e Consultar guias).
/// </summary>
public static class CapaImpressao
{
    public static async Task GerarESalvarAsync(
        IServiceScopeFactory scopeFactory, int atendimentoId, string? numeroAtendimento, DateOnly dataAtendimento)
    {
        byte[] pdf;
        using (var scope = scopeFactory.CreateScope())
        {
            var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
            pdf = await capa.GerarPdfAsync(atendimentoId, PrestadorStore.Carregar());
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"Capa-{numeroAtendimento ?? atendimentoId.ToString()}-{dataAtendimento:yyyy-MM-dd}.pdf",
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf"
        };
        if (dialog.ShowDialog() != true) return;

        await File.WriteAllBytesAsync(dialog.FileName, pdf);
        Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
    }
}
