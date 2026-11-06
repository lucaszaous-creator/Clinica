using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// A CONVERSA AO LADO (out/2026, fase 2 do WhatsApp nativo — docs/whatsapp-nativo-implementacao.md,
/// "Fase 2 — a conversa ao lado"): a Agenda do dia, a Ficha, as Confirmações e o Acompanhamento abrem a
/// conversa do paciente num painel à direita, SEM trocar de tela. O painel é a mesma tela de conversa da
/// aba WhatsApp (<c>ConversasDoWhatsApp</c> no modo painel) — uma porta por pergunta; quem o desenha é a
/// <c>CamadaDaConversaAoLado</c>, montada uma vez por navegador (na <c>Raiz</c> e no <c>PalcoDeTelas</c>),
/// como a camada dos formulários do paciente.
///
/// Quando ele serve (<see cref="LigadaAsync"/>): a aba WhatsApp existe neste app para quem entrou (o
/// Faturamento e o Consultório não a têm) e o CRM tem endereço
/// (<see cref="WhatsAppDaRecepcaoService.ConversaAoLadoAsync"/>). Fora disso o "Escrever no WhatsApp" de
/// sempre continua: a aba WhatsApp, ou o wa.me.
///
/// Um por navegador (Scoped). Sem camada montada (um formulário renderizado solto num teste),
/// <see cref="Disponivel"/> é falso e <see cref="AbrirAsync(IHostDaTela, PedidoDeConversa)"/> responde
/// "não abriu" — quem chama segue no caminho de antes, nunca num botão que não faz nada.
/// </summary>
public sealed class ConversaAoLado(IServiceScopeFactory escopos)
{
    /// <summary>Por quanto tempo a resposta "a tela nativa está ligada" vale para os botões das listas.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(1);

    private Task<bool>? _leitura;
    private DateTime _lidaEm;
    private int _camadas;

    /// <summary>O paciente cuja conversa está no painel, ou nulo (fechado).</summary>
    public PedidoDeConversa? Atual { get; private set; }

    /// <summary>Há quem desenhe o painel (a camada está montada).</summary>
    public bool Disponivel => _camadas > 0;

    /// <summary>A camada se redesenha quando o painel abre, troca de paciente ou fecha.</summary>
    public event Action? Mudou;

    /// <summary>
    /// O painel serve neste app, para quem entrou? A leitura das configurações vale por
    /// <see cref="Validade"/> e é UMA para todos os botões da lista (trinta linhas, uma ida ao banco).
    /// Falha de leitura vale "não": o botão novo some e os de sempre seguem no caminho de antes.
    /// </summary>
    /// <param name="agora">Relê agora (o clique), em vez de usar a resposta guardada.</param>
    public Task<bool> LigadaAsync(IHostDaTela host, bool agora = false)
    {
        if (!host.TemTela(ConversaNoCrm.ChaveDaAba)) return Task.FromResult(false);
        if (agora || _leitura is null || DateTime.UtcNow - _lidaEm > Validade)
        {
            _lidaEm = DateTime.UtcNow;
            _leitura = LerAsync();
        }
        return _leitura;
    }

    private async Task<bool> LerAsync()
    {
        try
        {
            using var escopo = escopos.CreateScope();
            return await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().ConversaAoLadoAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — a conversa ao lado não pôde conferir a escolha do WhatsApp", ex);
            return false;
        }
    }

    /// <summary>
    /// Abre (ou troca) o painel para o paciente pedido. Falso quando ele não serve aqui — sem camada,
    /// sem acesso ao WhatsApp (<see cref="AcessoAoWhatsApp"/>) ou sem o CRM — e quem chama segue no caminho de antes.
    /// Confere de NOVO no clique: a lista pode ter sido desenhada antes de o endereço do CRM mudar.
    /// </summary>
    public async Task<bool> AbrirAsync(IHostDaTela host, PedidoDeConversa pedido)
    {
        if (!Disponivel || !AcessoAoWhatsApp.Pode(SessaoUsuario.Atual)) return false;
        if (!await LigadaAsync(host, agora: true)) return false;
        Atual = pedido;
        Mudou?.Invoke();
        return true;
    }

    /// <summary>O atalho das telas: o painel deste navegador, se houver.</summary>
    public static Task<bool> AbrirAsync(IServiceProvider servicos, IHostDaTela host, PedidoDeConversa pedido)
        => servicos.GetService<ConversaAoLado>() is { } painel ? painel.AbrirAsync(host, pedido) : Task.FromResult(false);

    /// <summary>Fecha o painel (o X, o Esc, ou a aba WhatsApp que passou a ficar à vista).</summary>
    public void Fechar()
    {
        if (Atual is null) return;
        Atual = null;
        Mudou?.Invoke();
    }

    /// <summary>A camada se anuncia ao montar e ao sair: é isso que faz <see cref="Disponivel"/> dizer a verdade.</summary>
    internal void CamadaMontada() => _camadas++;

    internal void CamadaDesmontada()
    {
        _camadas = Math.Max(0, _camadas - 1);
        if (_camadas == 0) Fechar();
    }
}
