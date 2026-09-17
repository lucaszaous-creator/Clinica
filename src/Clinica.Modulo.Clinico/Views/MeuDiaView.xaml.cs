using System.Windows;
using System.Windows.Controls;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Views;

/// <summary>
/// Abertura do consultório: a agenda do profissional como LISTA (parcela 95) e o que
/// ficou sem registro.
///
/// A View liga e desliga a releitura periódica (parcela 38; 30 s desde set/2026 — o
/// porquê do intervalo está no <c>_relogio</c> da ViewModel). Quem marca a chegada,
/// cancela e corrige o que a sessão é continua sendo o balcão, na outra máquina, e sem
/// ela a lista só mudaria por clique em Atualizar. Ligar aqui, e não no ViewModel, é o
/// que impede um <c>DispatcherTimer</c> de manter vivo cada tela já trocada — o shell
/// constrói uma nova a cada navegação.
///
/// O arrasto entre raias morreu com as raias: a lista não tem para onde arrastar, e as
/// transições continuam nos botões da linha.
/// </summary>
public partial class MeuDiaView : UserControl
{
    private void AjustarColunas()
    {
        // As ações têm espaço desde a primeira carga, antes de a tabela medir os
        // botões trazidos pela consulta. O restante pertence ao nome e contexto.
        if (TabelaMeuDia.ActualWidth > 0)
            ColunaPaciente.Width = new DataGridLength(Math.Max(200,
                TabelaMeuDia.ActualWidth - SystemParameters.VerticalScrollBarWidth - 12
                - 110 - 160 - 110 - 140));
    }

    public MeuDiaView()
    {
        InitializeComponent();
        TabelaMeuDia.SizeChanged += (_, _) => AjustarColunas();
        Loaded += (_, _) => AjustarColunas();

        Loaded += (_, _) => (DataContext as MeuDiaViewModel)?.AoEntrarEmCena();
        Unloaded += (_, _) => (DataContext as MeuDiaViewModel)?.AoSairDeCena();
    }
}
