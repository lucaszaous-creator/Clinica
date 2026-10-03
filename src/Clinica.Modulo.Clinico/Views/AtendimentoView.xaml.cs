using System.Windows.Controls;
using System.Windows;

namespace Clinica.Clinico.Views;

/// <summary>A sessão escrita com o paciente na sala, com as últimas ao lado.</summary>
public partial class AtendimentoView : UserControl
{
    public AtendimentoView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => AjustarHistorico();
        HistoricoLateral.IsVisibleChanged += (_, _) =>
        {
            AjustarHistorico();
            if (!IsLoaded) return;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                if (!IsLoaded) return;
                if (HistoricoLateral.IsVisible) HistoricoLateral.FocarFechar();
                else
                {
                    EditorEvolucao.Focus();
                    System.Windows.Input.FocusManager.SetFocusedElement(System.Windows.Input.FocusManager.GetFocusScope(EditorEvolucao), EditorEvolucao);
                }
            }));
        };
    }
    private void AjustarHistorico()
    {
        // A consulta mantém toda a largura em notebooks. O histórico abre sobre a
        // folha quando as duas regiões não caberiam com campos legíveis.
        bool sobreposto = ActualWidth < 1050;
        Grid.SetColumn(HistoricoLateral, sobreposto ? 0 : 1);
        Grid.SetColumnSpan(HistoricoLateral, sobreposto ? 2 : 1);
        HistoricoLateral.Width = sobreposto ? double.NaN : 330;
        AreaEdicao.IsEnabled = !(sobreposto && HistoricoLateral.IsVisible);
    }
}
