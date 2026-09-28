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
    }
}
