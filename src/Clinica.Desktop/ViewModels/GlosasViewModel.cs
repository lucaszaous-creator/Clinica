using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Uma glosa na tabela, com o prazo de recurso calculado (semáforo).</summary>
public sealed record GlosaLinha(
    CodigoFaturamento Codigo,
    string MotivoAns,
    DateOnly? PrazoRecurso,
    int? DiasParaFimPrazo,
    NivelUrgencia Urgencia)
{
    public string PrazoRotulo => PrazoRecurso is null
        ? string.Empty
        : DiasParaFimPrazo < 0
            ? $"{PrazoRecurso:dd/MM/yyyy} (vencido)"
            : $"{PrazoRecurso:dd/MM/yyyy} ({DiasParaFimPrazo} d)";
}

/// <summary>Um motivo que se repete nas glosas da clínica, com quantas vezes apareceu.</summary>
public sealed record MotivoRecorrente(string Codigo, string Descricao, int Ocorrencias, string ComoEvitar)
{
    public string Rotulo => $"{Ocorrencias}× · {Codigo} — {Descricao}";
}

/// <summary>
/// Controle de glosas: acompanha guias recusadas com o prazo de recurso no semáforo,
/// reapresenta, marca recuperadas e gera o XML de recurso de glosa (TISS).
/// </summary>
public partial class GlosasViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<GlosaLinha> Glosas { get; } = new();

    /// <summary>
    /// Os motivos que mais glosam esta clínica, do maior para o menor. A cliente dizia
    /// nunca saber por que as guias caem; ver o próprio padrão é o primeiro passo para
    /// atacar a causa em vez de recorrer caso a caso.
    /// </summary>
    public ObservableCollection<MotivoRecorrente> MotivosRecorrentes { get; } = new();

    /// <summary>Quando true, mostra só as glosas ainda não recuperadas.</summary>
    [ObservableProperty] private bool _somenteEmAberto = true;

    [ObservableProperty] private string? _mensagem;

    public GlosasViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public Task CarregarAsync() => Buscar();

    partial void OnSomenteEmAbertoChanged(bool value) => _ = Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Glosas.Clear();
        foreach (var g in await glosas.ListarAsync(SomenteEmAberto))
        {
            var dias = g.DiasParaFimRecurso(hoje);
            Glosas.Add(new GlosaLinha(
                g,
                MotivosGlosa.Descricao(g.MotivoGlosaCodigo),
                g.DataLimiteRecurso,
                dias,
                dias is null ? NivelUrgencia.Verde
                    : dias <= 3 ? NivelUrgencia.Vermelho
                    : dias <= 7 ? NivelUrgencia.Amarelo
                    : NivelUrgencia.Verde));
        }

        MontarMotivosRecorrentes();
    }

    /// <summary>Ranking dos motivos da própria clínica — o padrão que dá para atacar.</summary>
    private void MontarMotivosRecorrentes()
    {
        MotivosRecorrentes.Clear();
        var ranking = Glosas
            .Where(g => !string.IsNullOrWhiteSpace(g.Codigo.MotivoGlosaCodigo))
            .GroupBy(g => g.Codigo.MotivoGlosaCodigo!)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(3);

        foreach (var grupo in ranking)
        {
            var motivo = MotivosGlosa.Obter(grupo.Key);
            MotivosRecorrentes.Add(new MotivoRecorrente(
                grupo.Key,
                motivo?.Descricao ?? grupo.Key,
                grupo.Count(),
                motivo?.ComoEvitar ?? string.Empty));
        }
    }

    /// <summary>Abre o guia com o que cada motivo de glosa significa e como evitá-lo.</summary>
    [RelayCommand]
    private void AbrirGuia(string? codigo)
    {
        var janela = new Alertas.GuiaGlosasWindow(codigo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        janela.ShowDialog();
    }

    [RelayCommand]
    private async Task Reapresentar(GlosaLinha? linha)
    {
        if (linha is null) return;
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
                await glosas.ReapresentarAsync(linha.Codigo.Id, DateOnly.FromDateTime(DateTime.Today), Environment.UserName);
            }
            await Buscar();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível reapresentar: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Recuperar(GlosaLinha? linha)
    {
        if (linha is null) return;
        if (!_dialogo.Confirmar("Confirmar",
            "Marcar esta glosa como recuperada (aceita pelo convênio)?")) return;

        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
                await glosas.MarcarRecuperadaAsync(linha.Codigo.Id, Environment.UserName);
            }
            await Buscar();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível marcar como recuperada: {ex.Message}";
        }
    }

    /// <summary>Gera o XML de recurso de glosa (TISS) com todas as glosas em aberto da lista.</summary>
    [RelayCommand]
    private async Task GerarRecursoXml()
    {
        var emAberto = Glosas.Where(l => l.Codigo.GlosaEmAberto).Select(l => l.Codigo).ToList();
        if (emAberto.Count == 0)
        {
            Mensagem = "Nenhuma glosa em aberto para recorrer.";
            return;
        }

        try
        {
            string xml;
            using (var scope = _scopeFactory.CreateScope())
            {
                var tiss = scope.ServiceProvider.GetRequiredService<TissExportService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                var dados = await parametros.ObterPrestadorAsync();
                var numero = DateTime.Now.ToString("yyyyMMddHHmm"); // identificador do recurso
                xml = tiss.GerarRecursoGlosaXml(emAberto, dados, numero);
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"TISS-Recurso-Glosa-{DateTime.Today:yyyy-MM-dd}.xml",
                Filter = "Arquivo TISS (*.xml)|*.xml",
                DefaultExt = ".xml"
            };
            if (dialog.ShowDialog() != true) return;

            await File.WriteAllTextAsync(dialog.FileName, xml);
            Mensagem = $"Recurso de glosa gerado com {emAberto.Count} guia(s): {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar o recurso: {ex.Message}";
        }
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
    public ICommand? AtalhoImprimir => GerarRecursoXmlCommand;
}
