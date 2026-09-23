using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Item editável do catálogo de modalidades na tela de Configurações. Espelha
/// <see cref="ModalidadeCadastro"/> com notificação de mudança.
/// </summary>
public partial class ModalidadeEdicao : ObservableObject
{
    public string Codigo { get; }

    /// <summary>Modalidade embutida: o comportamento vive no código; não muda de base nem pode ser excluída.</summary>
    public bool EhEmbutida { get; }

    public bool BaseEditavel => !EhEmbutida;
    public bool PodeExcluir => !EhEmbutida;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private ModalidadeAtendimento _base;
    [ObservableProperty] private bool _ativo = true;

    public ModalidadeEdicao(ModalidadeCadastro c)
    {
        Codigo = c.Codigo;
        EhEmbutida = ModalidadeCatalogoService.EhEmbutida(c.Codigo);
        _nome = c.Nome;
        _base = c.Base;
        _ativo = c.Ativo;
    }

    public ModalidadeCadastro ParaCadastro() => new()
    {
        Codigo = Codigo,
        Nome = Nome,
        Base = Base,
        Ativo = Ativo
    };
}
