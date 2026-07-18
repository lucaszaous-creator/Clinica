using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Domain;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Baixa em lote das pendências selecionadas no painel: uma data única e o número
/// da guia por linha. Linhas sem número de guia são simplesmente ignoradas.
/// </summary>
public partial class BaixaLoteWindow : Window
{
    /// <summary>Linha editável do lote (TextBox binda NumeroGuia direto, sem INPC).</summary>
    public sealed class Linha
    {
        public required int CodigoId { get; init; }
        public required string Descricao { get; init; }
        public string? NumeroGuia { get; set; }
    }

    public List<Linha> Linhas { get; }

    public DateOnly DataBaixa => DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);

    public BaixaLoteWindow(IEnumerable<PendenciaCodigo> itens)
    {
        InitializeComponent();
        Linhas = itens.Select(i => new Linha
        {
            CodigoId = i.CodigoId,
            Descricao = $"{i.PacienteNome} — {i.Tipo} ({(i.Ordem == OrdemCodigo.Primeiro ? "1º" : "2º")} código)"
        }).ToList();
        Lista.ItemsSource = Linhas;
        DpData.SelectedDate = DateTime.Today;
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
