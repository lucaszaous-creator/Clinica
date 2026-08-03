using System.Windows;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// O mapa corporal da sessão, em janela.
///
/// Ela NÃO grava: o mapa é 1:1 com a evolução e só se efetiva depois que a sessão existe
/// (é o `Mapa.SalvarAsync(evolucaoId)` que o Salvar da tela de atendimento chama). Fechar
/// em "Concluir" apenas devolve o foco — os pontos marcados continuam no ViewModel, que é
/// o mesmo objeto que a tela de atendimento segura. "Descartar" fecha sem confirmar, e
/// também não desfaz nada: desfazer marcação ponto a ponto é o que o botão "Limpar" do
/// próprio mapa faz, e duplicar isso no fechamento daria dois significados para a mesma
/// ação.
/// </summary>
public partial class MapaCorporalWindow : Window
{
    public MapaCorporalWindow(MapaCorporalViewModel vm, string titulo)
    {
        InitializeComponent();
        Titulo = titulo;
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

    private void Concluir(object remetente, RoutedEventArgs e) => DialogResult = true;
}
