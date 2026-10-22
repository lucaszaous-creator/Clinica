using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// O chat da equipe no CRM (out/2026). A secretária passa o dia no CRM; o médico, nos apps. Sem
/// isto, o recado do médico ("remarca a Maria das 10h") chegava num app que ela não estava olhando.
/// Aqui a ponte serve, ao CRM, o MESMO chat dos apps — os mesmos serviços
/// (<see cref="IChatEquipeService"/>, <see cref="IPedidosRecepcaoService"/>), as mesmas regras de
/// quem vê o quê — em nome de quem está logado no CRM.
///
/// Quem é a pessoa: o CRM manda o LOGIN da sessão (ele já conferiu a senha por
/// <see cref="ClinicTeamBridge.Enter"/>), e a ponte confere de novo que o login existe, está ativo e
/// pode entrar no CRM. A chave Bearer e a rede da ponte continuam sendo a porta; o login só diz
/// em nome de QUEM — e nunca alcança mais do que essa pessoa alcançaria no app.
///
/// O que fica de fora no CRM, de propósito: anexar arquivo, paciente e documento (a ficha e o
/// documento clínico abrem nos apps, com o registro de acesso ao prontuário) e corrigir mensagem.
/// O CRM mostra que há um anexo; quem abre é o app.
/// </summary>
public sealed class ClinicTeamChatBridge(ClinicaDbContext db, IChatEquipeService chat, IPedidosRecepcaoService pedidos)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    static DateTimeOffset At(DateTime local) => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone.GetUtcOffset(local));
    static DateTimeOffset? At(DateTime? local) => local is { } d ? At(d) : null;
    static DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone).DateTime;

    /// <summary>A pessoa do login, se ela pode usar o CRM; nulo quando não pode (a resposta é 403).</summary>
    async Task<UsuarioSistema?> PessoaAsync(string? login, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length > 60) return null;
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Login == login, ct);
        return u is not null && ClinicTeamBridge.PodeEntrar(u) ? u : null;
    }

    static IResult SemAcesso() => Results.Json(new { error = ClinicTeamBridge.SemAcesso }, statusCode: 403);

    /// <summary>
    /// Executa em nome da pessoa. A recusa de regra do chat (conversa que não é dela, mensagem
    /// vazia, pedido já encerrado) volta com a FRASE do serviço — ela foi escrita para quem usa a
    /// tela; a frase genérica de agenda da ponte ("a agenda mudou") não diria nada aqui.
    /// </summary>
    async Task<IResult> ComoAsync(string? login, Func<UsuarioSistema, Task<object>> acao, CancellationToken ct)
    {
        var eu = await PessoaAsync(login, ct);
        if (eu is null) return SemAcesso();
        try { return Results.Ok(await acao(eu)); }
        catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: 403); }
        catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 409); }
        catch (Exception ex) when (SemPermissaoDeGravar(ex))
        {
            // O papel restrito da ponte ainda não ganhou a escrita nas tabelas do chat: a
            // instalação pede o papel-ponte-crm.sql rodado de novo depois da atualização.
            return Results.Json(new { error = "O chat da equipe ainda não foi liberado para o CRM nesta instalação." }, statusCode: 503);
        }
    }

    static bool SemPermissaoDeGravar(Exception ex)
        => ex.GetBaseException() is System.Data.Common.DbException { SqlState: "42501" };

    // ================================================================ conversas

    public Task<IResult> Panel(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
    {
        var p = await chat.PainelAsync(eu.Id, true, ct);
        var itens = new List<TeamChatItem>();
        if (p.Sistema is { } s) itens.Add(Item(s, "system"));
        itens.AddRange(p.Canais.Select(c => Item(c, "channel")));
        itens.AddRange(p.Diretas.Select(d => Item(d, "direct")));
        return new TeamChatPanel(itens.ToArray(), p.TotalNaoLidas, p.Online);
    }, ct);

    static TeamChatItem Item(ConversaNaLista c, string kind) => new(c.Conversa, c.Titulo, c.Subtitulo, c.NaoLidas, c.UltimaPrevia,
        At(c.UltimaEm), kind, c.PessoaId, c.Iniciais, c.Presenca switch
        {
            PresencaChat.Online => "online",
            PresencaChat.EmAtendimento => "busy",
            _ => "away",
        }, c.PessoaAtiva);

    public Task<IResult> Open(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
    {
        var a = await chat.AbrirAsync(eu.Id, Conversa(input), input.Before, 40, ct);
        return new TeamChatConversation(a.Conversa, a.Titulo, a.Subtitulo, a.Mensagens.Select(Mensagem).ToArray(), a.UltimaLidaAntes,
            a.Escrevendo.ToArray(), a.TemAnteriores, At(a.Instante), a.SoLeitura);
    }, ct);

    public Task<IResult> Update(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
    {
        var desde = input.EditedSince is { } e ? Local(e) : DateTime.MinValue;
        var u = await chat.AtualizarAsync(eu.Id, Conversa(input), input.After ?? 0, desde, 60, ct);
        // As novas e as corrigidas vão juntas: o CRM troca pelo id o que já tem e acrescenta o resto.
        return new TeamChatConversation(Conversa(input), "", u.Subtitulo ?? "", u.Novas.Concat(u.Editadas).Select(Mensagem).ToArray(), 0,
            u.Escrevendo.ToArray(), false, At(u.Instante), false, u.HaMaisNovas);
    }, ct);

    public Task<IResult> Send(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
    {
        var texto = input.Text ?? "";
        if (texto.Length > 20000) throw new InvalidOperationException("A mensagem é longa demais para o CRM. Escreva o laudo pelo app.");
        var id = await chat.EnviarAsync(eu.Id, Conversa(input), new NovaMensagemEquipe(texto), ct);
        return new { id };
    }, ct);

    public Task<IResult> FullText(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
        (object)new { text = await chat.TextoAsync(eu.Id, input.MessageId ?? 0, ct) }, ct);

    public Task<IResult> Pulse(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
    {
        var p = await chat.PulsoAsync(eu.Id, input.After ?? 0, new PreferenciasSomChat(), ct);
        return new TeamChatPulse(p.UltimaMensagemId, p.Novidades.Select(n => new TeamChatNews(n.Id, n.Conversa, n.Titulo, n.AutorNome,
            n.Previa, At(n.EnviadaEm), n.Som switch
            {
                SomChat.Direta => "direct",
                SomChat.Mencao => "mention",
                SomChat.Pedido => "request",
                _ => "",
            })).ToArray());
    }, ct);

    public Task<IResult> Signal(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
    {
        await chat.SinalizarAsync(eu.Id, string.IsNullOrWhiteSpace(input.Conversation) ? null : input.Conversation, ct);
        return new { ok = true };
    }, ct);

    public Task<IResult> Search(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
        (object)(await chat.BuscarAsync(eu.Id, input.Text ?? "", null, 30, ct))
            .Select(r => new TeamChatHit(r.MensagemId, r.Conversa, r.Titulo, r.AutorNome, At(r.EnviadaEm), r.Trecho)).ToArray(), ct);

    static string Conversa(TeamChatAsk input)
        => string.IsNullOrWhiteSpace(input.Conversation) || input.Conversation.Length > 40
            ? throw new InvalidOperationException("Conversa inválida.") : input.Conversation;

    static TeamChatMessage Mensagem(MensagemNaConversa m) => new(m.Id, m.AutorNome, m.Iniciais, m.Texto, At(m.EnviadaEm),
        // Cartão não mostra "(editada)": quem muda o cartão é a situação do pedido.
        m.Cartao is null ? At(m.EditadaEm) : null, m.Minha, m.MeCita, m.DoSistema, m.PacienteNome, m.DocumentoRotulo, m.ArquivoNome,
        m.LidaPor, !m.TextoCompleto, m.Cartao is { } c ? Cartao(c) : null);

    public static TeamChatCard Cartao(CartaoChat c) => new(c.Tipo, c.Titulo, c.Subtitulo,
        (c.Resumo ?? []).Select(r => new TeamChatCardFigure(r.Rotulo, r.Valor, r.Tom)).ToArray(),
        (c.Linhas ?? []).Select(l => new TeamChatCardLine(l.Hora, l.Titulo, l.Detalhe, l.Selo, l.Tom, l.PacienteId, l.AgendamentoId, l.PodePedir)).ToArray(),
        (c.Sugestoes ?? []).ToArray(), c.Rodape, c.PedidoId, c.Situacao);

    // ================================================================ pedidos à recepção

    public Task<IResult> Requests(TeamChatAsk input, CancellationToken ct) => ComoAsync(input.Login, async eu =>
        (object)(await pedidos.ListarAsync(eu.Id, input.Text == "with-closed", ct)).Select(Pedido).ToArray(), ct);

    public Task<IResult> Act(TeamRequestAct input, CancellationToken ct) => ComoAsync(input.Login, async eu => (object)(input.Action switch
    {
        "take" => Pedido(await pedidos.AssumirAsync(eu.Id, input.RequestId, ct)),
        "done" => Pedido(await pedidos.EncerrarAsync(eu.Id, input.RequestId, true, input.Outcome ?? "", ct)),
        "not-done" => Pedido(await pedidos.EncerrarAsync(eu.Id, input.RequestId, false, input.Outcome ?? "", ct)),
        _ => throw new InvalidOperationException("Ação desconhecida."),
    }), ct);

    static TeamRequest Pedido(PedidoNaLista p) => new(p.Id, p.Tipo, p.TipoRotulo, p.PacienteId, p.PacienteNome, p.AgendamentoId,
        At(p.HorarioEm), p.Detalhe, p.CriadoPorNome, At(p.CriadoEm), p.Situacao, p.SituacaoRotulo, p.ResponsavelNome, At(p.AssumidoEm),
        p.Desfecho, At(p.EncerradoEm), p.PodeTratar, p.Meu);
}
