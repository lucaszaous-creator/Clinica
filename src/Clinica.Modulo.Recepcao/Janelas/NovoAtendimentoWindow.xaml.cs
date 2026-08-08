using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// A janela do lançamento avulso (parcela 47).
///
/// Ela NÃO fecha ao lançar, e isso é decisão: o resultado — as guias que nasceram, com a
/// data em que cada uma libera — é a parte que a recepcionista precisa LER, muitas vezes
/// com o paciente ainda na frente ("a segunda guia eu busco amanhã"). Fechar na hora do
/// sucesso levaria embora justamente a informação pela qual o produto existe.
///
/// Quem fecha é a pessoa, pelo botão Fechar (ou Esc). "Lançar outro" zera o formulário sem
/// fechar, porque no balcão a fila costuma vir em três.
/// </summary>
public partial class NovoAtendimentoWindow : Window
{
    public NovoAtendimentoWindow(NovoAtendimentoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
