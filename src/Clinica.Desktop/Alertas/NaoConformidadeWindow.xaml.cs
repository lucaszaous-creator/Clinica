using System.Windows;
using Clinica.Application.Modelos;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Justificativa para marcar uma guia pendente como NÃO CONFORMIDADE por decisão do usuário — sem
/// esperar o prazo da rodada vencer. A justificativa é obrigatória (o botão só confirma com texto).
/// </summary>
public partial class NaoConformidadeWindow : Window
{
    /// <summary>Justificativa informada (nunca vazia quando o diálogo confirma).</summary>
    public string Justificativa { get; private set; } = string.Empty;

    public NaoConformidadeWindow(PendenciaCodigo pendencia)
    {
        InitializeComponent();
        TxtTitulo.Text = $"Não conformidade — {pendencia.PacienteNome}";
        Loaded += (_, _) => TxtJustificativa.Focus();
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtJustificativa.Text))
        {
            MessageBox.Show(this,
                "Informe uma justificativa para registrar a não conformidade.",
                "Não conformidade", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Justificativa = TxtJustificativa.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
