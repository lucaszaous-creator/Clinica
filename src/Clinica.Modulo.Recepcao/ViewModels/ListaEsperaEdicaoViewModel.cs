using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Turno oferecido no formulário (o enum com rótulo em português).</summary>
public sealed record OpcaoPeriodo(PeriodoPreferido Valor, string Nome)
{
    public override string ToString() => Nome;
}

/// <summary>
/// Formulário de entrada na lista de espera. O que ele coleta é exatamente o que faz a
/// lista ser útil na hora do buraco na agenda: com quem, em que turno e dentro de que
/// janela de datas o paciente pode vir — sem isso a secretária teria de ligar para todo
/// mundo para descobrir quem serve.
/// </summary>
public sealed partial class ListaEsperaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = [];

    public IReadOnlyList<OpcaoPeriodo> Periodos { get; } =
    [
        new(PeriodoPreferido.Qualquer, "Qualquer turno"),
        new(PeriodoPreferido.Manha, "Manhã"),
        new(PeriodoPreferido.Tarde, "Tarde")
    ];

    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private EntradaModalidade? _modalidade;
    [ObservableProperty] private OpcaoPeriodo? _periodo;
    [ObservableProperty] private DateTime? _disponivelDe;
    [ObservableProperty] private DateTime? _disponivelAte;
    [ObservableProperty] private bool _prioritario;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public ListaEsperaEdicaoViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        Seletor = new SeletorPacienteViewModel(escopos);
        Periodo = Periodos[0];
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            Modalidades.Clear();
            foreach (var m in CatalogoModalidades.Ativas) Modalidades.Add(m);

            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAtivosAsync()) Profissionais.Add(p);

            await Seletor.BuscarAsync(imediato: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — formulário da lista de espera não pôde ser preparado", ex);
            Mensagem = $"Não foi possível carregar o formulário: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (Seletor.Selecionado is null)
        {
            Erro("Escolha o paciente.");
            return;
        }

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var espera = scope.ServiceProvider.GetRequiredService<ListaEsperaService>();

            await espera.AdicionarAsync(
                Seletor.Selecionado.Id,
                profissionalId: Profissional?.Id,
                modalidadeCodigo: Modalidade?.Codigo,
                periodo: Periodo?.Valor ?? PeriodoPreferido.Qualquer,
                disponivelDe: DisponivelDe is { } de ? DateOnly.FromDateTime(de) : null,
                disponivelAte: DisponivelAte is { } ate ? DateOnly.FromDateTime(ate) : null,
                prioritario: Prioritario,
                observacoes: Observacoes);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — pedido da lista de espera não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
