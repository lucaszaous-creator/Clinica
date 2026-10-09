using System.Windows;
using System.Windows.Controls;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.WebClinica;

namespace Clinica.Clinico.Views;

public partial class PrescricoesClinicasView : UserControl
{
    private PainelClinicoWeb? _painel;
    public PrescricoesClinicasView()
    {
        InitializeComponent();
        Loaded += (_, _) => Montar();
        DataContextChanged += (_, _) => { Desmontar(); if (IsLoaded) Montar(); };
        Unloaded += (_, _) => Desmontar();
    }
    private void Montar()
    {
        if (_painel is not null || DataContext is not PrescricoesClinicasViewModel vm) return;
        var adaptador = new PrescricoesWebAdapter(vm);
        _painel = new PainelClinicoWeb("prescricoes", adaptador.ObterEstado, adaptador.ExecutarAsync, adaptador.ExigirAcesso);
        ConteudoPrescricoes.Children.Add(_painel);
    }
    private void Desmontar()
    {
        _painel?.Dispose(); _painel = null;
        ConteudoPrescricoes.Children.Clear();
    }
}
