using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

public sealed partial class DiaDaAgenda : ObservableObject
{
    public DayOfWeek Dia { get; init; }
    public string Nome { get; init; } = string.Empty;
    [ObservableProperty] private bool _selecionado;
}

public sealed partial class HorariosProfissionalViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public IReadOnlyList<DiaDaAgenda> Dias { get; } = new[]
    {
        (DayOfWeek.Monday,"Segunda"),(DayOfWeek.Tuesday,"Terça"),(DayOfWeek.Wednesday,"Quarta"),
        (DayOfWeek.Thursday,"Quinta"),(DayOfWeek.Friday,"Sexta"),(DayOfWeek.Saturday,"Sábado"),(DayOfWeek.Sunday,"Domingo")
    }.Select(d => new DiaDaAgenda { Dia = d.Item1, Nome = d.Item2 }).ToArray();
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PodeSalvar))] private Profissional? _profissional;
    [ObservableProperty] private bool _agendaProtegida;
    [ObservableProperty] private string _das = string.Empty;
    [ObservableProperty] private string _ate = string.Empty;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PodeSalvar), nameof(PodeEditar))] private bool _carregando;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PodeSalvar), nameof(PodeEditar))] private bool _salvando;
    public bool PodeEditar => !Carregando && !Salvando;
    public bool PodeSalvar => Profissional is not null && !Carregando && !Salvando
        && SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);
    public bool Alterou { get; private set; }
    public int? ProfissionalPreferidoId { get; set; }
    public System.Windows.Input.ICommand? FecharAgendaCommand { get; set; }
    public bool TemFecharAgenda => FecharAgendaCommand is not null;
    public ObservableCollection<string> BloqueiosDoProfissional { get; } = [];
    [ObservableProperty] private string _resumoBloqueios = "Selecione um profissional para consultar os bloqueios.";
    private int _geracaoBloqueios;
    private int _geracaoCarga;

    public HorariosProfissionalViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;
        var escolhido = Profissional?.Id ?? ProfissionalPreferidoId;
        Carregando = true;
        Profissional = null;
        Profissionais.Clear();
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "configurar horários e travas");
            using var scope = _escopos.CreateScope();
            var lista = await scope.ServiceProvider.GetRequiredService<EquipeService>().ProfissionaisAtivosAsync();
            if (geracao != _geracaoCarga) return;
            foreach (var p in lista.OrderBy(p => p.Nome)) Profissionais.Add(p);
            if (escolhido is { } id) Profissional = Profissionais.FirstOrDefault(p => p.Id == id);
            Mensagem = lista.Count == 0 ? "Cadastre um profissional ativo em Profissionais e salas." : "Escolha o profissional que deseja configurar.";
            MensagemEhErro = false;
        }
        catch (Exception ex) { if (geracao == _geracaoCarga) { Mensagem = ex.Message; MensagemEhErro = true; } }
        finally { if (geracao == _geracaoCarga) Carregando = false; }
    }

    partial void OnProfissionalChanged(Profissional? value)
    {
        AgendaProtegida = value?.AgendaProtegida ?? false;
        Das = value?.AtendeDas?.ToString("HH:mm") ?? string.Empty;
        Ate = value?.AtendeAte?.ToString("HH:mm") ?? string.Empty;
        foreach (var d in Dias) d.Selecionado = value?.DiasDeAtendimento is not null && value.AtendeEm(d.Dia);
        Mensagem = "As regras valem em todos os postos. Horários já marcados são preservados.";
        MensagemEhErro = false;
        _ = CarregarBloqueiosAsync();
    }

    [RelayCommand]
    private async Task CarregarBloqueiosAsync()
    {
        var geracao = ++_geracaoBloqueios;
        BloqueiosDoProfissional.Clear();
        if (Profissional is not { } p) { ResumoBloqueios = "Selecione um profissional."; return; }
        ResumoBloqueios = "Consultando os próximos 60 dias…";
        try
        {
            using var scope = _escopos.CreateScope();
            var lista = await scope.ServiceProvider.GetRequiredService<BloqueioAgendaService>()
                .NoPeriodoAsync(DateTime.Today, DateTime.Today.AddDays(60));
            if (geracao != _geracaoBloqueios) return;
            foreach (var b in lista.Where(b => b.AlcancaRecurso(p.Id, null)).OrderBy(b => b.Inicio))
                BloqueiosDoProfissional.Add($"{b.Inicio:dd/MM HH:mm}–{b.Fim:dd/MM HH:mm} · {b.Motivo}");
            ResumoBloqueios = BloqueiosDoProfissional.Count == 0 ? "Nenhum bloqueio nos próximos 60 dias." : "Bloqueios do profissional e da clínica nos próximos 60 dias.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoBloqueios) return;
            ResumoBloqueios = "Bloqueios não verificados. Atualize para conferir.";
            Clinica.Application.Diagnostico.Registrar("Horários e travas — leitura dos bloqueios", ex);
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (!PodeSalvar || Profissional is not { } p) return;
        Salvando = true;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "configurar horários e travas");
            TimeOnly? das = null, ate = null;
            if (!string.IsNullOrWhiteSpace(Das) || !string.IsNullOrWhiteSpace(Ate))
            {
                if (!TimeOnly.TryParseExact(Das.Trim(), "HH:mm", out var inicio)
                    || !TimeOnly.TryParseExact(Ate.Trim(), "HH:mm", out var fim))
                    throw new InvalidOperationException("Preencha Das e Até no formato 08:00, ou deixe ambos vazios.");
                das = inicio; ate = fim;
            }
            var dias = Dias.Where(d => d.Selecionado).Aggregate(0, (valor, d) => valor | Clinica.Domain.Entities.Profissional.BitDe(d.Dia));
            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ConfiguracaoAgendaService>()
                .SalvarAsync(p.Id, AgendaProtegida, dias, das, ate, SessaoUsuario.Atual.Operador,
                    new JornadaAnterior(p.AgendaProtegida, p.DiasDeAtendimento, p.AtendeDas, p.AtendeAte));
            p.AgendaProtegida = AgendaProtegida; p.DiasDeAtendimento = dias == 0 ? null : dias;
            p.AtendeDas = das; p.AtendeAte = ate;
            Alterou = true;
            Mensagem = $"Configuração de {p.Rotulo} salva para todos os postos. "
                + (AgendaProtegida ? "A trava também vale para encaixes." : "Os conflitos continuam como avisos.");
            MensagemEhErro = false;
        }
        catch (Exception ex) { Mensagem = ex.Message; MensagemEhErro = true; }
        finally { Salvando = false; }
    }
}
