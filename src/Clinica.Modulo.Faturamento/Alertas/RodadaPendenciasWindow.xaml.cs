using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Regras;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Janela de "rodar as pendências": lista guias para uma decisão explícita — dar baixa (informando o
/// nº da guia) ou registrar como NÃO CONFORMIDADE (com justificativa). No modo BLOQUEANTE traz as
/// guias cujo prazo desde a data prevista venceu e só fecha depois que toda guia tiver uma decisão.
/// </summary>
public partial class RodadaPendenciasWindow : Window
{
    // A linha, o texto do topo e a conferência moram na Application (DecisoesDaRodada): o
    // painel Blazor abre a MESMA rodada, e duas cópias divergiriam na primeira correção.
    public IReadOnlyList<LinhaDaRodada> Linhas { get; }

    public DateOnly DataBaixa => DateOnly.FromDateTime(DpData.SelectedDate ?? DateTime.Today);

    private readonly bool _bloqueante;
    private bool _concluido;

    public RodadaPendenciasWindow(IEnumerable<PendenciaCodigo> itens, RodadaPendenciasStatus status, bool bloqueante)
    {
        InitializeComponent();
        _bloqueante = bloqueante;
        Linhas = DecisoesDaRodada.Montar(itens);
        Lista.ItemsSource = Linhas;
        DpData.SelectedDate = DateTime.Today;
        TxtResumo.Text = DecisoesDaRodada.Resumo(Linhas.Count, status, bloqueante);
        if (bloqueante)
            BtnCancelar.Visibility = Visibility.Collapsed;
    }

    private void Concluir_Click(object sender, RoutedEventArgs e)
    {
        if (DecisoesDaRodada.ImpedimentoParaConcluir(Linhas, _bloqueante) is { } impedimento)
        {
            MessageBox.Show(this, impedimento, "Rodar pendências", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _concluido = true;
        DialogResult = true;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        // No modo bloqueante, não deixa fechar (X/Alt+F4) sem concluir a rodada.
        if (_bloqueante && !_concluido)
            e.Cancel = true;
        base.OnClosing(e);
    }
}
