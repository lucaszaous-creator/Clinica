using System.Windows;
using Clinica.Clinico.ViewModels;
using Clinica.Clinico.WebInfusao;
using Clinica.Desktop.Shell.WebClinica;

namespace Clinica.Clinico.Janelas;

/// <summary>Editor local React; a gravação e a liberação continuam no fluxo clínico existente.</summary>
public partial class PrescricaoInternaWindow : Window
{
    private readonly PrescricaoInternaEdicaoViewModel _vm;
    private readonly PainelClinicoWeb _painel;
    private bool _liberou;

    public PrescricaoInternaWindow(PrescricaoInternaEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        var adaptador = new InfusaoWebAdapter(vm, Close);
        _painel = new PainelClinicoWeb("infusao", adaptador.ObterEstado, adaptador.ExecutarAsync, InfusaoWebAdapter.ExigirAcesso);
        Content = _painel;
        vm.Fechar = () => { _liberou = true; DialogResult = true; };
        Closing += (_, e) => { if (vm.Ocupado && !_liberou) e.Cancel = true; };
        Closed += (_, _) => { vm.Fechar = null; _painel.Dispose(); };
    }

    /// <summary>A folha foi liberada nesta janela — quem abriu recarrega a lista.</summary>
    public bool Assinou => _vm.Assinou;
}
