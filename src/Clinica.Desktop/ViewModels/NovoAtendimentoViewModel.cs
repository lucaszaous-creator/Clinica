using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Lança um atendimento. O sistema gera automaticamente os códigos (inclusive o 2º código +24h).</summary>
public partial class NovoAtendimentoViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Paciente> Pacientes { get; } = new();
    public ObservableCollection<CodigoFaturamento> CodigosGerados { get; } = new();
    public ObservableCollection<string> Avisos { get; } = new();

    public Array Modalidades => Enum.GetValues(typeof(ModalidadeAtendimento));

    /// <summary>Opções de qual código sai primeiro (hoje) numa modalidade dupla. Vazio nas simples.</summary>
    public ObservableCollection<TipoCodigo> OpcoesPrimeiroCodigo { get; } = new();

    [ObservableProperty] private string? _busca;
    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private ModalidadeAtendimento _modalidade = ModalidadeAtendimento.AcupunturaComEletro;
    [ObservableProperty] private TipoCodigo? _primeiroCodigo;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _lancado;
    [ObservableProperty] private string? _numeroAtendimento;

    private int _ultimoAtendimentoId;

    /// <summary>Modalidade dupla (gera 1º hoje + 2º em +24h): permite escolher qual código sai primeiro.</summary>
    public bool ModalidadeDupla =>
        Modalidade is ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

    public NovoAtendimentoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        AtualizarOpcoesPrimeiroCodigo();
    }

    partial void OnModalidadeChanged(ModalidadeAtendimento value)
    {
        AtualizarOpcoesPrimeiroCodigo();
        OnPropertyChanged(nameof(ModalidadeDupla));
    }

    /// <summary>Preenche as opções de "qual código primeiro" conforme a modalidade e escolhe o padrão.</summary>
    private void AtualizarOpcoesPrimeiroCodigo()
    {
        OpcoesPrimeiroCodigo.Clear();
        switch (Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaComEletro:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Eletroacupuntura);
                break;
            case ModalidadeAtendimento.BsvComAcupuntura:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Bsv);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                break;
        }
        PrimeiroCodigo = OpcoesPrimeiroCodigo.Count > 0 ? OpcoesPrimeiroCodigo[0] : null;
    }

    public Task CarregarAsync() => BuscarPacientes();

    /// <summary>Busca pacientes por nome ou CPF para o seletor.</summary>
    [RelayCommand]
    private async Task BuscarPacientes()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        Pacientes.Clear();
        foreach (var p in await service.BuscarAsync(Busca))
            Pacientes.Add(p);
    }

    partial void OnBuscaChanged(string? value) => _ = BuscarPacientes();

    // Pré-preenche a modalidade com a habitual do paciente (definida no cadastro).
    partial void OnPacienteSelecionadoChanged(Paciente? value)
    {
        if (value is not null)
            Modalidade = value.ModalidadePreferida;
    }

    [RelayCommand]
    private async Task Lancar()
    {
        if (PacienteSelecionado is null)
            return;

        CodigosGerados.Clear();
        Avisos.Clear();

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
        var resultado = await service.LancarAsync(
            PacienteSelecionado.Id, DateOnly.FromDateTime(Data), Modalidade, Observacoes,
            registrarNaAgenda: true, primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null);

        foreach (var c in resultado.Atendimento.Codigos)
            CodigosGerados.Add(c);
        foreach (var a in resultado.Avisos)
            Avisos.Add(a);

        _ultimoAtendimentoId = resultado.Atendimento.Id;
        NumeroAtendimento = resultado.Atendimento.Numero;
        Lancado = true;
    }

    /// <summary>Gera a capa de faturamento (PDF) do atendimento recém-lançado e abre o arquivo.</summary>
    [RelayCommand]
    private async Task GerarCapa()
    {
        if (_ultimoAtendimentoId == 0) return;

        byte[] pdf;
        using (var scope = _scopeFactory.CreateScope())
        {
            var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
            pdf = await capa.GerarPdfAsync(_ultimoAtendimentoId);
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"Capa-INICIAL-{NumeroAtendimento ?? _ultimoAtendimentoId.ToString()}-{Data:yyyy-MM-dd}.pdf",
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf"
        };
        if (dialog.ShowDialog() != true) return;

        await File.WriteAllBytesAsync(dialog.FileName, pdf);
        Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => LancarCommand;
    public ICommand? AtalhoImprimir => GerarCapaCommand;
}
