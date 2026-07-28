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

        var grupos = new List<GrupoMenuModulo>();
        foreach (var modulo in _modulos)
        {
            var permitidos = modulo.Itens
                .Where(i => sessao is null || !sessao.Autenticado || sessao.Pode(i.Requer))
                .ToList();

            // Módulo cujos itens o usuário não pode ver não vira cabeçalho órfão.
            if (permitidos.Count == 0) continue;

            foreach (var item in permitidos)
            {
                item.Grupo = modulo.Nome;
                Itens.Add(item);
            }
            grupos.Add(new GrupoMenuModulo(modulo.Nome, permitidos));
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
