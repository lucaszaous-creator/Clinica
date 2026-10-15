using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class AcompanhamentoView : UserControl
{
    private readonly DispatcherTimer _atualizacao = new() { Interval = TimeSpan.FromSeconds(60) };
    public AcompanhamentoView()
    {
        InitializeComponent();
        _atualizacao.Tick += async (_, _) => { if (IsVisible && DataContext is AcompanhamentoViewModel vm) await vm.RecarregarAsync(); };
        Loaded += (_, _) => _atualizacao.Start();
        Unloaded += (_, _) => _atualizacao.Stop();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is AcompanhamentoViewModel antigo) antigo.PropertyChanged -= AoMudarVm;
            if (e.NewValue is AcompanhamentoViewModel novo) novo.PropertyChanged += AoMudarVm;
            AjustarColunas();
        };
    }

    private void AoMudarVm(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AcompanhamentoViewModel.PainelVisivel) or null or "") AjustarColunas();
    }

    /// <summary>Com o painel de contato aberto, a tabela fica com PACIENTE e CONTATO: as outras
    /// duas colunas o painel já mostra, e as quatro não cabem ao lado dele em 880/1024.</summary>
    private void AjustarColunas()
    {
        var comPainel = DataContext is AcompanhamentoViewModel { PainelVisivel: true };
        var v = comPainel ? Visibility.Collapsed : Visibility.Visible;
        ColunaUltimaSessao.Visibility = v;
        ColunaTratamento.Visibility = v;
    }
}
