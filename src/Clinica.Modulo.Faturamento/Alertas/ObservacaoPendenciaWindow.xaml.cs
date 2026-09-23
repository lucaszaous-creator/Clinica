using System;
using System.Windows;
using Clinica.Application.Modelos;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Registra/edita a observação sobre por que uma guia ainda não foi baixada.
/// O texto anotado fica visível na pendência para consulta futura.
/// </summary>
public partial class ObservacaoPendenciaWindow : Window
{
    /// <summary>Texto final (null quando o campo é deixado em branco = limpar a anotação).</summary>
    public string? Observacao { get; private set; }

    public ObservacaoPendenciaWindow(PendenciaCodigo pendencia)
    {
        InitializeComponent();
        TxtTitulo.Text = $"Observação — {pendencia.PacienteNome}";
        TxtObservacao.Text = pendencia.ObservacaoPendencia ?? string.Empty;

        if (pendencia.ObservacaoPendenciaEm is { } quando)
        {
            TxtInfo.Text = $"Anotado em {quando:dd/MM/yyyy HH:mm}. Editar substitui o texto; apagar tudo remove a observação.";
            TxtInfo.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => { TxtObservacao.Focus(); TxtObservacao.CaretIndex = TxtObservacao.Text.Length; };
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        Observacao = string.IsNullOrWhiteSpace(TxtObservacao.Text) ? null : TxtObservacao.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
