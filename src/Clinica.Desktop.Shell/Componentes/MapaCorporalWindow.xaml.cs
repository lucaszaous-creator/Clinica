using System.Windows;
using System.ComponentModel;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O mapa corporal da sessão, em janela.
///
/// Ela NÃO grava: o mapa é 1:1 com a evolução e só se efetiva depois que a sessão existe
/// (evolução e mapa são confirmados juntos pelo salvamento da sessão). Fechar
/// em "Usar mapa nesta sessão" apenas devolve o foco — os pontos continuam no ViewModel, que é
/// o mesmo objeto que a tela de atendimento segura. "Descartar" fecha sem confirmar, e
/// restaura o rascunho que existia antes de abrir a janela, inclusive as observações.
/// Modelos salvos explicitamente são cadastros separados e permanecem disponíveis.
/// </summary>
public partial class MapaCorporalWindow : Window
{
    private readonly MapaCorporalViewModel _mapa;
    private readonly RascunhoMapaCorporal _aoAbrir;

    public MapaCorporalWindow(MapaCorporalViewModel vm, string titulo)
    {
        _mapa = vm;
        _aoAbrir = vm.CapturarRascunho();
        InitializeComponent();
        Titulo = titulo;
        vm.Titulo = titulo;
        DataContext = vm;
    }

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(MapaCorporalWindow),
            new PropertyMetadata("Mapa corporal"));

    /// <summary>De quem é a sessão — o cabeçalho da janela.</summary>
    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_mapa.Ocupado) e.Cancel = true;
        else if (DialogResult != true) _mapa.RestaurarRascunho(_aoAbrir);
        base.OnClosing(e);
    }

    private void Concluir(object remetente, RoutedEventArgs e)
    {
        if (_mapa.PodeEditar) DialogResult = true;
    }
}
