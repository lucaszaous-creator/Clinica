using System;
using System.Windows;

namespace Clinica.Desktop.Alertas;

/// <summary>Diálogo para registrar a glosa de uma guia (data + motivo).</summary>
public partial class GlosaWindow : Window
{
    public DateOnly DataGlosa { get; private set; }
    public string? Motivo { get; private set; }

    public GlosaWindow(string descricaoGuia)
    {
        InitializeComponent();
        TxtGuia.Text = descricaoGuia;
        DpData.SelectedDate = DateTime.Today;
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        DataGlosa = DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);
        Motivo = string.IsNullOrWhiteSpace(TxtMotivo.Text) ? null : TxtMotivo.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
