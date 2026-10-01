using System.Data;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

public sealed class A1TabletService(CofreA1Tablet cofre, AtendimentoTabletService acesso,
    ConferenciaAssinaturaTablet conferencia, AutorizacoesA1Tablet autorizacoes, ClinicaDbContext db,
    AssinaturaDeDocumentoClinicoService documentos, AssinaturaDePrescricaoService infusoes)
{
    private async Task<string> Fotografia(string tipo, int id, UsuarioSistema u, CancellationToken ct)
        => ContratoTablet.Hash(await conferencia.Conferir(tipo, id, true, ct)
            + ContratoTablet.Serializar(ConferenciaAssinaturaTablet.Cadastro(null, u.Profissional)));

    public async Task<object> Preparar(SessaoTablet s, int agenda, string tipo, int id, bool confirmou, CancellationToken ct)
    {
        var (u, _) = await acesso.ExigirDocumentoAsync(s, agenda, tipo, id, true, ct);
        if (!cofre.Habilitado) throw ErroFormularioTablet.Criar("O A1 ainda não está habilitado no portal.");
        if (!confirmou) throw ErroFormularioTablet.Criar("Confirme a revisão do documento antes de assinar.");
        var c = await cofre.Registro(u, ct) ?? throw ErroFormularioTablet.Criar("Cadastre seu A1 em Meu certificado antes de assinar.");
        var a = autorizacoes.Criar(s.Id, agenda, tipo, id, await Fotografia(tipo, id, u, ct), c.Versao);
        var documento = tipo == "documento"
            ? await db.DocumentosClinicos.Where(d => d.Id == id).Select(d => "Documento nº " + d.Numero).SingleAsync(ct)
            : await db.PrescricoesInternas.Where(p => p.Id == id).Select(p => (tipo == "execucao" ? "Execução da prescrição nº " : "Prescrição nº ") + p.Numero).SingleAsync(ct);
        return new { a.Id, c.Titular, c.ValidoAte, Documento = documento };
    }

    public async Task<object> Assinar(SessaoTablet s, Guid id, string senha, CancellationToken ct)
    {
        await cofre.Autorizar(s, ct);
        var a = autorizacoes.Consumir(id, s.Id);
        var (_, paciente) = await acesso.ExigirDocumentoAsync(s, a.Agendamento, a.Tipo, a.Documento, true, ct);
        // A confirmação é de um ato específico. Uma desconexão não abandona seu arquivamento.
        using var prazo = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        ct = prazo.Token;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260917, {paciente})", ct);
            if (a.Agendamento > 0) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260916, {a.Agendamento})", ct);
        }
        db.ChangeTracker.Clear();
        var (u, _) = await acesso.ExigirDocumentoAsync(s, a.Agendamento, a.Tipo, a.Documento, true, ct);
        if (await Fotografia(a.Tipo, a.Documento, u, ct) != a.Hash)
            throw new ConflitoClinicoTablet("O documento, o cadastro ou as alergias mudaram. Confira o PDF e autorize novamente.");
        var certificado = await cofre.Desbloquear(u, a.CertificadoVersao, senha, ct);
        using var chave = certificado.Certificado;
        if (a.Tipo == "infusao") await infusoes.AssinarPrescricaoAsync(a.Documento, certificado, true, u.Id, u.Login, ct);
        else if (a.Tipo == "execucao") await infusoes.AssinarExecucaoAsync(a.Documento, certificado, u.Id, u.Login, ct);
        else await documentos.AssinarAsync(a.Documento, certificado, u.Id, u.Login, ct);
        await tx.CommitAsync(ct);
        return new { estado = "concluido", a.Documento, a.Tipo };
    }
}
