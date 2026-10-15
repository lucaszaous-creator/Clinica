using System.Windows.Controls;

namespace Clinica.Clinico.Views;

/// <summary>
/// A semana de quem atende: sete colunas, leitura de carga e entrada no caso. A busca por
/// paciente, modalidade e situação (out/2026) é o <see cref="FiltroDaAgendaView"/>, o
/// mesmo do Meu dia.
/// </summary>
public partial class MinhaSemanaView : UserControl
{
    public MinhaSemanaView() => InitializeComponent();
}
