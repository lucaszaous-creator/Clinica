using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>
/// AS CHAMADAS DA v3 E DA v4 (nov/2026, F3): reação, ★, fixar, etiquetas, "escrevendo", endereço/contato/serviço, enviar mais
/// tarde, as pílulas do @SemDor, "Tentar de novo" e a saudação — as rotas da F2 (docs/api-para-os-apps.md §15–17), pelo
/// MESMO <c>IClienteDoCrm</c>. O CRM antigo responde 404 (<see cref="RecusaDoCrm"/>): a tela esconde o recurso. O que é
/// regra (janela, 30 dias, máximo de fixadas) mora em <see cref="ConversasNaTela"/>; aqui, só a chamada e a versão.
/// </summary>
public sealed partial class WhatsAppDaRecepcaoService
{
    /// <summary>Reage com o emoji (vazio tira a reação da clínica). A Meta só aceita na janela de 24 h: o 409 traz a frase.</summary>
    public Task<AppConversation> ReagirAsync(AppConversation c, string mensagemId, string emoji, CancellationToken ct = default)
        => crm.ReagirAsync(UsuarioId, c.Id, new AppReact(mensagemId, emoji ?? ""), ct);

    /// <summary>★ marca ou desmarca a mensagem como importante (só a equipe vê).</summary>
    public Task<AppConversation> EstrelarAsync(AppConversation c, string mensagemId, bool estrela, CancellationToken ct = default)
        => crm.EstrelarAsync(UsuarioId, c.Id, mensagemId, new AppStar(estrela), ct);

    /// <summary>As conversas que quem entrou fixou (até <c>Max</c>), na ordem em que fixou.</summary>
    public Task<AppPins> FixadasAsync(CancellationToken ct = default) => crm.FixadasAsync(UsuarioId, ct);

    /// <summary>Fixa ou desafixa a conversa no topo da lista de quem pede. Passou do máximo: 409 com a frase.</summary>
    public Task<AppPins> FixarAsync(string conversaId, bool fixar, CancellationToken ct = default)
        => crm.FixarAsync(UsuarioId, conversaId, new AppPin(fixar), ct);

    /// <summary>As etiquetas da clínica (as arquivadas também: continuam nas conversas que já as têm).</summary>
    public Task<AppLabel[]> EtiquetasAsync(CancellationToken ct = default) => crm.EtiquetasAsync(UsuarioId, ct);

    /// <summary>Troca as etiquetas da conversa por exatamente estas (ids; até 10).</summary>
    public Task<AppConversation> EtiquetarAsync(AppConversation c, IEnumerable<string> etiquetas, CancellationToken ct = default)
        => crm.EtiquetarConversaAsync(UsuarioId, c.Id, new AppConversationLabels(etiquetas.Distinct().Take(10).ToArray()), ct);

    /// <summary>O pulso de "estou escrevendo" (vale 6 s; a tela pulsa a cada ~3 s enquanto a pessoa digita).</summary>
    public Task<AppTyping> EscrevendoAsync(string conversaId, CancellationToken ct = default) => crm.EscrevendoAsync(UsuarioId, conversaId, ct);

    /// <summary>Esquece o pulso (enviou, apagou o texto ou trocou de conversa).</summary>
    public Task ParouDeEscreverAsync(string conversaId, CancellationToken ct = default) => crm.ParouDeEscreverAsync(UsuarioId, conversaId, ct);

    /// <summary>O que o clipe oferece: o endereço (nulo = não configurado), os contatos e os serviços.</summary>
    public Task<AppShareOptions> OpcoesDoClipeAsync(CancellationToken ct = default) => crm.OpcoesDoClipeAsync(UsuarioId, ct);

    /// <summary>Envia o endereço ("location"), um contato ("contact") ou um serviço ("service"). Idempotente pelo Id.</summary>
    public Task<AppConversation> CompartilharAsync(AppConversation c, string tipo, string? itemId, string idDoEnvio, CancellationToken ct = default)
    {
        var id = IdDoEnvio(idDoEnvio);
        return ComVersaoNovaAsync(c, v => crm.CompartilharAsync(UsuarioId, c.Id, new AppShare(v, id, tipo, itemId), ct), ct);
    }

    /// <summary>"Enviar mais tarde": o texto (com o anexo, se houver) sai no horário. Fora da janela, o 409 diz até quando vale.</summary>
    public Task<AppConversation> AgendarAsync(AppConversation c, string texto, DateTimeOffset quando, string? anexoId, string idDoEnvio,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto) && anexoId is null) throw new InvalidOperationException("Escreva a mensagem antes de agendar.");
        if (quando <= Agora.AddMinutes(1)) throw new InvalidOperationException("Escolha um horário a partir de daqui a 2 minutos.");
        return crm.AgendarAsync(UsuarioId, c.Id, new AppSchedule(IdDoEnvio(idDoEnvio), texto?.Trim() ?? "", quando, anexoId), ct);
    }

    /// <summary>Cancela a mensagem agendada (só quem agendou ou a gestão).</summary>
    public Task<AppConversation> CancelarAgendamentoAsync(AppConversation c, string agendamentoId, CancellationToken ct = default)
        => crm.CancelarAgendamentoAsync(UsuarioId, c.Id, agendamentoId, ct);

    /// <summary>As 2 a 3 respostas curtas do @SemDor para as pílulas (a escolhida vai para o CAMPO, nunca sai sozinha).</summary>
    public Task<AppSemDorOptions> OpcoesDoSemDorAsync(AppConversation c, CancellationToken ct = default)
        => crm.OpcoesDoSemDorAsync(UsuarioId, c.Id, ct);

    /// <summary>"Tentar de novo": um envio NOVO, com Id novo, do que falhou. A original perde o "!" quando a nova sai.</summary>
    public Task<AppConversation> TentarDeNovoAsync(AppConversation c, string mensagemId, CancellationToken ct = default)
        => ComVersaoNovaAsync(c, v => crm.TentarDeNovoAsync(UsuarioId, c.Id, mensagemId, new AppRetry(v, Guid.NewGuid().ToString("D")), ct), ct);

    /// <summary>A saudação do menu (a primeira mensagem ao paciente que escreve) e se ela está ligada.</summary>
    public Task<AppGreeting> SaudacaoAsync(CancellationToken ct = default) => crm.SaudacaoAsync(UsuarioId, ct);

    /// <summary>Grava a saudação (só a gestão; 409 quando outra pessoa salvou antes).</summary>
    public Task<AppGreeting> SalvarSaudacaoAsync(AppGreeting saudacao, CancellationToken ct = default)
    {
        if (saudacao.Enabled && string.IsNullOrWhiteSpace(saudacao.Text)) throw new InvalidOperationException("Escreva a saudação antes de ligar.");
        return crm.SalvarSaudacaoAsync(UsuarioId, saudacao with { Text = saudacao.Text.Trim() }, ct);
    }
}
