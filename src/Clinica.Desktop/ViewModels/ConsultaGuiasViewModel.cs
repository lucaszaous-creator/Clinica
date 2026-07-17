using System.Windows.Input;
using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Consulta central de guias: localiza qualquer código/guia por filtros combinados.</summary>
public partial class ConsultaGuiasViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<CodigoFaturamento> Resultados { get; } = new();

    public Array Status => Enum.GetValues(typeof(FiltroStatusGuia));
    public IReadOnlyList<object> OpcoesConvenio { get; }

    [ObservableProperty] private string? _termoPaciente;
    [ObservableProperty] private string? _numeroGuia;
    [ObservableProperty] private DateTime? _inicio;
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private FiltroStatusGuia _statusSelecionado = FiltroStatusGuia.Todos;
    [ObservableProperty] private object _convenioSelecionado = "Todos";
    [ObservableProperty] private string? _resumo;

    public ConsultaGuiasViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var ops = new List<object> { "Todos" };
        ops.AddRange(Enum.GetValues<Convenio>().Cast<object>());
        OpcoesConvenio = ops;
    }

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        var filtro = new FiltroConsultaGuias
        {
            TermoPaciente = TermoPaciente,
            NumeroGuia = NumeroGuia,
            Inicio = Inicio is { } i ? DateOnly.FromDateTime(i) : null,
            Fim = Fim is { } f ? DateOnly.FromDateTime(f) : null,
            Status = StatusSelecionado,
            Convenio = ConvenioSelecionado is Convenio c ? c : null
        };

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
        var lista = await repo.ConsultarCodigosAsync(filtro);

        Resultados.Clear();
        foreach (var g in lista) Resultados.Add(g);
        Resumo = $"{lista.Count} guia(s) encontrada(s)" + (lista.Count == 500 ? " (mostrando as 500 mais recentes)" : "");
    }

    [RelayCommand]
    private async Task Limpar()
    {
        TermoPaciente = null;
        NumeroGuia = null;
        Inicio = null;
        Fim = null;
        StatusSelecionado = FiltroStatusGuia.Todos;
        ConvenioSelecionado = "Todos";
        await Buscar();
    }

    /// <summary>Reimpressão da capa de faturamento do atendimento desta guia (estado atual).</summary>
    [RelayCommand]
    private async Task Capa(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        await Configuracao.CapaImpressao.GerarESalvarAsync(
            _scopeFactory, codigo.AtendimentoId, codigo.Atendimento?.Numero, codigo.Atendimento?.Data ?? default);
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
}
