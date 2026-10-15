using System.Windows.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Views;

public sealed class PedidosEnfermagemView : UserControl
{
    public PedidosEnfermagemView(IServiceProvider servicos)
    {
        PedidosEnfermagemWeb.Exigir();
        var adaptador = new PedidosEnfermagemWeb(servicos.GetRequiredService<IServiceScopeFactory>(), () => DateTime.Today,
            data => {
                var pedido = servicos.GetRequiredService<PedidoAgenda>();
                pedido.AbrirComFiltro(DateOnly.FromDateTime(data), null);
                if (!NavegacaoSuite.Ir(ChavesSuite.AgendaRecepcao)) pedido.Consumir();
            });
        var painel = new PainelClinicoWeb("pedidos-enfermagem", adaptador.Estado, adaptador.Executar, PedidosEnfermagemWeb.Exigir);
        Content = painel;
        adaptador.EstadoAlterado += painel.AtualizarEstado;
        Loaded += (_, _) => adaptador.Iniciar();
        Unloaded += (_, _) => adaptador.Parar();
    }
}
