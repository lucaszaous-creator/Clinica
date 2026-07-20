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
    public Array Especialidades => Enum.GetValues(typeof(Especialidade));

    /// <summary>Opções de qual código sai primeiro (hoje) numa modalidade dupla. Vazio nas simples.</summary>
    public ObservableCollection<TipoCodigo> OpcoesPrimeiroCodigo { get; } = new();

    [ObservableProperty] private string? _busca;
    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private ModalidadeAtendimento _modalidade = ModalidadeAtendimento.AcupunturaComEletro;
    [ObservableProperty] private Especialidade? _especialidadeConsulta;
    [ObservableProperty] private TipoCodigo? _primeiroCodigo;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _lancado;
    [ObservableProperty] private string? _numeroAtendimento;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    private int _ultimoAtendimentoId;

    /// <summary>Modalidade dupla (gera 1º hoje + 2º em +24h): permite escolher qual código sai primeiro.</summary>
    public bool ModalidadeDupla =>
        Modalidade is ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

    /// <summary>Consulta avulsa: pede a especialidade (discriminada nos relatórios).</summary>
    public bool ModalidadeConsulta => Modalidade == ModalidadeAtendimento.Consulta;

    public NovoAtendimentoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        AtualizarOpcoesPrimeiroCodigo();
    }

    partial void OnModalidadeChanged(ModalidadeAtendimento value)
    {
        AtualizarOpcoesPrimeiroCodigo();
        if (value != ModalidadeAtendimento.Consulta)
            EspecialidadeConsulta = null;
        OnPropertyChanged(nameof(ModalidadeDupla));
        OnPropertyChanged(nameof(ModalidadeConsulta));
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

    // Pré-preenche a modalidade com a habitual do paciente (definida no cadastro)
    // e avisa carteirinha vencida ANTES de gerar uma guia que o convênio vai recusar.
    partial void OnPacienteSelecionadoChanged(Paciente? value)
    {
        if (value is null) return;

        Modalidade = value.ModalidadePreferida;
        Mensagem = value.ValidadeCarteirinha is { } val && val < DateOnly.FromDateTime(DateTime.Today)
            ? $"Atenção: a carteirinha de {value.Nome} venceu em {val:dd/MM/yyyy} — o convênio pode recusar a guia."
            : null;
    }

    [RelayCommand]
    private async Task Lancar()
    {
        if (PacienteSelecionado is null)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (ModalidadeConsulta && EspecialidadeConsulta is null)
        {
            Mensagem = "Informe a especialidade da consulta.";
            return;
        }

        // Guarda contra duplo clique: dois lançamentos gerariam códigos duplicados.
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            CodigosGerados.Clear();
            Avisos.Clear();
            Mensagem = null;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            var resultado = await service.LancarAsync(
                PacienteSelecionado.Id, DateOnly.FromDateTime(Data), Modalidade, Observacoes,
                registrarNaAgenda: true, primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null,
                especialidadeConsulta: ModalidadeConsulta ? EspecialidadeConsulta : null);

            foreach (var c in resultado.Atendimento.Codigos)
                CodigosGerados.Add(c);
            foreach (var a in resultado.Avisos)
                Avisos.Add(a);

            _ultimoAtendimentoId = resultado.Atendimento.Id;
            NumeroAtendimento = resultado.Atendimento.Numero;
            Lancado = true;
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível lançar o atendimento: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Gera a capa de faturamento (PDF) do atendimento recém-lançado e abre o arquivo.</summary>
    [RelayCommand]
    private async Task GerarCapa()
    {
        if (_ultimoAtendimentoId == 0) return;

        byte[] pdf;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            pdf = await capa.GerarPdfAsync(_ultimoAtendimentoId, prestador);
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar a capa: {ex.Message}";
            return;
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
