using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.ViewModels;

/// <summary>Seções navegáveis do app (itens da sidebar; Baixa e FichaPaciente são telas de detalhe).</summary>
public enum Secao
{
    Pendencias,
    NaoConformidades,
    Agenda,
    Atendimento,
    Consultas,
    ConsultaGuias,
    Faturados,
    Glosas,
    Tiss,
    Pacientes,
    Relatorios,
    Parametros
}

/// <summary>Item do menu lateral (e da pesquisa global do shell).</summary>
public sealed partial class ItemMenu : ObservableObject
{
    public required Secao Secao { get; init; }
    public required string Rotulo { get; init; }
    /// <summary>Glifo Segoe Fluent/MDL2.</summary>
    public required string Glifo { get; init; }
    /// <summary>Nome do módulo (cabeçalho de grupo na sidebar e 1º nível do breadcrumb).</summary>
    public required string Grupo { get; init; }

    /// <summary>
    /// Permissão necessária para o item APARECER (parcela 45). É a mesma ideia do
    /// <c>ItemMenuModulo.Requer</c> da suíte, e é a metade VISÍVEL da barreira: quem não
    /// pode configurar o faturamento não vê "Configurações" na sidebar. A metade que
    /// IMPEDE é o <c>SessaoUsuario.Atual.Exigir(...)</c> dentro de cada comando que grava —
    /// só esconder o item seria enfeite, porque a pesquisa global e os atalhos chegariam
    /// na tela por outro caminho.
    ///
    /// <see cref="Permissao.Nenhuma"/> = todo mundo vê.
    /// </summary>
    public Permissao Requer { get; init; } = Permissao.Nenhuma;

    /// <summary>Quando preenchido, o item é um resultado de pesquisa de PACIENTE (abre a ficha).</summary>
    public int? PacienteId { get; init; }

    [ObservableProperty]
    private bool _estaAtivo;
}

/// <summary>Grupo de itens da sidebar (módulo).</summary>
public sealed record GrupoMenu(string Nome, IReadOnlyList<ItemMenu> Itens);
