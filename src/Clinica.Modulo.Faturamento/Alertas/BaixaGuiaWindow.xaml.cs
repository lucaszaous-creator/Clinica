using System;
using System.Windows;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Baixa de UMA guia, para ser dada no mesmo instante em que ela é gerada — sem passar
/// pelo painel de pendências. O número da guia é obrigatório, como na tela de Baixa:
/// baixa sem número não deixa lastro do que foi efetivado no convênio.
/// </summary>
public partial class BaixaGuiaWindow : Window
{
    public DateOnly DataBaixa { get; private set; }
    public string NumeroGuia { get; private set; } = string.Empty;
    public string? Observacao { get; private set; }

    public BaixaGuiaWindow(string descricaoGuia)
    {
        InitializeComponent();
        TxtGuia.Text = descricaoGuia;
        DpData.SelectedDate = DateTime.Today;
        Loaded += (_, _) => TxtNumero.Focus();
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNumero.Text))
        {
            TxtErro.Text = "Informe o número da guia gerada no sistema do convênio.";
            TxtErro.Visibility = Visibility.Visible;
            TxtNumero.Focus();
            return;
        }

        DataBaixa = DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);
        NumeroGuia = TxtNumero.Text.Trim();
        Observacao = string.IsNullOrWhiteSpace(TxtObservacao.Text) ? null : TxtObservacao.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
