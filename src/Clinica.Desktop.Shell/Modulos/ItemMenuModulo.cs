using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// Seção temática da sidebar. É a divisão da PROPOSTA COMERCIAL, e não a divisão por
/// módulo carregado.
///
/// A diferença importa no Gerente Geral, que carrega os três módulos: agrupar por módulo
/// lhe dava três cabeçalhos ("Recepção", "Financeiro", "Direção") que dizem de onde a tela
/// veio — informação de arquitetura, que só interessa a quem programa. Quem usa pergunta
/// outra coisa: "onde mexo no paciente?". Por isso Prontuário (que mora no módulo da
/// Recepção) e Faturamento (que mora no do Gerente) aparecem sob PACIENTE e FINANCEIRO,
/// junto do que é da mesma natureza.
///
/// A ordem dos valores é a ordem em que os grupos aparecem na sidebar.
/// </summary>
public enum GrupoSidebar
{
    /// <summary>O dia da clínica: painel, agenda, fila.</summary>
    Gestao,

    /// <summary>A pessoa atendida: cadastro, prontuário, prescrições.</summary>
    Paciente,

    /// <summary>O dinheiro: pacotes, caixa, faturamento, estoque.</summary>
    Financeiro,

    /// <summary>A leitura do negócio: marketing, BI, configurações.</summary>
    Inteligencia
}

/// <summary>Rótulo de cada grupo, como sai na sidebar.</summary>
public static class GruposSidebar
{
    public static string Rotulo(GrupoSidebar grupo) => grupo switch
    {
        GrupoSidebar.Gestao => "GESTÃO",
        GrupoSidebar.Paciente => "PACIENTE",
        GrupoSidebar.Financeiro => "FINANCEIRO",
        GrupoSidebar.Inteligencia => "INTELIGÊNCIA",
        _ => grupo.ToString().ToUpperInvariant()
    };
}

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

    /// <summary>
    /// Seção temática onde o item aparece. Declarada pelo módulo, porque só ele sabe a
    /// natureza da tela. O padrão existe para um módulo novo não deixar de funcionar por
    /// esquecer de declarar — mas todo item da suíte declara o seu explicitamente.
    /// </summary>
    public GrupoSidebar Grupo { get; init; } = GrupoSidebar.Gestao;

    /// <summary>
    /// Módulo dono — preenchido pelo shell ao montar o menu. É por ele que a navegação
    /// acha quem sabe construir a tela; não confundir com <see cref="Grupo"/>, que é
    /// onde o item aparece para quem usa.
    /// </summary>
    public string ModuloNome { get; internal set; } = string.Empty;

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

/// <summary>Grupo de itens na sidebar (um por seção temática presente).</summary>
public sealed record GrupoMenuModulo(string Nome, IReadOnlyList<ItemMenuModulo> Itens);
