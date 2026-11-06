using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que o vigia leu: quem espera a clínica agora e, entre eles, quem passou a esperar desde a leitura
/// anterior (a notificação passageira sai UMA vez por espera). <see cref="Ligado"/> falso = o CRM não tem
/// endereço nesta clínica: nada aparece, nem a seção do sino nem o número no menu. <see cref="NaoLidas"/> é o número do
/// item "WhatsApp" no menu: o MESMO da aba "Conversas" dentro da tela (<see cref="ContagemDasConversas.NaoLidas"/>), para os
/// dois nunca discordarem (26/10, pedido da direção: o vermelho não acompanhava o que a tela mostrava).
/// </summary>
public sealed record LeituraDaEspera(bool Ligado, IReadOnlyList<ConversaQueEspera> Esperando, IReadOnlyList<ConversaQueEspera> Novas, int NaoLidas = 0)
{
    public static LeituraDaEspera Desligada { get; } = new(false, [], []);
}

/// <summary>
/// O VIGIA DO WHATSAPP (25/10, R8 e D12 da revisão dos casos de uso): o que a janela do app faz com o CRM
/// FORA da aba WhatsApp — que para o próprio relógio quando sai de vista. Duas coisas, e só elas:
/// <list type="bullet">
/// <item><b>O pulso de presença</b> (<see cref="PulsarAsync"/>, decisão da direção: quem usa só o app conta
/// como conectado): sem ele o CRM via toda secretária do app como "sem sinal", a distribuição automática
/// nunca lhe entregava conversa e o painel da gestão era ruído.</item>
/// <item><b>Quem espera resposta</b> (<see cref="LerAsync"/>): a lista inteira na primeira leitura e a cada
/// <see cref="LeituraInteira"/>, e no meio só o <c>?desde=</c> (o que mudou), juntado por <c>Id</c> como a aba
/// faz (<see cref="WhatsAppDaRecepcaoService.Juntar"/>). A regra de quem espera é
/// <see cref="ConversasNaTela.PedemResposta"/>.</item>
/// </list>
/// Um por janela (o shell o cria no login e o <see cref="Parar"/> no fechamento). <b>Parar cancela a chamada em
/// voo</b>: o fechamento do app avisa o CRM da saída logo depois, e um pulso atrasado que tomasse 401 entraria
/// de novo pelo bilhete — a pessoa sairia do app e continuaria "conectada" no CRM.
/// </summary>
public sealed class VigiaDoWhatsApp(IClienteDoCrm crm)
{
    /// <summary>O ritmo do pulso: bem dentro dos 120 s que ele vale (uma batida perdida não derruba a presença).</summary>
    public static readonly TimeSpan Pulso = TimeSpan.FromSeconds(30);

    /// <summary>De quanto em quanto a espera e as não lidas são relidas (o <c>?desde=</c>: uma chamada pequena por posto).
    /// 15 s: o número do menu acompanha a mensagem que chega e a conversa que alguém leu (o <c>desde</c> traz a marca de
    /// leitura). Era 60 s, e o menu ficava um minuto atrás da tela.</summary>
    public static readonly TimeSpan RitmoDaEspera = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A lista inteira volta a cada tanto: o <c>?desde=</c> não traz a conversa que saiu da visão da pessoa
    /// (transferida para fora do que ela vê) — sem isso ela continuaria "esperando" no sino.
    /// </summary>
    public static readonly TimeSpan LeituraInteira = TimeSpan.FromMinutes(10);

    private readonly CancellationTokenSource _parada = new();
    private readonly HashSet<string> _anunciadas = new(StringComparer.Ordinal);
    private IReadOnlyList<AppConversationSummary>? _conversas;
    private DateTimeOffset _lidoEm, _ultimaInteira;
    private string? _meuAgente;

    /// <summary>Verdadeiro depois de <see cref="Parar"/>: nada mais vai ao CRM.</summary>
    public bool Parado => _parada.IsCancellationRequested;

    /// <summary>Para de vez e cancela o que estiver em voo (o fechamento da janela, ANTES de avisar a saída ao CRM).</summary>
    public void Parar()
    {
        if (!Parado) _parada.Cancel();
    }

    /// <summary>Um pulso. Falso quando parado ou quando o CRM não confirmou; as falhas sobem (quem chama registra no log).</summary>
    public async Task<bool> PulsarAsync(int usuarioId)
    {
        if (Parado || usuarioId <= 0) return false;
        try { return (await crm.PulsoDePresencaAsync(usuarioId, _parada.Token)).Connected; }
        catch (OperationCanceledException) when (Parado) { return false; }
        catch (CrmNaoConfigurado) { return false; }
    }

    /// <summary>
    /// Quem espera a clínica agora. A PRIMEIRA leitura semeia: o que já esperava quando o app abriu entra no sino
    /// sem a notificação passageira (abrir o app não pode ser uma rajada). <see cref="CrmNaoConfigurado"/> é
    /// <see cref="LeituraDaEspera.Desligada"/>, em silêncio; o resto (fora do ar, acesso recusado) sobe.
    /// </summary>
    public async Task<LeituraDaEspera> LerAsync(int usuarioId, DateTimeOffset agora)
    {
        if (Parado || usuarioId <= 0) return LeituraDaEspera.Desligada;
        var ct = _parada.Token;
        var primeira = _conversas is null;
        try
        {
            if (primeira || agora - _ultimaInteira >= LeituraInteira)
            {
                // O "desde" da próxima é o relógio de ANTES da leitura: o que mudar durante ela volta na seguinte.
                var antes = DateTimeOffset.UtcNow;
                _meuAgente = (await crm.SessaoAsync(usuarioId, ct)).AgentId;
                _conversas = await crm.ListarConversasAsync(usuarioId, ct);
                _lidoEm = antes;
                _ultimaInteira = agora;
            }
            else
            {
                var desde = await crm.ListarDesdeAsync(usuarioId, _lidoEm, ct);
                _conversas = WhatsAppDaRecepcaoService.Juntar(_conversas!, desde);
                _lidoEm = desde.Now;
            }
        }
        catch (OperationCanceledException) when (Parado) { return LeituraDaEspera.Desligada; }
        catch (CrmNaoConfigurado)
        {
            _conversas = null;
            return LeituraDaEspera.Desligada;
        }

        var esperando = ConversasNaTela.PedemResposta(_conversas, agora, ConversasNaTela.MinutosDeEspera, _meuAgente);
        // Cada espera é anunciada UMA vez na vida da janela — mesmo que saia da lista e volte (assumida e
        // devolvida sem resposta): a chave carrega a hora da recebida, então só uma mensagem NOVA anuncia de novo.
        var novas = esperando.Where(e => _anunciadas.Add(e.Chave)).ToList();
        return new LeituraDaEspera(true, esperando, primeira ? [] : novas, ConversasNaTela.Contagens(_conversas, _meuAgente).NaoLidas);
    }
}
