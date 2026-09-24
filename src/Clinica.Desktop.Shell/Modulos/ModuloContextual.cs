using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>Disponibiliza componentes de contexto sem acrescentar o menu de outro aplicativo.</summary>
public sealed class ModuloContextual(IModuloApp modulo, params string[] visiveis) : IModuloApp
{
    public string Nome => modulo.Nome;
    public IReadOnlyList<ItemMenuModulo> Itens { get; } = modulo.Itens.Select(i => new ItemMenuModulo
    {
        Chave = i.Chave, Rotulo = i.Rotulo, Grupo = i.Grupo, Glifo = i.Glifo, Icone = i.Icone,
        Requer = i.Requer, RequerAlgum = i.RequerAlgum, PerfilExclusivo = i.PerfilExclusivo, Oculto = !visiveis.Contains(i.Chave), Abas = i.Abas
    }).ToList();
    public void Registrar(IServiceCollection servicos) => modulo.Registrar(servicos);
    public object? CriarTela(string chave, IServiceProvider servicos) => modulo.CriarTela(chave, servicos);
}
