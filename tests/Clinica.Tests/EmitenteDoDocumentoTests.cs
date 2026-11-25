using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A janela de emitir documento diz QUEM emite, QUEM assina e COM O QUÊ (nov/2026, pedido do dono no iPhone: "não está
/// identificando o profissional logado na conta e tão pouco dá as opções de quem assina"). Dados fictícios.
/// </summary>
public sealed class EmitenteDoDocumentoTests : IDisposable
{
    private static Profissional Medica(int id = 1, string? registro = "52-0000000-0") => new() { Id = id, Nome = "Dra. Fictícia", RegistroConselho = registro, Ativo = true };

    [Fact]
    public void Codigos_TISS_do_cadastro_viram_sigla_no_rotulo_e_o_receituario_nao_acusa_falta()
    {
        // 25/11/2026: o cadastro guarda os campos TISS em CÓDIGO (06 = CRM, 33 = RJ). O rótulo do "Emitindo como" lê a
        // sigla, e o aviso "Complete o conselho (CRM) e a UF" some — mesmo com o RegistroConselho antigo ainda gravado.
        var p = new Profissional { Nome = "Dra. Ficticia", ConselhoProfissional = "06", NumeroConselho = "5201158546", UfConselho = "33",
            RegistroConselho = "52-0115854-6", Ativo = true };
        EmitenteDoDocumento.Rotulo(p).Should().Be("Dra. Ficticia · CRM 5201158546/RJ");
        EmitenteDoDocumento.FaltaNoCadastro(TipoDocumentoClinico.ReceituarioControleEspecial, p).Should().BeNull();
        var antigo = new Profissional { Nome = "Dra. Ficticia", RegistroConselho = "52-0115854-6", Ativo = true };
        EmitenteDoDocumento.FaltaNoCadastro(TipoDocumentoClinico.ReceituarioControleEspecial, antigo).Should().Contain("o conselho (CRM)").And.Contain("a UF");
    }

    [Fact]
    public void So_a_direcao_ou_quem_nao_tem_profissional_ligado_troca_o_emitente()
    {
        EmitenteDoDocumento.PodeEscolherOutro(PerfilAcesso.Gerente, 1).Should().BeTrue();
        EmitenteDoDocumento.PodeEscolherOutro(PerfilAcesso.Profissional, 1).Should().BeFalse("o profissional emite como ele mesmo");
        EmitenteDoDocumento.PodeEscolherOutro(PerfilAcesso.Recepcao, null).Should().BeTrue("sem vínculo, escolher é obrigatório");
    }

    [Fact]
    public void A_medica_com_so_o_numero_no_cadastro_continua_emitente_do_receituario_e_a_janela_pede_o_resto()
    {
        // O cadastro real da clínica: só o número em texto livre, sem "CRM" nem UF. Antes, o receituário a tirava da lista.
        var m = Medica();
        AssinanteDoDocumento.Resolver([m], 1, TipoDocumentoClinico.ReceituarioControleEspecial).Profissional.Should().Be(m);
        EmitenteDoDocumento.Registro(m).Should().Be("52-0000000-0");
        EmitenteDoDocumento.FaltaNoCadastro(TipoDocumentoClinico.ReceituarioControleEspecial, m).Should()
            .Contain("o conselho (CRM)").And.Contain("a UF").And.NotContain("o número");
        EmitenteDoDocumento.FaltaNoCadastro(TipoDocumentoClinico.Receita, m).Should().BeNull("a receita comum não imprime CRM e UF em campos");

        var completa = Medica(registro: "CRM-RJ 52-0000000-0");
        EmitenteDoDocumento.Rotulo(completa).Should().Be("Dra. Fictícia · CRM 52-0000000-0/RJ");
        EmitenteDoDocumento.FaltaNoCadastro(TipoDocumentoClinico.ReceituarioControleEspecial, completa).Should().BeNull();
    }

    [Fact]
    public void Conselho_conhecido_que_nao_prescreve_continua_fora_do_receituario()
    {
        var enfermeira = new Profissional { Id = 9, Nome = "Enf. Fictícia", RegistroConselho = "COREN-RJ 123456", Ativo = true };
        AssinanteDoDocumento.PodeAssinar(TipoDocumentoClinico.ReceituarioControleEspecial, enfermeira).Should().BeFalse();
        AssinanteDoDocumento.PodeAssinar(TipoDocumentoClinico.Atestado, enfermeira).Should().BeTrue("os outros papéis seguem como sempre");
    }

    [Fact]
    public void So_o_emitente_assina_e_outro_logado_emite_sem_assinar()
    {
        var m = Medica();
        EmitenteDoDocumento.Assinante(m, 1).LogadoAssina.Should().BeTrue();
        var outro = EmitenteDoDocumento.Assinante(m, 2);
        outro.LogadoAssina.Should().BeFalse();
        outro.Frase.Should().Contain("Só Dra. Fictícia assina").And.Contain("Para assinar");
        EmitenteDoDocumento.Assinante(null, 1).LogadoAssina.Should().BeFalse();
    }

    [Fact]
    public void Os_meios_de_assinatura_dizem_o_estado_de_cada_um()
    {
        var agora = new DateTime(2026, 11, 25, 10, 0, 0);
        var sem = EmitenteDoDocumento.Meios(null, false, agora);
        sem.Select(x => x.Meio).Should().Equal("A1", "SafeID");
        sem.Should().OnlyContain(x => !x.Disponivel);
        sem[0].Estado.Should().Contain("não cadastrado");
        EmitenteDoDocumento.Meios(agora.AddDays(-1), true, agora)[0].Estado.Should().StartWith("vencido em 24/11/2026");
        var com = EmitenteDoDocumento.Meios(new DateTime(2027, 3, 10), true, agora);
        com.Should().OnlyContain(x => x.Disponivel);
        com[0].Estado.Should().Be("válido até 10/03/2027");
    }

    private readonly SqliteConnection _conexao = new("DataSource=:memory:");

    [Fact]
    public async Task A_validade_do_A1_vem_do_banco_sem_o_arquivo()
    {
        _conexao.Open();
        await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        await db.Database.EnsureCreatedAsync();
        var prof = new Profissional { Nome = "Dra. Fictícia", Ativo = true };
        var usuario = new UsuarioSistema { Nome = "Fictícia", Login = "ficticia" };
        db.AddRange(prof, usuario);
        await db.SaveChangesAsync();
        var repo = new ClinicaRepositorio(db);
        (await new EquipeService(repo).ValidadeDoA1Async(prof.Id)).Should().BeNull();

        db.CertificadosA1.Add(new CertificadoA1Profissional { UsuarioId = usuario.Id, ProfissionalId = prof.Id, ValidoAte = new DateTime(2027, 3, 10), Versao = Guid.NewGuid() });
        await db.SaveChangesAsync();
        (await new EquipeService(repo).ValidadeDoA1Async(prof.Id)).Should().Be(new DateTime(2027, 3, 10));
    }

    public void Dispose() => _conexao.Dispose();
}
