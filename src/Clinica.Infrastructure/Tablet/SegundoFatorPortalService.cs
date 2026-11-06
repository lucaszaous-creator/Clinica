using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>Cifra e abre o segredo TOTP. Quem implementa é o hospedeiro (a proteção de dados da API).</summary>
public interface IProtecaoDoSegredo
{
    string Proteger(byte[] segredo);
    byte[] Abrir(string protegido);
}

/// <summary>
/// O segundo fator do portal (nov/2026): o autenticador de cada pessoa, os aparelhos que ela
/// marcou como confiáveis e os códigos de recuperação. Substitui o código de cadastro do
/// tablet, que era UM segredo para a equipe inteira.
///
/// Toda gravação acontece no mesmo <c>SaveChanges</c> da linha de auditoria correspondente
/// (ponto 7 do compromisso de conformidade).
/// </summary>
public sealed class SegundoFatorPortalService(ClinicaDbContext db, IProtecaoDoSegredo protecao, TimeProvider relogio)
{
    public const int DiasDeConfianca = 30;

    /// <summary>Falhas seguidas de código até travar o segundo fator (e exigir a direção).</summary>
    public const int FalhasAteTravar = 10;

    private long Agora => relogio.GetUtcNow().ToUnixTimeMilliseconds();

    public Task<bool> TemAutenticadorAsync(int usuarioId, CancellationToken ct)
        => db.SegundosFatoresPortal.AnyAsync(x => x.UsuarioId == usuarioId, ct);

    /// <summary>A pessoa marcou ESTE aparelho como confiável, e a confiança ainda vale?</summary>
    public Task<bool> ConfiavelAsync(UsuarioSistema u, string dispositivo, CancellationToken ct)
    {
        var id = IdDoDispositivo(u.Id, dispositivo);
        var agora = Agora;
        var credencial = Credencial(u);
        return db.DispositivosConfiaveisPortal.AnyAsync(x => x.Id == id && x.UsuarioId == u.Id && x.ExpiraEm > agora
            && x.Credencial == credencial, ct);
    }

    /// <summary>A versão da senha: muda quando a senha muda.</summary>
    public static string Credencial(UsuarioSistema u) => ContratoTablet.Hash("credencial-portal:" + u.SenhaHash);

    /// <summary>
    /// Confere o código e, se casar, marca o passo como usado (o mesmo código não entra duas
    /// vezes). Falha conta; no teto, o segundo fator trava e só a direção destrava.
    /// </summary>
    public async Task<bool> VerificarAsync(int usuarioId, string? codigo, CancellationToken ct)
    {
        var f = await db.SegundosFatoresPortal.SingleOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (f is null || f.FalhasSeguidas >= FalhasAteTravar) return false;
        var passo = Totp.Verificar(protecao.Abrir(f.SegredoProtegido), codigo, relogio.GetUtcNow(), f.UltimoPasso);
        if (passo is null)
        {
            f.FalhasSeguidas++;
            Auditar(usuarioId, f.FalhasSeguidas >= FalhasAteTravar ? "PortalSegundoFatorTravado" : "PortalSegundoFatorFalhou",
                $"falhas seguidas: {f.FalhasSeguidas}");
            await db.SaveChangesAsync(ct);
            return false;
        }
        f.UltimoPasso = passo.Value;
        f.FalhasSeguidas = 0;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// A ativação (dentro do portal, já com a sessão aberta): grava o autenticador SÓ depois que a
    /// pessoa provou que o celular gera o código certo (o segredo viaja até aqui num cookie
    /// cifrado), e devolve os dez códigos de recuperação — a única vez que eles existem em claro.
    /// </summary>
    public async Task<IReadOnlyList<string>?> CadastrarAsync(int usuarioId, byte[] segredo, string? codigo, CancellationToken ct)
    {
        var passo = Totp.Verificar(segredo, codigo, relogio.GetUtcNow(), long.MinValue);
        if (passo is null) return null;
        if (await TemAutenticadorAsync(usuarioId, ct)) return null; // corrida: outro aparelho cadastrou antes
        db.SegundosFatoresPortal.Add(new SegundoFatorPortal
        {
            UsuarioId = usuarioId, SegredoProtegido = protecao.Proteger(segredo), AtivadoEm = Agora, UltimoPasso = passo.Value
        });
        var codigos = await TrocarRecuperacaoAsync(usuarioId, ct);
        Auditar(usuarioId, "PortalSegundoFatorCadastrado", "autenticador cadastrado no portal");
        await db.SaveChangesAsync(ct);
        return codigos;
    }

    /// <summary>Perdeu o celular: um código de recuperação vale UMA vez.</summary>
    public async Task<bool> UsarRecuperacaoAsync(int usuarioId, string? codigo, CancellationToken ct)
    {
        // O erro de código de recuperação conta para o MESMO teto do código do autenticador:
        // senão a recuperação seria uma segunda porta sem travamento para quem tem a senha.
        var f = await db.SegundosFatoresPortal.SingleOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (f is null || f.FalhasSeguidas >= FalhasAteTravar) return false;
        var resumo = string.IsNullOrWhiteSpace(codigo) || codigo.Length > 20 ? null : Totp.ResumoRecuperacao(usuarioId, codigo);
        var c = resumo is null ? null
            : await db.CodigosRecuperacaoPortal.SingleOrDefaultAsync(x => x.UsuarioId == usuarioId && x.Resumo == resumo, ct);
        if (c is null || c.UsadoEm is not null)
        {
            f.FalhasSeguidas++;
            Auditar(usuarioId, f.FalhasSeguidas >= FalhasAteTravar ? "PortalSegundoFatorTravado" : "PortalRecuperacaoFalhou",
                $"código de recuperação recusado · falhas seguidas: {f.FalhasSeguidas}");
            await db.SaveChangesAsync(ct);
            return false;
        }
        f.FalhasSeguidas = 0;
        c.UsadoEm = Agora;
        Auditar(usuarioId, "PortalRecuperacaoUsada", "código de recuperação usado");
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Marca o aparelho como confiável por <see cref="DiasDeConfianca"/> dias para ESTA pessoa.</summary>
    public async Task ConfiarAsync(UsuarioSistema u, string dispositivo, string? origem, CancellationToken ct)
    {
        var usuarioId = u.Id;
        var credencial = Credencial(u);
        var id = IdDoDispositivo(usuarioId, dispositivo);
        var expira = Agora + DiasDeConfianca * 86_400_000L;
        var d = await db.DispositivosConfiaveisPortal.FindAsync([id], ct);
        if (d is null)
            db.DispositivosConfiaveisPortal.Add(new DispositivoConfiavelPortal
                { Id = id, UsuarioId = usuarioId, CriadoEm = Agora, ExpiraEm = expira, Origem = origem, Credencial = credencial });
        else { d.ExpiraEm = expira; d.Origem = origem; d.Credencial = credencial; }
        Auditar(usuarioId, "PortalDispositivoConfiavel", $"aparelho confiável por {DiasDeConfianca} dias" + (origem is null ? "" : $" · origem {origem}"));
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<string>> TrocarRecuperacaoAsync(int usuarioId, CancellationToken ct)
    {
        db.CodigosRecuperacaoPortal.RemoveRange(await db.CodigosRecuperacaoPortal.Where(x => x.UsuarioId == usuarioId).ToListAsync(ct));
        var codigos = Totp.NovosCodigosDeRecuperacao();
        foreach (var c in codigos)
            db.CodigosRecuperacaoPortal.Add(new CodigoRecuperacaoPortal { UsuarioId = usuarioId, Resumo = Totp.ResumoRecuperacao(usuarioId, c) });
        return codigos;
    }

    public static string IdDoDispositivo(int usuarioId, string dispositivo)
        => ContratoTablet.Hash($"dispositivo-confiavel:{usuarioId}|{dispositivo}");

    private void Auditar(int usuarioId, string acao, string detalhe)
        => db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = DateTime.Now, Operador = _login.TryGetValue(usuarioId, out var l) ? l : $"usuario:{usuarioId}",
            Acao = acao, Detalhe = detalhe
        });

    // O login de quem age, para a trilha (ponto 6: quem assina é quem entrou). Preenchido por
    // quem chama com o usuário já autenticado pela senha; sem ele, a trilha guarda o id.
    private readonly Dictionary<int, string> _login = [];

    /// <summary>Informa o login do usuário em cena, para a trilha de auditoria.</summary>
    public SegundoFatorPortalService Para(UsuarioSistema u)
    {
        _login[u.Id] = u.Login;
        return this;
    }
}
