using System.Collections.ObjectModel;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public ShellViewModel(string titulo, IEnumerable<IModuloApp> modulos, IServiceProvider servicos)
    {
        Titulo = titulo;
        _modulos = modulos.ToList();
        _servicos = servicos;

        var grupos = new List<GrupoMenuModulo>();
        foreach (var modulo in _modulos)
        {
            foreach (var item in modulo.Itens)
            {
                item.Grupo = modulo.Nome;
                Itens.Add(item);
            }
            grupos.Add(new GrupoMenuModulo(modulo.Nome, modulo.Itens));
        }
        Grupos = grupos;

        // Abre no primeiro item do primeiro módulo.
        if (Itens.Count > 0) Navegar(Itens[0]);
    }

    [RelayCommand]
    public void Navegar(ItemMenuModulo? item)
    {
        if (item is null) return;

        foreach (var i in Itens) i.EstaAtivo = ReferenceEquals(i, item);

        // O primeiro módulo que souber construir a chave é o dono da tela.
        foreach (var modulo in _modulos)
        {
            if (modulo.Nome != item.Grupo) continue;
            var tela = modulo.CriarTela(item.Chave, _servicos);
            if (tela is null) continue;

            TelaAtual = tela;
            TituloTela = item.Rotulo;
            ModuloAtual = modulo.Nome;
            return;
        }
    }
}
