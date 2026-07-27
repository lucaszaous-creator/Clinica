using Clinica.Desktop.Shell.Modulos;
using Clinica.Recepcao.ViewModels;
using Clinica.Recepcao.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// Módulo da Recepção. Publica os itens de menu e sabe construir as telas
/// correspondentes — o shell não conhece nenhuma delas.
/// </summary>
public sealed class ModuloRecepcao : IModuloApp
{
    public const string ChaveFila = "fila";

    public string Nome => "Recepção";

    public IReadOnlyList<ItemMenuModulo> Itens { get; } =
    [
        new ItemMenuModulo { Chave = ChaveFila, Rotulo = "Fila de hoje", Glifo = "\uE8FD" }
    ];

    public void Registrar(IServiceCollection servicos)
    {
        servicos.AddTransient<FilaViewModel>();
    }

    public object? CriarTela(string chave, IServiceProvider servicos) => chave switch
    {
        ChaveFila => new FilaView { DataContext = servicos.GetRequiredService<FilaViewModel>() },
        _ => null
    };
}
