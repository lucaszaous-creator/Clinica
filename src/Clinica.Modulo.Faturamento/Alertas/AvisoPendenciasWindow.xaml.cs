using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Clinica.Application.Modelos;

namespace Clinica.Desktop.Alertas;

/// <summary>Janela de aviso exibida na abertura e nos lembretes recorrentes, listando as baixas pendentes.</summary>
public partial class AvisoPendenciasWindow : Window
{
    public AvisoPendenciasWindow(IReadOnlyList<PendenciaCodigo> pendencias)
    {
        InitializeComponent();
        Lista.ItemsSource = pendencias;

        var atrasadas = pendencias.Count(p => p.DiasEmAtraso > 0);
        TxtResumo.Text = atrasadas > 0
            ? $"Ao abrir o sistema foram encontradas {pendencias.Count} pendências — {atrasadas} já atrasada(s). Dê baixa assim que possível para não perder o faturamento."
            : $"Ao abrir o sistema foram encontradas {pendencias.Count} pendências para faturar hoje.";
    }

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
