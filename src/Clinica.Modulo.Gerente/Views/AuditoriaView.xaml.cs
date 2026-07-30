using System.Windows.Controls;

namespace Clinica.Gerente.Views;

/// <summary>
/// A trilha de auditoria: quem fez o quê, e quando. Somente leitura — registro de
/// auditoria que se pode editar ou apagar não é auditoria, é rascunho.
/// </summary>
public partial class AuditoriaView : UserControl
{
    public AuditoriaView()
    {
        InitializeComponent();
    }
}
