using Clinica.Domain.Entities;
using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra a conversa DESTE paciente no WhatsApp da clínica" — o pedido que uma tela deixa antes de
/// navegar para a aba WhatsApp (out/2026, fase 2 — docs/crm-na-recepcao.md). Um pedido só; ler
/// LIMPA, como o <see cref="PedidoDeDocumento"/>. Quem consome é a <c>ConversasDoWhatsApp</c>, na
/// abertura e na volta à aba.
/// </summary>
public sealed class PedidoDeConversaNoCrm
{
    private readonly object _trava = new();
    private PedidoDeConversa? _pedido;

    public void Definir(PedidoDeConversa pedido)
    {
        lock (_trava) _pedido = pedido;
    }

    public PedidoDeConversa? Consumir()
    {
        lock (_trava)
        {
            var p = _pedido;
            _pedido = null;
            return p;
        }
    }

    /// <summary>
    /// O caminho de TODO botão "Escrever no WhatsApp": com a tela NATIVA escolhida, a conversa abre AO
    /// LADO da tela de onde se pediu (<see cref="ConversaAoLado"/>, fase 2 — sem trocar de tela); senão,
    /// a aba WhatsApp quando ela existe e o CRM tem endereço; senão, <c>false</c> — e quem chama segue no
    /// wa.me de sempre. Nunca deixa pedido órfão: navegação recusada limpa o que tinha posto.
    /// </summary>
    public static async Task<bool> EscreverAsync(IServiceProvider servicos, IHostDaTela host, PedidoDeConversa pedido)
    {
        if (ConversaNoCrm.Telefone(pedido.Telefone) is null) return false;
        // Quem não atende o WhatsApp da clínica (AcessoAoWhatsApp) não chega à aba por aqui.
        if (!AcessoAoWhatsApp.Pode(SessaoUsuario.Atual)) return false;
        if (await ConversaAoLado.AbrirAsync(servicos, host, pedido)) return true;
        if (servicos.GetService<PedidoDeConversaNoCrm>() is not { } fila) return false;
        try
        {
            using var escopo = servicos.GetRequiredService<IServiceScopeFactory>().CreateScope();
            if (await escopo.ServiceProvider.GetRequiredService<ParametrosService>().ObterEnderecoDoCrmAsync() is null) return false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — endereço do CRM não pôde ser lido para escrever ao paciente", ex);
            return false;
        }
        fila.Definir(pedido);
        if (await host.NavegarAsync(ConversaNoCrm.ChaveDaAba)) return true;
        fila.Consumir();
        return false;
    }
}
