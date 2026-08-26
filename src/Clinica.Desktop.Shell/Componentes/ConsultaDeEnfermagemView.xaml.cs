using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A porta da consulta de enfermagem: a caixinha que decide anotação × consulta, o aviso
/// do que falta, e o botão que reabre a janela das cinco etapas.
///
/// ⚠️ Sem ViewModel próprio: o <c>DataContext</c> é o
/// <see cref="EvolucaoEnfermagemViewModel"/> de quem o hospeda.
/// </summary>
public partial class ConsultaDeEnfermagemView : UserControl
{
    public ConsultaDeEnfermagemView() => InitializeComponent();

    /// <summary>
    /// Marcar a caixinha ABRE a janela; desmarcar não abre nada — quem pergunta se a
    /// pessoa quer mesmo voltar para anotação é o ViewModel, que sabe se há conteúdo.
    ///
    /// ⚠️ `Click` e não `Checked`: o segundo dispara também quando a propriedade muda por
    /// código, e a janela abriria sozinha ao carregar um registro para corrigir.
    /// </summary>
    private void AoClicarNaConsulta(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsChecked: true }) AbrirConsulta(sender, e);
    }

    // ⚠️ Abrir janela é ato de VIEW, não de comando do ViewModel — a mesma escolha do menu
    // "⋯" da fila (parcela 58) e do catálogo (4ª rodada). E aqui ela pesa mais: este VM é
    // compartilhado com a janela da sala de infusão, e não deve saber quem é dono de quê.
    private void AbrirConsulta(object sender, RoutedEventArgs e)
    {
        // Guarda sobre o DataContext: a seção do posto clínico é construída antes de o
        // paciente existir, e o controle pode estar montado sem VM por um instante.
        if (DataContext is EvolucaoEnfermagemViewModel vm)
            ConsultaDeEnfermagemWindow.Abrir(vm);
    }
}
