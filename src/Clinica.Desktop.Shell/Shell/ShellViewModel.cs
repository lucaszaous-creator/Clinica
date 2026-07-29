using System.Collections.ObjectModel;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Navegação do shell: monta a sidebar a partir dos módulos carregados e troca a
/// tela ativa. Equivale ao MainViewModel do faturamento, mas sem conhecer nenhuma
/// tela — quem resolve a View é o próprio módulo (<see cref="IModuloApp.CriarTela"/>).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IReadOnlyList<IModuloApp> _modulos;
    private readonly IServiceProvider _servicos;

    /// <summary>Título da janela (nome do app, ex.: "Recepção").</summary>
    public string Titulo { get; }

    public IReadOnlyList<GrupoMenuModulo> Grupos { get; }

    /// <summary>Todos os itens, em ordem — usado para achar o item por chave.</summary>
    public ObservableCollection<ItemMenuModulo> Itens { get; } = [];

    [ObservableProperty]
    private object? _telaAtual;

    [ObservableProperty]
    private string _tituloTela = string.Empty;

    /// <summary>Módulo da tela ativa — primeiro nível do breadcrumb.</summary>
    [ObservableProperty]
    private string _moduloAtual = string.Empty;

    /// <summary>Quem está usando o app — vai no canto da barra superior.</summary>
    public string UsuarioRotulo { get; }

    /// <summary>Data de hoje na barra superior — a mesma referência de todas as telas.</summary>
    public string DataHoje { get; } = DateTime.Today.ToString("ddd, dd/MM/yyyy");

    /// <summary>
    /// Sidebar recolhida (248 → 56px). Mesmo comportamento do app de faturamento: em
    /// 1366×768 recolher é o modo confortável, e o atalho é Ctrl+B.
    /// </summary>
    [ObservableProperty]
    private bool _menuRecolhido;

    // ===== Pesquisa global =====

    [ObservableProperty]
    private string _textoPesquisa = string.Empty;

    [ObservableProperty]
    private bool _pesquisaAberta;

    /// <summary>Seções que casam com o que foi digitado (paleta de comandos).</summary>
    public ObservableCollection<ItemMenuModulo> ResultadosPesquisa { get; } = [];

    public ShellViewModel(string titulo, IEnumerable<IModuloApp> modulos, IServiceProvider servicos)
    {
        Titulo = titulo;
        _modulos = modulos.ToList();
        _servicos = servicos;

        // Sem sessão (teste, ou app aberto por um caminho que não passa pelo login) o
        // menu aparece inteiro: filtrar por uma permissão que ninguém tem esconderia
        // tudo, e sidebar vazia parece defeito, não segurança.
        var sessao = servicos.GetService<SessaoUsuario>();
        UsuarioRotulo = sessao?.Rotulo ?? Environment.UserName;

        foreach (var modulo in _modulos)
        {
            // Pode() já libera quando não há sessão autenticada — a regra mora nela.
            foreach (var item in modulo.Itens.Where(i => sessao?.Pode(i.Requer) != false))
            {
                item.ModuloNome = modulo.Nome;
                Itens.Add(item);
            }
        }

        // Agrupa por SEÇÃO TEMÁTICA, não por módulo: no Gerente, que carrega os três,
        // agrupar por módulo daria cabeçalhos que explicam a arquitetura em vez do
        // trabalho. A ordem dos grupos é a do enum; dentro do grupo, a ordem em que os
        // módulos foram carregados — que já é a ordem do dia de trabalho.
        Grupos = Itens
            .GroupBy(i => i.Grupo)
            .OrderBy(g => g.Key)
            .Select(g => new GrupoMenuModulo(GruposSidebar.Rotulo(g.Key), g.ToList()))
            .ToList();

        // Abre no primeiro item disponível.
        if (Itens.Count > 0) Navegar(Itens[0]);
    }

    [RelayCommand]
    public void Navegar(ItemMenuModulo? item)
    {
        if (item is null) return;

        foreach (var i in Itens) i.EstaAtivo = ReferenceEquals(i, item);

        // O módulo dono é quem sabe construir a tela.
        foreach (var modulo in _modulos)
        {
            if (modulo.Nome != item.ModuloNome) continue;
            var tela = modulo.CriarTela(item.Chave, _servicos);
            if (tela is null) continue;

            TelaAtual = tela;
            TituloTela = item.Rotulo;
            ModuloAtual = GruposSidebar.Rotulo(item.Grupo);
            return;
        }
    }

    [RelayCommand]
    private void AlternarMenu() => MenuRecolhido = !MenuRecolhido;

    /// <summary>
    /// Pesquisa global: paleta de seções. Digitar e dar Enter navega para a primeira —
    /// num app de 15 telas, achar pelo nome é mais rápido do que caçar na sidebar,
    /// sobretudo com ela recolhida.
    ///
    /// Só seções, de propósito: buscar paciente daqui exigiria o shell saber qual tela
    /// de qual módulo abre uma ficha, e o shell não conhece tela nenhuma. Quem busca
    /// paciente é o <see cref="Componentes.SeletorPacienteViewModel"/>, dentro das telas.
    /// </summary>
    partial void OnTextoPesquisaChanged(string value)
    {
        ResultadosPesquisa.Clear();

        var termo = value.Trim();
        if (termo.Length == 0)
        {
            PesquisaAberta = false;
            return;
        }

        foreach (var item in Itens.Where(i =>
                     i.Rotulo.Contains(termo, StringComparison.OrdinalIgnoreCase)
                     || GruposSidebar.Rotulo(i.Grupo).Contains(termo, StringComparison.OrdinalIgnoreCase)))
            ResultadosPesquisa.Add(item);

        PesquisaAberta = ResultadosPesquisa.Count > 0;
    }

    [RelayCommand]
    private void NavegarResultado(ItemMenuModulo? item)
    {
        item ??= ResultadosPesquisa.FirstOrDefault();
        if (item is null) return;

        FecharPesquisa();
        Navegar(item);
    }

    [RelayCommand]
    private void FecharPesquisa()
    {
        PesquisaAberta = false;
        TextoPesquisa = string.Empty;
    }

}
