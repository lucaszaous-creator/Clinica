using System.Security.Cryptography;
using Clinica.Application.Assinatura;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

public sealed record CadastroA1Tablet(string Arquivo, string Senha, bool Consentiu);
public sealed record SenhaA1Tablet(string Senha);

public sealed class CofreA1Tablet(IConfiguration config, AtendimentoTabletService acesso,
    ClinicaDbContext db, IDataProtectionProvider protecao, IConfiancaCertificadoA1 confianca)
{
    public bool Habilitado => config.GetValue<bool>("Portal:A1:Habilitado");
    public async Task<UsuarioSistema> Autorizar(SessaoTablet s, CancellationToken ct)
    {
        var u = await acesso.AutorizarAsync(s, ct, Permissao.VerProntuario);
        if (!u.Pode(Permissao.Prescrever) && !u.Pode(Permissao.ChecarPrescricao)) throw new UnauthorizedAccessException();
        return u;
    }
    private void ExigirHabilitado()
    {
        if (!Habilitado) throw ErroFormularioTablet.Criar("O uso de A1 no portal ainda não foi habilitado pela clínica.");
    }
    private IDataProtector Protetor(UsuarioSistema u) => protecao.CreateProtector(
        "CertificadoA1.v1", u.Id.ToString(), u.ProfissionalId!.Value.ToString(), Cpf.Normalizar(u.Profissional!.Cpf));

    public async Task<object> Estado(SessaoTablet s, CancellationToken ct)
    {
        var u = await acesso.AutorizarAsync(s, ct, Permissao.VerProntuario);
        if (!u.Pode(Permissao.Prescrever) && !u.Pode(Permissao.ChecarPrescricao))
            return new { habilitado = false, cadastrado = false };
        if (!Habilitado) return new { habilitado = false, cadastrado = false };
        var c = await Registro(u, ct);
        return new { habilitado = true, cadastrado = c is not null, c?.Titular, c?.ValidoAte, c?.Versao };
    }
    public Task<CertificadoA1Profissional?> Registro(UsuarioSistema u, CancellationToken ct)
        => db.CertificadosA1.SingleOrDefaultAsync(c => c.UsuarioId == u.Id && c.ProfissionalId == u.ProfissionalId, ct);

    public async Task Cadastrar(SessaoTablet s, CadastroA1Tablet pedido, CancellationToken ct)
    {
        var u = await Autorizar(s, ct); ExigirHabilitado();
        if (!pedido.Consentiu) throw ErroFormularioTablet.Criar("Confirme a guarda protegida do seu certificado neste portal.");
        if (pedido.Arquivo?.Length is not (> 0 and <= 174764)) throw ErroFormularioTablet.Criar("Escolha um arquivo A1 de até 128 KB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(pedido.Arquivo); }
        catch (FormatException) { throw ErroFormularioTablet.Criar("Arquivo A1 inválido."); }
        try
        {
            var certificado = Abrir(bytes, pedido.Senha, u);
            using var chave = certificado.Certificado;
            // Só a senha apresentada nesta requisição abre o PFX; ela nunca entra na entidade.
            var c = await db.CertificadosA1.SingleOrDefaultAsync(c => c.UsuarioId == u.Id, ct);
            if (c is null) { c = new() { UsuarioId = u.Id }; db.CertificadosA1.Add(c); }
            c.ProfissionalId = u.ProfissionalId!.Value;
            c.ArquivoProtegido = Protetor(u).Protect(bytes);
            c.ImpressaoDigital = chave.GetCertHashString(HashAlgorithmName.SHA256);
            c.Titular = certificado.Titular;
            c.ValidoAte = DateTime.SpecifyKind(certificado.ValidoAte, DateTimeKind.Unspecified);
            c.Versao = Guid.NewGuid();
            db.Auditoria.Add(new() { Operador = u.Login, Acao = "CadastroCertificadoA1", Detalhe = "Certificado individual cadastrado ou substituído; senha não armazenada." });
            await db.SaveChangesAsync(ct);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private CertificadoAssinatura Abrir(byte[] bytes, string senha, UsuarioSistema u)
    {
        CertificadoAssinatura? c = null;
        try { c = CertificadoA1.Abrir(bytes, senha, u.Profissional!.Cpf); confianca.Exigir(c.Certificado); return c; }
        catch (InvalidOperationException e) { c?.Certificado.Dispose(); throw ErroFormularioTablet.Criar(e.Message); }
        catch { c?.Dispose(); throw; }
    }
    public async Task<CertificadoAssinatura> Desbloquear(UsuarioSistema u, Guid versao, string senha, CancellationToken ct)
    {
        ExigirHabilitado();
        var c = await Registro(u, ct) ?? throw ErroFormularioTablet.Criar("Cadastre seu certificado A1 antes de assinar.");
        if (c.Versao != versao) throw new ConflitoClinicoTablet("O certificado foi substituído. Confira o documento e autorize novamente.");
        byte[] bytes;
        try { bytes = Protetor(u).Unprotect(c.ArquivoProtegido); }
        catch (CryptographicException) { throw ErroFormularioTablet.Criar("Não foi possível abrir o cofre. Confira o vínculo profissional ou cadastre novamente seu A1."); }
        try { return Abrir(bytes, senha, u); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task Remover(SessaoTablet s, CancellationToken ct)
    {
        var u = await Autorizar(s, ct);
        // Remoção permanece disponível mesmo com novas assinaturas desabilitadas.
        var c = await db.CertificadosA1.SingleOrDefaultAsync(c => c.UsuarioId == u.Id, ct);
        if (c is null) return;
        db.CertificadosA1.Remove(c);
        db.Auditoria.Add(new() { Operador = u.Login, Acao = "RemocaoCertificadoA1", Detalhe = "Credencial removida do portal; documentos assinados preservados." });
        await db.SaveChangesAsync(ct);
    }
}
