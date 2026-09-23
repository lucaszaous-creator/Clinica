using System.Windows;
using Clinica.Application.Modelos;
using System.Linq;
using Clinica.Domain;
using Clinica.Domain.Regras;

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

        /// <summary>
        /// Forma do número da guia NESTE convênio. Resolvida na montagem da lista, porque
        /// aqui cada linha pode ser de uma operadora diferente — é o caso normal de uma
        /// baixa em lote, e é por isso que não há uma dica única no alto da janela.
        /// </summary>
        public FormatoNumeroGuia Formato { get; init; } = FormatoNumeroGuia.SemValidacao;
    }

    public List<Linha> Linhas { get; }

    public DateOnly DataBaixa => DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);

    public BaixaLoteWindow(IEnumerable<PendenciaCodigo> itens)
    {
        InitializeComponent();
        Linhas = itens.Select(i => new Linha
        {
            CodigoId = i.CodigoId,
            // Rótulo central, nunca o enum cru: "ConsultaEspecialidade" na linha é o
            // defeito da parcela 41 — o Ordem estava rotulado à mão e o Tipo passou batido.
            Descricao = $"{i.PacienteNome} — {RotulosEnum.De(i.Tipo)} ({RotulosEnum.De(i.Ordem)})",
            Formato = CatalogoConvenios.FormatoDoNumeroDaGuia(i.ConvenioCodigo ?? i.Convenio.ToString())
        }).ToList();
        Lista.ItemsSource = Linhas;
        DpData.SelectedDate = DateTime.Today;
    }

    /// <summary>
    /// Critica os números ANTES de processar. O serviço recusa cada guia de qualquer jeito
    /// — é lá que a regra mora —, mas aqui a recusa chegaria NO MEIO do lote: as linhas
    /// anteriores já teriam sido baixadas, e a pessoa ficaria com um lote pela metade e uma
    /// mensagem sobre uma guia que ela precisa procurar na lista.
    ///
    /// Linha em branco continua sendo ignorada, como sempre foi: "ainda não tenho o número"
    /// é resposta legítima num lote.
    /// </summary>
    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        var recusadas = Linhas
            .Select(l => (l, critica: RegraNumeroGuia.Criticar(l.NumeroGuia, l.Formato)))
            .Where(x => x.critica is not null)
            .ToList();

        if (recusadas.Count > 0)
        {
            var detalhe = string.Join("\n", recusadas.Select(x => $"• {x.l.Descricao}: {x.critica}"));
            MessageBox.Show(this,
                $"{recusadas.Count} guia(s) com o número fora do formato do convênio. "
                + "Corrija antes de confirmar — nenhuma baixa foi feita.\n\n" + detalhe,
                "Baixa em lote", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
