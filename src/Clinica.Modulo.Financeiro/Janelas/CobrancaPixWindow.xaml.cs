using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>
/// A janela do Pix. Ela não fecha sozinha ao gerar o código: o balcão precisa do texto
/// na tela para copiar e mostrar ao paciente, e uma janela que some depois do clique
/// obrigaria a refazer tudo.
/// </summary>
public partial class CobrancaPixWindow : Window
{
    public CobrancaPixWindow(CobrancaPixViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
