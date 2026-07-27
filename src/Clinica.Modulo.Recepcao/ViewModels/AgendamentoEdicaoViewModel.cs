using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Formulário de um horário da agenda da Recepção: paciente, quando, com quem, onde e
/// por quanto tempo.
///
/// A diferença para o formulário do faturamento é o "com quem/onde": profissional e
/// sala são recursos disputados, então o serviço RECUSA o choque — a não ser que a
/// recepção marque <see cref="Encaixe"/>, que é a clínica decidindo atender por cima.
/// O choque aparece na tela ANTES de salvar, para a decisão ser informada.
/// </summary>
public sealed partial class AgendamentoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    /// <summary>Preenchido na remarcação; nulo num horário novo.</summary>
    private readonly int? _agendamentoId;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<EntradaModalidade> Modalidades { get; } = [];
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = [];
    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public ObservableCollection<Sala> Salas { get; } = [];

    /// <summary>Choques detectados para o horário atual (vazio = livre).</summary>
    public ObservableCollection<string> Conflitos { get; } = [];

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private Sala? _sala;
    [ObservableProperty] private string _duracao = string.Empty;
    [ObservableProperty] private bool _encaixe;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _titulo = "Novo horário";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Pedido da lista de espera que originou este horário (fecha ao salvar).</summary>
    public int? PedidoListaEsperaId { get; set; }

    /// <summary>A janela fecha quando isto dispara — o comando de salvar segue assíncrono.</summary>
    public event Action? Concluido;

    public bool TemConflito => Conflitos.Count > 0;

    public bool ModalidadeConsulta
        => (ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro)
           == ModalidadeAtendimento.Consulta;

    public AgendamentoEdicaoViewModel(IServiceScopeFactory escopos, int? agendamentoId = null)
    {
        _escopos = escopos;
        _agendamentoId = agendamentoId;
        Seletor = new SeletorPacienteViewModel(escopos);
        // Trocar de paciente muda o aviso de choque com a própria agenda dele.
        Seletor.SelecaoMudou += AoTrocarPaciente;

        _ = CarregarAsync();
    }

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        if (!ModalidadeConsulta) EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeConsulta));
    }

    private void AoTrocarPaciente(Paciente? paciente) => _ = ConferirConflitosAsync();

    partial void OnDataChanged(DateTime value) => _ = ConferirConflitosAsync();
    partial void OnHoraChanged(string value) => _ = ConferirConflitosAsync();
    partial void OnDuracaoChanged(string value) => _ = ConferirConflitosAsync();
    partial void OnProfissionalChanged(Profissional? value) => _ = ConferirConflitosAsync();
    partial void OnSalaChanged(Sala? value) => _ = ConferirConflitosAsync();

    private async Task CarregarAsync()
    {
        try
        {
            Modalidades.Clear();
            foreach (var m in CatalogoModalidades.Ativas) Modalidades.Add(m);
            ModalidadeSelecionada =
                Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
                ?? Modalidades.FirstOrDefault();

            Especialidades.Clear();
            foreach (var e in CatalogoEspecialidades.Ativas) Especialidades.Add(e);

            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAtivosAsync()) Profissionais.Add(p);

            Salas.Clear();
            foreach (var s in await equipe.SalasAtivasAsync()) Salas.Add(s);

            await Seletor.BuscarAsync(imediato: true);

            if (_agendamentoId is not null) await CarregarExistenteAsync(scope, _agendamentoId.Value);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — formulário de agendamento não pôde ser preparado", ex);
            Mensagem = $"Não foi possível carregar o formulário: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    private async Task CarregarExistenteAsync(IServiceScope scope, int id)
    {
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
        var ag = await agenda.ObterAsync(id);
        if (ag is null) return;

        Titulo = "Remarcar horário";
        Data = ag.DataHora.Date;
        Hora = ag.DataHora.ToString("HH:mm");
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == ag.ModalidadeCodigo)
                                ?? Modalidades.FirstOrDefault(m => m.Base == ag.ModalidadePrevista)
                                ?? ModalidadeSelecionada;
        EspecialidadeSelecionada =
            Especialidades.FirstOrDefault(e => e.Codigo == ag.EspecialidadeConsultaCodigo);
        Profissional = Profissionais.FirstOrDefault(p => p.Id == ag.ProfissionalId);
        Sala = Salas.FirstOrDefault(s => s.Id == ag.SalaId);
        Duracao = ag.DuracaoMinutos?.ToString() ?? string.Empty;
        Encaixe = ag.Encaixe;
        Observacoes = ag.Observacoes;

        if (ag.Paciente is not null)
        {
            Seletor.SelecionarGarantindoNaLista(ag.Paciente);
            // Remarcar move o horário; não troca de pessoa.
            Seletor.Travado = true;
        }

        await ConferirConflitosAsync();
    }

    /// <summary>
    /// Mostra o choque antes de salvar. Silencioso quanto a falhas: é um aviso, e uma
    /// consulta que não respondeu não pode impedir a recepção de marcar.
    /// </summary>
    private async Task ConferirConflitosAsync()
    {
        Conflitos.Clear();
        OnPropertyChanged(nameof(TemConflito));

        if (!TentarMontarDataHora(out var dataHora)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            var achados = await agenda.ConflitosAsync(
                dataHora, DuracaoInformada(), Profissional?.Id, Sala?.Id,
                pacienteId: Seletor.Selecionado?.Id,
                ignorarAgendamentoId: _agendamentoId);

            foreach (var c in achados.Select(Descrever).Distinct())
                Conflitos.Add(c);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — conflitos da agenda não puderam ser conferidos", ex);
        }
        finally
        {
            OnPropertyChanged(nameof(TemConflito));
        }
    }

    private static string Descrever(ConflitoAgenda c) => c.Recurso switch
    {
        RecursoAgenda.Paciente => $"{c.Descricao} (aviso — não impede marcar)",
        _ => c.Descricao
    };

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        var paciente = Seletor.Selecionado;
        if (paciente is null)
        {
            Erro("Escolha o paciente.");
            return;
        }

        if (ModalidadeSelecionada is null)
        {
            Erro("Escolha a modalidade.");
            return;
        }

        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
        {
            Erro("Consulta precisa de especialidade.");
            return;
        }

        if (!TentarMontarDataHora(out var dataHora))
        {
            Erro("Horário inválido. Use HH:mm (ex.: 14:30).");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Duracao)
            && (!int.TryParse(Duracao, out var minutos) || minutos <= 0))
        {
            Erro("A duração precisa ser um número de minutos maior que zero.");
            return;
        }

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

            if (_agendamentoId is not null)
            {
                await agenda.RemarcarAsync(
                    _agendamentoId.Value, dataHora, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    operador: Environment.UserName,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(),
                    manterRecursos: false, encaixe: Encaixe);
            }
            else if (PedidoListaEsperaId is { } pedidoId)
            {
                // Chamado da lista: o serviço da lista cria o horário e fecha o pedido
                // no mesmo caminho, para o pedido nunca ficar aberto com hora marcada.
                var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();
                await espera.ChamarAsync(
                    pedidoId, dataHora, ModalidadeSelecionada.Base,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(), encaixe: Encaixe,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo);
            }
            else
            {
                await agenda.AgendarAsync(
                    paciente.Id, dataHora, ModalidadeSelecionada.Base, Observacoes,
                    modalidadeCodigo: ModalidadeSelecionada.Codigo,
                    especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null,
                    profissionalId: Profissional?.Id, salaId: Sala?.Id,
                    duracaoMinutos: DuracaoInformada(), encaixe: Encaixe);
            }

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — horário não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Assume o choque: marca por cima com o encaixe registrado.</summary>
    [RelayCommand]
    private async Task AssumirEncaixeAsync()
    {
        Encaixe = true;
        await SalvarAsync();
    }

    private int? DuracaoInformada()
        => int.TryParse(Duracao, out var m) && m > 0 ? m : null;

    private bool TentarMontarDataHora(out DateTime dataHora)
    {
        dataHora = default;
        if (!TimeOnly.TryParse(Hora, out var hora)) return false;
        dataHora = Data.Date.Add(hora.ToTimeSpan());
        return true;
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
