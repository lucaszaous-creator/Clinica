using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Formulário de novo agendamento, usado pela janela <c>Alertas.AgendamentoWindow</c>.
/// Nasce com data e hora já preenchidas quando a secretária clica numa faixa livre da
/// agenda — o caminho normal é escolher o paciente e confirmar.
/// </summary>
public partial class AgendamentoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<Paciente> Pacientes { get; } = new();
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    [ObservableProperty] private string? _busca;
    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    /// <summary>Aviso de contexto do paciente escolhido (carteirinha vencida etc.).</summary>
    [ObservableProperty] private string? _avisoPaciente;

    partial void OnBuscaChanged(string? value) => _ = BuscarPacientes();

    /// <summary>Comportamento (base) da modalidade selecionada.</summary>
    private ModalidadeAtendimento Modalidade =>
        ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro;

    /// <summary>Consulta avulsa: pede a especialidade (levada ao atendimento na confirmação).</summary>
    public bool ModalidadeConsulta => Modalidade == ModalidadeAtendimento.Consulta;

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        if (Modalidade != ModalidadeAtendimento.Consulta)
            EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeConsulta));
    }

    // Pré-preenche a modalidade com a habitual do paciente e avisa o que atrapalha a guia.
    partial void OnPacienteSelecionadoChanged(Paciente? value)
    {
        if (value is null)
        {
            AvisoPaciente = null;
            return;
        }

        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == value.ModalidadePreferidaCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == value.ModalidadePreferida)
            ?? ModalidadeSelecionada;

        AvisoPaciente = value.CarteirinhaVencida
            ? $"Carteirinha vencida em {value.ValidadeCarteirinha:dd/MM/yyyy} — renove antes do atendimento, senão a guia é recusada."
            : null;
    }

    /// <summary>Disparado quando o agendamento é criado (a janela fecha).</summary>
    public event Action? Agendou;

    public AgendamentoEdicaoViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    /// <summary>Prepara o formulário. <paramref name="inicio"/> vem da faixa clicada na agenda.</summary>
    public async Task CarregarAsync(DateTime? inicio)
    {
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
            ?? Modalidades.FirstOrDefault();

        Especialidades.Clear();
        foreach (var e in CatalogoEspecialidades.Ativas)
            Especialidades.Add(e);

        if (inicio is { } quando)
        {
            Data = quando.Date;
            Hora = quando.ToString("HH:mm");
        }

        await BuscarPacientes();
    }

    [RelayCommand]
    private async Task BuscarPacientes()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        var encontrados = await service.BuscarAsync(Busca);

        Pacientes.Clear();
        // Sem termo de busca, a lista inteira não ajuda ninguém: mostra as primeiras
        // e deixa o campo de busca fazer o trabalho.
        foreach (var p in encontrados.Take(50))
            Pacientes.Add(p);
    }

    [RelayCommand]
    private async Task Agendar()
    {
        if (PacienteSelecionado is null)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (!TimeOnly.TryParse(Hora, out var hora))
        {
            Mensagem = "Hora inválida (use HH:mm, ex.: 14:30).";
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

        // Hora de parede (sem fuso) para casar com a coluna 'timestamp without time zone'.
        var dataHora = DateTime.SpecifyKind(Data.Date.Add(hora.ToTimeSpan()), DateTimeKind.Unspecified);

        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            // Choque de horário: avisa quem já ocupa o slot e pede confirmação.
            var conflito = await agenda.ConflitoAsync(dataHora);
            if (conflito is not null &&
                !_dialogo.Confirmar("Horário ocupado",
                    $"{conflito.Paciente?.Nome} já está agendado em {dataHora:dd/MM/yyyy} às {dataHora:HH:mm}.\n" +
                    "Agendar mesmo assim (encaixe)?"))
                return;

            await agenda.AgendarAsync(PacienteSelecionado.Id, dataHora, Modalidade, Observacoes,
                modalidadeCodigo: ModalidadeSelecionada.Codigo,
                especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null);
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível agendar: {ex.Message}";
            return;
        }
        finally
        {
            Ocupado = false;
        }

        Agendou?.Invoke();
    }
}
