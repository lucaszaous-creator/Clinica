using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Item editável do catálogo de convênios na tela de Configurações. Espelha
/// <see cref="ConvenioCadastro"/> com notificação de mudança, para o grid e o
/// painel de edição ficarem sempre sincronizados.
/// </summary>
public partial class ConvenioEdicao : ObservableObject
{
    public string Codigo { get; }

    /// <summary>Convênio embutido: a regra vive no código; não muda de família nem pode ser excluído.</summary>
    public bool EhEmbutido { get; }

    public bool FamiliaEditavel => !EhEmbutido;
    public bool PodeExcluir => !EhEmbutido;

    [ObservableProperty] private string _nome = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhPersonalizado))]
    private Convenio _familia;

    [ObservableProperty] private bool _ativo = true;

    // Configuração da regra genérica (só tem efeito quando Familia == Personalizado)
    [ObservableProperty] private bool _fazEletro;
    [ObservableProperty] private bool _temSegundoCodigo;
    [ObservableProperty] private FormaObtencao _formaSegundoCodigo = FormaObtencao.Sistema;
    [ObservableProperty] private bool _segundoCodigoDependeApp;
    [ObservableProperty] private int _diasSegundoCodigo = 1;
    [ObservableProperty] private bool _faturaBsv = true;
    [ObservableProperty] private bool _inverteDatasBsv;
    [ObservableProperty] private int? _validadeConsultaDias;
    [ObservableProperty] private Categoria _categoriaComApp = Categoria.Verde;
    [ObservableProperty] private Categoria _categoriaSemApp = Categoria.Amarela;

    /// <summary>Fatura pela regra genérica configurável (mostra o painel de ajustes).</summary>
    public bool EhPersonalizado => Familia == Convenio.Personalizado;

    public ConvenioEdicao(ConvenioCadastro c)
    {
        Codigo = c.Codigo;
        EhEmbutido = ConvenioCatalogoService.EhEmbutido(c.Codigo);
        _nome = c.Nome;
        _familia = c.Familia;
        _ativo = c.Ativo;
        _fazEletro = c.FazEletro;
        _temSegundoCodigo = c.TemSegundoCodigo;
        _formaSegundoCodigo = c.FormaSegundoCodigo;
        _segundoCodigoDependeApp = c.SegundoCodigoDependeApp;
        _diasSegundoCodigo = c.DiasSegundoCodigo;
        _faturaBsv = c.FaturaBsv;
        _inverteDatasBsv = c.InverteDatasBsv;
        _validadeConsultaDias = c.ValidadeConsultaDias;
        _categoriaComApp = c.CategoriaComApp;
        _categoriaSemApp = c.CategoriaSemApp;
    }

    public ConvenioCadastro ParaCadastro() => new()
    {
        Codigo = Codigo,
        Nome = Nome,
        Familia = Familia,
        Ativo = Ativo,
        FazEletro = FazEletro,
        TemSegundoCodigo = TemSegundoCodigo,
        FormaSegundoCodigo = FormaSegundoCodigo,
        SegundoCodigoDependeApp = SegundoCodigoDependeApp,
        DiasSegundoCodigo = DiasSegundoCodigo,
        FaturaBsv = FaturaBsv,
        InverteDatasBsv = InverteDatasBsv,
        ValidadeConsultaDias = ValidadeConsultaDias,
        CategoriaComApp = CategoriaComApp,
        CategoriaSemApp = CategoriaSemApp
    };
}
