using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// As cinco etapas da COFEN 358/2009 — o compositor da consulta de enfermagem, usado pela
/// janela da sala de infusão E pela seção do Consultório.
///
/// ⚠️ Sem ViewModel próprio: o <c>DataContext</c> é o
/// <see cref="EvolucaoEnfermagemViewModel"/> de quem o hospeda. Um VM aqui seria uma
/// segunda verdade sobre a mesma passagem.
/// </summary>
public partial class ProcessoDeEnfermagemView : UserControl
{
    public ProcessoDeEnfermagemView() => InitializeComponent();

    // ⚠️ Os dois catálogos abrem em CÓDIGO, e não por comando do ViewModel: abrir janela é
    // ato de VIEW. Foi a mesma escolha do menu "⋯" da fila (parcela 58) — e aqui ela tem um
    // ganho a mais, porque `EvolucaoEnfermagemViewModel` é compartilhado com a janela da
    // sala e não deve saber quem é dono de quê na tela.
    private void AbrirCatalogoDeDiagnosticos(object sender, System.Windows.RoutedEventArgs e)
        => AbrirCatalogo(vm => vm.CatalogoDeDiagnosticos);

    private void AbrirCatalogoDeCuidados(object sender, System.Windows.RoutedEventArgs e)
        => AbrirCatalogo(vm => vm.CatalogoDeCuidados);

    private void AbrirCatalogo(Func<EvolucaoEnfermagemViewModel, CatalogoDeEnfermagem> qual)
    {
        // Guarda sobre o DataContext: a seção do posto clínico é construída antes de o
        // paciente existir, e o controle pode estar montado sem VM por um instante.
        if (DataContext is not EvolucaoEnfermagemViewModel vm) return;

        CatalogoEnfermagemWindow.Abrir(qual(vm));
    }
}
