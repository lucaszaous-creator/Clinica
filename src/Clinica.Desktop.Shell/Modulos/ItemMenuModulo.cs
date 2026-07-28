using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Item do menu lateral publicado por um módulo. Substitui o enum <c>Secao</c> do
/// faturamento: como os módulos são compostos em tempo de execução, a identidade do
/// item é uma string (<see cref="Chave"/>) e não um valor de enum fechado.
/// </summary>
public sealed partial class ItemMenuModulo : ObservableObject
{
    /// <summary>Identifica a tela dentro do módulo (ex.: "fila"). Única dentro do módulo.</summary>
    public required string Chave { get; init; }

    /// <summary>Texto exibido na sidebar.</summary>
    public required string Rotulo { get; init; }

    /// <summary>Glifo Segoe Fluent/MDL2.</summary>
    public required string Glifo { get; init; }

    /// <summary>Nome do módulo dono — preenchido pelo shell ao montar o menu.</summary>
    public string Grupo { get; internal set; } = string.Empty;

    /// <summary>
    /// Permissão exigida para o item aparecer na sidebar (parcela 5).
    /// <see cref="Permissao.Nenhuma"/>, que é o padrão, significa "sempre visível" —
    /// assim um módulo que ainda não declarou permissão nenhuma continua funcionando
    /// exatamente como antes.
    /// </summary>
    public Permissao Requer { get; init; } = Permissao.Nenhuma;

    [ObservableProperty]
    private bool _estaAtivo;
}

/// <summary>Grupo de itens na sidebar (um por módulo carregado).</summary>
public sealed record GrupoMenuModulo(string Nome, IReadOnlyList<ItemMenuModulo> Itens);
