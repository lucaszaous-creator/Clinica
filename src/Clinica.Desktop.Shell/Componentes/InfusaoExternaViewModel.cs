using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

public sealed partial class InfusaoExternaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private readonly int? _sessaoInicial;
    private int _geracao;
    public string Paciente { get; }
    public int? PrescricaoId { get; private set; }
    public event Action? Salvou;
    public ObservableCollection<Profissional> Medicos { get; } = [];
    public ObservableCollection<OpcaoSessaoInfusao> Sessoes { get; } = [];
    [ObservableProperty] private Profissional? _medico;
    [ObservableProperty] private OpcaoSessaoInfusao? _sessao;
    [ObservableProperty] private DateTime? _data = DateTime.Today;
    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private DateTime? _dataPrescricao = DateTime.Today;
    [ObservableProperty] private string _horaPrescricao = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _texto = "";
    [ObservableProperty] private string _orientacao = "";
    [ObservableProperty] private string _diluente = "SF 0,9%";
    [ObservableProperty] private string _volume = "";
    [ObservableProperty] private string _tempo = "1h";
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private string? _alertas;
    [ObservableProperty] private bool _confirmouAlergia;
    [ObservableProperty] private bool _carregando;
    public InfusaoExternaViewModel(IServiceScopeFactory escopos, int pacienteId, string paciente, int? agendamentoId = null)
    {
        _escopos = escopos; _pacienteId = pacienteId; Paciente = paciente; _sessaoInicial = agendamentoId;
    }
    partial void OnDataChanged(DateTime? value) { if (Medicos.Count > 0) _ = CarregarSessoesAsync(); }
    partial void OnSessaoChanged(OpcaoSessaoInfusao? value)
    {
        if (value?.MedicoId is { } medico) Medico = Medicos.FirstOrDefault(m => m.Id == medico);
    }
    public async Task CarregarAsync()
    {
        try {
            Carregando = true;
            using var scope = _escopos.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
            var profissionais = await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>().MedicosParaValidacaoAsync();
            Medicos.Clear();
            foreach (var medico in profissionais.Where(p => p.Ativo && p.Id != SessaoUsuario.Atual.ProfissionalId)) Medicos.Add(medico);
            var contexto = await scope.ServiceProvider.GetRequiredService<PrescricaoService>().ContextoAsync(_pacienteId);
            Alertas = contexto.Alergias.Count == 0 ? "Nenhuma alergia cadastrada. Confira com o paciente." : "Alergias: " + string.Join("; ", contexto.Alergias.Select(a => a.Descricao));
            if (_sessaoInicial is { } id && await repo.ObterAgendamentoAsync(id) is { } horario) Data = horario.DataHora.Date;
            await CarregarSessoesAsync();
        } catch (Exception ex) { Mensagem = ex.Message; }
        finally { Carregando = false; }
    }
    private async Task CarregarSessoesAsync()
    {
        if (Data is not { } data) return;
        var geracao = ++_geracao;
        try {
            using var scope = _escopos.CreateScope();
            var sessoes = await scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>()
                .AgendamentosDoPacienteNoDiaAsync(_pacienteId, DateOnly.FromDateTime(data));
            if (geracao != _geracao) return;
            Sessoes.Clear();
            Sessoes.Add(new(null, null, "Registro avulso do paciente"));
            foreach (var a in sessoes.Where(a => a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado))
                Sessoes.Add(new(a.Id, a.ProfissionalId, $"{a.DataHora:HH:mm} · sessão #{a.Id} · {a.Profissional?.Rotulo}"));
            Sessao = Sessoes.FirstOrDefault(s => s.Id == _sessaoInicial) ?? Sessoes[0];
        } catch (Exception ex) { if (geracao == _geracao) Mensagem = ex.Message; }
    }
    [RelayCommand]
    private async Task SalvarAsync()
    {
        try {
            SessaoUsuario.Atual.Exigir(Permissao.ChecarPrescricao | Permissao.RegistrarEvolucaoEnfermagem, "registrar infusão realizada");
            if (Data is not { } data || !TimeOnly.TryParse(Hora, out var hora)
                || DataPrescricao is not { } dataPrescricao || !TimeOnly.TryParse(HoraPrescricao, out var horaPrescricao)
                || Medico is null)
                throw new InvalidOperationException("Informe as datas e horas da prescrição e da execução e o médico responsável.");
            using var scope = _escopos.CreateScope();
            var p = await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>().RegistrarExecucaoExternaAsync(
                new(_pacienteId, Medico.Id, Sessao?.Id, DateOnly.FromDateTime(data), hora, Texto, Orientacao,
                    Volume, Diluente, Tempo, ConfirmouAlergia: ConfirmouAlergia,
                    DataPrescricao: DateOnly.FromDateTime(dataPrescricao), HoraPrescricao: horaPrescricao), SessaoUsuario.Atual.UsuarioId);
            PrescricaoId = p.Id;
            Salvou?.Invoke();
        } catch (Exception ex) { Mensagem = ex.Message; }
    }
}
public sealed record OpcaoSessaoInfusao(int? Id, int? MedicoId, string Rotulo);
