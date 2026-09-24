using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Item editável do catálogo de especialidades na tela de Configurações. Espelha
/// <see cref="EspecialidadeCadastro"/> com notificação de mudança.
/// </summary>
public partial class EspecialidadeEdicao : ObservableObject
{
    public string Codigo { get; }

    /// <summary>Especialidade embutida: participa da rotação da Petrobras; não pode ser excluída.</summary>
    public bool EhEmbutida { get; }

    public bool PodeExcluir => !EhEmbutida;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private bool _ativo = true;

    public EspecialidadeEdicao(EspecialidadeCadastro c)
    {
        Codigo = c.Codigo;
        EhEmbutida = EspecialidadeCatalogoService.EhEmbutida(c.Codigo);
        _nome = c.Nome;
        _ativo = c.Ativo;
    }

    public EspecialidadeCadastro ParaCadastro() => new()
    {
        Codigo = Codigo,
        Nome = Nome,
        Ativo = Ativo
    };
}
