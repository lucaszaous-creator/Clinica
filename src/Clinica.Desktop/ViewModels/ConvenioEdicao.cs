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

    /// <summary>
    /// Forma do número da guia neste convênio (parcela 45). Vale para QUALQUER entrada do
    /// catálogo, embutida ou não — a Unimed é embutida e tem formato, a Sul América é
    /// personalizada e também tem —, por isso fica fora do painel da regra genérica logo
    /// abaixo, que só aparece para os personalizados.
    /// </summary>
    [ObservableProperty] private FormatoNumeroGuia _formatoNumeroGuia = FormatoNumeroGuia.SemValidacao;

    /// <summary>
    /// Registro ANS desta operadora (parcela 60): é o destino que sai no cabeçalho do
    /// lote TISS dela. Em branco, o lote usa o registro global do prestador.
    /// </summary>
    [ObservableProperty] private string? _registroAnsOperadora;

    /// <summary>
    /// Este convênio gera guia para faturar? (parcela 60) Desmarcado, é o PARTICULAR.
    ///
    /// Fora do painel da regra genérica pela MESMA razão do formato acima: vale para
    /// qualquer família, e dentro dele seria uma caixinha morta num convênio embutido.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumoGuia))]
    private bool _geraGuia = true;

    /// <summary>
    /// Coluna "Guias" da lista de convênios (parcela 69). Escrever só "Não" faria a
    /// linha do particular parecer convênio mal configurado; o que ela é tem nome.
    /// </summary>
    public string ResumoGuia => GeraGuia ? "Gera guia" : "Particular (sem guia)";

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
        _formatoNumeroGuia = c.FormatoNumeroGuia;
        _registroAnsOperadora = c.RegistroAnsOperadora;
        _geraGuia = c.GeraGuia;
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
        FormatoNumeroGuia = FormatoNumeroGuia,
        RegistroAnsOperadora = string.IsNullOrWhiteSpace(RegistroAnsOperadora)
            ? null : RegistroAnsOperadora.Trim(),
        GeraGuia = GeraGuia,
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
