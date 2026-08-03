using System.Windows.Controls;

namespace Clinica.Clinico.Componentes;

/// <summary>
/// O painel de escolha de paciente das telas clínicas.
///
/// Sem código: o <see cref="ViewModels.SeletorClinicoViewModel"/> resolve os blocos e a
/// seleção. A View existe para que as cinco telas do consultório não repitam sessenta
/// linhas de XAML de lista — repetição que, na primeira correção, valeria em quatro telas
/// e esqueceria a quinta.
/// </summary>
public partial class PainelPacienteClinico : UserControl
{
    public PainelPacienteClinico() => InitializeComponent();
}
