using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Clinica.Application.Servicos;
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

/// <summary>Lança um atendimento. O sistema gera automaticamente os códigos (inclusive o 2º código +24h).</summary>
public partial class NovoAtendimentoViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Paciente> Pacientes { get; } = new();
    public ObservableCollection<CodigoFaturamento> CodigosGerados { get; } = new();
    public ObservableCollection<string> Avisos { get; } = new();

    /// <summary>Modalidades ativas do catálogo (embutidas + variantes criadas pela clínica).</summary>
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();

    /// <summary>Especialidades ativas do catálogo (para a consulta avulsa).</summary>
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    /// <summary>Opções de qual código sai primeiro (hoje) numa modalidade dupla. Vazio nas simples.</summary>
    public ObservableCollection<TipoCodigo> OpcoesPrimeiroCodigo { get; } = new();

    [ObservableProperty] private string? _busca;
    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private TipoCodigo? _primeiroCodigo;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _lancado;
    [ObservableProperty] private string? _numeroAtendimento;
    [ObservableProperty] private string? _mensagem;

    /// <summary>Aviso de guias pendentes do paciente selecionado (para a secretária cobrar na hora). Nulo = sem pendências.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoPendencias))]
    private string? _avisoPendencias;

    /// <summary>Há aviso de pendências a exibir?</summary>
    public bool TemAvisoPendencias => !string.IsNullOrWhiteSpace(AvisoPendencias);
    [ObservableProperty] private bool _ocupado;

    private int _ultimoAtendimentoId;

    /// <summary>Comportamento (base) da modalidade selecionada — o que o motor de regras usa.</summary>
    private ModalidadeAtendimento Modalidade =>
        ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro;

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

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        AtualizarOpcoesPrimeiroCodigo();
        if (Modalidade != ModalidadeAtendimento.Consulta)
            EspecialidadeSelecionada = null;
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

    public async Task CarregarAsync()
    {
        CarregarCatalogos();
        await BuscarPacientes();
    }

    /// <summary>Recarrega as opções de modalidade/especialidade do cache (reflete o que foi salvo em Configurações).</summary>
    private void CarregarCatalogos()
    {
        var modalidadeAtual = ModalidadeSelecionada?.Codigo;
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == modalidadeAtual)
            ?? Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
            ?? Modalidades.FirstOrDefault();

        var especialidadeAtual = EspecialidadeSelecionada?.Codigo;
        Especialidades.Clear();
        foreach (var e in CatalogoEspecialidades.Ativas)
            Especialidades.Add(e);
        EspecialidadeSelecionada = Especialidades.FirstOrDefault(e => e.Codigo == especialidadeAtual);
    }

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
        AvisoPendencias = null;
        if (value is null) return;

        // Pré-seleciona a modalidade habitual do paciente: primeiro pelo código salvo, senão pela base.
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == value.ModalidadePreferidaCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == value.ModalidadePreferida)
            ?? ModalidadeSelecionada;
        Mensagem = value.ValidadeCarteirinha is { } val && val < DateOnly.FromDateTime(DateTime.Today)
            ? $"Atenção: a carteirinha de {value.Nome} venceu em {val:dd/MM/yyyy} — o convênio pode recusar a guia."
            : null;

        _ = VerificarPendenciasAsync(value.Id);
    }

    /// <summary>
    /// Avisa se o paciente selecionado tem guias pendentes de baixa de atendimentos anteriores —
    /// oportunidade de a secretária cobrar a guia em aberto no mesmo instante do novo atendimento.
    /// </summary>
    private async Task VerificarPendenciasAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await pendencias.PendenciasDoPacienteAsync(pacienteId, hoje);
            var ncs = await pendencias.NaoConformidadesDoPacienteAsync(pacienteId, hoje);

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;
            if (lista.Count == 0 && ncs.Count == 0) { AvisoPendencias = null; return; }

            var partes = new List<string>();
            if (lista.Count > 0)
            {
                var itens = string.Join("; ", lista.Take(3).Select(p =>
                {
                    var ordinal = p.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
                    return $"{ordinal} guia de {RotuloTipo(p.Tipo)} de {p.DataPrevista:dd/MM}";
                }));
                if (lista.Count > 3) itens += $"; +{lista.Count - 3}";
                partes.Add($"{lista.Count} guia(s) pendente(s) de baixa — cobre a guia agora! ({itens}.)");
            }
            // Não conformidade: o paciente voltou, então ela será reaberta ao lançar o atendimento.
            if (ncs.Count > 0)
                partes.Add($"{ncs.Count} não conformidade(s) — serão reabertas ao lançar (o paciente voltou); cobre a(s) guia(s).");

            AvisoPendencias = "Este paciente tem " + string.Join(" ", partes);
        }
        catch
        {
            // Aviso é auxiliar: uma falha aqui nunca pode impedir o lançamento do atendimento.
            AvisoPendencias = null;
        }
    }

    private static string RotuloTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.ConsultaEspecialidade => "consulta de especialidade",
        TipoCodigo.Eletroacupuntura => "eletroacupuntura",
        TipoCodigo.Bsv => "BSV",
        TipoCodigo.Acupuntura => "acupuntura",
        TipoCodigo.Consulta => "consulta",
        _ => t.ToString()
    };

    [RelayCommand]
    private async Task Lancar()
    {
        if (PacienteSelecionado is null)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (ModalidadeSelecionada is null)
        {
            Mensagem = "Selecione a modalidade.";
            return;
        }
        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
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
                modalidadeCodigo: ModalidadeSelecionada.Codigo,
                especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null);

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
