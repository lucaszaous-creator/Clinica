using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>Ciclo completo do lote TISS: criar → enviar (protocolo) → retorno com glosas.</summary>
public class LoteTissServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly LoteTissService _lotes;

    public LoteTissServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _lotes = new LoteTissService(_repo, _parametros);
    }

    private async Task<CodigoFaturamento> CriarCodigoBaixadoAsync(string guia)
    {
        var p = new Paciente { Nome = "P " + guia, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaSimples);
        var codigo = r.Atendimento.Codigos.First();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), guia, "sec", null);
        return codigo;
    }

    [Fact]
    public async Task CriarLote_ConsomeSequencial_ENaoRepeteGuias()
    {
        await CriarCodigoBaixadoAsync("G-1");
        await CriarCodigoBaixadoAsync("G-2");

        var lote = await _lotes.CriarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "326305");

        lote.Should().NotBeNull();
        lote!.Numero.Should().Be(1);
        lote.Status.Should().Be(StatusLoteTiss.Gerado);
        lote.Codigos.Should().HaveCount(2);

        // As guias já exportadas não entram num segundo lote (era o risco de reenvio duplicado).
        var segundo = await _lotes.CriarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "326305");
        segundo.Should().BeNull();

        // A sequência avançou para o próximo lote.
        (await _parametros.ObterProximoNumeroLoteTissAsync()).Should().Be(2);
    }

    [Fact]
    public async Task FluxoCompleto_Enviar_ERegistrarRetornoComGlosa()
    {
        var c1 = await CriarCodigoBaixadoAsync("G-1");
        var c2 = await CriarCodigoBaixadoAsync("G-2");
        var lote = (await _lotes.CriarAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), null))!;

        // Retorno antes do envio é inválido.
        var cedo = () => _lotes.RegistrarRetornoAsync(lote.Id, new DateOnly(2026, 7, 20),
            Array.Empty<RetornoGuiaDecisao>(), null);
        await cedo.Should().ThrowAsync<InvalidOperationException>();

        await _lotes.MarcarEnviadoAsync(lote.Id, new DateOnly(2026, 7, 15), "PROT-42");

        var enviado = await _lotes.ObterAsync(lote.Id);
        enviado.Status.Should().Be(StatusLoteTiss.Enviado);
        enviado.ProtocoloOperadora.Should().Be("PROT-42");

        // Demonstrativo: G-1 aceita, G-2 glosada com motivo ANS.
        await _lotes.RegistrarRetornoAsync(lote.Id, new DateOnly(2026, 7, 25), new[]
        {
            new RetornoGuiaDecisao(c1.Id, Glosada: false, null, null),
            new RetornoGuiaDecisao(c2.Id, Glosada: true, "2006", "quantidade acima da autorizada")
        }, "demonstrativo 07/2026");

        var processado = await _lotes.ObterAsync(lote.Id);
        processado.Status.Should().Be(StatusLoteTiss.Processado);
        processado.DataRetorno.Should().Be(new DateOnly(2026, 7, 25));

        var g2 = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == c2.Id);
        g2.Glosa.Should().Be(StatusGlosa.Glosada);
        g2.MotivoGlosaCodigo.Should().Be("2006");
        g2.DataLimiteRecurso.Should().Be(new DateOnly(2026, 7, 25).AddDays(ParametrosService.PrazoRecursoGlosaPadrao));

        var g1 = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == c1.Id);
        g1.Glosa.Should().Be(StatusGlosa.SemGlosa);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
