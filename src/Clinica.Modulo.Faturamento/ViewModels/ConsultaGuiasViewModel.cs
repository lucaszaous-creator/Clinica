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

    /// <summary>
    /// Modalidades para o filtro (parcela 45), com "Todas" na frente. Mesma construção do
    /// convênio: a primeira opção é uma STRING, e o <c>EnumDescricao</c> a devolve como
    /// veio — é o que permite "sem filtro" e "filtrado por" viverem no mesmo combo sem um
    /// valor de enum inventado para significar "nenhum".
    /// </summary>
    public IReadOnlyList<object> OpcoesModalidade { get; }

    /// <summary>Especialidades para o filtro, com "Todas" na frente.</summary>
    public IReadOnlyList<object> OpcoesEspecialidade { get; }

    [ObservableProperty] private string? _termoPaciente;
    [ObservableProperty] private string? _numeroGuia;
    [ObservableProperty] private string? _termoObservacao;
    [ObservableProperty] private DateTime? _inicio;
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private FiltroStatusGuia _statusSelecionado = FiltroStatusGuia.Todos;
    [ObservableProperty] private object _convenioSelecionado = "Todos";
    [ObservableProperty] private object _modalidadeSelecionada = TodasAsModalidades;
    [ObservableProperty] private object _especialidadeSelecionada = TodasAsEspecialidades;
    [ObservableProperty] private string? _resumo;

    private const string TodasAsModalidades = "Todas";
    private const string TodasAsEspecialidades = "Todas";

    public ConsultaGuiasViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var ops = new List<object> { "Todos" };
        ops.AddRange(Enum.GetValues<Convenio>().Cast<object>());
        OpcoesConvenio = ops;

        var modalidades = new List<object> { TodasAsModalidades };
        modalidades.AddRange(Enum.GetValues<ModalidadeAtendimento>().Cast<object>());
        OpcoesModalidade = modalidades;

        var especialidades = new List<object> { TodasAsEspecialidades };
        especialidades.AddRange(Enum.GetValues<Especialidade>().Cast<object>());
        OpcoesEspecialidade = especialidades;
    }

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        var filtro = new FiltroConsultaGuias
        {
            TermoPaciente = TermoPaciente,
            NumeroGuia = NumeroGuia,
            TermoObservacao = TermoObservacao,
            Inicio = Inicio is { } i ? DateOnly.FromDateTime(i) : null,
            Fim = Fim is { } f ? DateOnly.FromDateTime(f) : null,
            Status = StatusSelecionado,
            Convenio = ConvenioSelecionado is Convenio c ? c : null,
            Modalidade = ModalidadeSelecionada is ModalidadeAtendimento m ? m : null,
            Especialidade = EspecialidadeSelecionada is Especialidade esp ? esp : null
        };

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
        var lista = await repo.ConsultarCodigosAsync(filtro);

        Resultados.Clear();
        foreach (var g in lista) Resultados.Add(g);

        // A lista filtrada DIZ que está filtrada: "12 guias" e "12 guias de acupuntura em
        // psiquiatria" respondem perguntas diferentes, e quem volta à tela depois do café
        // não lembra o que deixou marcado no combo.
        var recorte = new List<string>();
        if (filtro.Modalidade is { } mod) recorte.Add(ModalidadeInfo.NomeExibicao(mod));
        if (filtro.Especialidade is { } espec) recorte.Add(EspecialidadeInfo.NomeExibicao(espec));

        Resumo = $"{lista.Count} guia(s) encontrada(s)"
                 + (recorte.Count > 0 ? $" — {string.Join(" · ", recorte)}" : string.Empty)
                 + (lista.Count == 500 ? " (mostrando as 500 mais recentes)" : "");
    }

    [RelayCommand]
    private async Task Limpar()
    {
        TermoPaciente = null;
        NumeroGuia = null;
        TermoObservacao = null;
        Inicio = null;
        Fim = null;
        StatusSelecionado = FiltroStatusGuia.Todos;
        ConvenioSelecionado = "Todos";
        ModalidadeSelecionada = TodasAsModalidades;
        EspecialidadeSelecionada = TodasAsEspecialidades;
        await Buscar();
    }

    /// <summary>
    /// Ler a trilha e mexer em permissão são coisas diferentes: o bit é
    /// <c>VerAuditoria</c>, o mesmo que abre a tela de Auditoria do Gerente — quem a
    /// direção autorizou a investigar lá investiga aqui, sem virar gerente de acessos.
    /// </summary>
    public bool PodeVerHistorico => SessaoUsuario.Atual.Pode(Permissao.VerAuditoria);

    /// <summary>
    /// O histórico desta guia: baixa, estorno, glosa, recurso, lote, não conformidade.
    ///
    /// As DUAS barreiras (a regra do projeto): o <c>IsEnabled</c> do botão explica, e o
    /// <c>Exigir</c> impede — só desabilitar é enfeite, porque atalho de teclado passa
    /// direto.
    /// </summary>
    [RelayCommand]
    private void Historico(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.VerAuditoria, "ver o histórico da guia");

        new Alertas.HistoricoGuiaWindow(_scopeFactory, codigo)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        }.ShowDialog();
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
