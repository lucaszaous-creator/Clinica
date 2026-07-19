using System;
using System.Windows;

namespace Clinica.Desktop.Alertas;

/// <summary>Diálogo para registrar o envio de um lote TISS (data + protocolo da operadora).</summary>
public partial class EnvioLoteWindow : Window
{
    public DateOnly DataEnvio { get; private set; }
    public string? Protocolo { get; private set; }

    public EnvioLoteWindow(int numeroLote)
    {
        InitializeComponent();
        TxtTitulo.Text = $"Marcar lote nº {numeroLote} como enviado";
        DpData.SelectedDate = DateTime.Today;
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        DataEnvio = DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);
        Protocolo = string.IsNullOrWhiteSpace(TxtProtocolo.Text) ? null : TxtProtocolo.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
