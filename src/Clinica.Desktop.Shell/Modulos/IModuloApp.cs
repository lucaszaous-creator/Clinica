using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Um módulo da suíte (Recepção, Financeiro, Faturamento...). O executável é apenas
/// uma casca que escolhe quais módulos carregar — o shell monta a sidebar a partir
/// dos módulos registrados, na ordem informada.
///
/// Nenhum módulo conhece os outros: quem quiser o Gerente Geral registra todos.
/// </summary>
public interface IModuloApp
{
    /// <summary>Nome do módulo — vira o cabeçalho de grupo na sidebar.</summary>
    string Nome { get; }

    /// <summary>Itens de menu que este módulo publica.</summary>
    IReadOnlyList<ItemMenuModulo> Itens { get; }

    /// <summary>Registra no DI as ViewModels e serviços próprios do módulo.</summary>
    void Registrar(IServiceCollection servicos);

    /// <summary>
    /// Constrói a tela de um item (a View já com o DataContext resolvido).
    /// Recebe a <paramref name="chave"/> declarada em <see cref="ItemMenuModulo.Chave"/>.
    /// Retorna null se a chave não pertence a este módulo.
    /// </summary>
    object? CriarTela(string chave, IServiceProvider servicos);
}
