using System.Windows.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Views;

/// <summary>
/// A tela "Pedidos da enfermagem" (e a mesma lista dentro da janela aberta pela Agenda).
/// A View liga e desliga o relógio do ViewModel no Loaded/Unloaded — exceto quando ela é
/// só a JANELA da Agenda: ali o ciclo é da Agenda, que já mantém o resumo vivo, e parar
/// ao fechar a janela calaria o resumo da grade.
/// </summary>
public partial class PedidosEnfermagemView : UserControl
{
    /// <summary>A tela da sidebar: o ViewModel é dela, e "ver na agenda" navega para a grade.</summary>
    public PedidosEnfermagemView(IServiceProvider servicos)
    {
        PedidosEnfermagemViewModel.Exigir();
        InitializeComponent();
        var vm = new PedidosEnfermagemViewModel(servicos.GetRequiredService<IServiceScopeFactory>(), () => DateTime.Today,
            data =>
            {
                var pedido = servicos.GetRequiredService<PedidoAgenda>();
                pedido.AbrirComFiltro(DateOnly.FromDateTime(data), null);
                if (!NavegacaoSuite.Ir(ChavesSuite.AgendaRecepcao)) pedido.Consumir();
            });
        DataContext = vm;
        Loaded += (_, _) => vm.Iniciar();
        Unloaded += (_, _) => vm.Parar();
    }

    /// <summary>A lista dentro da janela da Agenda: o ciclo do ViewModel é de quem a abriu.</summary>
    internal PedidosEnfermagemView(PedidosEnfermagemViewModel vm)
    {
        PedidosEnfermagemViewModel.Exigir();
        InitializeComponent();
        DataContext = vm;
    }
}
