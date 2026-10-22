using System.Data;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>
/// O link de uso único do documento pedido pelo WhatsApp (@SemDor fase 4, out/2026 —
/// docs/semdor-assistente.md). Abrir o link gasta o token e cria uma sessão do portal em modo
/// <c>link</c>: 15 minutos, presa àquele rascunho, sem tablet cadastrado e sem senha do login —
/// quem prova a identidade é o celular que recebeu a mensagem, e quem prova a assinatura é o A1,
/// com a senha dele digitada aqui. A emissão e a assinatura são as do atendimento no tablet
/// (<see cref="AtendimentoTabletService"/>, A1TabletService): esta classe só abre a porta.
/// </summary>
public sealed class LinkDocumentoTabletService(ClinicaDbContext db, IClinicaRepositorio repo,
    AtendimentoTabletService atendimento, TimeProvider tempo)
{
    public const string Modo = "link";
    private const string PrefixoDispositivo = "link:";

    public long Agora => tempo.GetUtcNow().ToUnixTimeMilliseconds();
    private DateTime AgoraNaClinica => TimeZoneInfo.ConvertTime(tempo.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime;

    public const string LinkGasto = "Este link já foi usado ou venceu. Peça o documento de novo pelo WhatsApp da clínica.";

    private static bool PodeUsar(UsuarioSistema u) => u.Ativo && !u.DeveTrocarSenha && u.ProfissionalId is > 0
        && u.Pode(Permissao.Prescrever) && u.Profissional?.Ativo == true && !u.Travado(DateTime.Now);

    /// <summary>Abre o link (uma vez só) e devolve o token da sessão restrita, que vai num cookie próprio.</summary>
    public async Task<(string Token, SessaoTablet Sessao)> AbrirAsync(string? token, CancellationToken ct)
    {
        if (token?.Length != 64 || !token.All(Uri.IsHexDigit)) throw ErroFormularioTablet.Criar(LinkGasto);
        var hash = ContratoTablet.Hash(token.ToUpperInvariant());
        var p = await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (p is null || p.AbertoEm is not null || p.ExpiraEm <= AgoraNaClinica) throw ErroFormularioTablet.Criar(LinkGasto);
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Profissional).SingleOrDefaultAsync(x => x.Id == p.UsuarioId, ct);
        if (u is null || !PodeUsar(u)) throw new UnauthorizedAccessException();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var momento = AgoraNaClinica;
        // O "uso único": só um abre. Dois toques no link ao mesmo tempo, o segundo recebe a recusa.
        if (await db.PedidosDocumentoWhatsApp.Where(x => x.Id == p.Id && x.AbertoEm == null)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.AbertoEm, momento), ct) == 0)
            throw ErroFormularioTablet.Criar(LinkGasto);
        var novo = PortalTabletService.Token();
        var sessao = new SessaoTablet { Id = ContratoTablet.Hash(novo), UsuarioId = u.Id, Modo = Modo,
            CredencialVersao = ContratoTablet.Hash(u.SenhaHash), Dispositivo = PrefixoDispositivo + p.Id,
            ExpiraEm = Agora + (long)DocumentoPeloWhatsAppRegras.Validade.TotalMilliseconds, AtividadeClinicaEm = Agora };
        db.SessoesTablet.Add(sessao);
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = u.Login, PacienteId = p.PacienteId,
            Acao = "TabletLinkWhatsApp", Detalhe = $"Link do WhatsApp aberto ({DocumentoPeloWhatsAppRegras.Rotulo(p.Tipo)}, pedido {p.Id})" }, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.Entry(sessao).State = EntityState.Detached;
        return (novo, sessao);
    }

    /// <summary>A sessão do link e o rascunho dela. Revalida login, senha e prazo a cada chamada.</summary>
    public async Task<(SessaoTablet Sessao, PedidoDocumentoWhatsApp Pedido)> AutorizarAsync(string? token, CancellationToken ct)
    {
        if (token?.Length != 64) throw new UnauthorizedAccessException();
        var id = ContratoTablet.Hash(token);
        var s = await db.SessoesTablet.Include(x => x.Usuario).ThenInclude(u => u!.Profissional).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s?.Usuario is not { } u || s.Modo != Modo || s.ExpiraEm <= Agora || !PodeUsar(u)
            || s.CredencialVersao != ContratoTablet.Hash(u.SenhaHash)
            || !s.Dispositivo.StartsWith(PrefixoDispositivo, StringComparison.Ordinal)
            || !int.TryParse(s.Dispositivo[PrefixoDispositivo.Length..], out var pedidoId))
            throw new UnauthorizedAccessException();
        var p = await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pedidoId && x.UsuarioId == u.Id, ct)
            ?? throw new UnauthorizedAccessException();
        return (s, p);
    }

    /// <summary>O que a página mostra: a paciente, o rascunho e em que pé está.</summary>
    public async Task<object> EstadoAsync(SessaoTablet s, PedidoDocumentoWhatsApp p, CancellationToken ct)
    {
        var paciente = await db.Pacientes.AsNoTracking().Where(x => x.Id == p.PacienteId)
            .Select(x => new { x.Nome, Nascimento = x.DataNascimento }).SingleAsync(ct);
        var documento = p.DocumentoClinicoId is { } d
            ? await db.DocumentosClinicos.AsNoTracking().Where(x => x.Id == d)
                .Select(x => new { x.Id, x.Numero, Assinado = x.AssinadoEm != null, Cancelado = x.CanceladoEm != null }).SingleAsync(ct)
            : null;
        return new
        {
            paciente = paciente.Nome, nascimento = paciente.Nascimento, tipo = p.Tipo,
            rotulo = DocumentoPeloWhatsAppRegras.Rotulo(p.Tipo), texto = p.Texto, dias = p.DiasAfastamento,
            documento, concluido = p.ConcluidoEm is not null, assinadoComA1 = p.AssinadoComA1,
            enviado = p.EnviadoAoPacienteEm is not null, medico = s.Usuario!.Nome, expiraEm = s.ExpiraEm,
        };
    }

    /// <summary>
    /// Emite o documento a partir do rascunho revisado. Repetir (o toque duplo, a rede que caiu)
    /// devolve o MESMO documento: o rascunho guarda qual foi.
    /// </summary>
    public async Task<ResultadoDocumentoTablet> EmitirAsync(SessaoTablet s, PedidoDocumentoWhatsApp p, string? texto, int? dias, CancellationToken ct)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length == 0) throw ErroFormularioTablet.Criar("Escreva o conteúdo do documento.");
        if (texto.Length > DocumentoPeloWhatsAppRegras.TamanhoMaximoDoTexto)
            throw ErroFormularioTablet.Criar($"Use no máximo {DocumentoPeloWhatsAppRegras.TamanhoMaximoDoTexto} caracteres.");
        if (p.Tipo == "atestado" && dias is not (>= 1 and <= DocumentoPeloWhatsAppRegras.DiasMaximos))
            throw ErroFormularioTablet.Criar($"Informe de 1 a {DocumentoPeloWhatsAppRegras.DiasMaximos} dias de afastamento.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20261022, {p.Id})", ct);
        var atual = await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync(x => x.Id == p.Id, ct);
        if (atual.DocumentoClinicoId is { } existente)
        {
            var numero = await db.DocumentosClinicos.AsNoTracking().Where(x => x.Id == existente).Select(x => x.Numero).SingleAsync(ct);
            return new(existente, "documento", numero);
        }
        var r = await atendimento.EmitirPeloLinkAsync(s, p.PacienteId,
            new EmitirDocumentoTablet(Guid.NewGuid(), p.Tipo, texto, DiasAfastamento: p.Tipo == "atestado" ? dias : null), ct);
        await db.PedidosDocumentoWhatsApp.Where(x => x.Id == p.Id && x.DocumentoClinicoId == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.DocumentoClinicoId, r.Id), ct);
        await tx.CommitAsync(ct);
        return r;
    }

    /// <summary>O médico terminou: assinado com o A1 ou emitido sem assinatura digital.</summary>
    public async Task ConcluirAsync(PedidoDocumentoWhatsApp p, bool assinadoComA1, CancellationToken ct)
    {
        var momento = AgoraNaClinica;
        await db.PedidosDocumentoWhatsApp.Where(x => x.Id == p.Id && x.DocumentoClinicoId != null && x.ConcluidoEm == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ConcluidoEm, momento).SetProperty(y => y.AssinadoComA1, assinadoComA1), ct);
    }

    public async Task MarcarEnviadoAsync(PedidoDocumentoWhatsApp p, CancellationToken ct)
    {
        var momento = AgoraNaClinica;
        await db.PedidosDocumentoWhatsApp.Where(x => x.Id == p.Id && x.EnviadoAoPacienteEm == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.EnviadoAoPacienteEm, momento), ct);
    }

    public async Task SairAsync(SessaoTablet s, CancellationToken ct)
    {
        s.Modo = "revogada";
        s.ExpiraEm = Agora;
        await db.SaveChangesAsync(ct);
    }
}
