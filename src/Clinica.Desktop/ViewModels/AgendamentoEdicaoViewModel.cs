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

    /// <summary>Busca de paciente compartilhada com Pacientes e Novo atendimento.</summary>
    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    /// <summary>Aviso de contexto do paciente escolhido (carteirinha vencida etc.).</summary>
    [ObservableProperty] private string? _avisoPaciente;

    /// <summary>Id do agendamento em edição. Null = agendamento novo.</summary>
    [ObservableProperty] private int? _editandoId;

    /// <summary>Em edição o paciente não muda: remarcar é mover o horário, não trocar de pessoa.</summary>
    public bool Remarcando => EditandoId is not null;

    public string Titulo => Remarcando ? "Remarcar agendamento" : "Novo agendamento";
    public string RotuloConfirmar => Remarcando ? "Salvar alterações" : "Agendar";

    partial void OnEditandoIdChanged(int? value)
    {
        OnPropertyChanged(nameof(Remarcando));
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(RotuloConfirmar));
        Seletor.Travado = Remarcando;
    }

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
    private void AoTrocarPaciente(Paciente? value)
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
        Seletor = new SeletorPacienteViewModel(scopeFactory);
        Seletor.SelecaoMudou += AoTrocarPaciente;
    }

    /// <summary>
    /// Prepara o formulário. <paramref name="inicio"/> vem da faixa clicada na agenda;
    /// <paramref name="agendamentoId"/> abre em modo de remarcação.
    /// </summary>
    public async Task CarregarAsync(DateTime? inicio, int? agendamentoId = null)
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

        await Seletor.BuscarAsync(imediato: true);

        if (agendamentoId is not int id) return;

        // Remarcação: traz o agendamento e trava o paciente (mover horário ≠ trocar de pessoa).
        using var scope = _scopeFactory.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
        var ag = await agenda.ObterAsync(id);
        if (ag is null)
        {
            Mensagem = "Agendamento não encontrado.";
            return;
        }

        EditandoId = ag.Id;

        // O paciente entra ANTES da modalidade: escolher paciente pré-preenche a modalidade
        // habitual dele, e aqui quem manda é o que já estava marcado no agendamento.
        var paciente = ag.Paciente ?? Seletor.Resultados.FirstOrDefault(p => p.Id == ag.PacienteId);
        if (paciente is not null)
            Seletor.SelecionarGarantindoNaLista(paciente);

        Data = ag.DataHora.Date;
        Hora = ag.DataHora.ToString("HH:mm");
        Observacoes = ag.Observacoes;
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == ag.ModalidadeCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == ag.ModalidadePrevista)
            ?? ModalidadeSelecionada;
        EspecialidadeSelecionada = Especialidades.FirstOrDefault(e => e.Codigo == ag.EspecialidadeConsultaCodigo);
    }

    [RelayCommand]
    private async Task Agendar()
    {
        if (Seletor.Selecionado is not { } paciente)
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
            // Numa remarcação, o próprio agendamento não conflita consigo mesmo.
            var conflito = await agenda.ConflitoAsync(dataHora, ignorarAgendamentoId: EditandoId);
            if (conflito is not null &&
                !_dialogo.Confirmar("Horário ocupado",
                    $"{conflito.Paciente?.Nome} já está agendado em {dataHora:dd/MM/yyyy} às {dataHora:HH:mm}.\n" +
                    "Agendar mesmo assim (encaixe)?"))
                return;

            if (EditandoId is int id)
                await agenda.RemarcarAsync(id, dataHora, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    operador: Environment.UserName);
            else
                await agenda.AgendarAsync(paciente.Id, dataHora, Modalidade, Observacoes,
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
