using System.Windows;
using System.Windows.Controls;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Guia consultável dos motivos de glosa. A cliente relatou nunca saber por que as
/// guias caem: o demonstrativo da operadora chega com um código seco. Aqui cada código
/// vira significado, prevenção e o lastro que sustenta o recurso.
/// </summary>
public partial class GuiaGlosasWindow : Window
{
    /// <param name="codigoInicial">Motivo já selecionado ao abrir (vindo de uma glosa).</param>
    public GuiaGlosasWindow(string? codigoInicial = null)
    {
        InitializeComponent();

        Filtrar(null);
        Loaded += (_, _) =>
        {
            var alvo = MotivosGlosa.Obter(codigoInicial);
            ListaMotivos.SelectedItem = alvo is null
                ? ListaMotivos.Items.Count > 0 ? ListaMotivos.Items[0] : null
                : ListaMotivos.Items.OfType<MotivoGlosa>().FirstOrDefault(m => m.Codigo == alvo.Codigo);
        };
    }

    private void Busca_TextChanged(object sender, TextChangedEventArgs e) => Filtrar(TxtBusca.Text);

    private void Filtrar(string? termo)
    {
        var selecionado = ListaMotivos.SelectedItem as MotivoGlosa;

        ListaMotivos.ItemsSource = string.IsNullOrWhiteSpace(termo)
            ? MotivosGlosa.Todos
            : MotivosGlosa.Todos.Where(m =>
                m.Codigo.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                m.Descricao.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                m.Significado.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                m.ComoEvitar.Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();

        // Mantém a seleção quando ela sobrevive ao filtro; senão, mostra o primeiro.
        ListaMotivos.SelectedItem = ListaMotivos.Items.OfType<MotivoGlosa>()
            .FirstOrDefault(m => m.Codigo == selecionado?.Codigo)
            ?? (ListaMotivos.Items.Count > 0 ? ListaMotivos.Items[0] : null);
    }

    private void Motivo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaMotivos.SelectedItem is not MotivoGlosa motivo)
        {
            PainelDetalhe.Visibility = Visibility.Collapsed;
            return;
        }

        PainelDetalhe.Visibility = Visibility.Visible;
        TxtCodigo.Text = motivo.Codigo;
        TxtDescricao.Text = motivo.Descricao;
        TxtSignificado.Text = motivo.Significado;
        TxtComoEvitar.Text = motivo.ComoEvitar;
        TxtLastro.Text = motivo.Lastro;

        TxtRecurso.Text = motivo.ValeRecorrer ? "Costuma valer o recurso" : "Recurso raramente prospera";
        BadgeRecurso.Style = (Style)FindResource(motivo.ValeRecorrer ? "Badge.Sucesso" : "Badge.Aviso");
        AvisoNaoRecorrer.Visibility = motivo.ValeRecorrer ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
