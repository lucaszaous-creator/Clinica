using System;
using System.Windows;
using System.Windows.Controls;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Alertas;

/// <summary>Diálogo para registrar a glosa de uma guia (data + motivo da tabela ANS + complemento).</summary>
public partial class GlosaWindow : Window
{
    public DateOnly DataGlosa { get; private set; }
    public string? Motivo { get; private set; }

    /// <summary>Código do motivo na tabela de glosas da ANS (nulo se não selecionado).</summary>
    public string? MotivoCodigo { get; private set; }

    public GlosaWindow(string descricaoGuia, int prazoRecursoDias = 30)
    {
        InitializeComponent();
        TxtGuia.Text = descricaoGuia;
        DpData.SelectedDate = DateTime.Today;
        CmbMotivo.ItemsSource = MotivosGlosa.Todos;
        TxtPrazo.Text = $"O prazo de recurso desta glosa será de {prazoRecursoDias} dias " +
                        "e aparecerá no painel de pendências até ser resolvido.";
    }

    /// <summary>Mostra o que o motivo escolhido significa, como evitá-lo e o que juntar para recorrer.</summary>
    private void Motivo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbMotivo.SelectedItem is not MotivoGlosa motivo)
        {
            PainelOrientacao.Visibility = Visibility.Collapsed;
            return;
        }

        PainelOrientacao.Visibility = Visibility.Visible;
        TxtSignificado.Text = motivo.Significado;
        TxtComoEvitar.Text = motivo.ComoEvitar;
        TxtLastro.Text = motivo.Lastro;
    }

    private void Guia_Click(object sender, RoutedEventArgs e)
        => new GuiaGlosasWindow((CmbMotivo.SelectedItem as MotivoGlosa)?.Codigo) { Owner = this }.ShowDialog();

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        DataGlosa = DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);
        Motivo = string.IsNullOrWhiteSpace(TxtMotivo.Text) ? null : TxtMotivo.Text.Trim();
        MotivoCodigo = (CmbMotivo.SelectedItem as MotivoGlosa)?.Codigo;
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
