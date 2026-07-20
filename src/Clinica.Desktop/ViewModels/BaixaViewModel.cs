using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Registra a BAIXA de uma guia: data, número real da guia e forma de obtenção. Não trata recebíveis.</summary>
public partial class BaixaViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;
    private int _codigoId;

    [ObservableProperty] private CodigoFaturamento? _codigo;
    [ObservableProperty] private string _pacienteNome = string.Empty;
    [ObservableProperty] private DateTime _dataBaixa = DateTime.Today;
    [ObservableProperty] private string? _numeroGuia;
    [ObservableProperty] private string? _observacao;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    /// <summary>Observação registrada enquanto a guia estava pendente (por que não deu para baixar antes).</summary>
    [ObservableProperty] private string? _observacaoPendencia;
    public bool TemObservacaoPendencia => !string.IsNullOrWhiteSpace(ObservacaoPendencia);
    partial void OnObservacaoPendenciaChanged(string? value) => OnPropertyChanged(nameof(TemObservacaoPendencia));

    public event Action? BaixaConcluida;
    public event Action? Cancelado;

    public BaixaViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public async Task CarregarAsync(int codigoId)
    {
        _codigoId = codigoId;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Codigo = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .FirstOrDefaultAsync(c => c.Id == codigoId);
        PacienteNome = Codigo?.Atendimento?.Paciente?.Nome ?? string.Empty;
        ObservacaoPendencia = Codigo?.ObservacaoPendencia is { } obs && Codigo.ObservacaoPendenciaEm is { } quando
            ? $"{obs}  (anotado em {quando:dd/MM/yyyy HH:mm})"
            : Codigo?.ObservacaoPendencia;
    }

    [RelayCommand]
    private async Task Confirmar()
    {
        if (string.IsNullOrWhiteSpace(NumeroGuia))
        {
            Mensagem = "Informe o número da guia gerada no sistema do convênio.";
            return;
        }

        if (!_dialogo.Confirmar("Confirmar baixa",
            $"Confirmar a baixa da guia {NumeroGuia} de {PacienteNome}?")) return;

        var atendimentoId = Codigo?.AtendimentoId ?? 0;

        // Guarda contra duplo clique: baixa duplicada corrompe o histórico da guia.
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
                await service.DarBaixaAsync(_codigoId, DateOnly.FromDateTime(DataBaixa),
                    NumeroGuia, Environment.UserName, Observacao);
            }
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível registrar a baixa: {ex.Message}";
            return;
        }
        finally
        {
            Ocupado = false;
        }

        await OferecerCapaConclusaoAsync(atendimentoId);

        BaixaConcluida?.Invoke();
    }

    /// <summary>Se esta baixa concluiu a fatura, oferece gerar a capa de conclusão para imprimir e arquivar.</summary>
    private async Task OferecerCapaConclusaoAsync(int atendimentoId)
    {
        if (atendimentoId == 0) return;

        try
        {
            CapaConclusao resultado;
            using (var scope = _scopeFactory.CreateScope())
            {
                var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
                var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
                resultado = await capa.GerarConclusaoAsync(atendimentoId, prestador);
                if (!resultado.Concluido || resultado.Pdf is null) return;
            }

            if (!_dialogo.Confirmar("Fatura concluída",
                "Fatura concluída! Gerar a capa de conclusão para imprimir e arquivar na pasta do dia?")) return;

            var dialog = new SaveFileDialog
            {
                FileName = $"Capa-CONCLUIDA-{resultado.Numero}-{resultado.Data:yyyy-MM-dd}.pdf",
                Filter = "PDF (*.pdf)|*.pdf",
                DefaultExt = ".pdf"
            };
            if (dialog.ShowDialog() != true) return;

            await File.WriteAllBytesAsync(dialog.FileName, resultado.Pdf);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch
        {
            // A geração da capa nunca deve impedir a baixa.
        }
    }

    [RelayCommand]
    private void Cancelar() => Cancelado?.Invoke();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => ConfirmarCommand;
}
