using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O link do documento pedido pelo WhatsApp (@SemDor fase 4) contra o BANCO: o uso único
/// (ExecuteUpdate), a emissão que não duplica (transação e, no Postgres, o pg_advisory_xact_lock)
/// e as marcações do rascunho. Com <c>CLINICA_TESTES_POSTGRES</c>, roda no Postgres de verdade
/// (BancoDosTestes); o percurso HTTP com A1 está em Clinica.Assinaturas.Tests.
/// </summary>
public sealed class LinkDocumentoNoBancoTests : IDisposable
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private readonly SqliteConnection conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly LinkDocumentoTabletService link;
    private int pacienteId, usuarioId;

    public LinkDocumentoNoBancoTests()
    {
        conexao.Open();
        db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        var profissional = new Profissional { Nome = "Dra. Fictícia", Ativo = true };
        var paciente = new Paciente { Nome = "Paciente Fictícia" };
        db.AddRange(profissional, paciente);
        db.SaveChanges();
        var usuario = new UsuarioSistema { Nome = "Dra. Fictícia", Login = "ficticia", Perfil = PerfilAcesso.Profissional,
            ProfissionalId = profissional.Id, CriadoEm = new(2026, 1, 1) };
        db.Add(usuario);
        db.Add(new Agendamento { PacienteId = paciente.Id, ProfissionalId = profissional.Id, DataHora = DateTime.Today.AddHours(10) });
        db.SaveChanges();
        (pacienteId, usuarioId) = (paciente.Id, usuario.Id);
        db.ChangeTracker.Clear();

        var repo = new ClinicaRepositorio(db);
        var prontuario = new ProntuarioService(repo);
        var conferencia = new PrescricaoService(repo);
        var atendimento = new AtendimentoTabletService(db, repo, prontuario, new AgendaService(repo, new AtendimentoService(repo)),
            new DocumentoClinicoService(repo, prontuario, new ConsentimentoService(repo)),
            new PrescricaoInternaService(repo, conferencia), conferencia, TimeProvider.System);
        link = new LinkDocumentoTabletService(db, repo, atendimento, TimeProvider.System);
    }

    public void Dispose() { db.Dispose(); conexao.Dispose(); }

    private async Task<string> Rascunho(int minutos = 15)
    {
        var agora = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Brasilia).DateTime;
        var token = DocumentoPeloWhatsAppRegras.NovoToken();
        db.Add(new PedidoDocumentoWhatsApp { TokenHash = ContratoTablet.Hash(token), IdMensagem = "wamid." + Guid.NewGuid(),
            UsuarioId = usuarioId, PacienteId = pacienteId, Tipo = "atestado", Texto = "Texto fictício.", DiasAfastamento = 2,
            CriadoEm = agora, ExpiraEm = agora.AddMinutes(minutos) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return token;
    }

    [Fact]
    public async Task Abre_uma_vez_emite_uma_vez_e_marca_o_rascunho()
    {
        var token = await Rascunho();
        var (sessaoToken, _) = await link.AbrirAsync(token, default);
        await link.Invoking(l => l.AbrirAsync(token, default)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já foi usado ou venceu*");

        var (s, p) = await link.AutorizarAsync(sessaoToken, default);
        s.Modo.Should().Be(LinkDocumentoTabletService.Modo);
        var primeiro = await link.EmitirAsync(s, p, "Atesto, para os devidos fins, afastamento de 3 dias. Texto fictício.", 3, default);
        (_, p) = await link.AutorizarAsync(sessaoToken, default);
        var segundo = await link.EmitirAsync(s, p, "Outro texto", 3, default);
        segundo.Id.Should().Be(primeiro.Id, "repetir o toque devolve o mesmo documento");

        await link.ConcluirAsync(p, false, default);
        await link.MarcarEnviadoAsync(p, default);
        var gravado = await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync();
        (gravado.DocumentoClinicoId, gravado.AssinadoComA1).Should().Be((primeiro.Id, false));
        gravado.AbertoEm.Should().NotBeNull();
        gravado.ConcluidoEm.Should().NotBeNull();
        gravado.EnviadoAoPacienteEm.Should().NotBeNull();
        var d = await db.DocumentosClinicos.AsNoTracking().SingleAsync();
        (d.Tipo, d.DiasAfastamento, d.PacienteId, d.AgendamentoId).Should().Be((TipoDocumentoClinico.Atestado, 3, pacienteId, (int?)null));
        (await db.Auditoria.AsNoTracking().CountAsync(a => a.Acao == "TabletLinkWhatsApp" || a.Acao == "TabletLinkWhatsAppEmissao")).Should().Be(2);

        await link.SairAsync(s, default);
        await link.Invoking(l => l.AutorizarAsync(sessaoToken, default)).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Link_vencido_nao_abre()
    {
        var token = await Rascunho(minutos: -1);
        await link.Invoking(l => l.AbrirAsync(token, default)).Should().ThrowAsync<InvalidOperationException>();
        (await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync()).AbertoEm.Should().BeNull();
    }
}
