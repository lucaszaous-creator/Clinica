using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// A fila do @SemDor com IA nos apps (out/2026 — Clinica#307). O app põe aqui a pergunta que as
/// regras não entenderam (só quando o CRM passou há pouco — <see cref="IaDisponivelAsync"/>) e
/// responde "relendo"; a ponte entrega a fila ao CRM, que a reescreve com a IA, e devolve a
/// releitura em <see cref="ResponderAsync"/>. Quem responde é a clínica: a pergunta reescrita
/// passa pelo MESMO <see cref="SistemaEquipeService"/>, com as permissões de quem perguntou, e a
/// resposta é gravada na conversa como as outras do sistema (<see cref="GravacaoDoChat"/>).
///
/// Ninguém fica com um "relendo" sem fim: a pergunta que passa da validade recebe a ajuda de
/// sempre (<see cref="VencerAsync"/>), na leitura da fila e na próxima pergunta feita pelos apps.
/// </summary>
public sealed class PerguntasAoSemDorService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public PerguntasAoSemDorService(ClinicaDbContext db) : this(db, () => DateTime.Now) { }

    public PerguntasAoSemDorService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    /// <summary>O CRM leu a fila há pouco? Uma ida; só é perguntado quando as regras não entenderam.</summary>
    public async Task<bool> IaDisponivelAsync(CancellationToken ct = default)
    {
        var lidas = await db.SemDorNoCrm.AsNoTracking().Where(x => x.Id == SemDorNoCrm.Unico)
            .Select(x => x.PerguntasLidasEm).SingleOrDefaultAsync(ct);
        return SemDorNosAppsRegras.IaDisponivel(lidas, agora());
    }

    /// <summary>Põe a pergunta no contexto, sem gravar: quem chama grava junto com o "relendo".</summary>
    public PerguntaAoSemDor Enfileirar(int usuarioId, string conversa, string texto, DateTime momento)
    {
        texto = texto.Trim();
        var p = new PerguntaAoSemDor
        {
            UsuarioId = usuarioId, Conversa = conversa, CriadaEm = momento, Situacao = SemDorNosAppsRegras.Aguardando,
            Texto = texto.Length > SemDorNosAppsRegras.TamanhoMaximo ? texto[..SemDorNosAppsRegras.TamanhoMaximo] : texto,
        };
        db.PerguntasAoSemDor.Add(p);
        return p;
    }

    /// <summary>
    /// A leitura do CRM (pela ponte): marca o pulso, vence as que esperaram demais e devolve as que
    /// aguardam, da mais antiga para a mais nova.
    /// </summary>
    public async Task<IReadOnlyList<(int Id, string Texto)>> PendentesAsync(int limite = 10, CancellationToken ct = default)
    {
        var momento = agora();
        await PulsoAsync(momento, ct);
        await VencerAsync(ct);
        var linhas = await db.PerguntasAoSemDor.AsNoTracking()
            .Where(p => p.Situacao == SemDorNosAppsRegras.Aguardando)
            .OrderBy(p => p.Id).Take(limite)
            .Select(p => new { p.Id, p.Texto }).ToListAsync(ct);
        return linhas.Select(l => (l.Id, l.Texto)).ToList();
    }

    /// <summary>
    /// A releitura do CRM. <paramref name="reescrita"/> nula: a IA não achou pergunta conhecida, e
    /// a pessoa recebe a ajuda. Devolve falso quando a pergunta não estava mais aguardando (já
    /// respondida, vencida, ou repetida pelo CRM) — e então nada é gravado.
    /// </summary>
    public async Task<bool> ResponderAsync(int id, string? reescrita, CancellationToken ct = default)
    {
        // A reserva é condicional: dois envios da mesma releitura não geram duas respostas.
        var reservada = await db.PerguntasAoSemDor
            .Where(p => p.Id == id && p.Situacao == SemDorNosAppsRegras.Aguardando)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Situacao, SemDorNosAppsRegras.Lendo), ct);
        if (reservada == 0) return false;

        var pergunta = await db.PerguntasAoSemDor.SingleAsync(p => p.Id == id, ct);
        var eu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == pergunta.UsuarioId && u.Ativo, ct);
        var texto = SemDorNosAppsRegras.Reescrita(reescrita);
        pergunta.Reescrita = texto;
        pergunta.ResolvidaEm = agora();
        if (eu is null)
        {
            // Saiu da equipe enquanto a pergunta esperava: não há a quem responder.
            pergunta.Situacao = SemDorNosAppsRegras.SemResposta;
            await GravarAsync(null, ct);
            return true;
        }

        (string Texto, CartaoChat? Cartao) resposta = ("", null);
        var entendeu = false;
        if (texto is not null)
        {
            try
            {
                resposta = await new SistemaEquipeService(db, agora).ResponderAsync(eu, texto, ct);
                entendeu = !SemDorNosAppsRegras.NaoEntendeu(resposta.Cartao);
                if (entendeu) resposta = (SemDorNosAppsRegras.Entendida(texto, resposta.Texto), resposta.Cartao);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A pergunta foi entendida; o que falhou foi ler a agenda — a mesma frase do chat.
                Clinica.Application.Diagnostico.Registrar("@SemDor nos apps — resposta da releitura", ex);
                resposta = ("Não consegui ler os dados da agenda agora. Tente de novo em instantes.", null);
                entendeu = true;
            }
        }
        if (entendeu)
            pergunta.Situacao = SemDorNosAppsRegras.Respondida;
        else
        {
            pergunta.Situacao = SemDorNosAppsRegras.SemResposta;
            resposta = SistemaEquipeRegras.Ajuda(true, PedidosRecepcaoRegras.PodeTratar(eu));
        }
        await GravarAsync(GravacaoDoChat.Mensagem(pergunta.Conversa, eu.Id, eu.Nome, resposta.Texto, resposta.Cartao, true, agora()), ct);
        return true;
    }

    /// <summary>
    /// A pergunta que esperou mais que <see cref="SemDorNosAppsRegras.Validade"/> (o CRM caiu, a IA
    /// demorou) recebe a ajuda de "não entendi". Também vale para a reservada e nunca concluída.
    /// </summary>
    public async Task VencerAsync(CancellationToken ct = default)
    {
        var limite = agora() - SemDorNosAppsRegras.Validade;
        var vencidas = await db.PerguntasAoSemDor.AsNoTracking()
            .Where(p => (p.Situacao == SemDorNosAppsRegras.Aguardando || p.Situacao == SemDorNosAppsRegras.Lendo) && p.CriadaEm < limite)
            .OrderBy(p => p.Id).Take(20)
            .Select(p => new { p.Id, p.UsuarioId, p.Conversa }).ToListAsync(ct);
        foreach (var v in vencidas)
        {
            var momento = agora();
            var reservada = await db.PerguntasAoSemDor
                .Where(p => p.Id == v.Id && (p.Situacao == SemDorNosAppsRegras.Aguardando || p.Situacao == SemDorNosAppsRegras.Lendo))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Situacao, SemDorNosAppsRegras.SemResposta).SetProperty(p => p.ResolvidaEm, momento), ct);
            if (reservada == 0) continue;
            var eu = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == v.UsuarioId && u.Ativo, ct);
            if (eu is null) continue;
            var (t, c) = SistemaEquipeRegras.Ajuda(true, PedidosRecepcaoRegras.PodeTratar(eu));
            await GravarAsync(GravacaoDoChat.Mensagem(v.Conversa, eu.Id, eu.Nome, t, c, true, momento), ct);
        }
    }

    /// <summary>
    /// O pulso: grava "o CRM leu agora" no máximo a cada <see cref="SemDorNosAppsRegras.PulsoGravadoACada"/>.
    /// A linha nasce na primeira leitura.
    /// </summary>
    private async Task PulsoAsync(DateTime momento, CancellationToken ct)
    {
        var antes = momento - SemDorNosAppsRegras.PulsoGravadoACada;
        var mudou = await db.SemDorNoCrm
            .Where(x => x.Id == SemDorNoCrm.Unico && (x.PerguntasLidasEm == null || x.PerguntasLidasEm < antes || x.PerguntasLidasEm > momento))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PerguntasLidasEm, momento), ct);
        if (mudou > 0 || await db.SemDorNoCrm.AnyAsync(x => x.Id == SemDorNoCrm.Unico, ct)) return;
        var linha = db.SemDorNoCrm.Add(new SemDorNoCrm { PerguntasLidasEm = momento });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* outra leitura criou a linha ao mesmo tempo */ }
        finally { linha.State = EntityState.Detached; }
    }

    private async Task GravarAsync(MensagemEquipe? m, CancellationToken ct)
    {
        if (m is not null) await GravacaoDoChat.AdicionarAsync(db, m, ct);
        await db.SaveChangesAsync(ct);
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.Entity is MensagemEquipe or ConversaEquipe or PerguntaAoSemDor).ToList())
            e.State = EntityState.Detached;
    }
}
