using System.Collections.ObjectModel;
using System.Windows.Input;
using Clinica.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Shell de navegação: sidebar recolhível agrupada em módulos, breadcrumb,
/// pesquisa global (command palette de seções), contador de pendências e snackbar.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private int _pendenciasBadge;

    [ObservableProperty]
    private Secao _secaoAtual = Secao.Pendencias;

    [ObservableProperty]
    private bool _menuRecolhido;

    [ObservableProperty]
    private string _breadcrumbModulo = "Painel";

    [ObservableProperty]
    private string _breadcrumbTela = "Pendências";

    /// <summary>Título curto exibido no breadcrumb quando em tela de detalhe (vazio fora delas).</summary>
    [ObservableProperty]
    private string _breadcrumbDetalhe = string.Empty;

    [ObservableProperty]
    private string _textoPesquisaGlobal = string.Empty;

    [ObservableProperty]
    private bool _pesquisaAberta;

    public ObservableCollection<ItemMenu> ResultadosPesquisa { get; } = [];

    public IReadOnlyList<GrupoMenu> Grupos { get; }

    private readonly List<ItemMenu> _itens;

    public SnackbarService Snackbar { get; }

    /// <summary>Versão exibida no rodapé da sidebar: a instalada (Velopack) ou a do assembly com aviso de build portátil.</summary>
    public string VersaoApp { get; } = UpdateService.VersaoInstalada is { } v
        ? $"v{v}"
        : $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?"} (portátil — sem auto-update)";

    public MainViewModel(IServiceProvider sp, SnackbarService snackbar)
    {
        _sp = sp;
        Snackbar = snackbar;

        _itens =
        [
            new ItemMenu { Secao = Secao.Pendencias, Rotulo = "Pendências", Glifo = "\uE9D5", Grupo = "Painel" },
            new ItemMenu { Secao = Secao.NaoConformidades, Rotulo = "NC", Glifo = "", Grupo = "Painel" },
            new ItemMenu { Secao = Secao.Agenda, Rotulo = "Agenda", Glifo = "\uE787", Grupo = "Agenda" },
            new ItemMenu { Secao = Secao.Atendimento, Rotulo = "Novo atendimento", Glifo = "\uEB51", Grupo = "Atendimento" },
            new ItemMenu { Secao = Secao.Consultas, Rotulo = "Consultas", Glifo = "\uE8A5", Grupo = "Atendimento" },
            new ItemMenu { Secao = Secao.ConsultaGuias, Rotulo = "Consultar guias", Glifo = "\uE721", Grupo = "Faturamento" },
            new ItemMenu { Secao = Secao.Faturados, Rotulo = "Faturados", Glifo = "\uE8C7", Grupo = "Faturamento" },
            new ItemMenu { Secao = Secao.Glosas, Rotulo = "Glosas", Glifo = "\uF140", Grupo = "Faturamento" },
            new ItemMenu { Secao = Secao.Tiss, Rotulo = "Guias TISS", Glifo = "\uE7C3", Grupo = "Faturamento" },
            new ItemMenu { Secao = Secao.Pacientes, Rotulo = "Pacientes", Glifo = "\uE716", Grupo = "Cadastros e ajustes" },
            new ItemMenu { Secao = Secao.Relatorios, Rotulo = "Relatórios", Glifo = "\uE9D2", Grupo = "Cadastros e ajustes" },
            new ItemMenu { Secao = Secao.Parametros, Rotulo = "Configurações", Glifo = "\uE713", Grupo = "Cadastros e ajustes" },
        ];

        Grupos = _itens.GroupBy(i => i.Grupo)
                       .Select(g => new GrupoMenu(g.Key, g.ToList()))
                       .ToList();

        Navegar(Secao.Pendencias);
    }

    [RelayCommand]
    private void AlternarMenu() => MenuRecolhido = !MenuRecolhido;

    // ===== Atalhos globais (roteados para a tela ativa via IAtalhosDeTela) =====

    [RelayCommand]
    private void AtalhoSalvar() => Executar((CurrentViewModel as IAtalhosDeTela)?.AtalhoSalvar);

    [RelayCommand]
    private void AtalhoImprimir() => Executar((CurrentViewModel as IAtalhosDeTela)?.AtalhoImprimir);

    [RelayCommand]
    private void AtalhoAtualizar() => Executar((CurrentViewModel as IAtalhosDeTela)?.AtalhoAtualizar);

    private static void Executar(ICommand? comando)
    {
        if (comando?.CanExecute(null) == true)
            comando.Execute(null);
    }

    [RelayCommand]
    private void Navegar(Secao secao)
    {
        switch (secao)
        {
            case Secao.Pendencias: MostrarDashboard(); break;
            case Secao.NaoConformidades: MostrarNaoConformidades(); break;
            case Secao.Agenda: MostrarAgenda(); break;
            case Secao.Atendimento: MostrarNovoAtendimento(); break;
            case Secao.Consultas: MostrarConsultas(); break;
            case Secao.ConsultaGuias: MostrarConsultaGuias(); break;
            case Secao.Faturados: MostrarFaturados(); break;
            case Secao.Glosas: MostrarGlosas(); break;
            case Secao.Tiss: MostrarTiss(); break;
            case Secao.Pacientes: MostrarPacientes(); break;
            case Secao.Relatorios: MostrarRelatorios(); break;
            case Secao.Parametros: MostrarParametros(); break;
        }
    }

    /// <summary>Atualiza seção ativa, destaque do menu e breadcrumb.</summary>
    private void DefinirSecao(Secao secao)
    {
        SecaoAtual = secao;
        BreadcrumbDetalhe = string.Empty;
        foreach (var item in _itens)
            item.EstaAtivo = item.Secao == secao;

        var ativo = _itens.FirstOrDefault(i => i.Secao == secao);
        if (ativo is not null)
        {
            BreadcrumbModulo = ativo.Grupo;
            BreadcrumbTela = ativo.Rotulo;
        }
    }

    // ===== Pesquisa global (seções + pacientes por nome/CPF) =====

    partial void OnTextoPesquisaGlobalChanged(string value)
    {
        ResultadosPesquisa.Clear();
        var termo = value.Trim();
        if (termo.Length == 0)
        {
            PesquisaAberta = false;
            return;
        }

        foreach (var item in _itens.Where(i =>
                     i.Rotulo.Contains(termo, StringComparison.CurrentCultureIgnoreCase) ||
                     i.Grupo.Contains(termo, StringComparison.CurrentCultureIgnoreCase)))
            ResultadosPesquisa.Add(item);

        PesquisaAberta = ResultadosPesquisa.Count > 0;

        // Pacientes entram de forma assíncrona (banco); 2+ letras para não varrer tudo.
        if (termo.Length >= 2)
            _ = PesquisarPacientesAsync(termo);
    }

    private async Task PesquisarPacientesAsync(string termo)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var pacientes = await scope.ServiceProvider
                .GetRequiredService<Clinica.Application.Servicos.PacienteService>()
                .BuscarAsync(termo);

            // O usuário pode ter continuado digitando enquanto o banco respondia.
            if (TextoPesquisaGlobal.Trim() != termo) return;

            foreach (var p in pacientes.Take(6))
                ResultadosPesquisa.Add(new ItemMenu
                {
                    Secao = Secao.Pacientes,
                    Rotulo = p.Nome,
                    Glifo = "\uE77B", // pessoa
                    Grupo = "Paciente",
                    PacienteId = p.Id
                });

            PesquisaAberta = ResultadosPesquisa.Count > 0;
        }
        catch
        {
            // Banco fora do ar não pode quebrar a digitação na pesquisa.
        }
    }

    [RelayCommand]
    private void NavegarResultado(ItemMenu? item)
    {
        item ??= ResultadosPesquisa.FirstOrDefault();
        if (item is null) return;

        FecharPesquisa();

        if (item.PacienteId is int pacienteId)
        {
            DefinirSecao(Secao.Pacientes);
            AbrirFicha(pacienteId);
            return;
        }

        Navegar(item.Secao);
    }

    [RelayCommand]
    private void FecharPesquisa()
    {
        PesquisaAberta = false;
        TextoPesquisaGlobal = string.Empty;
    }

    // ===== Seções =====

    [RelayCommand]
    private void MostrarDashboard()
    {
        var vm = _sp.GetRequiredService<DashboardViewModel>();
        vm.PendenciasAtualizadas += total => PendenciasBadge = total;
        vm.AbrirBaixaSolicitado += AbrirBaixa;
        vm.FichaSolicitada += AbrirFicha;
        vm.AbrirGlosasSolicitado += MostrarGlosas;
        DefinirSecao(Secao.Pendencias);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarPacientes()
    {
        var vm = _sp.GetRequiredService<PacientesViewModel>();
        vm.FichaSolicitada += AbrirFicha;
        DefinirSecao(Secao.Pacientes);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarNovoAtendimento()
    {
        var vm = _sp.GetRequiredService<NovoAtendimentoViewModel>();
        DefinirSecao(Secao.Atendimento);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarAgenda()
    {
        var vm = _sp.GetRequiredService<AgendaViewModel>();
        DefinirSecao(Secao.Agenda);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarConsultas()
    {
        var vm = _sp.GetRequiredService<ConsultasViewModel>();
        DefinirSecao(Secao.Consultas);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarFaturados()
    {
        var vm = _sp.GetRequiredService<FaturadosViewModel>();
        DefinirSecao(Secao.Faturados);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarConsultaGuias()
    {
        var vm = _sp.GetRequiredService<ConsultaGuiasViewModel>();
        DefinirSecao(Secao.ConsultaGuias);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarGlosas()
    {
        var vm = _sp.GetRequiredService<GlosasViewModel>();
        DefinirSecao(Secao.Glosas);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarNaoConformidades()
    {
        var vm = _sp.GetRequiredService<NaoConformidadesViewModel>();
        DefinirSecao(Secao.NaoConformidades);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarTiss()
    {
        var vm = _sp.GetRequiredService<TissViewModel>();
        DefinirSecao(Secao.Tiss);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarParametros()
    {
        var vm = _sp.GetRequiredService<ParametrosViewModel>();
        DefinirSecao(Secao.Parametros);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    [RelayCommand]
    private void MostrarRelatorios()
    {
        var vm = _sp.GetRequiredService<RelatoriosViewModel>();
        DefinirSecao(Secao.Relatorios);
        CurrentViewModel = vm;
        _ = vm.CarregarAsync();
    }

    private void AbrirBaixa(int codigoId)
    {
        var vm = _sp.GetRequiredService<BaixaViewModel>();
        vm.BaixaConcluida += MostrarDashboard;
        vm.Cancelado += MostrarDashboard;
        BreadcrumbDetalhe = "Dar baixa";
        CurrentViewModel = vm;
        _ = vm.CarregarAsync(codigoId);
    }

    private void AbrirFicha(int pacienteId)
    {
        var vm = _sp.GetRequiredService<FichaPacienteViewModel>();
        vm.Voltar += MostrarPacientes;
        vm.EditarSolicitado += AbrirEdicaoPaciente;
        BreadcrumbDetalhe = "Ficha do paciente";
        CurrentViewModel = vm;
        _ = vm.CarregarAsync(pacienteId);
    }

    /// <summary>Abre a tela de Pacientes já com o cadastro em edição (botão Editar da ficha).</summary>
    private void AbrirEdicaoPaciente(int pacienteId)
    {
        var vm = _sp.GetRequiredService<PacientesViewModel>();
        vm.FichaSolicitada += AbrirFicha;
        DefinirSecao(Secao.Pacientes);
        CurrentViewModel = vm;
        _ = vm.CarregarEEditarAsync(pacienteId);
    }
}
