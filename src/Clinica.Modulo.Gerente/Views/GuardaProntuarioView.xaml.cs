using System.Windows.Controls;

namespace Clinica.Gerente.Views;

/// <summary>
/// A guarda do prontuário: o que está guardado, até quando, e como tirar cópia disso.
///
/// É a tela que responde à auditoria de fornecedor da cliente (Lei 13.787/2018, art. 6º).
/// Não tem botão de eliminar: o prazo é piso de guarda, não agendamento de destruição.
/// </summary>
public partial class GuardaProntuarioView : UserControl
{
    public GuardaProntuarioView()
    {
        InitializeComponent();
    }
}
