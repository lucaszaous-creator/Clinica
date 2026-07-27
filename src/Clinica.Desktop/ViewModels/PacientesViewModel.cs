using System.Windows.Input;
using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Opção do filtro de convênio da lista de pacientes. Código null = todos.</summary>
public sealed record OpcaoConvenio(string? Codigo, string Nome);

/// <summary>
/// Listagem de pacientes: busca (nome/CPF), filtro por convênio, ordenação e as ações de
/// linha. O cadastro em si mora na janela <c>Alertas.PacienteEdicaoWindow</c> — a tela
/// fica inteira para a lista.
/// </summary>
public partial class PacientesViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    /// <summary>
    /// A busca compartilhada com Novo atendimento e Agendamento. Aqui vai sem limite — uma
    /// LISTA precisa mostrar todo mundo — e com <c>Refinar</c> para aplicar convênio e ordenação.
    /// </summary>
    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>Opções do filtro de convênio (a primeira é sempre "Todos os convênios").</summary>
    public ObservableCollection<OpcaoConvenio> FiltrosConvenio { get; } = new();

    /// <summary>Filtro de convênio aplicado sobre o resultado da busca.</summary>
    [ObservableProperty] private OpcaoConvenio? _filtroConvenio;
    partial void OnFiltroConvenioChanged(OpcaoConvenio? value) { if (!_carregando) _ = Seletor.BuscarAsync(imediato: true); }

    /// <summary>Ordenação da lista. "Recentes" usa o Id (ordem de cadastro).</summary>
    public string[] Ordenacoes { get; } = { "Nome (A–Z)", "Cadastro mais recente" };

    [ObservableProperty] private string _ordenacao = "Nome (A–Z)";
    partial void OnOrdenacaoChanged(string value) { if (!_carregando) _ = Seletor.BuscarAsync(imediato: true); }

    /// <summary>Quantos dos pacientes listados estão com a carteirinha vencida.</summary>
    [ObservableProperty] private int _totalCarteirinhasVencidas;

    private bool _carregando;

    /// <summary>Pede ao shell para abrir a ficha de um paciente.</summary>
    public event Action<int>? FichaSolicitada;

    public PacientesViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(scopeFactory, limite: null)
        {
            Refinar = Aplicar
        };
        Seletor.Atualizou += () =>
            TotalCarteirinhasVencidas = Seletor.Resultados.Count(p => p.CarteirinhaVencida);
    }

    /// <summary>Filtro de convênio + ordenação, aplicados sobre o que o banco devolveu.</summary>
    private IEnumerable<Paciente> Aplicar(IReadOnlyList<Paciente> encontrados)
    {
        IEnumerable<Paciente> resultado = encontrados;

        // Comparado pelo código do catálogo, com fallback na família para cadastros antigos
        // gravados antes dos convênios dinâmicos.
        if (FiltroConvenio?.Codigo is { } codigo)
            resultado = resultado.Where(p => (p.ConvenioCodigo ?? p.Convenio.ToString()) == codigo);

        // A busca já vem por nome; só o "mais recente" precisa reordenar (Id = cadastro).
        if (Ordenacao == Ordenacoes[1])
            resultado = resultado.OrderByDescending(p => p.Id);

        return resultado;
    }

    public async Task CarregarAsync()
    {
        _carregando = true;
        FiltrosConvenio.Clear();
        FiltrosConvenio.Add(new OpcaoConvenio(null, "Todos os convênios"));
        foreach (var c in CatalogoConvenios.Ativos)
            FiltrosConvenio.Add(new OpcaoConvenio(c.Codigo, c.Nome));
        FiltroConvenio = FiltrosConvenio[0];
        _carregando = false;

        await Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>Abre a janela de cadastro (vazia para novo, preenchida para edição).</summary>
    public async Task<bool> AbrirCadastroAsync(int? pacienteId)
    {
        var janela = new Alertas.PacienteEdicaoWindow(new PacienteEdicaoViewModel(_scopeFactory), pacienteId)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return false;

        await Seletor.BuscarAsync(imediato: true);
        return true;
    }

    [RelayCommand]
    private async Task Novo() => await AbrirCadastroAsync(null);

    [RelayCommand]
    private async Task Editar(Paciente? p)
    {
        if (p is not null) await AbrirCadastroAsync(p.Id);
    }

    [RelayCommand]
    private async Task Excluir(Paciente? p)
    {
        if (p is null) return;
        if (!_dialogo.ConfirmarPerigo("Confirmar exclusão",
            $"Excluir o paciente \"{p.Nome}\"?\nTodos os atendimentos e códigos dele também serão removidos.")) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        await service.RemoverAsync(p.Id);
        await Seletor.BuscarAsync(imediato: true);
    }

    [RelayCommand]
    private void AbrirFicha(Paciente? p)
    {
        if (p is not null) FichaSolicitada?.Invoke(p.Id);
    }

    /// <summary>Carrega a lista e já abre o cadastro do paciente (botão Editar da ficha).</summary>
    public async Task CarregarEEditarAsync(int pacienteId)
    {
        await CarregarAsync();
        await AbrirCadastroAsync(pacienteId);
    }

    // Atalhos globais do shell (IAtalhosDeTela). Salvar não se aplica: a tela é uma
    // listagem e a gravação acontece dentro da janela de cadastro.
    public ICommand? AtalhoAtualizar => Seletor.AtualizarCommand;
}
