using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Uma linha da tabela de lotes (dados prontos para exibição).</summary>
public sealed record LoteLinha(
    int Id,
    int Numero,
    DateOnly DataGeracao,
    string StatusRotulo,
    int Guias,
    int Glosadas,
    DateOnly? DataEnvio,
    string? Protocolo,
    DateOnly? DataRetorno,
    bool PodeMarcarEnviado,
    bool PodeRegistrarRetorno);

/// <summary>
/// Guias TISS: exporta o lote (XML 4.01), mantém o histórico de lotes e acompanha o
/// ciclo gerado → enviado (protocolo) → processado (demonstrativo com glosas guia a guia).
/// </summary>
public partial class TissViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    public ObservableCollection<LoteLinha> Lotes { get; } = new();

    public TissViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1);
        _fim = _inicio.AddMonths(1).AddDays(-1);
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var lotes = scope.ServiceProvider.GetRequiredService<LoteTissService>();

        Lotes.Clear();
        foreach (var l in await lotes.ListarAsync())
            Lotes.Add(ParaLinha(l));
    }

    private static LoteLinha ParaLinha(LoteTiss l) => new(
        l.Id, l.Numero, l.DataGeracao,
        l.Status switch
        {
            StatusLoteTiss.Gerado => "Gerado",
            StatusLoteTiss.Enviado => "Enviado",
            StatusLoteTiss.Processado => "Processado",
            _ => l.Status.ToString()
        },
        l.Codigos.Count,
        l.Codigos.Count(c => c.Glosa != StatusGlosa.SemGlosa),
        l.DataEnvio, l.ProtocoloOperadora, l.DataRetorno,
        PodeMarcarEnviado: l.Status == StatusLoteTiss.Gerado,
        PodeRegistrarRetorno: l.Status == StatusLoteTiss.Enviado);

    /// <summary>Gera um NOVO lote com as guias baixadas do período que ainda não foram exportadas.</summary>
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
        var lotes = scope.ServiceProvider.GetRequiredService<LoteTissService>();
        var tiss = scope.ServiceProvider.GetRequiredService<TissExportService>();
        var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

        var inicio = DateOnly.FromDateTime(Inicio);
        var fim = DateOnly.FromDateTime(Fim);

        var candidatas = await lotes.CandidatasAsync(inicio, fim);
        if (candidatas.Count == 0)
        {
            Mensagem = "Nenhuma guia baixada nova no período — as já exportadas ficam nos lotes abaixo.";
            return;
        }

        // Pré-validação: operadoras rejeitam lotes com dados obrigatórios em branco.
        var dados = await parametros.ObterPrestadorAsync();
        var pendencias = tiss.ValidarPrestador(dados, candidatas.Select(c => c.Tipo));
        if (pendencias.Count > 0 &&
            !_dialogo.ConfirmarPerigo("Dados incompletos para o TISS",
                "O lote será gerado com pendências que a operadora pode rejeitar:\n\n• " +
                string.Join("\n• ", pendencias) +
                "\n\nCorrija na tela Configurações (Clínica/prestador e Códigos TUSS) ou exporte mesmo assim.\n\nExportar mesmo assim?"))
        {
            Mensagem = "Exportação cancelada — complete as Configurações.";
            return;
        }

        // Escolhe o destino ANTES de criar o lote (cancelar aqui não consome número).
        var numeroPrevisto = await parametros.ObterProximoNumeroLoteTissAsync();
        var dialog = new SaveFileDialog
        {
            FileName = $"TISS-Lote-{numeroPrevisto:000000}-{Fim:yyyy-MM}.xml",
            Filter = "Arquivo TISS (*.xml)|*.xml",
            DefaultExt = ".xml"
        };
        if (dialog.ShowDialog() != true) return;

        var lote = await lotes.CriarAsync(inicio, fim, dados.RegistroAnsOperadora);
        if (lote is null) return; // corrida improvável: outra máquina exportou no meio-tempo

        var xml = tiss.GerarLoteXml(lote.Codigos, dados, lote.Numero.ToString());
        await File.WriteAllTextAsync(dialog.FileName, xml);

        // Arquiva uma cópia do lote (histórico auditável do que foi enviado à operadora).
        var copia = ArquivarCopiaLote(xml, lote.Numero);

        Mensagem = $"Lote nº {lote.Numero} gerado com {lote.Codigos.Count} guia(s): {dialog.FileName}" +
                   (copia is null ? string.Empty : $" (cópia arquivada em {copia})");
        await CarregarAsync();
    }

    /// <summary>Regenera o XML de um lote já criado (reenvio/2ª via).</summary>
    [RelayCommand]
    private async Task BaixarXml(LoteLinha? linha)
    {
        if (linha is null) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var lotes = scope.ServiceProvider.GetRequiredService<LoteTissService>();
            var tiss = scope.ServiceProvider.GetRequiredService<TissExportService>();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

            var lote = await lotes.ObterAsync(linha.Id);
            var dados = await parametros.ObterPrestadorAsync();
            var xml = tiss.GerarLoteXml(lote.Codigos, dados, lote.Numero.ToString());

            var dialog = new SaveFileDialog
            {
                FileName = $"TISS-Lote-{lote.Numero:000000}.xml",
                Filter = "Arquivo TISS (*.xml)|*.xml",
                DefaultExt = ".xml"
            };
            if (dialog.ShowDialog() != true) return;
            await File.WriteAllTextAsync(dialog.FileName, xml);
            Mensagem = $"XML do lote nº {lote.Numero} salvo em {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível regenerar o XML: {ex.Message}";
        }
    }

    /// <summary>Registra que o lote foi entregue à operadora (com o protocolo devolvido).</summary>
    [RelayCommand]
    private async Task MarcarEnviado(LoteLinha? linha)
    {
        if (linha is null) return;
        var janela = new Alertas.EnvioLoteWindow(linha.Numero)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var lotes = scope.ServiceProvider.GetRequiredService<LoteTissService>();
            await lotes.MarcarEnviadoAsync(linha.Id, janela.DataEnvio, janela.Protocolo);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível marcar o envio: {ex.Message}";
        }
    }

    /// <summary>Registra o demonstrativo de análise: guias aceitas x glosadas (motivo ANS).</summary>
    [RelayCommand]
    private async Task RegistrarRetorno(LoteLinha? linha)
    {
        if (linha is null) return;
        try
        {
            LoteTiss lote;
            using (var scope = _scopeFactory.CreateScope())
                lote = await scope.ServiceProvider.GetRequiredService<LoteTissService>().ObterAsync(linha.Id);

            var janela = new Alertas.RetornoLoteWindow(lote.Numero, lote.Codigos)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (janela.ShowDialog() != true) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var lotes = scope.ServiceProvider.GetRequiredService<LoteTissService>();
                await lotes.RegistrarRetornoAsync(linha.Id, janela.DataRetorno, janela.Decisoes, janela.Observacao);
            }

            var glosadas = janela.Decisoes.Count(d => d.Glosada);
            Mensagem = glosadas == 0
                ? $"Retorno do lote nº {linha.Numero} registrado — todas as guias aceitas."
                : $"Retorno do lote nº {linha.Numero} registrado — {glosadas} guia(s) glosada(s) foram para o Controle de glosas, com prazo de recurso correndo.";
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível registrar o retorno: {ex.Message}";
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
    public ICommand? AtalhoAtualizar => new AsyncRelayCommand(CarregarAsync);
}
